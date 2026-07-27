using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class NewCareerTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private SaveGameService _service = null!;

        public NewCareerTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_career_db_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _service = new SaveGameService(new AppDatabase(_dbPath));
            await _service.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _service.CloseAsync();
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }

        [Fact]
        public async Task StartNewCareer_SavesFixturesAndSetsManagerTeam()
        {
            // Zwei Ligen mit je zwei Teams (Fixture-Generator braucht gerade Anzahl).
            var teams = new List<Team>();
            for (int tier = 1; tier <= 2; tier++)
            {
                for (int i = 0; i < 2; i++)
                {
                    var t = TestHelpers.CreateTeam($"T{tier}-{i}", baseRating: 60);
                    t.LeagueTier = tier;
                    teams.Add(t);
                }
            }

            var leagues = new List<League>
            {
                new() { Name = "Liga 1", Tier = 1, Season = 1 },
                new() { Name = "Liga 2", Tier = 2, Season = 1 },
            };

            var managerTeam = teams.First(t => t.LeagueTier == 2);
            var seasonStart = new DateTime(2026, 8, 1);

            var state = await _service.StartNewCareerAsync(
                "Karriere", season: 1, leagues, teams, managerTeam, seasonStart);

            Assert.Equal(managerTeam.Id, state.ManagerTeamId);
            Assert.NotEqual(0, state.ManagerTeamId);

            var fixtures = await _service.GetFixturesAsync(1);
            // 2 Teams pro Liga → Hin+Rück = 2 Partien pro Liga, 2 Ligen = 4.
            Assert.Equal(4, fixtures.Count);
            Assert.Equal(2, fixtures.Count(f => f.LeagueTier == 1));

            var savedLeagues = await _service.GetLeaguesAsync(1);
            Assert.Equal(2, savedLeagues.Count);

            var loaded = await _service.LoadGameAsync();
            Assert.NotNull(loaded);
            Assert.Equal(4, loaded!.Value.Teams.Count);
        }

        [Fact]
        public async Task StartNewCareer_SeedsLastSettlementToStartMonth_SoFirstRealSettlementWaitsAFullMonth()
        {
            // Regression test: without this, a brand-new career would already deduct a full
            // month of wages/upkeep on the 15th of the very first (partial) preseason month -
            // felt like an immediate punishment right after starting. Seeding LastSettlementMonth/
            // Year to the starting month makes FinanceService.ApplyMonthlySettlementAsync treat
            // that month as already settled, so the first REAL settlement waits for next month.
            var teams = new List<Team>();
            for (int i = 0; i < 2; i++)
            {
                var t = TestHelpers.CreateTeam($"T{i}", baseRating: 60);
                t.LeagueTier = 1;
                t.Finances = new Finances { CurrentBalance = 100_000 };
                teams.Add(t);
            }

            var leagues = new List<League> { new() { Name = "Liga 1", Tier = 1, Season = 1 } };
            var seasonStart = new DateTime(2026, 8, 1);

            await _service.StartNewCareerAsync("Karriere", season: 1, leagues, teams, teams[0], seasonStart);

            var initialDate = seasonStart.AddMonths(-2); // = 1. Juni
            foreach (var team in teams)
            {
                Assert.Equal(initialDate.Month, team.Finances!.LastSettlementMonth);
                Assert.Equal(initialDate.Year, team.Finances.LastSettlementYear);
            }
        }
    }
}
