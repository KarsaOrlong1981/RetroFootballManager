using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TrainingCampServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TrainingCampRepository _campRepo = null!;
        private FixtureRepository _fixtureRepo = null!;
        private MessageService _messages = null!;
        private TrainingCampService _service = null!;

        private static readonly DateTime Today = new(2026, 6, 1);

        public TrainingCampServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_trainingcamp_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _campRepo = new TrainingCampRepository(_db);
            _fixtureRepo = new FixtureRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            _service = new TrainingCampService(_campRepo, _fixtureRepo, _messages, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task CanBookAsync_RejectsCampThatWouldOverlapAnAlreadyScheduledFriendly()
        {
            // Regression test: FriendlyService.CanScheduleAsync already rejected a friendly date
            // overlapping an active training camp, but the reverse never checked - a friendly
            // scheduled FIRST, then a training camp booked over the same date range, was silently
            // allowed. Both directions must be symmetric.
            var team = TestHelpers.CreateTeam("Symmetrie FC", baseRating: 60);
            await _fixtureRepo.SaveAsync(new Fixture
            {
                Season = 1, LeagueTier = 1, Matchday = 0, Date = Today.AddDays(1),
                HomeTeamId = team.Id, AwayTeamId = 999, IsFriendly = true, Played = false,
            });

            var (allowed, reason) = await _service.CanBookAsync(
                team.Id, durationWeeks: 1, Today, windowEnd: Today.AddDays(30));

            Assert.False(allowed);
            Assert.NotNull(reason);
        }

        [Fact]
        public async Task GetActiveCampAsync_ReturnsCamp_WhenCurrentDateIsWithinItsRange()
        {
            var team = TestHelpers.CreateTeam("Basic FC", baseRating: 60);
            await _campRepo.SaveAsync(new TrainingCamp
            {
                TeamId = team.Id, Tier = TrainingCampTier.Advanced, DurationWeeks = 2,
                StartDate = Today, EndDate = Today.AddDays(14),
            });

            var active = await _service.GetActiveCampAsync(team.Id, Today.AddDays(5));

            Assert.NotNull(active);
            Assert.Equal(TrainingCampTier.Advanced, active!.Tier);
        }

        [Fact]
        public async Task GetActiveCampAsync_ReturnsNull_WhenNoCampOverlapsCurrentDate()
        {
            var team = TestHelpers.CreateTeam("No Camp FC", baseRating: 60);

            var active = await _service.GetActiveCampAsync(team.Id, Today);

            Assert.Null(active);
        }

        [Fact]
        public async Task CanBookAsync_RejectsWithoutWindow()
        {
            var (allowed, reason) = await _service.CanBookAsync(teamId: 1, durationWeeks: 1, Today, windowEnd: null);

            Assert.False(allowed);
            Assert.NotNull(reason);
        }

        [Fact]
        public async Task CanBookAsync_RejectsWhenTooLittleTimeLeft()
        {
            var windowEnd = Today.AddDays(3);

            var (allowed, reason) = await _service.CanBookAsync(teamId: 1, durationWeeks: 2, Today, windowEnd);

            Assert.False(allowed);
            Assert.Contains("Tag", reason);
        }

        [Fact]
        public async Task CanBookAsync_AllowsWhenDurationFits()
        {
            var windowEnd = Today.AddDays(30);

            var (allowed, _) = await _service.CanBookAsync(teamId: 1, durationWeeks: 2, Today, windowEnd);

            Assert.True(allowed);
        }

        [Fact]
        public async Task CanBookAsync_RejectsSecondCampWhileOneUnapplied()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Id = 1;
            var windowEnd = Today.AddDays(60);
            await _service.BookAsync(team, TrainingCampTier.Basic, 1, Today);

            var (allowed, reason) = await _service.CanBookAsync(team.Id, 1, Today, windowEnd);

            Assert.False(allowed);
            Assert.NotNull(reason);
        }

        [Fact]
        public async Task BookAsync_DeductsCostImmediately()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Id = 1;
            team.Finances = new Finances { CurrentBalance = 1_000_000 };

            var camp = await _service.BookAsync(team, TrainingCampTier.Elite, 2, Today);

            Assert.Equal(1_000_000 - (int)camp.Cost, team.Finances.CurrentBalance);
        }

        [Fact]
        public async Task ApplyDueCampsAsync_DoesNotApplyBeforeEndDate()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Id = 1;
            await _service.BookAsync(team, TrainingCampTier.Basic, 1, Today);

            await _service.ApplyDueCampsAsync(team, Today.AddDays(3));

            Assert.Empty(await _messages.GetInboxAsync());
        }

        [Fact]
        public async Task ApplyDueCampsAsync_AppliesMoraleBoost_OnEndDate()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Id = 1;
            int moraleBefore = team.Statistics!.Morale;
            await _service.BookAsync(team, TrainingCampTier.Basic, 1, Today);

            await _service.ApplyDueCampsAsync(team, Today.AddDays(7));

            Assert.True(team.Statistics.Morale > moraleBefore);
            Assert.Single(await _messages.GetInboxAsync());
        }

        [Fact]
        public async Task ApplyDueCampsAsync_BoostsKeyAttributes_ForEliteTwoWeekCamp()
        {
            var team = TestHelpers.CreateTeam("Verein", baseRating: 60);
            team.Id = 1;
            var forward = team.Players.First(p => p.Position == Position.Forward);
            int offenseBefore = forward.OffensivePower;
            await _service.BookAsync(team, TrainingCampTier.Elite, 2, Today);

            await _service.ApplyDueCampsAsync(team, Today.AddDays(14));

            Assert.True(forward.OffensivePower > offenseBefore);
        }
    }
}
