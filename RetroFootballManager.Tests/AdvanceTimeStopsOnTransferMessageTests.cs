using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Reproduces the reported "Zeit vorstellen doesn't stop when a team accepts a transfer"
    // scenario end-to-end via CalendarAdvanceService.AdvanceOneDayAsync (the exact primitive
    // MainMenuViewModel.AdvanceTime loops over), to confirm whether the message-on-accept wiring
    // actually works when driven through the calendar-advance path (not just the isolated
    // TransferAiService unit tests, which never touch MessageService at all).
    public class AdvanceTimeStopsOnTransferMessageTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private FixtureRepository _fixtureRepo = null!;
        private TransferListingRepository _listingRepo = null!;
        private TransferOfferRepository _offerRepo = null!;
        private MessageService _messages = null!;
        private CalendarAdvanceService _service = null!;
        private TransferMarketService _transferMarket = null!;

        private static readonly DateTime Today = new(2026, 6, 1); // Kalendertage ab GameState.SeasonStart

        public AdvanceTimeStopsOnTransferMessageTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_advancetime_transfer_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _fixtureRepo = new FixtureRepository(_db);
            var contractRepo = new ContractRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            _offerRepo = new TransferOfferRepository(_db);
            var loanRepo = new LoanAgreementRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            // WICHTIG (im Gegensatz zu CalendarAdvanceServiceTests' Setup): MessageService muss
            // hier durchgereicht werden, sonst kann TransferMarketService niemals eine Nachricht
            // versenden - das genau ist die Voraussetzung, die dieser Test überprüft.
            _transferMarket = new TransferMarketService(_listingRepo, _offerRepo, loanRepo, _teamRepo, contractRepo, _messages);
            var staffMarket = new StaffMarketService(_db, _teamRepo, contractRepo, new Random(1));
            var aiManager = new AiManagerService(_transferMarket, staffMarket, contractRepo, _listingRepo, new PlayerRepository(_db));
            var expiryWarnings = new ExpiryWarningService(contractRepo, loanRepo, _messages);
            var sponsorRepo = new SponsorRepository(_db);
            var sponsorshipRepo = new SponsorshipRepository(_db);
            var finance = new FinanceService(sponsorRepo, sponsorshipRepo, contractRepo, _messages);
            var trainingCamps = new TrainingCampService(new TrainingCampRepository(_db), _fixtureRepo, _messages, new Random(1));
            _service = new CalendarAdvanceService(
                _teamRepo, _fixtureRepo, aiManager, expiryWarnings, finance, trainingCamps, _messages,
                contractRepo, _listingRepo, _offerRepo, new PlayerRepository(_db), _transferMarket, new Random(1));
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task AdvancingDayByDay_SendsAMessage_AssoonAsTheAiAcceptsTheHumansOffer()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiTeam = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            aiTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiTeam);

            var target = aiTeam.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiTeam, askingPrice: 100_000, season: 1, Today);
            // Angebot über der 80%-Annahmeschwelle (TransferAiService.ShouldAcceptOffer) - muss
            // beim nächsten wöchentlichen KI-Tick angenommen werden.
            await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 100_000, wageOffer: 15_000, Today);

            var state = new GameState
            {
                ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today,
            };
            var teams = new List<Team> { humanTeam, aiTeam };

            int unreadBefore = await _messages.GetUnreadCountAsync();
            bool sawIncrease = false;
            for (int day = 1; day <= 14; day++)
            {
                await _service.AdvanceOneDayAsync(state, teams);
                int unreadNow = await _messages.GetUnreadCountAsync();
                if (unreadNow > unreadBefore)
                {
                    sawIncrease = true;
                    break;
                }
            }

            Assert.True(sawIncrease, "Erwartet: spätestens beim ersten wöchentlichen KI-Tick sendet AcceptOfferAsync eine Nachricht ans menschliche Postfach.");
            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.TransferOfferAccepted);
        }
    }
}
