using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ScoutedPlayerRepositoryTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private ScoutedPlayerRepository _repo = null!;

        public ScoutedPlayerRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_scoutedplayer_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _repo = new ScoutedPlayerRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task SaveAndGetByTeam_RoundTrips()
        {
            await _repo.SaveAsync(new ScoutedPlayer { TeamId = 1, PlayerId = 42, ScoutedDate = new DateTime(2026, 8, 1) });

            var found = await _repo.GetByTeamAsync(1);

            var only = Assert.Single(found);
            Assert.Equal(42, only.PlayerId);
        }

        [Fact]
        public async Task GetByTeamAsync_OnlyReturnsThatTeamsRows()
        {
            await _repo.SaveAsync(new ScoutedPlayer { TeamId = 1, PlayerId = 1, ScoutedDate = DateTime.Today });
            await _repo.SaveAsync(new ScoutedPlayer { TeamId = 2, PlayerId = 2, ScoutedDate = DateTime.Today });

            var team1Rows = await _repo.GetByTeamAsync(1);

            var only = Assert.Single(team1Rows);
            Assert.Equal(1, only.PlayerId);
        }

        [Fact]
        public async Task RemoveAsync_RemovesOnlyThatPlayersRow()
        {
            await _repo.SaveAsync(new ScoutedPlayer { TeamId = 1, PlayerId = 1, ScoutedDate = DateTime.Today });
            await _repo.SaveAsync(new ScoutedPlayer { TeamId = 1, PlayerId = 2, ScoutedDate = DateTime.Today });

            await _repo.RemoveAsync(1, 1);

            var remaining = await _repo.GetByTeamAsync(1);
            var only = Assert.Single(remaining);
            Assert.Equal(2, only.PlayerId);
        }
    }
}
