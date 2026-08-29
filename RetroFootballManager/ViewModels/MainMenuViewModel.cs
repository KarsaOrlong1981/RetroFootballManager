using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class MainMenuViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<MainMenuViewModel>();

        private readonly GameSession _session;
        private readonly CalendarService _calendar;
        private readonly SaveGameService _saveGame;
        private readonly CupMatchDayService _cupMatchDayService;
        private readonly MessageService _messages;
        private readonly CalendarAdvanceService _calendarAdvance;
        private readonly FriendlyService _friendly;
        private readonly TrainingCampService _trainingCamps;
        private readonly INavigationService _navigation;
        private readonly SponsorService _sponsorService;
        private readonly AppSettingsService _appSettings;

        private CupTie? _pendingCupTie;
        private CompetitionType _pendingCompetition;
        private DateTime? _nextLeagueFixtureDate;
        private Fixture? _nextLeagueFixture;
        private Fixture? _pendingFriendly;
        private SeasonPhase _phase;
        private DateTime? _windowEnd;
        private Finances? _subscribedFinances;

        public MainMenuViewModel(
            IDispatcher dispatcher,
            GameSession session,
            CalendarService calendar,
            SaveGameService saveGame,
            CupMatchDayService cupMatchDayService,
            MessageService messages,
            CalendarAdvanceService calendarAdvance,
            FriendlyService friendly,
            TrainingCampService trainingCamps,
            INavigationService navigation,
            SponsorService sponsorService,
            AppSettingsService appSettings,
            FinanceService financeService)
            : base(dispatcher)
        {
            _session = session;
            _calendar = calendar;
            _saveGame = saveGame;
            _cupMatchDayService = cupMatchDayService;
            _messages = messages;
            _calendarAdvance = calendarAdvance;
            _friendly = friendly;
            _trainingCamps = trainingCamps;
            _navigation = navigation;
            _sponsorService = sponsorService;
            _appSettings = appSettings;
            _sponsorService.SponsorChanged += SponsorService_SponsorChanged;
            Title = "Hauptmenü";
            _showTooltips = _appSettings.ShowTooltips;
        }

        [ObservableProperty]
        private bool _showTooltips;

        partial void OnShowTooltipsChanged(bool value) => _appSettings.ShowTooltips = value;

        private void SponsorService_SponsorChanged(object? sender, EventArgs e)
        {
            _ = HandleActiveSponsors();
        }

        // Re-subscribes only if the instance changed - InitializeAsync runs on every day
        // advance, so guard against piling up duplicate handlers on the same Finances object.
        private void SubscribeToFinances(Finances? finances)
        {
            if (ReferenceEquals(_subscribedFinances, finances))
                return;
            if (_subscribedFinances is not null)
                _subscribedFinances.CurrentBalanceChanged -= Finances_CurrentBalanceChanged;
            _subscribedFinances = finances;
            if (_subscribedFinances is not null)
                _subscribedFinances.CurrentBalanceChanged += Finances_CurrentBalanceChanged;
        }

        private void Finances_CurrentBalanceChanged(object? sender, EventArgs e)
        {
            if (sender is not Finances finances)
                return;
            Budget = $"{finances.CurrentBalance:N0} €";
            OnPropertyChanged(nameof(IsPositiveBudget));
        }

        [ObservableProperty]
        private Sponsor? _mainSponsor;
        [ObservableProperty]
        private Sponsor? _perimeterSponsor;
        [ObservableProperty]
        private Sponsor? _kitSponsor;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStatusText))]
        private string _statusText = string.Empty;

        public bool HasStatusText => !string.IsNullOrEmpty(StatusText);

        [ObservableProperty]
        private string _clubName = string.Empty;

        [ObservableProperty]
        private string _clubShortName = string.Empty;

        [ObservableProperty]
        private string? _clubLogoPath;

        [ObservableProperty]
        private string _currentDate = string.Empty;

        [ObservableProperty]
        private string _season = string.Empty;

        [ObservableProperty]
        private string _budget = string.Empty;

        [ObservableProperty]
        private string _nextMatchInfo = string.Empty;

        [ObservableProperty]
        private string _nextCupInfo = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private string _busyText = string.Empty;

        [ObservableProperty]
        private string _transferWindowInfo = string.Empty;

        [ObservableProperty]
        private Color _transferWindowColor = Colors.Gray;

        [ObservableProperty]
        private string _preSeasonInfo = string.Empty;

        [ObservableProperty]
        private bool _hasPreSeasonInfo;

        [ObservableProperty]
        private int _unreadMessageCount;

        [ObservableProperty]
        private bool _hasUnreadMessages;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasNoDueMatch))]
        private bool _hasDueMatch;

        public bool IsPositiveBudget => _session?.ManagerTeam?.Finances?.CurrentBalance > 0;
        public bool HasNoDueMatch => !HasDueMatch;

        [ObservableProperty]
        private bool _isPreSeasonOrWinterBreak;

        [ObservableProperty]
        private bool _hasActiveTrainingCamp;

        [ObservableProperty]
        private string _activeTrainingCampText = string.Empty;

        public ObservableCollection<string> UpcomingFriendlies { get; } = [];

        public bool HasUpcomingFriendlies => UpcomingFriendlies.Count > 0;
        public bool HasOverviewContent => HasActiveTrainingCamp || HasUpcomingFriendlies;

        [ObservableProperty]
        private bool _isFriendlyPickerOpen;

        [ObservableProperty]
        private FriendlyOpponentOption? _selectedFriendlyOpponent;

        [ObservableProperty]
        private DateTime? _selectedFriendlyDate;

        [ObservableProperty]
        private string _friendlyValidationText = string.Empty;

        public ObservableCollection<FriendlyOpponentOption> FriendlyOpponents { get; } = [];
        public ObservableCollection<DateTime> SuggestedFriendlyDates { get; } = [];

        [ObservableProperty]
        private bool _isTrainingCampDialogOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBasicTierSelected))]
        [NotifyPropertyChangedFor(nameof(IsAdvancedTierSelected))]
        [NotifyPropertyChangedFor(nameof(IsEliteTierSelected))]
        private TrainingCampTier _selectedTrainingCampTier = TrainingCampTier.Basic;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Is1WeekSelected))]
        [NotifyPropertyChangedFor(nameof(Is2WeeksSelected))]
        private int _selectedTrainingCampDuration = 1;

        public bool IsBasicTierSelected => SelectedTrainingCampTier == TrainingCampTier.Basic;
        public bool IsAdvancedTierSelected => SelectedTrainingCampTier == TrainingCampTier.Advanced;
        public bool IsEliteTierSelected => SelectedTrainingCampTier == TrainingCampTier.Elite;
        public bool Is1WeekSelected => SelectedTrainingCampDuration == 1;
        public bool Is2WeeksSelected => SelectedTrainingCampDuration == 2;

        [ObservableProperty]
        private string _trainingCampPreviewText = string.Empty;

        [ObservableProperty]
        private string _trainingCampValidationText = string.Empty;

        [ObservableProperty]
        private bool _canConfirmTrainingCamp;

        [ObservableProperty]
        private bool _hasSponsor;

        [ObservableProperty]
        private int _fanMood;
        [ObservableProperty]
        private int _boardMood;
        [ObservableProperty]
        private Color _fanMoodColor = Colors.Gray;
        [ObservableProperty]
        private Color _boardMoodColor = Colors.Gray;

        // Green >= warning threshold, orange between warning and game-over, red below.
        private static Color MoodColor(int mood) => mood >= ClubMoodService.WarningThreshold
            ? Color.FromArgb("#22C55E")
            : mood >= ClubMoodService.GameOverThreshold
                ? Color.FromArgb("#F59E0B")
                : Color.FromArgb("#EF4444");

        public async Task InitializeAsync()
        {
            var state = _session.State;
            var team = _session.ManagerTeam;
            if (state is null || team is null)
                return;

            if (state.IsGameOver)
            {
                await _navigation.GoToRootAsync("gameover");
                return;
            }

            FanMood = team.FanMood;
            BoardMood = team.BoardMood;
            FanMoodColor = MoodColor(team.FanMood);
            BoardMoodColor = MoodColor(team.BoardMood);

            ClubName = team.Name;
            ClubShortName = team.ShortName;
            ClubLogoPath = team.LogoPath;
            CurrentDate = state.CurrentDate.ToString("dd.MM.yyyy");
            Season = $"Saison {state.Season}";
            Budget = team.Finances is not null
                ? $"{team.Finances.CurrentBalance:N0} €"
                : "–";
            SubscribeToFinances(team.Finances);

            _nextLeagueFixtureDate = null;
            // Fetched once and reused below (season-phase/training-camp window calc) - this
            // whole method reruns once per simulated day during "Zeit vorstellen", so a second
            // full season-fixtures query here doubled that cost for no reason.
            List<Fixture> seasonFixtures = [];
            try
            {
                seasonFixtures = await _saveGame.GetFixturesAsync(state.Season);
                var nextFixture = seasonFixtures
                    .Where(f => !f.Played && (f.HomeTeamId == team.Id || f.AwayTeamId == team.Id))
                    .OrderBy(f => f.Matchday)
                    .FirstOrDefault();
                _nextLeagueFixture = nextFixture;
                _nextLeagueFixtureDate = nextFixture?.Date;
                NextMatchInfo = nextFixture is null
                    ? "Keine weiteren Spiele"
                    : $"Nächster Spieltag: {nextFixture.Matchday}";
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine next matchday.", ex);
            }

            try
            {
                var candidates = new List<(CompetitionType Competition, CupTie Tie)>();

                var pokal = await _saveGame.GetNextCompetitionTieForTeamAsync(
                    CompetitionType.GermanCup, state.Season, team.Id, _session.Teams, _cupMatchDayService, state.SeasonStart);
                if (pokal is not null)
                    candidates.Add((CompetitionType.GermanCup, pokal));

                foreach (var competition in new[] { CompetitionType.ChampionsLeague, CompetitionType.EuropaCup })
                {
                    if (!await _saveGame.IsTeamInCompetitionAsync(competition, state.Season, team.Id))
                        continue;

                    var tie = await _saveGame.GetNextCompetitionTieForTeamAsync(
                        competition, state.Season, team.Id, _session.Teams, _cupMatchDayService, state.SeasonStart);
                    if (tie is not null)
                        candidates.Add((competition, tie));
                }

                var next = candidates.OrderBy(c => c.Tie.Date).FirstOrDefault();
                _pendingCupTie = next.Tie;
                _pendingCompetition = next.Competition;
                NextCupInfo = _pendingCupTie is null
                    ? string.Empty
                    : $"Nächstes Spiel ({CupMatchDayViewModel.CompetitionLabel(_pendingCompetition)}): {CupMatchDayViewModel.RoundDisplayName(_pendingCupTie.Round, _pendingCupTie.Group, _pendingCupTie.LegNumber)}";
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine next cup/tournament match.", ex);
            }

            try
            {
                var phase = await _calendar.GetSeasonPhaseAsync(state);
                _phase = phase.Phase;
                bool open = phase.TransferWindow == TransferWindowState.Open;
                string phaseLabel = phase.Phase switch
                {
                    SeasonPhase.PreSeason => "Vorbereitung",
                    SeasonPhase.WinterBreak => "Winterpause",
                    SeasonPhase.FirstHalf => "Hinrunde",
                    _ => "Rückrunde",
                };
                TransferWindowInfo = open
                    ? $"Transferfenster offen ({phaseLabel})"
                    : $"Transferfenster geschlossen ({phaseLabel})";
                TransferWindowColor = open ? Color.FromArgb("#22C55E") : Color.FromArgb("#EF4444");

                HasPreSeasonInfo = _phase == SeasonPhase.PreSeason;
                if (HasPreSeasonInfo)
                {
                    int weeksTillSaisonStart = Math.Max(0, (int)Math.Ceiling(
                        (state.SeasonStart.Date - state.CurrentDate.Date).TotalDays / 7.0));
                    PreSeasonInfo = $"In Vorbereitungsphase - noch {weeksTillSaisonStart} Wochen";
                }
                else
                {
                    PreSeasonInfo = string.Empty;
                }

                IsPreSeasonOrWinterBreak = _phase is SeasonPhase.PreSeason or SeasonPhase.WinterBreak;

                _windowEnd = TrainingCampService.GetWindowEndDate(state, phase, seasonFixtures);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine season phase.", ex);
            }

            _pendingFriendly = null;
            try
            {
                var dueFriendlies = await _friendly.GetDueFriendliesAsync(team.Id, state.CurrentDate);
                _pendingFriendly = dueFriendlies.OrderBy(f => f.Date).FirstOrDefault();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine due friendly match.", ex);
            }

            try
            {
                UpcomingFriendlies.Clear();
                var upcoming = await _friendly.GetUpcomingFriendliesAsync(team.Id);
                foreach (var fixture in upcoming.OrderBy(f => f.Date))
                {
                    var opponentId = fixture.HomeTeamId == team.Id ? fixture.AwayTeamId : fixture.HomeTeamId;
                    var opponent = _session.Teams.FirstOrDefault(t => t.Id == opponentId);
                    string vs = fixture.HomeTeamId == team.Id ? "H" : "A";
                    UpcomingFriendlies.Add($"{fixture.Date:dd.MM.yyyy} ({vs}) vs. {opponent?.Name ?? "?"}");
                }
                OnPropertyChanged(nameof(HasUpcomingFriendlies));
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine upcoming friendly matches.", ex);
            }

            try
            {
                var activeCamp = await _trainingCamps.GetActiveCampAsync(team.Id, state.CurrentDate);
                HasActiveTrainingCamp = activeCamp is not null;
                if (activeCamp is not null)
                {
                    int daysLeft = Math.Max(0, (activeCamp.EndDate.Date - state.CurrentDate.Date).Days);
                    ActiveTrainingCampText = $"Trainingslager ({activeCamp.Tier}) - noch {daysLeft} Tag(e)";
                }
                else
                {
                    ActiveTrainingCampText = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine active training camp.", ex);
            }

            OnPropertyChanged(nameof(HasOverviewContent));

            bool leagueDue = _nextLeagueFixtureDate is not null && _nextLeagueFixtureDate.Value.Date <= state.CurrentDate.Date;
            bool cupDue = _pendingCupTie is not null && _pendingCupTie.Date.Date <= state.CurrentDate.Date;
            bool friendlyDue = _pendingFriendly is not null;
            HasDueMatch = leagueDue || cupDue || friendlyDue;

            try
            {
                await SendPreMatchAnalysisIfDueAsync(team, state, leagueDue, cupDue, friendlyDue);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to send pre-match analysis.", ex);
            }

            // Check only AFTER the analysis message may have been sent, otherwise the counter
            // won't show a message sent during this run until the next call.
            try
            {
                UnreadMessageCount = await _messages.GetUnreadCountAsync();
                HasUnreadMessages = UnreadMessageCount > 0;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to determine unread messages.", ex);
            }

            await HandleActiveSponsors();
        }

        // "Before every match" - bundled across all three competition types since league/cup/friendly
        // are already merged to the earliest due date here (like OpenMatchDay). Only the earliest
        // due match gets an analysis, guarded by an AnalysisSent flag on Fixture/CupTie so a repeat
        // main-menu visit doesn't resend it.
        private async Task SendPreMatchAnalysisIfDueAsync(Team team, GameState state, bool leagueDue, bool cupDue, bool friendlyDue)
        {
            var candidates = new List<(DateTime Date, string Kind)>();
            if (leagueDue && _nextLeagueFixture is { AnalysisSent: false })
                candidates.Add((_nextLeagueFixture.Date, "league"));
            if (cupDue && _pendingCupTie is { AnalysisSent: false })
                candidates.Add((_pendingCupTie.Date, "cup"));
            if (friendlyDue && _pendingFriendly is { AnalysisSent: false })
                candidates.Add((_pendingFriendly.Date, "friendly"));

            var next = candidates.OrderBy(c => c.Date).FirstOrDefault();
            if (next.Kind is null)
                return;

            int opponentId;
            List<StandingRow> standings = [];
            string matchLabel;

            if (next.Kind == "league" && _nextLeagueFixture is not null)
            {
                var fixture = _nextLeagueFixture;
                opponentId = fixture.HomeTeamId == team.Id ? fixture.AwayTeamId : fixture.HomeTeamId;
                var leagueFixtures = (await _saveGame.GetFixturesAsync(state.Season))
                    .Where(f => f.LeagueTier == team.LeagueTier).ToList();
                var names = _session.Teams.ToDictionary(t => t.Id, t => t.Name);
                standings = StandingsCalculator.Calculate(leagueFixtures, names);
                matchLabel = $"Spieltag {fixture.Matchday}";

                var opponent = _session.Teams.FirstOrDefault(t => t.Id == opponentId);
                if (opponent is null) return;
                var pool = _session.Teams.Where(t => t.LeagueTier == opponent.LeagueTier).ToList();
                await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                    _messages, team, opponent, standings, pool, state.CurrentDate, matchLabel);
                fixture.AnalysisSent = true;
                await _saveGame.SaveFixtureAsync(fixture);
            }
            else if (next.Kind == "cup" && _pendingCupTie is not null)
            {
                var tie = _pendingCupTie;
                opponentId = tie.HomeTeamId == team.Id ? tie.AwayTeamId : tie.HomeTeamId;
                var opponent = _session.Teams.FirstOrDefault(t => t.Id == opponentId);
                if (opponent is null) return;
                var pool = _session.Teams.Where(t => t.LeagueTier == opponent.LeagueTier).ToList();
                matchLabel = $"{CupMatchDayViewModel.CompetitionLabel(_pendingCompetition)} · {CupMatchDayViewModel.RoundDisplayName(tie.Round, tie.Group, tie.LegNumber)}";
                await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                    _messages, team, opponent, standings, pool, state.CurrentDate, matchLabel);
                tie.AnalysisSent = true;
                await _saveGame.SaveCupTieAsync(tie);
            }
            else if (next.Kind == "friendly" && _pendingFriendly is not null)
            {
                var fixture = _pendingFriendly;
                opponentId = fixture.HomeTeamId == team.Id ? fixture.AwayTeamId : fixture.HomeTeamId;
                var opponent = _session.Teams.FirstOrDefault(t => t.Id == opponentId);
                if (opponent is null) return;
                var pool = _session.Teams.Where(t => t.LeagueTier == opponent.LeagueTier).ToList();
                matchLabel = "Freundschaftsspiel";
                await PreMatchAnalysisService.SendIfAnalystEmployedAsync(
                    _messages, team, opponent, standings, pool, state.CurrentDate, matchLabel);
                fixture.AnalysisSent = true;
                await _saveGame.SaveFixtureAsync(fixture);
            }
        }

        [RelayCommand]
        private Task OpenMatchDay()
        {
            var state = _session.State;
            var currentDate = state?.CurrentDate ?? DateTime.MaxValue;

            var candidates = new List<(DateTime Date, string Route)>();
            if (_nextLeagueFixtureDate is not null && _nextLeagueFixtureDate.Value.Date <= currentDate.Date)
                candidates.Add((_nextLeagueFixtureDate.Value, "matchday"));
            if (_pendingCupTie is not null && _pendingCupTie.Date.Date <= currentDate.Date)
                candidates.Add((_pendingCupTie.Date, $"cupmatchday?competition={_pendingCompetition}"));
            if (_pendingFriendly is not null)
                candidates.Add((_pendingFriendly.Date, $"friendlymatchday?fixtureId={_pendingFriendly.Id}"));

            var next = candidates.OrderBy(c => c.Date).FirstOrDefault();
            return next.Route is null ? Task.CompletedTask : _navigation.GoToAsync(next.Route);
        }

        [RelayCommand]
        private Task OpenLineup() => _navigation.GoToAsync("lineup");

        [RelayCommand]
        private Task OpenTraining() => _navigation.GoToAsync("training");

        [RelayCommand]
        private Task OpenTeamTraining() => _navigation.GoToAsync("teamtraining");

        [RelayCommand]
        private Task OpenYouth() => _navigation.GoToAsync("youth");

        [RelayCommand]
        private Task OpenFixtures() => _navigation.GoToAsync("fixtures");

        [RelayCommand]
        private Task OpenStatistics() => _navigation.GoToAsync("statistics");

        [RelayCommand]
        private Task OpenCupOverview() => _navigation.GoToAsync("cupoverview");

        [RelayCommand]
        private Task OpenTrophyCase() => _navigation.GoToAsync("trophies");

        [RelayCommand]
        private Task OpenScouting() => _navigation.GoToAsync("scouting");

        [RelayCommand]
        private Task OpenClub() => _navigation.GoToAsync("club");

        [RelayCommand]
        private Task OpenStadium() => _navigation.GoToAsync("stadium");

        [RelayCommand]
        private Task OpenSponsors() => _navigation.GoToAsync("sponsors");

        [RelayCommand]
        private Task OpenStaff() => _navigation.GoToAsync("staff");

        [RelayCommand]
        private Task OpenTransferMarket() => _navigation.GoToAsync("transfermarket");

        [RelayCommand]
        private Task OpenOptions() => _navigation.GoToAsync("options");

        [RelayCommand]
        private Task OpenFinances() => _navigation.GoToAsync("finances");

        [RelayCommand]
        private Task OpenInbox() => _navigation.GoToAsync("inbox");

        private const int MaxAdvanceDays = 60;

        private bool _cancelAdvanceRequested;

        [ObservableProperty]
        private bool _isAdvancingTime;

        [RelayCommand]
        private void CancelAdvanceTime()
        {
            if (!IsAdvancingTime) return;
            _cancelAdvanceRequested = true;
            BusyText = "Wird abgebrochen, einen Moment....";
        }

        [RelayCommand]
        private async Task AdvanceTime()
        {
            if (_session.State is null || IsBusy) return;
            IsBusy = true;
            IsAdvancingTime = true;
            _cancelAdvanceRequested = false;
            StatusText = string.Empty;
            try
            {
                int unreadBefore = await _messages.GetUnreadCountAsync();
                var ceiling = _session.State.CurrentDate.AddDays(MaxAdvanceDays);

                for (int day = 1; day <= MaxAdvanceDays; day++)
                {
                    if (!_cancelAdvanceRequested)
                    {
                        var nextDate = _session.State.CurrentDate.AddDays(1);
                        // The weekly AI tick (see CalendarAdvanceService.RunWeeklyTickAsync) makes
                        // this day noticeably slower to load - hint so it doesn't look like a hang.
                        bool isWeeklyTickDay = (nextDate.Date - _session.State.SeasonStart.Date).Days % 7 == 0;
                        BusyText = isWeeklyTickDay
                            ? $"Tag {day}: {nextDate:dd.MM.yyyy} - COM-Teams agieren gerade sehr stark, es kann nun etwas länger dauern ..."
                            : $"Tag {day}: {nextDate:dd.MM.yyyy}";
                    }
                    await _calendarAdvance.AdvanceOneDayAsync(_session.State, _session.Teams);
                    await InitializeAsync();

                    if (_session.State.IsGameOver)
                        break;
                    if (_cancelAdvanceRequested)
                    {
                        StatusText = "Zeit vorstellen abgebrochen.";
                        break;
                    }
                    if (HasDueMatch)
                        break;
                    if (UnreadMessageCount > unreadBefore)
                        break;
                    if (_windowEnd is not null && _session.State.CurrentDate.Date >= _windowEnd.Value.Date)
                        break;
                    if (_session.State.CurrentDate >= ceiling)
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to advance time.", ex);
                StatusText = "Zeit vorstellen abgebrochen - siehe Log für Details.";
            }
            finally
            {
                IsBusy = false;
                IsAdvancingTime = false;
                _cancelAdvanceRequested = false;
            }
        }

        [RelayCommand]
        private async Task OpenTrainingCampDialog()
        {
            SelectedTrainingCampTier = TrainingCampTier.Basic;
            SelectedTrainingCampDuration = 1;
            await RefreshTrainingCampPreviewAsync();
            IsTrainingCampDialogOpen = true;
        }

        [RelayCommand]
        private void CloseTrainingCampDialog() => IsTrainingCampDialogOpen = false;

        [RelayCommand]
        private async Task SelectTrainingCampTier(string tier)
        {
            if (!Enum.TryParse<TrainingCampTier>(tier, out var parsed))
                return;
            SelectedTrainingCampTier = parsed;
            await RefreshTrainingCampPreviewAsync();
        }

        [RelayCommand]
        private async Task SelectTrainingCampDuration(string weeks)
        {
            if (!int.TryParse(weeks, out int parsed))
                return;
            SelectedTrainingCampDuration = parsed;
            await RefreshTrainingCampPreviewAsync();
        }

        private async Task RefreshTrainingCampPreviewAsync()
        {
            var option = TrainingCampCatalog.Get(SelectedTrainingCampTier, SelectedTrainingCampDuration);
            TrainingCampPreviewText = option.GrantsAttributeBoost
                ? $"{option.Cost:N0} € · {SelectedTrainingCampDuration} Woche(n) · Moral +{option.MoraleBoost} · zusätzlich +1/+2 auf Kern-Attribute"
                : $"{option.Cost:N0} € · {SelectedTrainingCampDuration} Woche(n) · Moral +{option.MoraleBoost}";

            var state = _session.State;
            if (state is null)
            {
                CanConfirmTrainingCamp = false;
                return;
            }

            var (allowed, reason) = await _trainingCamps.CanBookAsync(
                state.ManagerTeamId, SelectedTrainingCampDuration, state.CurrentDate, _windowEnd);
            CanConfirmTrainingCamp = allowed;
            TrainingCampValidationText = reason ?? string.Empty;
        }

        private async Task HandleActiveSponsors()
        {
            try
            {
                var team = _session?.ManagerTeam;
                if (team is null)
                    return;

                var sponsors = await _sponsorService.GetActiveSponsorshipsAsync(team.Id);
                HasSponsor = sponsors.Count != 0;
                MainSponsor = sponsors.Where(sp => sp.Sponsor.SponsorType == SponsorType.Main).FirstOrDefault().Sponsor;
                PerimeterSponsor = sponsors.Where(sp => sp.Sponsor.SponsorType == SponsorType.Perimeter).FirstOrDefault().Sponsor;
                KitSponsor = sponsors.Where(sp => sp.Sponsor.SponsorType == SponsorType.Kit).FirstOrDefault().Sponsor;
            }
            catch (Exception ex)
            {
                Log.Error("Error on Sponsor Changed handling: ", ex);
            }
        }

        [RelayCommand]
        private async Task ConfirmTrainingCamp()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null)
                return;

            var (allowed, reason) = await _trainingCamps.CanBookAsync(
                team.Id, SelectedTrainingCampDuration, state.CurrentDate, _windowEnd);
            if (!allowed)
            {
                TrainingCampValidationText = reason ?? string.Empty;
                return;
            }

            IsBusy = true;
            BusyText = "Trainingslager wird gebucht …";
            try
            {
                await _trainingCamps.BookAsync(team, SelectedTrainingCampTier, SelectedTrainingCampDuration, state.CurrentDate);
                await _saveGame.SaveTeamProgressAsync(state, team);
                StatusMessage = "Trainingslager gebucht.";
                IsTrainingCampDialogOpen = false;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to book training camp.", ex);
                StatusMessage = "Trainingslager konnte nicht gebucht werden.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task OpenFriendlyPicker()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null)
                return;

            FriendlyOpponents.Clear();
            foreach (var opponent in _session.Teams.Where(t => t.Id != team.Id).OrderBy(t => t.Name))
                FriendlyOpponents.Add(new FriendlyOpponentOption(opponent.Id, opponent.Name, opponent.AverageRating));
            SelectedFriendlyOpponent = FriendlyOpponents.FirstOrDefault();

            SuggestedFriendlyDates.Clear();
            FriendlyValidationText = string.Empty;
            if (_windowEnd is not null)
            {
                var suggestions = await _friendly.GetSuggestedDatesAsync(team.Id, state.CurrentDate, _windowEnd.Value);
                foreach (var date in suggestions)
                    SuggestedFriendlyDates.Add(date);
                SelectedFriendlyDate = SuggestedFriendlyDates.FirstOrDefault();
                if (SuggestedFriendlyDates.Count == 0)
                    FriendlyValidationText = "Kein freier Termin in der aktuellen Vorbereitung/Winterpause gefunden.";
            }
            else
            {
                FriendlyValidationText = "Freundschaftsspiele sind nur in Vorbereitung oder Winterpause möglich.";
            }

            IsFriendlyPickerOpen = true;
        }

        [RelayCommand]
        private void CloseFriendlyPicker() => IsFriendlyPickerOpen = false;

        [RelayCommand]
        private async Task ConfirmFriendly()
        {
            var team = _session.ManagerTeam;
            var state = _session.State;
            if (team is null || state is null || SelectedFriendlyOpponent is null || SelectedFriendlyDate is null)
                return;

            var opponent = _session.Teams.FirstOrDefault(t => t.Id == SelectedFriendlyOpponent.TeamId);
            if (opponent is null)
                return;

            var (allowed, reason) = await _friendly.CanScheduleAsync(team.Id, SelectedFriendlyDate.Value, _windowEnd);
            if (!allowed)
            {
                FriendlyValidationText = reason ?? string.Empty;
                return;
            }

            IsBusy = true;
            BusyText = "Freundschaftsspiel wird angesetzt …";
            try
            {
                await _friendly.ScheduleAsync(state.Season, team, opponent, SelectedFriendlyDate.Value);
                StatusMessage = $"Freundschaftsspiel gegen {opponent.Name} am {SelectedFriendlyDate.Value:dd.MM.yyyy} angesetzt.";
                IsFriendlyPickerOpen = false;
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to schedule friendly match.", ex);
                StatusMessage = "Freundschaftsspiel konnte nicht angesetzt werden.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SaveGame()
        {
            if (_session.State is null) return;
            IsBusy = true;
            try
            {
                BusyText = "Speichere Spielstand......";
                await _saveGame.SaveProgressAsync(_session.State, _session.Teams);
                StatusMessage = "Spielstand gespeichert.";
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save game.", ex);
                StatusMessage = "Speichern fehlgeschlagen.";
            }
            finally 
            { 
                IsBusy = false;
                BusyText = "";
            }
        }

        [RelayCommand]
        private Task BackToStart() => _navigation.GoToRootAsync("start");
    }

    public record FriendlyOpponentOption(int TeamId, string Name, double Rating)
    {
        public string DisplayText => $"{Name} (Ø {Rating:0.0})";
    }
}
