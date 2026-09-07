using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    public class TransferWindowDigestServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TransferHistoryRepository _history = null!;
        private MessageRepository _messageRepo = null!;
        private MessageService _messages = null!;

        public TransferWindowDigestServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_digest_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _history = new TransferHistoryRepository(_db);
            _messageRepo = new MessageRepository(_db);
            _messages = new MessageService(_messageRepo);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        // Fixtures shaped so the transfer window is open on day 1 (preseason) and closed
        // once matchday 5 is reached (TransferWindowMatchdays = 4).
        private static List<Fixture> SeasonFixtures(DateTime seasonStart)
        {
            var fixtures = new List<Fixture>();
            for (int matchday = 1; matchday <= 34; matchday++)
                fixtures.Add(new Fixture
                {
                    LeagueTier = 4, Matchday = matchday, Season = 1,
                    Date = seasonStart.AddDays((matchday - 1) * 7),
                    HomeTeamId = 1, AwayTeamId = 2,
                });
            return fixtures;
        }

        private static GameState State(DateTime currentDate, int matchdayIndex) => new()
        {
            ManagerTeamId = 1, Season = 1, CurrentDate = currentDate, MatchdayIndex = matchdayIndex,
            TransferWindowWasOpen = true,
        };

        [Fact]
        public async Task CheckAndSendAsync_WindowStillOpen_SendsNoMessage()
        {
            var seasonStart = new DateTime(2026, 8, 1);
            var state = State(seasonStart, matchdayIndex: 0);

            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);

            Assert.Empty(await _messages.GetInboxAsync());
            Assert.True(state.TransferWindowWasOpen);
        }

        [Fact]
        public async Task CheckAndSendAsync_WindowJustClosed_SendsDigestWithOwnTransferHighlighted()
        {
            var seasonStart = new DateTime(2026, 8, 1);

            await _history.SaveAsync(new TransferHistoryEntry
            {
                Date = seasonStart.AddDays(3), PlayerId = 1, PlayerName = "Max Mustermann", PlayerRating = 72.5,
                FromTeamId = 1, FromTeamName = "Eigener Verein", FromLeagueTier = 4,
                ToTeamId = 3, ToTeamName = "FC Anders", ToLeagueTier = 4, Fee = 150_000,
            });
            await _history.SaveAsync(new TransferHistoryEntry
            {
                Date = seasonStart.AddDays(4), PlayerId = 2, PlayerName = "Peter Fremd", PlayerRating = 65.0,
                FromTeamId = 5, FromTeamName = "FC Fremd A", FromLeagueTier = 4,
                ToTeamId = 6, ToTeamName = "FC Fremd B", ToLeagueTier = 4, Fee = 50_000,
            });

            // matchdayIndex 5 with the 4-matchday window means the window is closed today.
            var state = State(seasonStart.AddDays((5 - 1) * 7), matchdayIndex: 5);

            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);

            var inbox = await _messages.GetInboxAsync();
            var digest = Assert.Single(inbox);
            Assert.Equal(MessageType.TransferWindowDigest, digest.Type);
            Assert.Contains("★Max Mustermann", digest.Body);
            Assert.Contains("Peter Fremd", digest.Body);
            Assert.DoesNotContain("★Peter Fremd", digest.Body);
            Assert.False(state.TransferWindowWasOpen);
            Assert.NotNull(state.LastTransferDigestDate);
        }

        [Fact]
        public async Task CheckAndSendAsync_NoTransfersThisWindow_SendsNoMessage()
        {
            var seasonStart = new DateTime(2026, 8, 1);
            var state = State(seasonStart.AddDays((5 - 1) * 7), matchdayIndex: 5);

            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);

            Assert.Empty(await _messages.GetInboxAsync());
        }

        [Fact]
        public async Task CheckAndSendAsync_TransferInOtherLeagueTier_IsExcluded()
        {
            var seasonStart = new DateTime(2026, 8, 1);
            await _history.SaveAsync(new TransferHistoryEntry
            {
                Date = seasonStart.AddDays(2), PlayerId = 1, PlayerName = "Anderer Liga Spieler", PlayerRating = 70,
                FromTeamId = 10, FromTeamName = "Liga2 A", FromLeagueTier = 2,
                ToTeamId = 11, ToTeamName = "Liga2 B", ToLeagueTier = 2, Fee = 200_000,
            });
            var state = State(seasonStart.AddDays((5 - 1) * 7), matchdayIndex: 5);

            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);

            Assert.Empty(await _messages.GetInboxAsync());
        }

        [Fact]
        public async Task CheckAndSendAsync_CalledTwiceOnSameClosedTransition_OnlySendsOnce()
        {
            var seasonStart = new DateTime(2026, 8, 1);
            await _history.SaveAsync(new TransferHistoryEntry
            {
                Date = seasonStart.AddDays(3), PlayerId = 1, PlayerName = "Max Mustermann", PlayerRating = 72.5,
                FromTeamId = 1, FromTeamName = "Eigener Verein", FromLeagueTier = 4,
                ToTeamId = 3, ToTeamName = "FC Anders", ToLeagueTier = 4, Fee = 150_000,
            });
            var state = State(seasonStart.AddDays((5 - 1) * 7), matchdayIndex: 5);

            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);
            // Second call the same day: window is already closed, so wasOpen is now false -
            // no new digest (matches the "fires exactly on the transition" contract).
            await TransferWindowDigestService.CheckAndSendAsync(state, SeasonFixtures(seasonStart), 4, _history, _messages);

            Assert.Single(await _messages.GetInboxAsync());
        }
    }
}
