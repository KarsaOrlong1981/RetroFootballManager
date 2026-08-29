using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TransferAiServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TransferMarketService _service = null!;
        private TeamRepository _teamRepo = null!;
        private TransferListingRepository _listingRepo = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public TransferAiServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_transfer_ai_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            var contractRepo = new ContractRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            var offerRepo = new TransferOfferRepository(_db);
            var loanRepo = new LoanAgreementRepository(_db);
            _service = new TransferMarketService(_listingRepo, offerRepo, loanRepo, _teamRepo, contractRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private async Task<Team> CreateTeamWithSurplusAsync(string name, int balance)
        {
            var team = TestHelpers.CreateTeam(name, baseRating: 60);
            team.Finances = new Finances { CurrentBalance = balance };
            // Fünf weitere Innenverteidiger hinzufügen -> diese Position ist jetzt überbesetzt.
            for (int i = 0; i < 5; i++)
            {
                team.Players.Add(new Player
                {
                    Name = $"Extra IV {i}", Position = Position.CentralDefender, Age = 25, Rating = 50 + i,
                    Status = PlayerStatus.Available,
                });
            }
            await _teamRepo.SaveTeamAsync(team);
            return team;
        }

        // Regression test: a freshly-generated squad (PlayerGenerator.DefaultPositionPlan, not
        // the artificially-inflated CreateTeamWithSurplusAsync helper above) used to never
        // trigger a surplus listing at all - the old ">4" threshold could never fire against a
        // generation plan that never produces more than 4 of any position, leaving the AI
        // transfer market structurally dead for an entire first season (confirmed via a season-
        // long AI-only simulation: 0 transfers across 72 clubs).
        [Fact]
        public async Task RunWeeklyTickAsync_ListsAPlayer_FromAFreshlyGeneratedSquad()
        {
            var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 60, squadSize: 27, random: new Random(7));
            int id = 1;
            foreach (var p in players)
                p.Id = id++;

            var team = new Team { Name = "Frisch FC", Statistics = new TeamStats(), Finances = new Finances { CurrentBalance = 1_000_000 } };
            team.Players.AddRange(players);
            await _teamRepo.SaveTeamAsync(team);

            await TransferAiService.RunWeeklyTickAsync(team, [], _service, Difficulty.Hard, season: 1, Today, ForceActive());

            var listings = await _listingRepo.GetByTeamAsync(team.Id);
            Assert.Single(listings);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_ListsWeakestPlayerFromOverstockedPosition()
        {
            var team = await CreateTeamWithSurplusAsync("KI FC", 1_000_000);
            var rng = new Random(1); // erste Zahl < ActivityChance(Hard) garantiert Aktivität

            await TransferAiService.RunWeeklyTickAsync(
                team, [], _service, Difficulty.Hard, season: 1, Today, ForceActive());

            var listings = await _listingRepo.GetByTeamAsync(team.Id);
            Assert.Single(listings);

            var listedPlayer = team.Players.First(p => p.Id == listings[0].PlayerId);
            Assert.Equal(Position.CentralDefender, listedPlayer.Position);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_NeverListsSamePlayerTwice()
        {
            var team = await CreateTeamWithSurplusAsync("KI FC", 1_000_000);

            var firstListings = new List<TransferListing>();
            for (int i = 0; i < 3; i++)
            {
                await TransferAiService.RunWeeklyTickAsync(
                    team, firstListings, _service, Difficulty.Hard, season: 1, Today, ForceActive());
                firstListings = await _listingRepo.GetByTeamAsync(team.Id);
            }

            var playerIds = firstListings.Select(l => l.PlayerId).ToList();
            Assert.Equal(playerIds.Count, playerIds.Distinct().Count());
        }

        [Fact]
        public async Task RunWeeklyTickAsync_MakesOffer_OnlyWhenAffordable()
        {
            var richTeam = TestHelpers.CreateTeam("Reich", baseRating: 60);
            richTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(richTeam);

            var poorTeam = TestHelpers.CreateTeam("Arm", baseRating: 60);
            poorTeam.Finances = new Finances { CurrentBalance = 1_000 };
            await _teamRepo.SaveTeamAsync(poorTeam);

            var sellerListing = new TransferListing
            {
                PlayerId = 9999, TeamId = 555, AskingPrice = 500_000, Season = 1, ListedDate = Today,
            };

            await TransferAiService.RunWeeklyTickAsync(
                poorTeam, [sellerListing], _service, Difficulty.Hard, season: 1, Today, ForceActive());
            await TransferAiService.RunWeeklyTickAsync(
                richTeam, [sellerListing], _service, Difficulty.Hard, season: 1, Today, ForceActive());

            var offers = await _service.GetOffersForListingAsync(sellerListing);
            Assert.Single(offers);
            Assert.Equal(richTeam.Id, offers[0].OfferingTeamId);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_SkipsListing_WhenFeeIsTooLargeAShareOfCurrentBalance()
        {
            // A team with plenty of cash by the flat "afford it at all" check, but the fee would
            // still eat most of that balance in one shot - the season-end trend (cautionFactor)
            // stays healthy here (default 1.0), so only the new per-transfer balance-share cap
            // can catch this. Normal difficulty caps a single fee at 40% of current balance.
            var team = TestHelpers.CreateTeam("Sparsam", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(team);

            var tooExpensiveListing = new TransferListing
            {
                PlayerId = 9999, TeamId = 555, AskingPrice = 8_000_000, Season = 1, ListedDate = Today,
            };

            await TransferAiService.RunWeeklyTickAsync(
                team, [tooExpensiveListing], _service, Difficulty.Normal, season: 1, Today, ForceActive());

            Assert.Empty(await _service.GetOffersForListingAsync(tooExpensiveListing));
        }

        [Fact]
        public async Task RunWeeklyTickAsync_PrefersAListingThatFillsARealSquadShortage()
        {
            // Team is missing goalkeepers entirely (0 < MinDepthPerPosition) - a real hole, not
            // just a nice-to-have upgrade. Two affordable listings exist: a random midfielder and
            // the actually-needed goalkeeper - the AI should recruit for the position it needs,
            // not pick randomly ("scouten und anwerben, wenn sie die Spieler brauchen").
            var team = TestHelpers.CreateTeam("Bedarf FC", baseRating: 60);
            team.Players.RemoveAll(p => p.Position == Position.Goalkeeper);
            team.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(team);

            var otherTeam = TestHelpers.CreateTeam("Andere", baseRating: 60);
            await _teamRepo.SaveTeamAsync(otherTeam);

            var neededPlayer = otherTeam.Players.First(p => p.Position == Position.Goalkeeper);
            var neededListing = await _service.ListPlayerAsync(neededPlayer, otherTeam, askingPrice: 100_000, season: 1, Today);
            var randomPlayer = otherTeam.Players.First(p => p.Position == Position.CentralMidfielder);
            var randomListing = await _service.ListPlayerAsync(randomPlayer, otherTeam, askingPrice: 100_000, season: 1, Today);

            var teamsById = new Dictionary<int, Team> { [team.Id] = team, [otherTeam.Id] = otherTeam };
            var allListings = await _listingRepo.GetBySeasonAsync(1);
            await TransferAiService.RunWeeklyTickAsync(
                team, allListings, _service, Difficulty.Hard, season: 1, Today, ForceActive(), teamsById: teamsById);

            var offers = await _service.GetOffersForListingAsync(neededListing);
            Assert.Single(offers);
            Assert.Empty(await _service.GetOffersForListingAsync(randomListing));
        }

        [Fact]
        public async Task RunWeeklyTickAsync_NeverMakesOffer_WhenCautionFactorIsLow()
        {
            // A financially-healthy team by balance alone, but the caller (AiManagerService)
            // passed a near-zero caution factor - e.g. because its own season-end projection is
            // in real trouble (see FinanceAiService.ComputeCautionFactor). Selling (the surplus-
            // listing half) must still work; only spending on a bid is suppressed.
            var team = await CreateTeamWithSurplusAsync("Vorsichtig", 1_000_000);
            var sellerListing = new TransferListing
            {
                PlayerId = 9999, TeamId = 555, AskingPrice = 1, Season = 1, ListedDate = Today,
            };

            await TransferAiService.RunWeeklyTickAsync(
                team, [sellerListing], _service, Difficulty.Hard, season: 1, Today, ForceActive(), cautionFactor: 0.05);

            var offers = await _service.GetOffersForListingAsync(sellerListing);
            Assert.Empty(offers);

            var ownListings = await _listingRepo.GetByTeamAsync(team.Id);
            Assert.Single(ownListings);
        }

        [Fact]
        public async Task RunWeeklyTickAsync_NeverMakesOffer_WhenBalanceIsNegative()
        {
            var brokeTeam = TestHelpers.CreateTeam("Pleite", baseRating: 60);
            brokeTeam.Finances = new Finances { CurrentBalance = -1 };
            await _teamRepo.SaveTeamAsync(brokeTeam);

            // Deliberately cheap - would easily be "affordable" by price alone.
            var sellerListing = new TransferListing
            {
                PlayerId = 9999, TeamId = 555, AskingPrice = 1, Season = 1, ListedDate = Today,
            };

            await TransferAiService.RunWeeklyTickAsync(
                brokeTeam, [sellerListing], _service, Difficulty.Hard, season: 1, Today, ForceActive());

            var offers = await _service.GetOffersForListingAsync(sellerListing);
            Assert.Empty(offers);
        }

        [Fact]
        public async Task EvaluateIncomingOffersAsync_LeavesAGoodOfferPending_WhenTransferWindowClosed()
        {
            var seller = await CreateTeamWithSurplusAsync("Verkäufer", 1_000_000);
            var buyer = TestHelpers.CreateTeam("Käufer", baseRating: 60);
            buyer.Finances = new Finances { CurrentBalance = 5_000_000 };
            await _teamRepo.SaveTeamAsync(buyer);

            var player = seller.Players.First(p => p.Position == Position.CentralDefender);
            var listing = await _service.ListPlayerAsync(player, seller, askingPrice: 1_000_000, season: 1, Today);
            var offer = await _service.MakeOfferAsync(listing, buyer, fee: 900_000, wageOffer: 100_000, Today);

            var teamsById = new Dictionary<int, Team> { [seller.Id] = seller, [buyer.Id] = buyer };
            await TransferAiService.EvaluateIncomingOffersAsync(
                seller, [listing], _service, teamsById, Today, isTransferWindowOpen: false);

            // Negotiating still happened (offer wasn't rejected) - just not completed yet.
            Assert.Contains(player, seller.Players);
            Assert.DoesNotContain(player, buyer.Players);
            var offers = await _service.GetOffersForListingAsync(listing);
            Assert.Equal(TransferOfferStatus.Pending, offers.Single(o => o.Id == offer.Id).Status);
        }

        [Fact]
        public async Task EvaluateIncomingOffersAsync_AcceptsGoodEnoughTransferOffer()
        {
            var seller = await CreateTeamWithSurplusAsync("Verkäufer", 1_000_000);
            var buyer = TestHelpers.CreateTeam("Käufer", baseRating: 60);
            buyer.Finances = new Finances { CurrentBalance = 5_000_000 };
            await _teamRepo.SaveTeamAsync(buyer);

            var player = seller.Players.First(p => p.Position == Position.CentralDefender);
            var listing = await _service.ListPlayerAsync(player, seller, askingPrice: 1_000_000, season: 1, Today);
            await _service.MakeOfferAsync(listing, buyer, fee: 900_000, wageOffer: 100_000, Today);

            var teamsById = new Dictionary<int, Team> { [seller.Id] = seller, [buyer.Id] = buyer };
            await TransferAiService.EvaluateIncomingOffersAsync(seller, [listing], _service, teamsById, Today);

            Assert.Contains(player, buyer.Players);
            Assert.DoesNotContain(player, seller.Players);
        }

        [Fact]
        public async Task EvaluateIncomingOffersAsync_RejectsTooLowTransferOffer()
        {
            var seller = await CreateTeamWithSurplusAsync("Verkäufer", 1_000_000);
            var buyer = TestHelpers.CreateTeam("Käufer", baseRating: 60);
            buyer.Finances = new Finances { CurrentBalance = 5_000_000 };
            await _teamRepo.SaveTeamAsync(buyer);

            var player = seller.Players.First(p => p.Position == Position.CentralDefender);
            var listing = await _service.ListPlayerAsync(player, seller, askingPrice: 1_000_000, season: 1, Today);
            await _service.MakeOfferAsync(listing, buyer, fee: 200_000, wageOffer: 50_000, Today);

            var teamsById = new Dictionary<int, Team> { [seller.Id] = seller, [buyer.Id] = buyer };
            await TransferAiService.EvaluateIncomingOffersAsync(seller, [listing], _service, teamsById, Today);

            Assert.Contains(player, seller.Players);
            var offers = await _service.GetOffersForListingAsync(listing);
            Assert.Equal(TransferOfferStatus.Rejected, offers[0].Status);
        }

        [Fact]
        public async Task EvaluateIncomingOffersAsync_AcceptsLoanOffer_MovesPlayerOnly()
        {
            var origin = await CreateTeamWithSurplusAsync("Verleiher", 1_000_000);
            var loanTeam = TestHelpers.CreateTeam("Leihteam", baseRating: 60);
            loanTeam.Finances = new Finances { CurrentBalance = 5_000_000 };
            await _teamRepo.SaveTeamAsync(loanTeam);

            var player = origin.Players.First(p => p.Position == Position.CentralDefender);
            var listing = await _service.ListPlayerAsync(player, origin, askingPrice: 1_000_000, season: 1, Today, isLoanListing: true);
            await _service.MakeOfferAsync(listing, loanTeam, fee: 0, wageOffer: 60_000, Today);

            var teamsById = new Dictionary<int, Team> { [origin.Id] = origin, [loanTeam.Id] = loanTeam };
            await TransferAiService.EvaluateIncomingOffersAsync(origin, [listing], _service, teamsById, Today);

            Assert.Contains(player, loanTeam.Players);
            Assert.DoesNotContain(player, origin.Players);
            var remainingListings = await _listingRepo.GetByTeamAsync(origin.Id);
            Assert.Empty(remainingListings);
        }

        // Ein Random, dessen erster NextDouble()-Wert garantiert unter jeder ActivityChance
        // liegt (0.0), damit die KI in Tests deterministisch aktiv wird.
        private static Random ForceActive() => new AlwaysZeroRandom();

        private sealed class AlwaysZeroRandom : Random
        {
            public override double NextDouble() => 0.0;
        }
    }
}
