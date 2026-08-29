using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;
using System.Collections.ObjectModel;

namespace RetroFootballManager.ViewModels
{
    // Which side of the negotiation dialog is currently running - drives whose fee
    // expectation the manager-phase mood reacts to (see StartManagerNegotiation), and
    // whether a manager phase runs at all (ContractRenewal skips straight to the player phase).
    public enum NegotiationScenario
    {
        Buy,
        Loan,
        Sell,
        ContractRenewal,
    }

    // One negotiable bonus line shown in the player phase - Amount is edited directly via
    // the bound Entry (see NegotiationBonusRows), Label is already the German display text
    // so the raw ContractBonusType enum is never bound in XAML.
    public class NegotiationBonusRow(ContractBonusType type, string label)
    {
        public ContractBonusType Type { get; } = type;
        public string Label { get; } = label;
        public double Amount { get; set; }
    }

    // The interactive negotiation dialog's full state/logic (manager phase + player phase),
    // shared across every page that can trigger a negotiation - TransferMarketPage (buy/loan
    // a listed player, negotiate an incoming offer on our own listing), ScoutingPage
    // (unsolicited offer for a scouted player never put up for sale) and TrainingPage
    // (renew one of our own player's contracts). Registered as a DI singleton so the SAME
    // PlayerNegotiationsDialog instance/state is reused everywhere via
    // `BindingContext="{Binding Negotiation}"` instead of duplicating this ~500-line flow
    // per page. Each "TryStart...NegotiationAsync" entry point runs its own preconditions
    // (balance, cooldown, existing offer) and returns an error string on failure, or opens
    // the dialog and returns null on success.
    public partial class NegotiationDialogViewModel : ObservableObject
    {
        private static readonly ILog Log = LogManager.GetLogger<NegotiationDialogViewModel>();

        // char1..char4_phase_{one..five}.png (Resources/Images/Managers) - index 0 = Furious
        // (matches NegotiationMoodLevel's declaration order) through index 4 = Delighted.
        private static readonly string[] MoodPhaseSuffix = ["phase_five", "phase_four", "phase_three", "phase_two", "phase_one"];

        // A loan never has a transfer fee - the manager phase negotiates a wage-share
        // percentage (how much of the player's current salary we take over) and a loan
        // duration instead (see StartManagerNegotiation/SubmitManagerOffer).
        private const double BaseExpectedWageSharePercentage = 50.0;

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly TransferMarketService _market;
        private readonly TransferOfferRepository _offerRepo;
        private readonly ContractRepository _contractRepo;
        private readonly NegotiationCooldownRepository _cooldownRepo;
        private readonly PendingNegotiationRepository _pendingRepo;
        private readonly CupTieRepository _cupTieRepo;
        private readonly CalendarService _calendar;
        private readonly Random _rng = new();

        private Team? _myTeam;
        private NegotiationScenario _currentScenario;
        private Player? _negotiationPlayer;
        private PlayerStats? _negotiationSeasonStats;
        private Team? _negotiationCounterpartTeam;
        private TransferListing? _negotiationListing;
        private TransferOffer? _negotiationOffer;
        private double _negotiationExpectedFee;
        private bool _negotiationConcluded;
        private bool _isLoanDeal;
        private double _negotiationOriginalWage;
        private Func<Task>? _onCompleted;

        public NegotiationDialogViewModel(
            GameSession session, SaveGameService saveGame, TransferMarketService market, TransferOfferRepository offerRepo,
            ContractRepository contractRepo, NegotiationCooldownRepository cooldownRepo,
            PendingNegotiationRepository pendingRepo, CupTieRepository cupTieRepo, CalendarService calendar)
        {
            _session = session;
            _saveGame = saveGame;
            _market = market;
            _offerRepo = offerRepo;
            _contractRepo = contractRepo;
            _cooldownRepo = cooldownRepo;
            _pendingRepo = pendingRepo;
            _cupTieRepo = cupTieRepo;
            _calendar = calendar;
        }

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _isOpen;
        [ObservableProperty] private string _playerName = string.Empty;
        [ObservableProperty] private string _subtitle = string.Empty;
        [ObservableProperty] private string _lastOutcomeMessage = string.Empty;

        // Manager phase (Ablöseverhandlung) - see StartManagerNegotiation/SubmitManagerOffer.
        [ObservableProperty] private int _managerCharacterIndex = 1;
        [ObservableProperty] private NegotiationMoodLevel _managerMood = NegotiationMoodLevel.Neutral;
        [ObservableProperty] private string _managerImageSource = "avatar.png";
        [ObservableProperty] private string _managerMoodText = string.Empty;
        [ObservableProperty] private string _negotiationLogText = string.Empty;
        [ObservableProperty] private string _negotiationStatusText = string.Empty;
        // Shared by every amount stepper in the dialog (fee, wage, exit clause) - how big
        // each click on their +/- buttons is (see PresetStepInput/SteppedAmountInput.StepSize).
        [ObservableProperty] private double _negotiationStepSize = 10_000;
        [ObservableProperty] private bool _isManagerPhaseOpen;

        [ObservableProperty] private double _negotiationFee;
        [ObservableProperty] private bool _showFeeField = true;
        [ObservableProperty] private double _negotiationSellOnPercentage;
        [ObservableProperty] private bool _showSellOnField;

        // Loan-only manager-phase fields (see StartManagerNegotiation) - shown instead of the
        // fee/sell-on fields whenever the listing is a loan.
        [ObservableProperty] private bool _showLoanWageFields;
        [ObservableProperty] private int _negotiationWageSharePercentage = (int)BaseExpectedWageSharePercentage;
        [ObservableProperty] private int _negotiationLoanDurationMonths = 6;
        [ObservableProperty] private double _negotiationLoanAnnualWage;
        [ObservableProperty] private double _negotiationLoanMonthlyWage;

        // Player phase (Vertragskonditionen) - see OpenPlayerPhaseAsync/SubmitPlayerOffer.
        [ObservableProperty] private bool _isPlayerPhaseOpen;
        [ObservableProperty] private string _playerPortraitPath = string.Empty;
        [ObservableProperty] private Color _playerMoodBorderColor = Colors.Gray;
        [ObservableProperty] private string _playerMoodText = string.Empty;
        [ObservableProperty] private double _negotiatedWage;
        [ObservableProperty] private int _negotiatedContractYears = 3;
        [ObservableProperty] private bool _showContractYearsField = true;
        [ObservableProperty] private int _negotiatedRoleIndex;
        [ObservableProperty] private bool _hasExitClause;
        [ObservableProperty] private double _exitClauseAmount;
        [ObservableProperty] private bool _showCleanSheetBonus;
        [ObservableProperty] private bool _showGermanCupBonus;
        [ObservableProperty] private bool _showChampionsLeagueBonus;
        [ObservableProperty] private bool _showEuropaCupBonus;

        public string[] AvailableRoleLabels { get; } = Enum.GetValues<RoleInTeam>().Select(RoleInTeamDisplay.Label).ToArray();

        public ObservableCollection<NegotiationBonusRow> NegotiationBonusRows { get; } = [];

        // --- Public entry points, one per host scenario ---

        // Buy a listed player (transfer) or loan one in - listing already exists on the market.
        public async Task<string?> TryStartBuyOrLoanNegotiationAsync(
            Team myTeam, TransferListing listing, Player player, Team sellingTeam, PlayerStats? seasonStats,
            int season, DateTime currentDate, Func<Task> onCompleted)
        {
            if (!TransferMarketService.CanBuy(myTeam, out string? balanceError))
                return balanceError;

            var cooldown = await _cooldownRepo.GetActiveAsync(myTeam.Id, player.Id, season);
            if (cooldown is not null)
                return $"Die Verhandlungen um {player.Name} sind für diese Saison beendet.";

            var existingOffers = await _offerRepo.GetByListingAsync(listing.Id);
            if (existingOffers.Any(o => o.OfferingTeamId == myTeam.Id
                && (o.Status == TransferOfferStatus.Pending || o.Status == TransferOfferStatus.Countered)))
                return $"Du hast bereits ein laufendes Angebot für {player.Name}.";

            var currentContract = await _saveGame.GetActivePlayerContractAsync(player.Id, currentDate);
            double originalWage = currentContract?.AnnualSalary ?? PlayerValuationService.EstimateAnnualSalary(player);

            StartManagerNegotiation(
                listing.IsLoanListing ? NegotiationScenario.Loan : NegotiationScenario.Buy,
                myTeam, player, seasonStats, sellingTeam, listing, listing.AskingPrice, listing.IsLoanListing,
                originalWage, offer: null, onCompleted);

            PlayerName = player.Name;
            Subtitle = listing.IsLoanListing
                ? $"Leihverhandlung mit {sellingTeam.Name}" : $"Ablöseverhandlung mit {sellingTeam.Name}";
            IsOpen = true;
            return null;
        }

        // Free agent (contract expired, no club - see FreeAgentService): no selling club to
        // negotiate a fee with, so the manager phase is skipped entirely and this goes straight
        // to the player phase (wage/role/years/bonuses only). NegotiationFee is locked to 0 -
        // CompletePlayerPhaseAsync's normal Buy path then creates the offer/PendingNegotiation
        // exactly as usual, resolved by NegotiationResolutionService (free-agent branch).
        public async Task<string?> TryStartFreeAgentNegotiationAsync(
            Team myTeam, TransferListing listing, Player player, PlayerStats? seasonStats, int season,
            DateTime currentDate, Func<Task> onCompleted)
        {
            if (!TransferMarketService.CanBuy(myTeam, out string? balanceError))
                return balanceError;

            var cooldown = await _cooldownRepo.GetActiveAsync(myTeam.Id, player.Id, season);
            if (cooldown is not null)
                return $"Die Verhandlungen um {player.Name} sind für diese Saison beendet.";

            var existingOffers = await _offerRepo.GetByListingAsync(listing.Id);
            if (existingOffers.Any(o => o.OfferingTeamId == myTeam.Id
                && (o.Status == TransferOfferStatus.Pending || o.Status == TransferOfferStatus.Countered)))
                return $"Du hast bereits ein laufendes Angebot für {player.Name}.";

            _currentScenario = NegotiationScenario.Buy;
            _myTeam = myTeam;
            _negotiationPlayer = player;
            _negotiationSeasonStats = seasonStats;
            _negotiationCounterpartTeam = null;
            _negotiationListing = listing;
            _negotiationOffer = null;
            _negotiationExpectedFee = 0;
            _negotiationConcluded = false;
            _isLoanDeal = false;
            _negotiationOriginalWage = PlayerValuationService.EstimateAnnualSalary(player);
            _onCompleted = onCompleted;
            NegotiationFee = 0;

            PlayerName = player.Name;
            Subtitle = "Ablösefrei - Vertragsverhandlung";
            await OpenPlayerPhaseAsync();
            IsOpen = true;
            return null;
        }

        // Unsolicited offer for a player found via scouting, never put up for sale - creates
        // the shadow listing the moment the negotiation starts (see
        // TransferMarketService.CreateUnsolicitedListingAsync), same as the market-listed flow
        // from there on.
        public async Task<string?> TryStartUnsolicitedNegotiationAsync(
            Team myTeam, Player player, Team sellingTeam, PlayerStats? seasonStats, bool isLoan, int season,
            DateTime currentDate, Func<Task> onCompleted)
        {
            if (!TransferMarketService.CanBuy(myTeam, out string? balanceError))
                return balanceError;

            var cooldown = await _cooldownRepo.GetActiveAsync(myTeam.Id, player.Id, season);
            if (cooldown is not null)
                return $"Die Verhandlungen um {player.Name} sind für diese Saison beendet.";

            var listing = await _market.CreateUnsolicitedListingAsync(player, sellingTeam, season, currentDate, isLoan);
            double originalWage = PlayerValuationService.EstimateAnnualSalary(player);

            StartManagerNegotiation(
                isLoan ? NegotiationScenario.Loan : NegotiationScenario.Buy,
                myTeam, player, seasonStats, sellingTeam, listing, listing.AskingPrice, isLoan, originalWage,
                offer: null, onCompleted);

            PlayerName = player.Name;
            Subtitle = isLoan
                ? $"Leihverhandlung mit {sellingTeam.Name}" : $"Ablöseverhandlung mit {sellingTeam.Name}";
            IsOpen = true;
            return null;
        }

        // Negotiate an incoming offer on our own listing (we're the seller/lender).
        public async Task TryStartSellNegotiationAsync(
            Team myTeam, TransferOffer offer, TransferListing listing, Player player, Team buyingTeam,
            PlayerStats? seasonStats, DateTime currentDate, Func<Task> onCompleted)
        {
            double originalWage = (await _saveGame.GetActivePlayerContractAsync(player.Id, currentDate))?.AnnualSalary
                ?? PlayerValuationService.EstimateAnnualSalary(player);

            StartManagerNegotiation(
                NegotiationScenario.Sell, myTeam, player, seasonStats, buyingTeam, listing, listing.AskingPrice,
                listing.IsLoanListing, originalWage, offer, onCompleted);

            PlayerName = player.Name;
            Subtitle = listing.IsLoanListing
                ? $"Leihverhandlung mit {buyingTeam.Name}" : $"Verkaufsverhandlung mit {buyingTeam.Name}";
            if (!listing.IsLoanListing)
                NegotiationFee = Math.Round(Math.Max(offer.OfferedFee, listing.AskingPrice));
            IsOpen = true;
        }

        // Renew one of our own player's contracts - goes straight to the player phase, no
        // manager involved.
        public async Task<string?> TryStartRenewalNegotiationAsync(
            Team myTeam, Player player, int season, DateTime currentDate, Func<Task> onCompleted)
        {
            double squadAverage = myTeam.Players.Count > 0 ? myTeam.Players.Average(p => p.Rating) : 0;
            if (!PlayerTermsExpectationService.IsWillingToDiscussRenewal(player, squadAverage))
                return $"{player.Name} ist an einer Vertragsverlängerung derzeit nicht interessiert.";

            var cooldown = await _cooldownRepo.GetActiveAsync(myTeam.Id, player.Id, season);
            if (cooldown is not null)
                return $"Die Verhandlungen mit {player.Name} sind für diese Saison beendet.";

            var contracts = await _contractRepo.GetByHolderAsync(player.Id, ContractHolderType.Player);
            var activeContract = PlayerContractService.GetActiveContract(player.Id, contracts, currentDate);
            if (activeContract is null)
                return "Kein aktiver Vertrag gefunden.";

            _myTeam = myTeam;
            _currentScenario = NegotiationScenario.ContractRenewal;
            _negotiationPlayer = player;
            _negotiationCounterpartTeam = myTeam;
            _negotiationOffer = null;
            _negotiationSeasonStats = await _saveGame.GetPlayerSeasonStatsAsync(player.Id, season);
            _negotiationConcluded = false;
            _onCompleted = onCompleted;

            PlayerName = player.Name;
            Subtitle = "Vertragsverlängerung";
            NegotiatedWage = Math.Max(activeContract.AnnualSalary, PlayerValuationService.EstimateAnnualSalary(player));

            await OpenPlayerPhaseAsync();
            IsOpen = true;
            return null;
        }

        // Starts a fresh manager-phase negotiation - picks one of the 4 manager characters at
        // random and computes the counterpart's secret expectation once (talent/form premium
        // included, see NegotiationExpectationService). Never shown to the user directly -
        // only their mood reaction to each offer is. A loan listing negotiates a wage-share
        // percentage + duration instead of a fee - no transfer fee is ever paid for a loan.
        private void StartManagerNegotiation(
            NegotiationScenario scenario, Team myTeam, Player player, PlayerStats? seasonStats, Team counterpartTeam,
            TransferListing listing, double baseFee, bool isLoanDeal, double originalWage, TransferOffer? offer,
            Func<Task> onCompleted)
        {
            _myTeam = myTeam;
            _currentScenario = scenario;
            _negotiationPlayer = player;
            _negotiationSeasonStats = seasonStats;
            _negotiationCounterpartTeam = counterpartTeam;
            _negotiationListing = listing;
            _negotiationOffer = offer;
            _negotiationConcluded = false;
            _isLoanDeal = isLoanDeal;
            _negotiationOriginalWage = originalWage;
            _onCompleted = onCompleted;

            _negotiationExpectedFee = isLoanDeal
                ? NegotiationExpectationService.EstimateExpectedFee(BaseExpectedWageSharePercentage, player, seasonStats)
                : NegotiationExpectationService.EstimateExpectedFee(baseFee, player, seasonStats);

            ManagerCharacterIndex = _rng.Next(1, 5);
            ManagerMood = NegotiationMoodLevel.Neutral;
            // Always starts content/relaxed (phase_one) - only reacts once an offer comes in
            // (see SubmitManagerOffer), not before.
            ManagerImageSource = $"char{ManagerCharacterIndex}_phase_one.png";
            ManagerMoodText = "Der Manager erwartet dein Angebot.";
            NegotiationLogText = string.Empty;
            NegotiationStatusText = string.Empty;

            ShowFeeField = !isLoanDeal;
            ShowSellOnField = !isLoanDeal;
            ShowLoanWageFields = isLoanDeal;
            NegotiationFee = Math.Round(baseFee);
            NegotiationSellOnPercentage = 0;
            NegotiationWageSharePercentage = (int)BaseExpectedWageSharePercentage;
            NegotiationLoanDurationMonths = 6;
            NegotiatedWage = Math.Round(baseFee * 0.15);
            IsManagerPhaseOpen = true;
            IsPlayerPhaseOpen = false;
            UpdateLoanWagePreview();
        }

        // Keeps the "das zahlen wir tatsächlich" preview (annual + monthly) in sync whenever
        // the wage-share slider moves, so the effect of the percentage is visible immediately
        // - not just after submitting an offer.
        partial void OnNegotiationWageSharePercentageChanged(int value) => UpdateLoanWagePreview();

        private void UpdateLoanWagePreview()
        {
            NegotiationLoanAnnualWage = Math.Round(_negotiationOriginalWage * NegotiationWageSharePercentage / 100.0);
            NegotiationLoanMonthlyWage = Math.Round(NegotiationLoanAnnualWage / 12.0);
        }

        private static string BuildManagerImageSource(int characterIndex, NegotiationMoodLevel mood) =>
            $"char{characterIndex}_{MoodPhaseSuffix[(int)mood]}.png";

        private static string MoodText(NegotiationMoodLevel mood) => mood switch
        {
            NegotiationMoodLevel.Delighted => "Der Manager ist hellauf begeistert von diesem Angebot!",
            NegotiationMoodLevel.Happy => "Der Manager ist zufrieden - das Angebot wird angenommen.",
            NegotiationMoodLevel.Neutral => "Der Manager wägt noch ab.",
            NegotiationMoodLevel.Impatient => "Der Manager wird langsam ungeduldig.",
            _ => "Der Manager tobt - die Verhandlung ist gescheitert!",
        };

        [RelayCommand]
        private async Task SubmitManagerOffer()
        {
            if (IsBusy || _negotiationConcluded) return;
            if (_myTeam is null || _negotiationPlayer is null || _negotiationCounterpartTeam is null)
                return;

            double offeredValue = _isLoanDeal ? NegotiationWageSharePercentage : NegotiationFee;
            double ratio = _negotiationExpectedFee > 0 ? offeredValue / _negotiationExpectedFee : 1.0;
            var mood = NegotiationExpectationService.EvaluateFeeMood(ratio);
            ManagerMood = mood;
            ManagerImageSource = BuildManagerImageSource(ManagerCharacterIndex, mood);
            ManagerMoodText = MoodText(mood);
            NegotiationLogText += _isLoanDeal
                ? $"Angebot {NegotiationWageSharePercentage:N0}% Gehaltsanteil, {NegotiationLoanDurationMonths} Monate: {ManagerMoodText}\n"
                : $"Angebot {NegotiationFee:N0} €: {ManagerMoodText}\n";

            if (mood == NegotiationMoodLevel.Furious)
            {
                _negotiationConcluded = true;
                await _cooldownRepo.SaveAsync(new NegotiationCooldown
                {
                    BuyingTeamId = _myTeam.Id, PlayerId = _negotiationPlayer.Id, Season = CurrentSeason(),
                });
                NegotiationStatusText = "Die Verhandlung ist abgebrochen - frühestens nächste Saison wieder möglich.";
                LastOutcomeMessage = $"Verhandlung um {_negotiationPlayer.Name} gescheitert.";
                await RaiseCompletedAsync();
                return;
            }

            if (mood is NegotiationMoodLevel.Happy or NegotiationMoodLevel.Delighted)
            {
                IsBusy = true;
                try
                {
                    await CompleteManagerPhaseAsync();
                }
                catch (Exception ex)
                {
                    Log.Error("Could not complete manager-phase negotiation.", ex);
                    LastOutcomeMessage = "Verhandlung fehlgeschlagen.";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private int CurrentSeason() => _session.State?.Season ?? 0;

        private DateTime CurrentDate() => _session.State?.CurrentDate ?? DateTime.Today;

        // Manager phase succeeded. Sell (scenario 3, transfer or loan-out) finalizes
        // immediately - no Bedenkzeit, see plan (we're the seller/lender, no personal terms
        // to set for the other club's use of the player). A loan-in (buy side) also finalizes
        // immediately, since a loan never negotiates personal terms with the player (see
        // StartManagerNegotiation) - only an outright Buy moves on to the player phase.
        private async Task CompleteManagerPhaseAsync()
        {
            if (_myTeam is null || _negotiationPlayer is null || _negotiationCounterpartTeam is null
                || _negotiationListing is null)
                return;

            var currentDate = CurrentDate();
            double negotiatedWage = NegotiationLoanAnnualWage;

            if (_currentScenario == NegotiationScenario.Sell)
            {
                // Negotiating (agreeing terms) is always possible - only actually completing the
                // move is gated on the transfer window (matches SeasonPhaseCalculator/
                // TransferAiService/NegotiationResolutionService). Doesn't conclude the
                // negotiation - the offer/listing stay exactly as they are, the manager can just
                // try "Annehmen" again once the window reopens.
                if (_session.State is not null)
                {
                    var phase = await _calendar.GetSeasonPhaseAsync(_session.State);
                    if (phase.TransferWindow != TransferWindowState.Open)
                    {
                        NegotiationStatusText = "Transferfenster geschlossen - der Wechsel kann erst im nächsten Transferfenster abgeschlossen werden.";
                        return;
                    }
                }

                if (_isLoanDeal)
                {
                    _negotiationConcluded = true;
                    await _market.LoanOutAsync(
                        _negotiationPlayer, _myTeam, _negotiationCounterpartTeam, currentDate,
                        currentDate.AddMonths(NegotiationLoanDurationMonths), negotiatedWage);
                    await _market.RemoveListingAsync(_negotiationListing);
                    NegotiationStatusText = "Der Deal ist besiegelt!";
                    LastOutcomeMessage = $"{_negotiationPlayer.Name} an {_negotiationCounterpartTeam.Name} verliehen.";
                }
                else
                {
                    if (_negotiationOffer is null)
                        return;

                    // The mood/ratio check above only reflects the buyer's willingness, not
                    // whether they can actually afford it - without this, the fee could be
                    // negotiated up to the performance-premium ceiling regardless of the
                    // buying club's real balance (see TransferMarketService.CanAffordFee, used
                    // the same way on the buying side elsewhere). Doesn't conclude the
                    // negotiation - the user can lower the fee and resubmit.
                    if (!TransferMarketService.CanAffordFee(_negotiationCounterpartTeam, NegotiationFee))
                    {
                        ManagerMood = NegotiationMoodLevel.Impatient;
                        ManagerImageSource = BuildManagerImageSource(ManagerCharacterIndex, ManagerMood);
                        ManagerMoodText = "Der Verein kann sich diese Ablöse schlicht nicht leisten.";
                        NegotiationLogText += $"{_negotiationCounterpartTeam.Name} kann {NegotiationFee:N0} € nicht aufbringen - versuch es mit einer niedrigeren Ablöse.\n";
                        return;
                    }

                    _negotiationConcluded = true;
                    _negotiationOffer.OfferedFee = NegotiationFee;
                    await _market.AcceptOfferAsync(
                        _negotiationOffer, _negotiationListing, _myTeam, _negotiationCounterpartTeam, _negotiationPlayer,
                        currentDate);

                    double sellOnPercentage = Math.Clamp(NegotiationSellOnPercentage, 0, 30);
                    if (sellOnPercentage > 0)
                    {
                        var contracts = await _contractRepo.GetByHolderAsync(_negotiationPlayer.Id, ContractHolderType.Player);
                        var newContract = PlayerContractService.GetActiveContract(_negotiationPlayer.Id, contracts, currentDate);
                        if (newContract is not null)
                        {
                            double buyerSquadAverage = _negotiationCounterpartTeam.Players.Count > 0
                                ? _negotiationCounterpartTeam.Players.Average(p => p.Rating) : 0;
                            newContract.SellOnPercentage = sellOnPercentage;
                            newContract.RoleInTeam = PlayerTermsExpectationService.EstimateExpectedRole(_negotiationPlayer, buyerSquadAverage);
                            newContract.HasNegotiatedTerms = true;
                            await _contractRepo.SaveAsync(newContract);
                        }
                    }

                    NegotiationStatusText = "Der Deal ist besiegelt!";
                    LastOutcomeMessage = $"{_negotiationPlayer.Name} an {_negotiationCounterpartTeam.Name} verkauft.";
                }

                await RaiseCompletedAsync();
            }
            else if (_isLoanDeal)
            {
                _negotiationConcluded = true;
                var offer = await _market.MakeOfferAsync(
                    _negotiationListing, _myTeam, fee: 0, wageOffer: negotiatedWage, currentDate);
                int bedenkzeitDays = _rng.Next(3, 5);
                offer.LockedUntilDate = currentDate.AddDays(bedenkzeitDays);
                await _offerRepo.SaveAsync(offer);

                await _pendingRepo.SaveAsync(new PendingNegotiation
                {
                    Kind = NegotiationKind.TransferOrLoanBuy,
                    TransferOfferId = offer.Id,
                    PlayerId = _negotiationPlayer.Id,
                    TeamId = _myTeam.Id,
                    CreatedDate = currentDate,
                    DecisionDate = offer.LockedUntilDate.Value,
                    LoanDurationMonths = NegotiationLoanDurationMonths,
                });

                NegotiationStatusText = $"Leihe verhandelt - in {bedenkzeitDays} Tagen fällt die Entscheidung.";
                LastOutcomeMessage = $"Leihverhandlung um {_negotiationPlayer.Name} erfolgreich - Bedenkzeit läuft.";
                await RaiseCompletedAsync();
            }
            else
            {
                await OpenPlayerPhaseAsync();
            }
        }

        // Whether the selling club (buy/loan) or our own club (renewal) is currently active in
        // a given cup competition this season - gates the matching bonus row (see
        // OpenPlayerPhaseAsync). NotEntered also covers "draw hasn't happened yet".
        private async Task<bool> IsInCompetitionAsync(int teamId, int season, CompetitionType competition)
        {
            var status = await _cupTieRepo.GetParticipationStatusAsync(teamId, season, competition);
            return status != CupParticipationStatus.NotEntered;
        }

        // Enters the player phase - personal terms (wage/role/contract length/bonuses/exit
        // clause) negotiated directly with the player, prefilled with fair defaults. Reused by
        // both the post-manager-phase buy flow and the standalone renewal entry point.
        private async Task OpenPlayerPhaseAsync()
        {
            if (_myTeam is null || _negotiationPlayer is null)
                return;

            IsManagerPhaseOpen = false;
            IsPlayerPhaseOpen = true;

            double squadAverage = _myTeam.Players.Count > 0 ? _myTeam.Players.Average(p => p.Rating) : 0;
            var expectedRole = PlayerTermsExpectationService.EstimateExpectedRole(_negotiationPlayer, squadAverage);
            NegotiatedRoleIndex = Array.IndexOf(Enum.GetValues<RoleInTeam>(), expectedRole);
            NegotiatedContractYears = _currentScenario == NegotiationScenario.ContractRenewal
                ? 2 : PlayerTermsExpectationService.EstimatePreferredContractYears(_negotiationPlayer.Age);
            // Reached only for ContractRenewal/Buy - Loan finalizes straight from the manager
            // phase and never opens the player phase (see CompleteManagerPhaseAsync).
            ShowContractYearsField = true;
            HasExitClause = false;
            ExitClauseAmount = Math.Round(PlayerTermsExpectationService.EstimateExpectedWage(_negotiationPlayer) * 10);
            PlayerMoodText = string.Empty;
            PlayerMoodBorderColor = Colors.Gray;
            NegotiationLogText = string.Empty;
            NegotiationStatusText = string.Empty;
            PlayerPortraitPath = _negotiationPlayer.ImagePath ?? string.Empty;

            int season = CurrentSeason();
            ShowCleanSheetBonus = _negotiationPlayer.Position == Position.Goalkeeper;
            ShowGermanCupBonus = await IsInCompetitionAsync(_myTeam.Id, season, CompetitionType.GermanCup);
            ShowChampionsLeagueBonus = await IsInCompetitionAsync(_myTeam.Id, season, CompetitionType.ChampionsLeague);
            ShowEuropaCupBonus = await IsInCompetitionAsync(_myTeam.Id, season, CompetitionType.EuropaCup);

            NegotiationBonusRows.Clear();
            NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.Goal, "Torprämie (pro Tor)"));
            NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.Appearance, "Einsatzprämie (pro Einsatz)"));
            NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.StartingEleven, "Auflaufprämie (pro Startelf-Einsatz)"));
            if (ShowCleanSheetBonus)
                NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.CleanSheet, "Zu-Null-Prämie (Torwart)"));
            NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.ChampionshipOrPromotion, "Meisterschaft/Aufstieg"));
            if (ShowGermanCupBonus)
                NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.GermanCupWin, "Pokalsieg (Deutscher Pokal)"));
            if (ShowChampionsLeagueBonus)
                NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.ChampionsLeagueWin, "Europapokal der Meister - Sieg"));
            if (ShowEuropaCupBonus)
                NegotiationBonusRows.Add(new NegotiationBonusRow(ContractBonusType.EuropaCupWin, "Europapokal - Sieg"));
        }

        private static string PlayerMoodTextFor(NegotiationMoodLevel mood) => mood switch
        {
            NegotiationMoodLevel.Delighted => "Der Spieler ist hellauf begeistert von diesem Angebot!",
            NegotiationMoodLevel.Happy => "Der Spieler ist zufrieden - er unterschreibt.",
            NegotiationMoodLevel.Neutral => "Der Spieler überlegt noch.",
            NegotiationMoodLevel.Impatient => "Der Spieler zeigt sich enttäuscht.",
            _ => "Der Spieler lehnt empört ab - keine Einigung möglich!",
        };

        // Continuous green(100)->amber(50)->red(0) lerp for the player portrait border, per
        // the satisfaction score - unlike the manager's discrete character images, the player
        // only has the one portrait, so the mood shows via a stepless color instead.
        private static Color MoodToColor(double satisfaction)
        {
            double t = Math.Clamp(satisfaction, 0, 100) / 100.0;
            var (from, to, localT) = t < 0.5
                ? (Color.FromArgb("#EF4444"), Color.FromArgb("#F59E0B"), t / 0.5)
                : (Color.FromArgb("#F59E0B"), Color.FromArgb("#22C55E"), (t - 0.5) / 0.5);
            return Color.FromRgba(
                from.Red + ((to.Red - from.Red) * localT),
                from.Green + ((to.Green - from.Green) * localT),
                from.Blue + ((to.Blue - from.Blue) * localT),
                1.0);
        }

        [RelayCommand]
        private async Task SubmitPlayerOffer()
        {
            if (IsBusy || _negotiationConcluded) return;
            if (_myTeam is null || _negotiationPlayer is null)
                return;

            double squadAverage = _myTeam.Players.Count > 0 ? _myTeam.Players.Average(p => p.Rating) : 0;
            var roles = Enum.GetValues<RoleInTeam>();
            var negotiatedRole = roles[Math.Clamp(NegotiatedRoleIndex, 0, roles.Length - 1)];
            double totalBonusValue = NegotiationBonusRows.Sum(b => b.Amount);

            double satisfaction = PlayerTermsExpectationService.EstimateSatisfaction(
                _negotiationPlayer, squadAverage, NegotiatedWage, negotiatedRole, NegotiatedContractYears,
                HasExitClause, totalBonusValue);
            var mood = PlayerTermsExpectationService.EvaluateMood(satisfaction);
            PlayerMoodText = PlayerMoodTextFor(mood);
            PlayerMoodBorderColor = MoodToColor(satisfaction);
            NegotiationLogText += $"Angebot {NegotiatedWage:N0} €/Jahr, {RoleInTeamDisplay.Label(negotiatedRole)}: {PlayerMoodText}\n";

            if (mood == NegotiationMoodLevel.Furious)
            {
                _negotiationConcluded = true;
                await _cooldownRepo.SaveAsync(new NegotiationCooldown
                {
                    BuyingTeamId = _myTeam.Id, PlayerId = _negotiationPlayer.Id, Season = CurrentSeason(),
                });
                NegotiationStatusText = "Der Spieler lehnt ab - frühestens nächste Saison wieder möglich.";
                LastOutcomeMessage = $"Verhandlung mit {_negotiationPlayer.Name} gescheitert.";
                await RaiseCompletedAsync();
                return;
            }

            if (mood is NegotiationMoodLevel.Happy or NegotiationMoodLevel.Delighted)
            {
                IsBusy = true;
                try
                {
                    await CompletePlayerPhaseAsync(negotiatedRole);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not complete player-phase negotiation.", ex);
                    LastOutcomeMessage = "Verhandlung fehlgeschlagen.";
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        // Player phase succeeded - queues the Bedenkzeit (3-4 days) for the actual
        // sign/renew, materialized by NegotiationResolutionService on the daily tick.
        private async Task CompletePlayerPhaseAsync(RoleInTeam negotiatedRole)
        {
            if (_myTeam is null || _negotiationPlayer is null)
                return;

            var currentDate = CurrentDate();
            _negotiationConcluded = true;
            int bedenkzeitDays = _rng.Next(3, 5);
            double exitClauseAmount = HasExitClause ? ExitClauseAmount : 0;
            var bonuses = NegotiationBonusRows.Where(b => b.Amount > 0)
                .Select(b => new NegotiatedBonusLine(b.Type, b.Amount)).ToList();

            if (_currentScenario == NegotiationScenario.ContractRenewal)
            {
                var contracts = await _contractRepo.GetByHolderAsync(_negotiationPlayer.Id, ContractHolderType.Player);
                var activeContract = PlayerContractService.GetActiveContract(_negotiationPlayer.Id, contracts, currentDate);
                if (activeContract is null)
                {
                    LastOutcomeMessage = "Kein aktiver Vertrag gefunden.";
                    await RaiseCompletedAsync();
                    return;
                }

                await _pendingRepo.SaveAsync(new PendingNegotiation
                {
                    Kind = NegotiationKind.ContractRenewal,
                    PlayerId = _negotiationPlayer.Id,
                    TeamId = _myTeam.Id,
                    ContractId = activeContract.Id,
                    CreatedDate = currentDate,
                    DecisionDate = currentDate.AddDays(bedenkzeitDays),
                    RoleInTeam = negotiatedRole,
                    ContractYears = NegotiatedContractYears,
                    ExitClauseAmount = exitClauseAmount,
                    NegotiatedWage = NegotiatedWage,
                    Bonuses = bonuses,
                });

                NegotiationStatusText = $"Angebot unterbreitet - in {bedenkzeitDays} Tagen fällt die Entscheidung.";
                LastOutcomeMessage = $"Vertragsangebot an {_negotiationPlayer.Name} übermittelt.";
            }
            else
            {
                if (_negotiationListing is null)
                    return;

                var offer = await _market.MakeOfferAsync(_negotiationListing, _myTeam, NegotiationFee, NegotiatedWage, currentDate);
                offer.LockedUntilDate = currentDate.AddDays(bedenkzeitDays);
                await _offerRepo.SaveAsync(offer);

                await _pendingRepo.SaveAsync(new PendingNegotiation
                {
                    Kind = NegotiationKind.TransferOrLoanBuy,
                    TransferOfferId = offer.Id,
                    PlayerId = _negotiationPlayer.Id,
                    TeamId = _myTeam.Id,
                    CreatedDate = currentDate,
                    DecisionDate = offer.LockedUntilDate.Value,
                    RoleInTeam = negotiatedRole,
                    ContractYears = NegotiatedContractYears,
                    ExitClauseAmount = exitClauseAmount,
                    SellOnPercentage = Math.Clamp(NegotiationSellOnPercentage, 0, 30),
                    Bonuses = bonuses,
                });

                NegotiationStatusText = $"Angebot komplett - in {bedenkzeitDays} Tagen fällt die Entscheidung.";
                LastOutcomeMessage = $"Verhandlung um {_negotiationPlayer.Name} erfolgreich - Bedenkzeit läuft.";
            }

            IsPlayerPhaseOpen = false;
            await RaiseCompletedAsync();
        }

        private async Task RaiseCompletedAsync()
        {
            var callback = _onCompleted;
            if (callback is not null)
                await callback();
        }

        [RelayCommand]
        private void Close()
        {
            IsOpen = false;
            IsManagerPhaseOpen = false;
            IsPlayerPhaseOpen = false;
            PlayerName = string.Empty;
            _myTeam = null;
            _negotiationListing = null;
            _negotiationPlayer = null;
            _negotiationCounterpartTeam = null;
            _negotiationOffer = null;
            _negotiationConcluded = false;
            _onCompleted = null;
        }
    }
}
