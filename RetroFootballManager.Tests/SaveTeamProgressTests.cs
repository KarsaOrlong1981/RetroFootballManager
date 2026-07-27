using RetroFootballManager.Data;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Regression test: screens that only ever touch the manager's own team (Lineup/Training/
    // Youth) must be able to persist JUST that team, not the whole league - re-saving all 72
    // teams for a single-team edit was the reported "saving takes forever" cause.
    public class SaveTeamProgressTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private SaveGameService _service = null!;

        public SaveTeamProgressTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_teamsave_{Guid.NewGuid():N}.db3");
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

        [Fact]
        public async Task SaveTeamProgressAsync_OnlyPersistsTheGivenTeam_AndUpdatesTimestamp()
        {
            var home = TestHelpers.CreateTeam("Heim FC", baseRating: 65);
            var away = TestHelpers.CreateTeam("Gast FC", baseRating: 60);
            var state = await _service.NewGameAsync("Test", managerTeamId: 0, [home, away]);
            var originalTimestamp = state.LastSavedAt;

            // Mutate the away team in memory only - it must NOT be persisted by the call below.
            away.Name = "Geändert (nicht gespeichert)";
            home.Name = "Heim FC Geändert";

            await Task.Delay(10); // ensure LastSavedAt actually advances
            await _service.SaveTeamProgressAsync(state, home);

            var loaded = await _service.LoadGameAsync();
            Assert.NotNull(loaded);

            var loadedHome = loaded!.Value.Teams.Single(t => t.Name.StartsWith("Heim FC"));
            var loadedAway = loaded.Value.Teams.Single(t => t.Id == away.Id);

            Assert.Equal("Heim FC Geändert", loadedHome.Name);
            Assert.NotEqual("Geändert (nicht gespeichert)", loadedAway.Name);
            Assert.True(loaded.Value.State.LastSavedAt > originalTimestamp);
        }
    }
}
