using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Contract expiry: a player whose contract reaches EndDate is released and listed
    // ablösefrei (fee-free) - for the human team and AI teams alike - and can then be signed
    // by any club for wage only. See FreeAgentService.
    public class FreeAgentServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private ContractRepository _contractRepo = null!;
        private TransferListingRepository _listingRepo = null!;
        private TransferOfferRepository _offerRepo = null!;
        private PlayerRepository _playerRepo = null!;
        private TransferMarketService _transferMarket = null!;
        private MessageService _messages = null!;

        private static readonly DateTime Today = new(2026, 6, 1);

        public FreeAgentServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_freeagent_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _contractRepo = new ContractRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            _offerRepo = new TransferOfferRepository(_db);
            _playerRepo = new PlayerRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            _transferMarket = new TransferMarketService(
                _listingRepo, _offerRepo, new LoanAgreementRepository(_db), _teamRepo, _contractRepo, _messages);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task ReleaseExpiredContractsAsync_RemovesPlayerAndListsHimFeeFree()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            await _teamRepo.SaveTeamAsync(team);
            var player = team.Players[0];

            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = player.Id,
                HolderType = ContractHolderType.Player,
                TeamId = team.Id,
                StartDate = Today.AddYears(-2),
                EndDate = Today.AddDays(-1), // already expired
                AnnualSalary = 100_000,
            });

            await FreeAgentService.ReleaseExpiredContractsAsync(
                [team], _contractRepo, _listingRepo, _playerRepo, _messages, season: 1, Today, humanTeamId: team.Id);

            Assert.DoesNotContain(team.Players, p => p.Id == player.Id);

            var reloadedPlayer = await _playerRepo.GetPlayerAsync(player.Id);
            Assert.Equal(0, reloadedPlayer!.TeamId);

            var listing = await _listingRepo.GetByPlayerAsync(player.Id);
            Assert.NotNull(listing);
            Assert.True(listing!.IsFreeAgent);
            Assert.Equal(0, listing.AskingPrice);
            Assert.Equal(0, listing.TeamId);

            Assert.Empty(await _contractRepo.GetByTeamAsync(team.Id));
        }

        [Fact]
        public async Task ReleaseExpiredContractsAsync_LeavesAStillActiveContractAlone()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            await _teamRepo.SaveTeamAsync(team);
            var player = team.Players[0];

            await _contractRepo.SaveAsync(new Contract
            {
                HolderId = player.Id,
                HolderType = ContractHolderType.Player,
                TeamId = team.Id,
                StartDate = Today.AddYears(-1),
                EndDate = Today.AddYears(1),
                AnnualSalary = 100_000,
            });

            await FreeAgentService.ReleaseExpiredContractsAsync(
                [team], _contractRepo, _listingRepo, _playerRepo, _messages, season: 1, Today, humanTeamId: team.Id);

            Assert.Contains(team.Players, p => p.Id == player.Id);
            Assert.Null(await _listingRepo.GetByPlayerAsync(player.Id));
        }

        [Fact]
        public async Task EvaluateOffersAsync_AcceptsAFairWageOffer_SignsThePlayerAtTheBuyer()
        {
            var buyingTeam = TestHelpers.CreateTeam("Käufer FC", baseRating: 60);
            buyingTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(buyingTeam);

            var freeAgent = TestHelpers.CreateTeam("Ex Verein", baseRating: 60).Players[0];
            freeAgent.TeamId = 0;
            await _playerRepo.SavePlayerAsync(freeAgent);

            var listing = new TransferListing
            {
                PlayerId = freeAgent.Id, TeamId = 0, AskingPrice = 0, Season = 1, ListedDate = Today, IsFreeAgent = true,
            };
            await _listingRepo.SaveAsync(listing);

            double fairWage = PlayerValuationService.EstimateAnnualSalary(freeAgent);
            await _offerRepo.SaveAsync(new TransferOffer
            {
                ListingId = listing.Id, OfferingTeamId = buyingTeam.Id, OfferedFee = 0, WageOffer = fairWage,
                Status = TransferOfferStatus.Pending, CreatedDate = Today,
            });

            var teamsById = new Dictionary<int, Team> { [buyingTeam.Id] = buyingTeam };
            await FreeAgentService.EvaluateOffersAsync(
                teamsById, _playerRepo, _listingRepo, _offerRepo, _transferMarket, _messages, Today);

            Assert.Contains(buyingTeam.Players, p => p.Id == freeAgent.Id);
            Assert.Null(await _listingRepo.GetByIdAsync(listing.Id));

            var contracts = await _contractRepo.GetByHolderAsync(freeAgent.Id, ContractHolderType.Player);
            var newContract = contracts.FirstOrDefault(c => c.TeamId == buyingTeam.Id);
            Assert.NotNull(newContract);
            Assert.Equal(fairWage, newContract!.AnnualSalary);
        }

        [Fact]
        public async Task EvaluateOffersAsync_RejectsALowballWageOffer_PlayerStaysUnsigned()
        {
            var buyingTeam = TestHelpers.CreateTeam("Käufer FC", baseRating: 60);
            buyingTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(buyingTeam);

            var freeAgent = TestHelpers.CreateTeam("Ex Verein", baseRating: 60).Players[0];
            freeAgent.TeamId = 0;
            await _playerRepo.SavePlayerAsync(freeAgent);

            var listing = new TransferListing
            {
                PlayerId = freeAgent.Id, TeamId = 0, AskingPrice = 0, Season = 1, ListedDate = Today, IsFreeAgent = true,
            };
            await _listingRepo.SaveAsync(listing);

            await _offerRepo.SaveAsync(new TransferOffer
            {
                ListingId = listing.Id, OfferingTeamId = buyingTeam.Id, OfferedFee = 0, WageOffer = 1,
                Status = TransferOfferStatus.Pending, CreatedDate = Today,
            });

            var teamsById = new Dictionary<int, Team> { [buyingTeam.Id] = buyingTeam };
            await FreeAgentService.EvaluateOffersAsync(
                teamsById, _playerRepo, _listingRepo, _offerRepo, _transferMarket, _messages, Today);

            Assert.DoesNotContain(buyingTeam.Players, p => p.Id == freeAgent.Id);
            Assert.NotNull(await _listingRepo.GetByIdAsync(listing.Id));
        }
    }
}
