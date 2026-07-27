using RetroFootballManager.Data;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class SaveGameServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private SaveGameService _service = null!;

        public SaveGameServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_test_{Guid.NewGuid():N}.db3");
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
        public async Task NewGame_Then_LoadGame_RoundTripsTeamsAndPlayers()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 70);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 60);

            var state = await _service.NewGameAsync("Meine Karriere", managerTeamId: 0, [home, away]);
            state.ManagerTeamId = home.Id;
            await _service.SaveProgressAsync(state, [home, away]);

            var loaded = await _service.LoadGameAsync();

            Assert.NotNull(loaded);
            Assert.Equal("Meine Karriere", loaded.Value.State.SaveName);
            Assert.Equal(2, loaded.Value.Teams.Count);

            var loadedHome = loaded.Value.Teams.Single(t => t.Name == "Heim FC");
            Assert.Equal(11, loadedHome.Players.Count);
            Assert.Equal(70, loadedHome.Players.First().OffensivePower);
        }

        [Fact]
        public async Task HasSaveGame_ReflectsWhetherGameStateExists()
        {
            Assert.False(await _service.HasSaveGameAsync());

            var team = TestHelpers.CreateTeam("Solo FC", baseRating: 55);
            await _service.NewGameAsync("Solo Save", managerTeamId: 0, [team]);

            Assert.True(await _service.HasSaveGameAsync());
        }

        [Fact]
        public async Task SimulatedMatch_ResultCanBePersistedIntoTeamStats()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 65);
            await _service.NewGameAsync("Match Save", managerTeamId: 0, [home, away]);

            var match = new Common.Match(home, away, new Random(5));
            var result = match.Simulate();
            result.ApplyToTeamStats(home.Statistics!, away.Statistics!);

            var state = (await _service.LoadGameAsync())!.Value.State;
            await _service.SaveProgressAsync(state, [home, away]);

            var reloaded = await _service.LoadGameAsync();
            var reloadedHome = reloaded!.Value.Teams.Single(t => t.Name == "Heim FC");

            Assert.Equal(1, reloadedHome.Statistics!.MatchesPlayed);
        }
    }
}
