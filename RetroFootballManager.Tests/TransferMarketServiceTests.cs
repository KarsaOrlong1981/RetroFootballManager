using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TransferMarketServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TransferMarketService _service = null!;
        private TeamRepository _teamRepo = null!;
        private ContractRepository _contractRepo = null!;
        private TransferListingRepository _listingRepo = null!;
        private TransferOfferRepository _offerRepo = null!;
        private LoanAgreementRepository _loanRepo = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public TransferMarketServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_transfer_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _contractRepo = new ContractRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            _offerRepo = new TransferOfferRepository(_db);
            _loanRepo = new LoanAgreementRepository(_db);
            _service = new TransferMarketService(_listingRepo, _offerRepo, _loanRepo, _teamRepo, _contractRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        // Teams+Spieler VOR jedem Transfer persistieren (wie im echten Spiel via
        // StartNewCareerAsync) - sonst vergibt SQLite beim ersten echten Insert (ausgelöst
        // durch TransferMarketService's eigenes SaveTeamAsync) eine neue Autoincrement-Id für
        // den Spieler und überschreibt jede hier manuell gesetzte Id.
        private async Task<(Team Seller, Team Buyer, Player Player)> SetupTeamsAsync()
        {
            var seller = TestHelpers.CreateTeam("Verkäufer", baseRating: 60);
            seller.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(seller);

            var buyer = TestHelpers.CreateTeam("Käufer", baseRating: 60);
            buyer.Finances = new Finances { CurrentBalance = 5_000_000 };
            await _teamRepo.SaveTeamAsync(buyer);

            var player = seller.Players[0];
            return (seller, buyer, player);
        }

        [Fact]
        public void CanBuy_NegativeBalance_BlocksBuying()
        {
            var team = TestHelpers.CreateTeam("Miese FC", baseRating: 55);
            team.Finances = new Finances { CurrentBalance = -1 };

            Assert.False(TransferMarketService.CanBuy(team, out string? error));
            Assert.NotNull(error);
        }

        [Fact]
        public void CanBuy_ZeroOrPositiveBalance_AllowsBuying()
        {
            var team = TestHelpers.CreateTeam("Solvente FC", baseRating: 55);
            team.Finances = new Finances { CurrentBalance = 0 };

            Assert.True(TransferMarketService.CanBuy(team, out _));
        }

        [Fact]
        public async Task ListPlayerAsync_PersistsListing()
        {
            var (seller, _, player) = await SetupTeamsAsync();

            var listing = await _service.ListPlayerAsync(player, seller, askingPrice: 500_000, season: 1, Today);

            var byTeam = await _listingRepo.GetByTeamAsync(seller.Id);
            Assert.Single(byTeam);
            Assert.Equal(listing.Id, byTeam[0].Id);
        }

        [Fact]
        public async Task MakeOfferAsync_CreatesPendingOffer()
        {
            var (seller, buyer, player) = await SetupTeamsAsync();
            var listing = await _service.ListPlayerAsync(player, seller, 500_000, 1, Today);

            var offer = await _service.MakeOfferAsync(listing, buyer, fee: 450_000, wageOffer: 200_000, Today);

            Assert.Equal(TransferOfferStatus.Pending, offer.Status);
            var offers = await _service.GetOffersForListingAsync(listing);
            Assert.Single(offers);
        }

        [Fact]
        public async Task AcceptOfferAsync_MovesPlayer_UpdatesFinancesAndContract_RemovesListing()
        {
            var (seller, buyer, player) = await SetupTeamsAsync();
            var listing = await _service.ListPlayerAsync(player, seller, 500_000, 1, Today);
            var offer = await _service.MakeOfferAsync(listing, buyer, fee: 450_000, wageOffer: 200_000, Today);

            await _service.AcceptOfferAsync(offer, listing, seller, buyer, player, Today);

            Assert.DoesNotContain(player, seller.Players);
            Assert.Contains(player, buyer.Players);
            Assert.Equal(buyer.Id, player.TeamId);

            Assert.Equal(450_000, seller.Finances!.TransferIncome);
            Assert.Equal(1_450_000, seller.Finances.CurrentBalance);
            Assert.Equal(450_000, buyer.Finances!.TransferExpense);
            Assert.Equal(4_550_000, buyer.Finances.CurrentBalance);

            var contracts = await _contractRepo.GetByHolderAsync(player.Id, ContractHolderType.Player);
            Assert.Single(contracts);
            Assert.Equal(buyer.Id, contracts[0].TeamId);
            Assert.Equal(200_000, contracts[0].AnnualSalary);

            var remainingListings = await _listingRepo.GetByTeamAsync(seller.Id);
            Assert.Empty(remainingListings);
        }

        [Fact]
        public async Task RejectOfferAsync_SetsStatusRejected()
        {
            var (seller, buyer, player) = await SetupTeamsAsync();
            var listing = await _service.ListPlayerAsync(player, seller, 500_000, 1, Today);
            var offer = await _service.MakeOfferAsync(listing, buyer, 400_000, 200_000, Today);

            await _service.RejectOfferAsync(offer);

            var offers = await _service.GetOffersForListingAsync(listing);
            Assert.Equal(TransferOfferStatus.Rejected, offers[0].Status);
        }

        [Fact]
        public async Task LoanOutAsync_MovesPlayerTemporarily()
        {
            var (origin, loanTeam, player) = await SetupTeamsAsync();

            var loan = await _service.LoanOutAsync(player, origin, loanTeam, Today, Today.AddMonths(6), negotiatedWage: 50_000);

            Assert.Contains(player, loanTeam.Players);
            Assert.DoesNotContain(player, origin.Players);
            Assert.Equal(loanTeam.Id, player.TeamId);
            Assert.False(loan.Returned);
        }

        [Fact]
        public async Task LoanOutAsync_MovesContractToLoanTeam_WithOnlyNegotiatedWage()
        {
            var (origin, loanTeam, player) = await SetupTeamsAsync();
            var originalContract = new Contract
            {
                HolderId = player.Id, HolderType = ContractHolderType.Player, TeamId = origin.Id,
                StartDate = Today.AddYears(-1), EndDate = Today.AddYears(1),
                AnnualSalary = 500_000, MarketValue = 3_000_000,
            };
            await _contractRepo.SaveAsync(originalContract);

            await _service.LoanOutAsync(player, origin, loanTeam, Today, Today.AddMonths(6), negotiatedWage: 50_000);

            var contract = await _contractRepo.GetByIdAsync(originalContract.Id);
            Assert.NotNull(contract);
            Assert.Equal(loanTeam.Id, contract!.TeamId);
            Assert.Equal(50_000, contract.AnnualSalary);
            Assert.Equal(3_000_000, contract.MarketValue); // Marktwert bleibt unangetastet
        }

        [Fact]
        public async Task ReturnExpiredLoansAsync_MovesPlayerBack_AfterEndDate()
        {
            var (origin, loanTeam, player) = await SetupTeamsAsync();
            var loan = await _service.LoanOutAsync(player, origin, loanTeam, Today, Today.AddMonths(1), negotiatedWage: 50_000);

            var teamsById = new Dictionary<int, Team> { [origin.Id] = origin, [loanTeam.Id] = loanTeam };
            await _service.ReturnExpiredLoansAsync(Today.AddMonths(2), teamsById);

            Assert.Contains(player, origin.Players);
            Assert.DoesNotContain(player, loanTeam.Players);
            Assert.Equal(origin.Id, player.TeamId);

            var active = await _loanRepo.GetActiveAsync();
            Assert.Empty(active);
        }

        [Fact]
        public async Task ReturnExpiredLoansAsync_RestoresOriginalContractTeamAndSalary()
        {
            var (origin, loanTeam, player) = await SetupTeamsAsync();
            var originalContract = new Contract
            {
                HolderId = player.Id, HolderType = ContractHolderType.Player, TeamId = origin.Id,
                StartDate = Today.AddYears(-1), EndDate = Today.AddYears(1), AnnualSalary = 500_000,
            };
            await _contractRepo.SaveAsync(originalContract);
            await _service.LoanOutAsync(player, origin, loanTeam, Today, Today.AddMonths(1), negotiatedWage: 50_000);

            var teamsById = new Dictionary<int, Team> { [origin.Id] = origin, [loanTeam.Id] = loanTeam };
            await _service.ReturnExpiredLoansAsync(Today.AddMonths(2), teamsById);

            var contract = await _contractRepo.GetByIdAsync(originalContract.Id);
            Assert.NotNull(contract);
            Assert.Equal(origin.Id, contract!.TeamId);
            Assert.Equal(500_000, contract.AnnualSalary);
        }

        [Fact]
        public async Task ReturnExpiredLoansAsync_DoesNotTouchStillActiveLoans()
        {
            var (origin, loanTeam, player) = await SetupTeamsAsync();
            await _service.LoanOutAsync(player, origin, loanTeam, Today, Today.AddMonths(6), negotiatedWage: 50_000);

            var teamsById = new Dictionary<int, Team> { [origin.Id] = origin, [loanTeam.Id] = loanTeam };
            await _service.ReturnExpiredLoansAsync(Today.AddMonths(1), teamsById);

            Assert.Contains(player, loanTeam.Players);
            Assert.DoesNotContain(player, origin.Players);
        }

        private static Team ForeignTeam(int id, int playerCount = 22)
        {
            var team = TestHelpers.CreateTeam($"Foreign{id}", baseRating: 60);
            team.Id = id;
            team.LeagueTier = 0;
            for (int i = team.Players.Count; i < playerCount; i++)
                team.Players.Add(new Player { Id = id * 1000 + i, Name = $"F{id}-{i}", Rating = 55, Position = Position.CentralMidfielder });
            return team;
        }

        [Fact]
        public async Task EnsureMinimumForeignListingsAsync_CreatesListingsUpToMinimum()
        {
            var foreignTeams = Enumerable.Range(1, 3).Select(id => ForeignTeam(id)).ToList();

            await _service.EnsureMinimumForeignListingsAsync(foreignTeams, season: 1, Today, minimumCount: 30, new Random(1));

            var listings = await _listingRepo.GetBySeasonAsync(1);
            Assert.Equal(30, listings.Count);
        }

        [Fact]
        public async Task EnsureMinimumForeignListingsAsync_DoesNotDuplicate_WhenAlreadyEnough()
        {
            var foreignTeams = Enumerable.Range(1, 3).Select(id => ForeignTeam(id)).ToList();

            await _service.EnsureMinimumForeignListingsAsync(foreignTeams, season: 1, Today, minimumCount: 30, new Random(1));
            await _service.EnsureMinimumForeignListingsAsync(foreignTeams, season: 1, Today, minimumCount: 30, new Random(2));

            var listings = await _listingRepo.GetBySeasonAsync(1);
            Assert.Equal(30, listings.Count);
        }

        [Fact]
        public async Task EnsureMinimumForeignListingsAsync_IgnoresGermanTeams()
        {
            var germanTeam = TestHelpers.CreateTeam("German", baseRating: 60);
            germanTeam.Id = 99;
            germanTeam.LeagueTier = 1;

            await _service.EnsureMinimumForeignListingsAsync([germanTeam], season: 1, Today, minimumCount: 30, new Random(1));

            var listings = await _listingRepo.GetBySeasonAsync(1);
            Assert.Empty(listings);
        }
    }
}
