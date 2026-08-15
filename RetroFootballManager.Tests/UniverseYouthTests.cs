using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class UniverseYouthTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;

        public UniverseYouthTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_uni_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public void CreateUniverse_ProducesTeamsWithYouthAndStaff()
        {
            var (leagues, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(1));

            Assert.Equal(UniverseGenerator.LeagueCount, leagues.Count);
            Assert.Equal(UniverseGenerator.LeagueCount * UniverseGenerator.TeamsPerLeague, teams.Count);

            var sample = teams[0];
            Assert.Equal(PlayerGenerator.DefaultPositionPlanSize, sample.Players.Count);
            Assert.Equal(UniverseGenerator.YouthPerTeam, sample.YouthPlayers.Count);
            Assert.NotEmpty(sample.Employees);
            Assert.All(sample.YouthPlayers, y => Assert.True(y.IsYouthProspect));
        }

        [Fact]
        public void CreateUniverse_UsesFixedClubRoster_SameNamesAndTiersAcrossSeeds()
        {
            var (_, teamsSeedA) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(1));
            var (_, teamsSeedB) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(999));

            var namesA = teamsSeedA.OrderBy(t => t.LeagueTier).ThenBy(t => t.Name).Select(t => (t.Name, t.LeagueTier)).ToList();
            var namesB = teamsSeedB.OrderBy(t => t.LeagueTier).ThenBy(t => t.Name).Select(t => (t.Name, t.LeagueTier)).ToList();

            // The club roster (name + tier assignment) must be identical regardless of the RNG
            // seed - only player stats/development are meant to vary between playthroughs.
            Assert.Equal(namesA, namesB);

            // No club name (and therefore no place name) appears twice across the whole world.
            Assert.Equal(teamsSeedA.Count, teamsSeedA.Select(t => t.Name).Distinct().Count());
        }

        [Fact]
        public async Task SavingAndHydrating_SeparatesSeniorsFromYouth()
        {
            var (_, teams) = UniverseGenerator.CreateUniverse(season: 1, random: new Random(2));
            var team = teams[0];

            var repo = new TeamRepository(_db);
            await repo.SaveTeamAsync(team);

            var loaded = await repo.GetTeamAsync(team.Id);

            Assert.NotNull(loaded);
            Assert.Equal(PlayerGenerator.DefaultPositionPlanSize, loaded!.Players.Count);
            Assert.All(loaded.Players, p => Assert.False(p.IsYouthProspect));
            Assert.Equal(UniverseGenerator.YouthPerTeam, loaded.YouthPlayers.Count);
            Assert.All(loaded.YouthPlayers, p => Assert.True(p.IsYouthProspect));
        }
    }
}
