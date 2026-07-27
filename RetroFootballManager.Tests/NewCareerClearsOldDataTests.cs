using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Regression test: starting a new career must wipe any previous career's data, or
    // teams/leagues/fixtures accumulate on every "New Game" click (reported bug: 18 -> 36 ->
    // 54 teams per league).
    public class NewCareerClearsOldDataTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private SaveGameService _service = null!;

        public NewCareerClearsOldDataTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_dup_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _service = new SaveGameService(new AppDatabase(_dbPath));
            await _service.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _service.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        private static (List<League> Leagues, List<Team> Teams) BuildSmallUniverse(Random rng)
        {
            var leagues = new List<League> { new() { Name = "Liga 4", Tier = 4, Season = 1 } };
            var teams = new List<Team>();
            for (int i = 0; i < 4; i++)
            {
                var players = PlayerGenerator.GenerateSquad(Nationality.Germany, 55, squadSize: 16, random: rng);
                var team = new Team { Name = $"Team{i}", LeagueTier = 4, Statistics = new TeamStats { Season = 1 } };
                team.Players.AddRange(players);
                teams.Add(team);
            }
            return (leagues, teams);
        }

        [Fact]
        public async Task StartingASecondNewCareer_DoesNotDuplicateTeams()
        {
            var rng = new Random(1);

            var (leagues1, teams1) = BuildSmallUniverse(rng);
            await _service.StartNewCareerAsync("Erste Karriere", 1, leagues1, teams1, teams1[0], new DateTime(2026, 8, 1));

            var (leagues2, teams2) = BuildSmallUniverse(rng);
            var state = await _service.StartNewCareerAsync("Zweite Karriere", 1, leagues2, teams2, teams2[0], new DateTime(2026, 8, 1));

            var loaded = await _service.LoadGameAsync();
            Assert.NotNull(loaded);
            Assert.Equal(4, loaded!.Value.Teams.Count); // not 8
            Assert.Equal("Zweite Karriere", loaded.Value.State.SaveName);
            Assert.Equal(state.ManagerTeamId, loaded.Value.State.ManagerTeamId);

            var fixtures = await _service.GetFixturesAsync(1);
            Assert.Equal(12, fixtures.Count); // 4 teams double round-robin (4x3), not double that

            var savedLeagues = await _service.GetLeaguesAsync(1);
            Assert.Single(savedLeagues);
        }
    }
}
