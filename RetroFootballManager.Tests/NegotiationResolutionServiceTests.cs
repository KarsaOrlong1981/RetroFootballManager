using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Models;
using Xunit;

namespace RetroFootballManager.Tests
{
    // Covers the "Bedenkzeit" (think-it-over period) path end-to-end via
    // CalendarAdvanceService.AdvanceOneDayAsync - the same primitive
    // AdvanceTimeStopsOnTransferMessageTests uses for the plain weekly-tick flow.
    public class NegotiationResolutionServiceTests : IAsyncLifetime
    {
        private readonly string _dbPath;
        private AppDatabase _db = null!;
        private TeamRepository _teamRepo = null!;
        private FixtureRepository _fixtureRepo = null!;
        private TransferListingRepository _listingRepo = null!;
        private TransferOfferRepository _offerRepo = null!;
        private ContractRepository _contractRepo = null!;
        private ContractBonusRepository _bonusRepo = null!;
        private PendingNegotiationRepository _pendingRepo = null!;
        private MessageService _messages = null!;
        private TransferMarketService _transferMarket = null!;
        private CalendarAdvanceService _service = null!;

        private static readonly DateTime Today = new(2026, 6, 1);

        public NegotiationResolutionServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"rfm_negotiation_resolution_{Guid.NewGuid():N}.db3");
        }

        public async Task InitializeAsync()
        {
            _db = new AppDatabase(_dbPath);
            await _db.InitializeAsync();
            _teamRepo = new TeamRepository(_db);
            _fixtureRepo = new FixtureRepository(_db);
            _contractRepo = new ContractRepository(_db);
            _bonusRepo = new ContractBonusRepository(_db);
            _listingRepo = new TransferListingRepository(_db);
            _offerRepo = new TransferOfferRepository(_db);
            _pendingRepo = new PendingNegotiationRepository(_db);
            var loanRepo = new LoanAgreementRepository(_db);
            _messages = new MessageService(new MessageRepository(_db));
            _transferMarket = new TransferMarketService(_listingRepo, _offerRepo, loanRepo, _teamRepo, _contractRepo, _messages);
            var staffMarket = new StaffMarketService(_db, _teamRepo, _contractRepo, new Random(1));
            var aiManager = new AiManagerService(_transferMarket, staffMarket, _contractRepo, _listingRepo, new PlayerRepository(_db));
            var expiryWarnings = new ExpiryWarningService(_contractRepo, loanRepo, _messages);
            var finance = new FinanceService(new SponsorRepository(_db), new SponsorshipRepository(_db), _contractRepo, _messages);
            var trainingCamps = new TrainingCampService(new TrainingCampRepository(_db), _fixtureRepo, _messages, new Random(1));
            var playerRepo = new PlayerRepository(_db);
            var negotiations = new NegotiationResolutionService(
                _pendingRepo, _offerRepo, _listingRepo, _contractRepo, _bonusRepo, playerRepo, _transferMarket, _messages);
            _service = new CalendarAdvanceService(
                _teamRepo, _fixtureRepo, aiManager, expiryWarnings, finance, trainingCamps, _messages,
                _contractRepo, _listingRepo, _offerRepo, playerRepo, _transferMarket, new Random(1),
                negotiations: negotiations);
        }

        public async Task DisposeAsync()
        {
            await _db.CloseAsync();
            if (File.Exists(_dbPath)) File.Delete(_dbPath);
        }

        [Fact]
        public async Task LockedOffer_IsIgnoredByWeeklyTick_UntilDecisionDate()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiTeam = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            aiTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiTeam);

            var target = aiTeam.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiTeam, askingPrice: 100_000, season: 1, Today);
            var offer = await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 100_000, wageOffer: 15_000, Today);

            // Locked well past the day-7 weekly AI tick, so the loop below exercises the
            // skip-logic in TransferAiService.EvaluateIncomingOffersAsync while still locked.
            offer.LockedUntilDate = Today.AddDays(10);
            await _offerRepo.SaveAsync(offer);
            await _pendingRepo.SaveAsync(new PendingNegotiation
            {
                Kind = NegotiationKind.TransferOrLoanBuy,
                TransferOfferId = offer.Id,
                PlayerId = target.Id,
                TeamId = humanTeam.Id,
                CreatedDate = Today,
                DecisionDate = Today.AddDays(10),
                RoleInTeam = RoleInTeam.RotationPlayer,
                ContractYears = 3,
            });

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today };
            var teams = new List<Team> { humanTeam, aiTeam };

            // Day 7 hits the weekly AI tick - the locked offer must survive it untouched.
            for (int day = 1; day <= 7; day++)
                await _service.AdvanceOneDayAsync(state, teams);

            var offerAfterWeeklyTick = await _offerRepo.GetByIdAsync(offer.Id);
            Assert.Equal(TransferOfferStatus.Pending, offerAfterWeeklyTick!.Status);
            Assert.Contains(target, aiTeam.Players);
        }

        [Fact]
        public async Task DueNegotiation_CompletesTransfer_AndAppliesNegotiatedTerms()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiTeam = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            aiTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiTeam);

            var target = aiTeam.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiTeam, askingPrice: 100_000, season: 1, Today);
            var offer = await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 100_000, wageOffer: 15_000, Today);
            offer.LockedUntilDate = Today.AddDays(3);
            await _offerRepo.SaveAsync(offer);

            await _pendingRepo.SaveAsync(new PendingNegotiation
            {
                Kind = NegotiationKind.TransferOrLoanBuy,
                TransferOfferId = offer.Id,
                PlayerId = target.Id,
                TeamId = humanTeam.Id,
                CreatedDate = Today,
                DecisionDate = Today.AddDays(3),
                RoleInTeam = RoleInTeam.KeyPlayer,
                ContractYears = 4,
                SellOnPercentage = 15,
                ExitClauseAmount = 5_000_000,
                Bonuses = [new NegotiatedBonusLine(ContractBonusType.Goal, 1_000)],
            });

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today };
            var teams = new List<Team> { humanTeam, aiTeam };

            for (int day = 1; day <= 3; day++)
                await _service.AdvanceOneDayAsync(state, teams);

            Assert.Contains(target, humanTeam.Players);
            Assert.DoesNotContain(target, aiTeam.Players);

            var contracts = await _contractRepo.GetByHolderAsync(target.Id, ContractHolderType.Player);
            var contract = PlayerContractService.GetActiveContract(target.Id, contracts, state.CurrentDate);
            Assert.NotNull(contract);
            Assert.Equal(RoleInTeam.KeyPlayer, contract!.RoleInTeam);
            Assert.Equal(15, contract.SellOnPercentage);
            Assert.Equal(5_000_000, contract.ReleaseClause);

            var bonuses = await _bonusRepo.GetByContractAsync(contract.Id);
            Assert.Contains(bonuses, b => b.BonusType == ContractBonusType.Goal && b.Amount == 1_000);

            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.TransferOfferAccepted);

            var pendingAfter = await _pendingRepo.GetByTeamAsync(humanTeam.Id);
            Assert.Empty(pendingAfter);
        }

        [Fact]
        public async Task DueNegotiation_RivalOffer_WinsListing_AndSendsOutbidMessage()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiSeller = TestHelpers.CreateTeam("Verkäufer FC", baseRating: 60);
            aiSeller.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiSeller);

            var rival = TestHelpers.CreateTeam("Rivale FC", baseRating: 60);
            rival.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(rival);

            var target = aiSeller.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiSeller, askingPrice: 100_000, season: 1, Today);
            var humanOffer = await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 100_000, wageOffer: 15_000, Today);
            // Bedenkzeit runs past the day-7 weekly AI tick, so the rival's un-locked bid gets
            // a chance to win the listing there, before our own DecisionDate is even reached.
            humanOffer.LockedUntilDate = Today.AddDays(10);
            await _offerRepo.SaveAsync(humanOffer);
            await _pendingRepo.SaveAsync(new PendingNegotiation
            {
                Kind = NegotiationKind.TransferOrLoanBuy,
                TransferOfferId = humanOffer.Id,
                PlayerId = target.Id,
                TeamId = humanTeam.Id,
                CreatedDate = Today,
                DecisionDate = Today.AddDays(10),
                RoleInTeam = RoleInTeam.RotationPlayer,
                ContractYears = 2,
            });

            // A rival massively outbids the locked human offer - must still win via the
            // normal weekly-tick evaluation, since only the human's offer is locked.
            await _transferMarket.MakeOfferAsync(listing, rival, fee: 500_000, wageOffer: 60_000, Today);

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today };
            var teams = new List<Team> { humanTeam, aiSeller, rival };

            for (int day = 1; day <= 10; day++)
                await _service.AdvanceOneDayAsync(state, teams);

            Assert.Contains(target, rival.Players);
            Assert.DoesNotContain(target, humanTeam.Players);

            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.TransferOfferRejected);
        }

        [Fact]
        public async Task DueNegotiation_InsufficientFunds_CancelsTransfer_KeepsPlayer_AndSendsMessage()
        {
            // Balance would land at -300k after the fee - below the -200k floor, so the
            // signing must be cancelled even though both sides already agreed on the fee
            // (e.g. several simultaneous negotiations that were each affordable on their own).
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 100_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiTeam = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            aiTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiTeam);

            var target = aiTeam.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiTeam, askingPrice: 400_000, season: 1, Today);
            var offer = await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 400_000, wageOffer: 15_000, Today);
            offer.LockedUntilDate = Today.AddDays(3);
            await _offerRepo.SaveAsync(offer);
            await _pendingRepo.SaveAsync(new PendingNegotiation
            {
                Kind = NegotiationKind.TransferOrLoanBuy,
                TransferOfferId = offer.Id,
                PlayerId = target.Id,
                TeamId = humanTeam.Id,
                CreatedDate = Today,
                DecisionDate = Today.AddDays(3),
                RoleInTeam = RoleInTeam.RotationPlayer,
                ContractYears = 3,
            });

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today };
            var teams = new List<Team> { humanTeam, aiTeam };

            for (int day = 1; day <= 3; day++)
                await _service.AdvanceOneDayAsync(state, teams);

            Assert.Contains(target, aiTeam.Players);
            Assert.DoesNotContain(target, humanTeam.Players);
            Assert.Equal(100_000, humanTeam.Finances!.CurrentBalance);

            var offerAfter = await _offerRepo.GetByIdAsync(offer.Id);
            Assert.Equal(TransferOfferStatus.Rejected, offerAfter!.Status);

            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.TransferOfferRejected);
        }

        [Fact]
        public async Task DueRenewal_AppliesNegotiatedWageAndTerms()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var player = humanTeam.Players.First();
            var contract = new Contract
            {
                HolderId = player.Id,
                HolderType = ContractHolderType.Player,
                TeamId = humanTeam.Id,
                StartDate = Today.AddYears(-1),
                EndDate = Today.AddMonths(3),
                AnnualSalary = 100_000,
            };
            await _contractRepo.SaveAsync(contract);

            await _pendingRepo.SaveAsync(new PendingNegotiation
            {
                Kind = NegotiationKind.ContractRenewal,
                PlayerId = player.Id,
                TeamId = humanTeam.Id,
                ContractId = contract.Id,
                CreatedDate = Today,
                DecisionDate = Today.AddDays(3),
                NegotiatedWage = 250_000,
                ContractYears = 2,
                RoleInTeam = RoleInTeam.KeyPlayer,
            });

            var state = new GameState { ManagerTeamId = humanTeam.Id, Season = 1, CurrentDate = Today, SeasonStart = Today };
            var teams = new List<Team> { humanTeam };

            for (int day = 1; day <= 3; day++)
                await _service.AdvanceOneDayAsync(state, teams);

            var updatedContract = await _contractRepo.GetByIdAsync(contract.Id);
            Assert.Equal(250_000, updatedContract!.AnnualSalary);
            Assert.Equal(RoleInTeam.KeyPlayer, updatedContract.RoleInTeam);
            Assert.Equal(Today.AddMonths(3).AddYears(2), updatedContract.EndDate);

            var inbox = await _messages.GetInboxAsync();
            Assert.Contains(inbox, m => m.Type == MessageType.ContractRenewed);
        }

        [Fact]
        public async Task DueTransferNegotiation_StaysPending_WhileTransferWindowClosed_ThenCompletesOnceOpen()
        {
            var humanTeam = TestHelpers.CreateTeam("Mensch FC", baseRating: 60);
            humanTeam.Finances = new Finances { CurrentBalance = 10_000_000 };
            await _teamRepo.SaveTeamAsync(humanTeam);

            var aiTeam = TestHelpers.CreateTeam("KI FC", baseRating: 60);
            aiTeam.Finances = new Finances { CurrentBalance = 1_000_000 };
            await _teamRepo.SaveTeamAsync(aiTeam);

            var target = aiTeam.Players.First();
            var listing = await _transferMarket.ListPlayerAsync(target, aiTeam, askingPrice: 100_000, season: 1, Today);
            var offer = await _transferMarket.MakeOfferAsync(listing, humanTeam, fee: 100_000, wageOffer: 15_000, Today);

            var pending = new PendingNegotiation
            {
                Kind = NegotiationKind.TransferOrLoanBuy,
                TransferOfferId = offer.Id,
                PlayerId = target.Id,
                TeamId = humanTeam.Id,
                CreatedDate = Today,
                DecisionDate = Today,
                RoleInTeam = RoleInTeam.RotationPlayer,
                ContractYears = 3,
            };
            await _pendingRepo.SaveAsync(pending);

            var negotiations = new NegotiationResolutionService(
                _pendingRepo, _offerRepo, _listingRepo, _contractRepo, _bonusRepo, new PlayerRepository(_db), _transferMarket, _messages);
            var teamsById = new Dictionary<int, Team> { [humanTeam.Id] = humanTeam, [aiTeam.Id] = aiTeam };

            // Window closed: the deal is due and good, but must not complete yet - negotiating
            // already happened (the offer exists), only finishing the move is gated.
            await negotiations.ApplyDueNegotiationsAsync(humanTeam.Id, Today, teamsById, isTransferWindowOpen: false);
            Assert.Contains(await _pendingRepo.GetByTeamAsync(humanTeam.Id), p => p.Id == pending.Id);
            Assert.Contains(target, aiTeam.Players);
            Assert.DoesNotContain(target, humanTeam.Players);

            // Window opens later - the same still-pending deal now completes.
            await negotiations.ApplyDueNegotiationsAsync(humanTeam.Id, Today.AddDays(1), teamsById, isTransferWindowOpen: true);
            Assert.DoesNotContain(await _pendingRepo.GetByTeamAsync(humanTeam.Id), p => p.Id == pending.Id);
            Assert.Contains(target, humanTeam.Players);
            Assert.DoesNotContain(target, aiTeam.Players);
        }
    }
}
