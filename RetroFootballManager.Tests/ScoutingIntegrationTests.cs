using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Drives the scouting flow through SaveGameService (real persistence), same pattern as
    // MatchDayIntegrationTests.
    public class ScoutingIntegrationTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private SaveGameService _saveGame = null!;
        private TeamRepository _teamRepo = null!;
        private MessageRepository _messageRepo = null!;

        public ScoutingIntegrationTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_scouting_int_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            _saveGame = new SaveGameService(_db);
            await _saveGame.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _messageRepo = new MessageRepository(_db);
        }

        public async Task DisposeAsync()
        {
            await _saveGame.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task TryStartScoutingAsync_RejectsSecondAssignmentForSamePlayer()
        {
            var scoutingTeam = TestHelpers.CreateTeam("Scout FC", baseRating: 60);
            scoutingTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 });
            await _teamRepo.SaveTeamAsync(scoutingTeam);

            var targetTeam = TestHelpers.CreateTeam("Target FC", baseRating: 60);
            await _teamRepo.SaveTeamAsync(targetTeam);
            var targetPlayer = targetTeam.Players[0];

            var date = new DateTime(2026, 8, 1);
            var (first, firstError) = await _saveGame.TryStartScoutingAsync(scoutingTeam, targetPlayer, date);
            var (second, secondError) = await _saveGame.TryStartScoutingAsync(scoutingTeam, targetPlayer, date);

            Assert.True(first);
            Assert.Null(firstError);
            Assert.False(second);
            Assert.NotNull(secondError);
        }

        [Fact]
        public async Task ApplyDueScoutingAsync_MarksPlayerScouted_DeletesAssignment_SendsMessage()
        {
            var scoutingTeam = TestHelpers.CreateTeam("Scout FC", baseRating: 60);
            scoutingTeam.Id = 1;
            scoutingTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 });
            await _teamRepo.SaveTeamAsync(scoutingTeam);

            var targetTeam = TestHelpers.CreateTeam("Target FC", baseRating: 60);
            targetTeam.Id = 2;
            await _teamRepo.SaveTeamAsync(targetTeam);
            var targetPlayer = targetTeam.Players[0];
            Assert.False(targetPlayer.IsScouted);

            var startDate = new DateTime(2026, 8, 1);
            await _saveGame.TryStartScoutingAsync(scoutingTeam, targetPlayer, startDate);

            // Noch nicht fällig.
            await _saveGame.ApplyDueScoutingAsync(scoutingTeam.Id, startDate.AddDays(10));
            Assert.NotNull(await _saveGame.GetActiveScoutingForPlayerAsync(scoutingTeam.Id, targetPlayer.Id));

            // 14 Tage später fällig.
            await _saveGame.ApplyDueScoutingAsync(scoutingTeam.Id, startDate.AddDays(14));

            Assert.Null(await _saveGame.GetActiveScoutingForPlayerAsync(scoutingTeam.Id, targetPlayer.Id));
            var reloadedTarget = await _teamRepo.GetTeamAsync(targetTeam.Id);
            Assert.True(reloadedTarget!.Players.First(p => p.Id == targetPlayer.Id).IsScouted);

            var messages = await _messageRepo.GetAllAsync();
            Assert.Contains(messages, m => m.Type == MessageType.ScoutingCompleted);

            var scoutedPlayers = await _saveGame.GetScoutedPlayersAsync(scoutingTeam.Id);
            Assert.Contains(scoutedPlayers, p => p.PlayerId == targetPlayer.Id);
        }

        [Fact]
        public async Task RemoveScoutedPlayerAsync_RemovesOnlyThatRow_LeavesIsScoutedUntouched()
        {
            var scoutingTeam = TestHelpers.CreateTeam("Scout FC", baseRating: 60);
            scoutingTeam.Id = 1;
            scoutingTeam.Employees.Add(new Employee { EmployeeType = EmployeeType.Scout, ScoutingAbility = 60 });
            await _teamRepo.SaveTeamAsync(scoutingTeam);

            var targetTeam = TestHelpers.CreateTeam("Target FC", baseRating: 60);
            targetTeam.Id = 2;
            await _teamRepo.SaveTeamAsync(targetTeam);
            var targetPlayer = targetTeam.Players[0];

            var startDate = new DateTime(2026, 8, 1);
            await _saveGame.TryStartScoutingAsync(scoutingTeam, targetPlayer, startDate);
            await _saveGame.ApplyDueScoutingAsync(scoutingTeam.Id, startDate.AddDays(14));

            await _saveGame.RemoveScoutedPlayerAsync(scoutingTeam.Id, targetPlayer.Id);

            var scoutedPlayers = await _saveGame.GetScoutedPlayersAsync(scoutingTeam.Id);
            Assert.DoesNotContain(scoutedPlayers, p => p.PlayerId == targetPlayer.Id);

            var reloadedTarget = await _teamRepo.GetTeamAsync(targetTeam.Id);
            Assert.True(reloadedTarget!.Players.First(p => p.Id == targetPlayer.Id).IsScouted);
        }
    }
}
