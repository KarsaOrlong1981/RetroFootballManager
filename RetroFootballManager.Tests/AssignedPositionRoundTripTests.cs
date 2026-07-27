using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Guards the DB round-trip step of the WB-toggle bug: confirms AssignedPosition (incl. a
    // WingBack role) is not the persistence layer's fault - the actual bug turned out to be
    // nested XAML gesture recognizers (see LineupPage.xaml) arming a spurious tap-to-swap.
    public class AssignedPositionRoundTripTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;

        public AssignedPositionRoundTripTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_assignedpos_{Guid.NewGuid():N}.db3");
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
        public async Task AssignedPosition_SurvivesSaveAndReload()
        {
            var team = TestHelpers.CreateTeam("WB Test", baseRating: 60);
            LineupSelector.SelectLineup(team, FormationCatalog.F442);
            var lv = team.Players.First(p => p.Status == PlayerStatus.InStartingXI && p.Position == Position.LeftDefender);
            lv.AssignedPosition = Position.LeftWingBack;

            await _teamRepo.SaveTeamAsync(team);

            var reloaded = await _teamRepo.GetTeamAsync(team.Id);
            var reloadedLv = reloaded!.Players.First(p => p.Id == lv.Id);

            Assert.Equal(Position.LeftWingBack, reloadedLv.AssignedPosition);
            Assert.Equal(PlayerStatus.InStartingXI, reloadedLv.Status);
        }
    }
}
