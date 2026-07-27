using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class ScoutingAssignmentRepositoryTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private ScoutingAssignmentRepository _repo = null!;

        public ScoutingAssignmentRepositoryTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_scouting_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _repo = new ScoutingAssignmentRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task SaveAndGetForPlayer_RoundTrips()
        {
            var assignment = new ScoutingAssignment
            {
                TeamId = 1, PlayerId = 42, StartDate = new DateTime(2026, 8, 1),
                CompletionDate = new DateTime(2026, 8, 15),
            };
            await _repo.SaveAsync(assignment);

            var found = await _repo.GetForPlayerAsync(1, 42);

            Assert.NotNull(found);
            Assert.Equal(assignment.CompletionDate, found!.CompletionDate);
        }

        [Fact]
        public async Task GetByTeamAsync_OnlyReturnsThatTeamsAssignments()
        {
            await _repo.SaveAsync(new ScoutingAssignment { TeamId = 1, PlayerId = 1, StartDate = DateTime.Today, CompletionDate = DateTime.Today.AddDays(14) });
            await _repo.SaveAsync(new ScoutingAssignment { TeamId = 2, PlayerId = 2, StartDate = DateTime.Today, CompletionDate = DateTime.Today.AddDays(14) });

            var team1Assignments = await _repo.GetByTeamAsync(1);

            var only = Assert.Single(team1Assignments);
            Assert.Equal(1, only.PlayerId);
        }

        [Fact]
        public async Task DeleteAsync_RemovesTheAssignment()
        {
            var assignment = new ScoutingAssignment { TeamId = 1, PlayerId = 1, StartDate = DateTime.Today, CompletionDate = DateTime.Today.AddDays(14) };
            await _repo.SaveAsync(assignment);

            await _repo.DeleteAsync(assignment.Id);

            var found = await _repo.GetForPlayerAsync(1, 1);
            Assert.Null(found);
        }
    }
}
