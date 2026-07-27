using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class CalendarAdvanceServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private FixtureRepository _fixtureRepo = null!;
        private MessageService _messages = null!;
        private CalendarAdvanceService _service = null!;

        private static readonly DateTime Today = new(2026, 6, 1);

        public CalendarAdvanceServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_calendaradvance_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _fixtureRepo = new FixtureRepository(_db);
            var contractRepo = new ContractRepository(_db);
            var listingRepo = new TransferListingRepository(_db);
            var offerRepo = new TransferOfferRepository(_db);
            var loanRepo = new LoanAgreementRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            var transferMarket = new TransferMarketService(listingRepo, offerRepo, loanRepo, _teamRepo, contractRepo);
            var staffMarket = new StaffMarketService(_db, _teamRepo, contractRepo, new Random(1));
            var aiManager = new AiManagerService(transferMarket, staffMarket, contractRepo, listingRepo);
            var expiryWarnings = new ExpiryWarningService(contractRepo, loanRepo, _messages);
            var sponsorRepo = new SponsorRepository(_db);
            var sponsorshipRepo = new SponsorshipRepository(_db);
            var finance = new FinanceService(sponsorRepo, sponsorshipRepo, contractRepo, _messages);
            var trainingCamps = new TrainingCampService(new TrainingCampRepository(_db), _fixtureRepo, _messages, new Random(1));
            _service = new CalendarAdvanceService(_teamRepo, _fixtureRepo, aiManager, expiryWarnings, finance, trainingCamps, _messages, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private async Task<Team> SetupHumanTeamAsync()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 100_000 };
            await _teamRepo.SaveTeamAsync(team);
            return team;
        }

        [Fact]
        public async Task AdvanceOneDayAsync_AdvancesCurrentDateByOneDay()
        {
            var team = await SetupHumanTeamAsync();
            var state = new GameState { ManagerTeamId = team.Id, Season = 1, CurrentDate = Today };

            await _service.AdvanceOneDayAsync(state, [team]);

            Assert.Equal(Today.AddDays(1), state.CurrentDate);
        }

        [Fact]
        public async Task AdvanceOneDayAsync_SevenCallsInARow_AdvanceCurrentDateBySevenDays()
        {
            var team = await SetupHumanTeamAsync();
            var state = new GameState { ManagerTeamId = team.Id, Season = 1, CurrentDate = Today };

            for (int i = 0; i < 7; i++)
                await _service.AdvanceOneDayAsync(state, [team]);

            Assert.Equal(Today.AddDays(7), state.CurrentDate);
        }

        [Fact]
        public async Task AdvanceOneDayAsync_AppliesMonthlySettlement_OnThe15th()
        {
            var team = await SetupHumanTeamAsync();
            team.Employees.Add(new Employee { Salary = 12 * 100 });
            var state = new GameState { ManagerTeamId = team.Id, Season = 1, CurrentDate = new DateTime(2026, 6, 14) };

            await _service.AdvanceOneDayAsync(state, [team]);

            Assert.Equal(100, team.Finances!.StaffWages);
            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.CalendarAdvanceSummary);
        }

        [Fact]
        public async Task AdvanceOneDayAsync_RecoversInjuredPlayer_OnceInjuredUntilReached_AndSendsMessage()
        {
            var team = await SetupHumanTeamAsync();
            var player = team.Players[0];
            player.Status = PlayerStatus.Injured;
            player.InjuredUntil = Today.AddDays(1);
            var state = new GameState { ManagerTeamId = team.Id, Season = 1, CurrentDate = Today };

            await _service.AdvanceOneDayAsync(state, [team]);

            Assert.Equal(PlayerStatus.Available, player.Status);
            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.PlayerRecovered);
        }

        [Fact]
        public async Task AdvanceOneDayAsync_SendsFinanceWarning_WhenBalanceNegative()
        {
            var team = await SetupHumanTeamAsync();
            team.Finances!.CurrentBalance = -5_000;
            var state = new GameState { ManagerTeamId = team.Id, Season = 1, CurrentDate = Today };

            await _service.AdvanceOneDayAsync(state, [team]);

            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.FinanceWarning);
        }

        [Fact]
        public async Task AdvanceOneDayAsync_ScoutingCompletion_UpdatesInMemoryPlayerObject_NotJustDb()
        {
            // Regression: ApplyDueScoutingAsync used to only update a fresh DB-loaded Player
            // instance, leaving the object held in the caller's `teams` list (== GameSession.Teams
            // in production) stale - the "?" info-button bug.
            var humanTeam = await SetupHumanTeamAsync();
            var targetTeam = TestHelpers.CreateTeam("Ziel FC", baseRating: 60);
            await _teamRepo.SaveTeamAsync(targetTeam);
            var targetPlayer = targetTeam.Players[0];
            Assert.False(targetPlayer.IsScouted);

            var saveGame = new SaveGameService(_db);
            var (started, _) = await saveGame.TryStartScoutingAsync(humanTeam, targetPlayer, Today);
            Assert.True(started);

            var serviceWithSaveGame = new CalendarAdvanceService(
                _teamRepo, _fixtureRepo, new AiManagerService(
                    new TransferMarketService(new TransferListingRepository(_db), new TransferOfferRepository(_db),
                        new LoanAgreementRepository(_db), _teamRepo, new ContractRepository(_db)),
                    new StaffMarketService(_db, _teamRepo, new ContractRepository(_db), new Random(1)),
                    new ContractRepository(_db), new TransferListingRepository(_db)),
                new ExpiryWarningService(new ContractRepository(_db), new LoanAgreementRepository(_db), _messages),
                new FinanceService(new SponsorRepository(_db), new SponsorshipRepository(_db), new ContractRepository(_db), _messages),
                new TrainingCampService(new TrainingCampRepository(_db), _fixtureRepo, _messages, new Random(1)),
                _messages, new Random(1), saveGame);

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today.AddDays(13) };
            await serviceWithSaveGame.AdvanceOneDayAsync(state, [humanTeam, targetTeam]);

            Assert.True(targetPlayer.IsScouted);
        }
    }
}
