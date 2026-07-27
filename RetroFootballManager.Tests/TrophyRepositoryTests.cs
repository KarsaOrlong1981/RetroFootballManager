using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TrophyRepositoryTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TrophyRepository _repo = null!;

        public TrophyRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_trophy_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _repo = new TrophyRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task RecordWinAsync_FirstWin_CreatesRowWithCountOne()
        {
            await _repo.RecordWinAsync(teamId: 1, TrophyType.DeutscherPokal, season: 1);

            var records = await _repo.GetByTeamAsync(1);
            var record = Assert.Single(records);
            Assert.Equal(TrophyType.DeutscherPokal, record.Type);
            Assert.Equal(1, record.Count);
            Assert.Equal(1, record.LastWonSeason);
        }

        [Fact]
        public async Task RecordWinAsync_SecondWin_IncrementsExistingRow_UpdatesLastWonSeason()
        {
            await _repo.RecordWinAsync(teamId: 1, TrophyType.DeutscherPokal, season: 1);
            await _repo.RecordWinAsync(teamId: 1, TrophyType.DeutscherPokal, season: 3);

            var records = await _repo.GetByTeamAsync(1);
            var record = Assert.Single(records);
            Assert.Equal(2, record.Count);
            Assert.Equal(3, record.LastWonSeason);
        }

        [Fact]
        public async Task GetByTeamAsync_OnlyReturnsThatTeamsTrophies()
        {
            await _repo.RecordWinAsync(teamId: 1, TrophyType.DeutscherMeister, season: 1);
            await _repo.RecordWinAsync(teamId: 2, TrophyType.DeutscherPokal, season: 1);

            var team1Trophies = await _repo.GetByTeamAsync(1);
            var record = Assert.Single(team1Trophies);
            Assert.Equal(TrophyType.DeutscherMeister, record.Type);
        }
    }
}
