using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class PreMatchAnalysisServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private MessageService _messages = null!;
        private MessageRepository _messageRepo = null!;

        public PreMatchAnalysisServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_analysis_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _messageRepo = new MessageRepository(_db);
            _messages = new MessageService(_messageRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task SendIfAnalystEmployedAsync_NoAnalyst_SendsNothing()
        {
            var human = TestHelpers.CreateTeam("Human", baseRating: 60);
            var opponent = TestHelpers.CreateTeam("Opponent", baseRating: 60);
            opponent.Id = 2;

            await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                _messages, human, opponent, [], [opponent], DateTime.Today, "Spieltag 1");

            var messages = await _messageRepo.GetAllAsync();
            Assert.Empty(messages);
        }

        [Fact]
        public async Task SendIfAnalystEmployedAsync_WithAnalyst_SendsOpponentAnalysisMessage()
        {
            var human = TestHelpers.CreateTeam("Human", baseRating: 60);
            human.Employees.Add(new Employee { EmployeeType = EmployeeType.Analyst, AnalysisAbility = 60 });
            var opponent = TestHelpers.CreateTeam("Opponent", baseRating: 60);
            opponent.Id = 2;

            await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                _messages, human, opponent, [], [opponent], DateTime.Today, "Spieltag 1");

            var messages = await _messageRepo.GetAllAsync();
            var message = Assert.Single(messages);
            Assert.Equal(MessageType.OpponentAnalysis, message.Type);
            Assert.Contains("Opponent", message.Body);
        }

        [Fact]
        public async Task SendIfAnalystEmployedAsync_TopAnalyst_RevealsFormationInBody()
        {
            var human = TestHelpers.CreateTeam("Human", baseRating: 60);
            human.Employees.Add(new Employee { EmployeeType = EmployeeType.Analyst, AnalysisAbility = 95 });
            var opponent = TestHelpers.CreateTeam("Opponent", baseRating: 60);
            opponent.Id = 2;
            opponent.FormationName = "4-3-3";

            await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                _messages, human, opponent, [], [opponent], DateTime.Today, "Spieltag 1");

            var messages = await _messageRepo.GetAllAsync();
            var message = Assert.Single(messages);
            Assert.Contains("4-3-3", message.Body);
            Assert.Contains("Aufstellung", message.Body);
        }
    }
}
