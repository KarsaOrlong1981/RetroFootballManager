using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FinancialCrisisTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private FinanceService _service = null!;
        private MessageService _messages = null!;

        private static readonly DateTime StartDate = new(2026, 8, 1);

        public FinancialCrisisTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_crisis_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _messages = new MessageService(new MessageRepository(_db));
            _service = new FinanceService(new SponsorRepository(_db), new SponsorshipRepository(_db),
                new ContractRepository(_db), _messages);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        // A team whose season-end projection lands comfortably below the -500k crisis
        // threshold (matchdaysPlayed=10 out of 34, heavy wage spend so far).
        private static Team CreateCrisisTeam()
        {
            var team = TestHelpers.CreateTeam("Krise FC", baseRating: 60);
            team.Id = 1;
            team.Finances = new Finances { CurrentBalance = -300_000, PlayerWages = 500_000 };
            return team;
        }

        private static Team CreateHealthyTeam()
        {
            var team = TestHelpers.CreateTeam("Gesund FC", baseRating: 60);
            team.Id = 2;
            team.Finances = new Finances { CurrentBalance = 1_000_000, SponsorIncome = 100_000 };
            return team;
        }

        [Fact]
        public async Task CheckSeasonEndProjectionAsync_BelowThreshold_SetsCrisisStartDateAndSendsUltimatum()
        {
            var team = CreateCrisisTeam();
            var state = new GameState { ManagerTeamId = team.Id, MatchdayIndex = 10, CurrentDate = StartDate };

            await _service.CheckSeasonEndProjectionAsync(team, state, StartDate);

            Assert.Equal(StartDate, team.Finances!.FinancialCrisisStartDate);
            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.BoardUltimatum);
        }

        [Fact]
        public async Task CheckSeasonEndProjectionAsync_HealthyProjection_NeverStartsCrisis()
        {
            var team = CreateHealthyTeam();
            var state = new GameState { ManagerTeamId = team.Id, MatchdayIndex = 10, CurrentDate = StartDate };

            await _service.CheckSeasonEndProjectionAsync(team, state, StartDate);

            Assert.Null(team.Finances!.FinancialCrisisStartDate);
        }

        [Fact]
        public async Task CheckSeasonEndProjectionAsync_RecoversBeforeDeadline_ClearsCrisis_NoBoardMoodCrash()
        {
            var team = CreateCrisisTeam();
            var state = new GameState { ManagerTeamId = team.Id, MatchdayIndex = 10, CurrentDate = StartDate };
            await _service.CheckSeasonEndProjectionAsync(team, state, StartDate);
            Assert.NotNull(team.Finances!.FinancialCrisisStartDate);
            int boardMoodBefore = team.BoardMood;

            // Finances recover well before the 3-month deadline.
            team.Finances.CurrentBalance = 2_000_000;
            team.Finances.PlayerWages = 0;
            var recoveryDate = StartDate.AddDays(20);

            await _service.CheckSeasonEndProjectionAsync(team, state, recoveryDate);

            Assert.Null(team.Finances.FinancialCrisisStartDate);
            Assert.False(team.Finances.FinancialCrisisEscalated);
            Assert.Equal(boardMoodBefore, team.BoardMood);
        }

        [Fact]
        public async Task CheckSeasonEndProjectionAsync_ThreeMonthsUnresolved_CrashesBoardModeIntoGameOverThreshold()
        {
            var team = CreateCrisisTeam();
            team.BoardMood = ClubMoodService.GameOverThreshold + 5; // just above the dismissal line
            var state = new GameState { ManagerTeamId = team.Id, MatchdayIndex = 10, CurrentDate = StartDate };

            await _service.CheckSeasonEndProjectionAsync(team, state, StartDate);
            Assert.NotNull(team.Finances!.FinancialCrisisStartDate);

            var deadlineDate = StartDate.AddMonths(3);
            await _service.CheckSeasonEndProjectionAsync(team, state, deadlineDate);

            Assert.True(team.Finances.FinancialCrisisEscalated);
            Assert.True(team.BoardMood < ClubMoodService.GameOverThreshold, $"BoardMood={team.BoardMood}");

            // The existing dismissal path picks this up without any new logic.
            await ClubMoodService.CheckThresholds(team, state, _messages, deadlineDate);
            Assert.True(state.IsGameOver);
            Assert.Equal("Vorstand", state.GameOverReason);
        }

        [Fact]
        public async Task CheckSeasonEndProjectionAsync_EscalationOnlyHappensOnce()
        {
            var team = CreateCrisisTeam();
            var state = new GameState { ManagerTeamId = team.Id, MatchdayIndex = 10, CurrentDate = StartDate };

            await _service.CheckSeasonEndProjectionAsync(team, state, StartDate);
            var deadlineDate = StartDate.AddMonths(3);
            await _service.CheckSeasonEndProjectionAsync(team, state, deadlineDate);
            int boardMoodAfterFirstEscalation = team.BoardMood;

            // A further day past the deadline must not crash BoardMood again.
            await _service.CheckSeasonEndProjectionAsync(team, state, deadlineDate.AddDays(1));

            Assert.Equal(boardMoodAfterFirstEscalation, team.BoardMood);
        }

        [Fact]
        public async Task CheckBoardMoodPraise_FiresOnce_WhenAboveThreshold()
        {
            var team = TestHelpers.CreateTeam("Erfolgreich FC", baseRating: 60);
            team.Id = 3;
            team.BoardMood = ClubMoodService.PraiseThreshold + 1;

            await ClubMoodService.CheckBoardMoodPraise(team, _messages, StartDate);
            Assert.True(team.BoardMoodPraiseActive);
            int countAfterFirst = (await _messages.GetInboxAsync()).Count(m => m.Type == MessageType.BoardPraise);
            Assert.Equal(1, countAfterFirst);

            // Still above threshold the next day - must not fire again.
            await ClubMoodService.CheckBoardMoodPraise(team, _messages, StartDate.AddDays(1));
            int countAfterSecond = (await _messages.GetInboxAsync()).Count(m => m.Type == MessageType.BoardPraise);
            Assert.Equal(1, countAfterSecond);
        }

        [Fact]
        public async Task CheckBoardMoodPraise_ResetsFlag_WhenMoodDropsBelowResetThreshold()
        {
            var team = TestHelpers.CreateTeam("Schwankend FC", baseRating: 60);
            team.Id = 4;
            team.BoardMood = ClubMoodService.PraiseThreshold + 1;
            await ClubMoodService.CheckBoardMoodPraise(team, _messages, StartDate);
            Assert.True(team.BoardMoodPraiseActive);

            team.BoardMood = ClubMoodService.PraiseResetThreshold - 1;
            await ClubMoodService.CheckBoardMoodPraise(team, _messages, StartDate.AddDays(1));

            Assert.False(team.BoardMoodPraiseActive);
        }

        [Fact]
        public void ApplyFinancialHealthMoodCoupling_PositiveBalance_RaisesBoardMood()
        {
            var team = TestHelpers.CreateTeam("Reich FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = 1_000_000 };
            team.BoardMood = 65;

            FinanceService.ApplyFinancialHealthMoodCoupling(team);

            Assert.True(team.Finances.FinancialHealth > 50);
            Assert.True(team.BoardMood > 65);
        }

        [Fact]
        public void ApplyFinancialHealthMoodCoupling_NegativeBalance_LowersBoardMood()
        {
            var team = TestHelpers.CreateTeam("Arm FC", baseRating: 60);
            team.Finances = new Finances { CurrentBalance = -1_000_000 };
            team.BoardMood = 65;

            FinanceService.ApplyFinancialHealthMoodCoupling(team);

            Assert.True(team.Finances.FinancialHealth < 50);
            Assert.True(team.BoardMood < 65);
        }
    }
}
