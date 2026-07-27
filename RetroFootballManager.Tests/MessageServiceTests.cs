using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class MessageServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private MessageService _service = null!;

        private static readonly DateTime Today = new(2026, 9, 1);

        public MessageServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_messages_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _service = new MessageService(new MessageRepository(_db));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task SendAsync_PersistsUnreadMessage()
        {
            await _service.SendAsync(MessageType.FinanceWarning, "Titel", "Text", Today, relatedTeamId: 1);

            var inbox = await _service.GetInboxAsync();
            Assert.Single(inbox);
            Assert.False(inbox[0].IsRead);
            Assert.Equal(1, await _service.GetUnreadCountAsync());
        }

        [Fact]
        public async Task MarkReadAsync_ReducesUnreadCount()
        {
            var message = await _service.SendAsync(MessageType.PlayerInjured, "Titel", "Text", Today);

            await _service.MarkReadAsync(message);

            Assert.Equal(0, await _service.GetUnreadCountAsync());
        }

        [Fact]
        public async Task GetInboxAsync_OrdersNewestFirst()
        {
            await _service.SendAsync(MessageType.PlayerInjured, "Alt", "Text", Today);
            await _service.SendAsync(MessageType.PlayerRecovered, "Neu", "Text", Today.AddDays(1));

            var inbox = await _service.GetInboxAsync();
            Assert.Equal("Neu", inbox[0].Title);
        }

        [Fact]
        public async Task DeleteAsync_RemovesMessageFromInbox()
        {
            var message = await _service.SendAsync(MessageType.PlayerInjured, "Titel", "Text", Today);

            await _service.DeleteAsync(message);

            var inbox = await _service.GetInboxAsync();
            Assert.Empty(inbox);
        }

        [Fact]
        public async Task HasWarnedAsync_TrueOnlyAfterMatchingThresholdSent()
        {
            Assert.False(await _service.HasWarnedAsync(playerId: 5, MessageType.ContractExpiringSoon, thresholdDays: 30));

            await _service.SendAsync(
                MessageType.ContractExpiringSoon, "Titel", "Text", Today, relatedPlayerId: 5, warningThresholdDays: 30);

            Assert.True(await _service.HasWarnedAsync(playerId: 5, MessageType.ContractExpiringSoon, thresholdDays: 30));
            Assert.False(await _service.HasWarnedAsync(playerId: 5, MessageType.ContractExpiringSoon, thresholdDays: 14));
        }
    }
}
