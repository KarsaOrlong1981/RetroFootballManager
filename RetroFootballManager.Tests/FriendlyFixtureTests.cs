using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class FriendlyFixtureTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private FixtureRepository _fixtureRepo = null!;
        private TeamRepository _teamRepo = null!;
        private TrainingCampRepository _campRepo = null!;
        private FriendlyService _service = null!;

        private static readonly DateTime Today = new(2026, 6, 1);

        public FriendlyFixtureTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_friendly_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _fixtureRepo = new FixtureRepository(_db);
            _teamRepo = new TeamRepository(_db);
            _campRepo = new TrainingCampRepository(_db);
            _service = new FriendlyService(
                _fixtureRepo, _teamRepo, _campRepo,
                new MessageService(new MessageRepository(_db)), new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private async Task<(Team Home, Team Away)> SetupTeamsAsync()
        {
            var home = TestHelpers.CreateTeam("Heim", baseRating: 60);
            await _teamRepo.SaveTeamAsync(home);
            var away = TestHelpers.CreateTeam("Gast", baseRating: 60);
            await _teamRepo.SaveTeamAsync(away);
            return (home, away);
        }

        [Fact]
        public async Task ScheduleAsync_CreatesFriendlyFixture()
        {
            var (home, away) = await SetupTeamsAsync();

            var fixture = await _service.ScheduleAsync(season: 1, home, away, Today);

            Assert.True(fixture.IsFriendly);
            Assert.False(fixture.Played);
        }

        [Fact]
        public async Task PlayDueFriendlyAsync_MarksPlayed_DoesNotSetMatchday()
        {
            var (home, away) = await SetupTeamsAsync();
            var fixture = await _service.ScheduleAsync(season: 1, home, away, Today);

            await _service.PlayDueFriendlyAsync(fixture, home, away, humanTeamId: home.Id);

            Assert.True(fixture.Played);
            Assert.Equal(0, fixture.Matchday);
        }

        [Fact]
        public async Task GetBySeasonAsync_ExcludesFriendlies()
        {
            var (home, away) = await SetupTeamsAsync();
            await _service.ScheduleAsync(season: 1, home, away, Today);
            await _fixtureRepo.SaveAsync(new Fixture
            {
                Season = 1, LeagueTier = 1, Matchday = 1, Date = Today,
                HomeTeamId = home.Id, AwayTeamId = away.Id, IsFriendly = false,
            });

            var seasonFixtures = await _fixtureRepo.GetBySeasonAsync(1);

            Assert.Single(seasonFixtures);
            Assert.False(seasonFixtures[0].IsFriendly);
        }

        [Fact]
        public async Task CanScheduleAsync_RejectsWithoutWindow()
        {
            var (home, _) = await SetupTeamsAsync();

            var (allowed, reason) = await _service.CanScheduleAsync(home.Id, Today.AddDays(5), windowEnd: null);

            Assert.False(allowed);
            Assert.NotNull(reason);
        }

        [Fact]
        public async Task CanScheduleAsync_RejectsDateAfterWindowEnd()
        {
            var (home, _) = await SetupTeamsAsync();
            var windowEnd = Today.AddDays(10);

            var (allowed, _) = await _service.CanScheduleAsync(home.Id, windowEnd.AddDays(1), windowEnd);

            Assert.False(allowed);
        }

        [Fact]
        public async Task CanScheduleAsync_RejectsDateWithExistingFixture()
        {
            var (home, away) = await SetupTeamsAsync();
            var windowEnd = Today.AddDays(30);
            await _service.ScheduleAsync(season: 1, home, away, Today.AddDays(5));

            var (allowed, reason) = await _service.CanScheduleAsync(home.Id, Today.AddDays(5), windowEnd);

            Assert.False(allowed);
            Assert.NotNull(reason);
        }

        [Fact]
        public async Task CanScheduleAsync_RejectsDateDuringTrainingCamp()
        {
            var (home, _) = await SetupTeamsAsync();
            var windowEnd = Today.AddDays(30);
            await _campRepo.SaveAsync(new TrainingCamp
            {
                TeamId = home.Id, Tier = TrainingCampTier.Basic, DurationWeeks = 1,
                StartDate = Today, EndDate = Today.AddDays(7),
            });

            var (allowed, _) = await _service.CanScheduleAsync(home.Id, Today.AddDays(3), windowEnd);

            Assert.False(allowed);
        }

        [Fact]
        public async Task GetUpcomingFriendliesAsync_ReturnsUnplayedFriendlies_IncludingNotYetDueOnes()
        {
            var (home, away) = await SetupTeamsAsync();
            // Weit in der Zukunft - noch nicht fällig, muss aber trotzdem als "anstehend" gelten.
            await _service.ScheduleAsync(season: 1, home, away, Today.AddDays(20));

            var upcoming = await _service.GetUpcomingFriendliesAsync(home.Id);

            Assert.Single(upcoming);
            Assert.False(upcoming[0].Played);
        }

        [Fact]
        public async Task GetUpcomingFriendliesAsync_ExcludesAlreadyPlayedFriendlies()
        {
            var (home, away) = await SetupTeamsAsync();
            var fixture = await _service.ScheduleAsync(season: 1, home, away, Today);
            await _service.PlayDueFriendlyAsync(fixture, home, away, humanTeamId: home.Id);

            var upcoming = await _service.GetUpcomingFriendliesAsync(home.Id);

            Assert.Empty(upcoming);
        }

        [Fact]
        public async Task GetSuggestedDatesAsync_SkipsCampAndFixtureConflicts()
        {
            var (home, away) = await SetupTeamsAsync();
            var windowEnd = Today.AddDays(20);
            await _campRepo.SaveAsync(new TrainingCamp
            {
                TeamId = home.Id, Tier = TrainingCampTier.Basic, DurationWeeks = 1,
                StartDate = Today.AddDays(1), EndDate = Today.AddDays(8),
            });
            await _service.ScheduleAsync(season: 1, home, away, Today.AddDays(10));

            var suggestions = await _service.GetSuggestedDatesAsync(home.Id, Today, windowEnd, count: 3);

            Assert.All(suggestions, d => Assert.True(d > Today.AddDays(8) || d < Today.AddDays(1)));
            Assert.DoesNotContain(Today.AddDays(10), suggestions);
        }
    }
}
