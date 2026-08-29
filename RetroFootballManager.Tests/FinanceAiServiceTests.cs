using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // AI counterpart to the human-only finance-crisis machinery (FinanceService.
    // CheckSeasonEndProjectionAsync, Phase 9) - see FinanceAiService.
    public class FinanceAiServiceTests
    {
        private static Team HealthyTeam() => new()
        {
            Name = "Gesund FC",
            Finances = new Finances
            {
                CurrentBalance = 2_000_000,
                TicketIncome = 500_000,
                MerchandiseIncome = 100_000,
                SponsorIncome = 400_000,
                StaffWages = 100_000,
                PlayerWages = 300_000,
                StadiumCosts = 50_000,
            },
        };

        private static Team StrugglingTeam() => new()
        {
            Name = "Pleite FC",
            Finances = new Finances
            {
                CurrentBalance = -400_000,
                TicketIncome = 50_000,
                MerchandiseIncome = 10_000,
                SponsorIncome = 50_000,
                StaffWages = 200_000,
                PlayerWages = 600_000,
                StadiumCosts = 80_000,
            },
        };

        [Fact]
        public void ComputeCautionFactor_ReturnsFull_WhenNoMatchdaysPlayedYet()
        {
            // Not reliable yet (preseason) - nothing to react to.
            double factor = FinanceAiService.ComputeCautionFactor(StrugglingTeam(), Difficulty.Hard, matchdaysPlayed: 0);
            Assert.Equal(1.0, factor);
        }

        [Fact]
        public void ComputeCautionFactor_ReturnsFull_ForAHealthyProjection()
        {
            double factor = FinanceAiService.ComputeCautionFactor(HealthyTeam(), Difficulty.Normal, matchdaysPlayed: 10);
            Assert.Equal(1.0, factor);
        }

        [Fact]
        public void ComputeCautionFactor_TapersTowardZero_ForAStrugglingProjection()
        {
            double factor = FinanceAiService.ComputeCautionFactor(StrugglingTeam(), Difficulty.Normal, matchdaysPlayed: 10);
            Assert.InRange(factor, 0.0, 1.0);
            Assert.True(factor < 1.0, $"expected tapered caution, got {factor}");
        }

        [Fact]
        public void ComputeCautionFactor_HardDifficulty_ReactsSoonerThanEasy()
        {
            var hardTeam = StrugglingTeam();
            var easyTeam = StrugglingTeam();

            double hardFactor = FinanceAiService.ComputeCautionFactor(hardTeam, Difficulty.Hard, matchdaysPlayed: 10);
            double easyFactor = FinanceAiService.ComputeCautionFactor(easyTeam, Difficulty.Easy, matchdaysPlayed: 10);

            Assert.True(hardFactor <= easyFactor, $"hard={hardFactor}, easy={easyFactor}");
        }

        [Fact]
        public void ComputeCautionFactor_ReturnsFull_WhenNoFinances()
        {
            var team = new Team { Name = "Ohne Finanzen" };
            double factor = FinanceAiService.ComputeCautionFactor(team, Difficulty.Hard, matchdaysPlayed: 10);
            Assert.Equal(1.0, factor);
        }

    }

    public class FinanceAiServiceCrisisSaleTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TransferMarketService _market = null!;
        private TransferListingRepository _listingRepo = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public FinanceAiServiceCrisisSaleTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_finance_ai_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            var teamRepo = new TeamRepository(_db);
            var contractRepo = new ContractRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            var offerRepo = new TransferOfferRepository(_db);
            var loanRepo = new LoanAgreementRepository(_db);
            _market = new TransferMarketService(_listingRepo, offerRepo, loanRepo, teamRepo, contractRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task ListsTheWeakestNonGoalkeeper()
        {
            var team = TestHelpers.CreateTeam("Notverkauf FC", baseRating: 60);
            var weakest = team.Players.Where(p => p.Position != Position.Goalkeeper).OrderBy(p => p.Rating).First();
            weakest.Rating = 1; // unambiguously the weakest

            var listed = await FinanceAiService.TryListPlayerForCrisisFundsAsync(
                team, _market, alreadyListedIds: [], season: 1, Today);

            Assert.NotNull(listed);
            Assert.Equal(weakest.Id, listed!.Id);
            Assert.NotEqual(Position.Goalkeeper, listed.Position);

            var listings = await _listingRepo.GetByTeamAsync(team.Id);
            Assert.Single(listings);
        }

        [Fact]
        public async Task NeverListsAGoalkeeper()
        {
            var team = TestHelpers.CreateTeam("Notverkauf FC", baseRating: 60);
            var keeper = team.Players.First(p => p.Position == Position.Goalkeeper);
            keeper.Rating = 1; // weakest overall, but must never be the crisis sale

            var listed = await FinanceAiService.TryListPlayerForCrisisFundsAsync(
                team, _market, alreadyListedIds: [], season: 1, Today);

            Assert.NotNull(listed);
            Assert.NotEqual(Position.Goalkeeper, listed!.Position);
        }
    }
}
