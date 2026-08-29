using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Regression test for the transaction-batched SaveTeamAsync (see TeamRepository): a brand
    // new team (Id == 0) must get its DB-assigned auto-increment Id written back onto the same
    // object, exactly like the old Find-then-Insert/Update version did.
    public class TeamRepositorySaveTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;

        public TeamRepositorySaveTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_teamsave_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task SaveTeamAsync_AssignsAutoIncrementId_ToBrandNewTeam()
        {
            var team = TestHelpers.CreateTeam("New Team", baseRating: 60);
            Assert.Equal(0, team.Id);

            await _teamRepo.SaveTeamAsync(team);

            Assert.NotEqual(0, team.Id);
            foreach (var player in team.Players)
                Assert.Equal(team.Id, player.TeamId);
        }

        [Fact]
        public async Task SaveTeamAsync_AssignsDistinctIds_ForMultipleNewTeams()
        {
            var teamA = TestHelpers.CreateTeam("Team A", baseRating: 60);
            var teamB = TestHelpers.CreateTeam("Team B", baseRating: 60);

            await _teamRepo.SaveTeamAsync(teamA);
            await _teamRepo.SaveTeamAsync(teamB);

            Assert.NotEqual(0, teamA.Id);
            Assert.NotEqual(0, teamB.Id);
            Assert.NotEqual(teamA.Id, teamB.Id);
        }
    }
}
