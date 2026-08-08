using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public enum MatchdayUiPhase { PreMatch, Live, HalfTime, FullTime, Results, Table, SeasonEnd }

    public partial class MatchDayViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<MatchDayViewModel>();

        // Speed levels: index -> delay per simulated minute (ms).
        private static readonly int[] SpeedDelaysMs = [1300, 850, 500, 180, 80];
        private static readonly string[] SpeedLabels = ["Sehr langsam", "Langsam", "Normal", "Schnell", "Ultra"];

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly MatchDayService _matchDayService;
        private readonly MessageService _messages;
        private readonly CareerService _career;
        private readonly INavigationService _navigation;

        private Match? _match;
        private Fixture? _humanFixture;
        private Team? _homeTeam;
        private Team? _awayTeam;
        private bool _isHumanHome;
        private int _matchday;
        private int _matchdayCount;
        private MatchResult? _humanResult;
        private MatchdaySummary? _summary;
        private List<Fixture> _seasonFixtures = [];
        private Dictionary<int, string> _teamNames = new();

        private bool _isPaused;
        private bool _halfTimeShown;
        private int _tickerCursor;
        private TaskCompletionSource? _resumeSignal;

        // --- Team management staging (pause -> edit -> confirm/cancel -> resume) ---
        private Team? _managementTeam;
        private Formation _managementFormation = FormationCatalog.Default;
        private int?[] _managementLineup = new int?[11];
        private HashSet<int> _originalOnPitchIds = [];
        private int? _managementSelectedId;
        private int? _managementDraggedId;

        public MatchDayViewModel(
            IDispatcher dispatcher,
            GameSession session,
            SaveGameService saveGame,
            MatchDayService matchDayService,
            MessageService messages,
            CareerService career,
            INavigationService navigation,
            AppSettingsService appSettings)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _matchDayService = matchDayService;
            _messages = messages;
            _career = career;
            _navigation = navigation;
            Title = "Spieltag";
            SpeedLevel = appSettings.DefaultMatchSpeed;
        }

        // Phase flags for view visibility.
        [ObservableProperty] private bool _isPreMatch;
        [ObservableProperty] private bool _isLive;
        [ObservableProperty] private bool _isHalfTime;
        [ObservableProperty] private bool _isFullTime;
        [ObservableProperty] private bool _isResults;
        [ObservableProperty] private bool _isTable;
        [ObservableProperty] private bool _isSeasonEnd;

        [ObservableProperty] private bool _isTrophyDialogOpen;
        [ObservableProperty] private string _trophyImageSource = string.Empty;
        [ObservableProperty] private string _trophyTitleText = string.Empty;

        [ObservableProperty] private string _headerText = string.Empty;
        [ObservableProperty] private string _homeTeamShortName = string.Empty;
        [ObservableProperty] private string _awayTeamShortName = string.Empty;
        [ObservableProperty] private string _homeTeamFullName = string.Empty;
        [ObservableProperty] private string _awayTeamFullName = string.Empty;
        [ObservableProperty] private string? _homeTeamLogoPath;
        [ObservableProperty] private string? _awayTeamLogoPath;
        [ObservableProperty] private string _scoreText = string.Empty;
        [ObservableProperty] private string _minuteText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _subsRemainingText = string.Empty;
        [ObservableProperty] private string _pauseResumeLabel = "Pause";
        [ObservableProperty] private double _speedLevel = 2;
        [ObservableProperty] private string _speedLabel = "Normal";
        [ObservableProperty] private string _seasonEndText = string.Empty;
        [ObservableProperty] private string _busyText = string.Empty;

        // Scouting report (M6a), shown on the pre-match panel.
        [ObservableProperty] private string _opponentTableText = string.Empty;
        [ObservableProperty] private string _opponentFormText = string.Empty;
        [ObservableProperty] private bool _hasAnalyst;
        [ObservableProperty] private string _weaknessText = string.Empty;
        [ObservableProperty] private string _strengthText = string.Empty;
        [ObservableProperty] private string _tacticSuggestionText = string.Empty;

        [ObservableProperty] private LeagueChoice? _selectedResultsLeague;
        [ObservableProperty] private LeagueChoice? _selectedTableLeague;

        // Team management overlay state.
        [ObservableProperty] private bool _isTeamManagementOpen;
        [ObservableProperty] private bool _isHalfTimeManagement;
        [ObservableProperty] private bool _isRedCardManagement;
        [ObservableProperty] private Formation? _selectedManagementFormation;
        [ObservableProperty] private PlayingStyleOption? _selectedManagementStyle;
        [ObservableProperty] private OrientationOption? _selectedManagementOrientation;
        [ObservableProperty] private TacklingOption? _selectedManagementTackling;
        [ObservableProperty] private string _managementSubsInfo = string.Empty;
        [ObservableProperty] private string _managementStatusText = string.Empty;

        // Player profile dialog (info button on pitch/bench tokens during the match).
        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;

        public ObservableCollection<TickerEntry> Ticker { get; } = [];
        // Persistent (non-scrolling) summaries shown next to the scoreboard, unlike the
        // ticker which scrolls entries out of view.
        public ObservableCollection<MatchCardEntry> Cards { get; } = [];
        public ObservableCollection<MatchScorerEntry> Scorers { get; } = [];
        public ObservableCollection<PlayingStyleOption> PlayingStyles { get; } = [];
        public ObservableCollection<OrientationOption> Orientations { get; } = [];
        public ObservableCollection<TacklingOption> TacklingOptions { get; } = [];
        public ObservableCollection<Formation> ManagementFormations { get; } = [];
        public ObservableCollection<PitchToken> ManagementPitch { get; } = [];
        public ObservableCollection<SquadToken> ManagementBench { get; } = [];
        public ObservableCollection<LeagueChoice> ResultLeagues { get; } = [];
        public ObservableCollection<MatchResultRow> ResultGames { get; } = [];
        public ObservableCollection<StandingDisplayRow> TableRows { get; } = [];
        public ObservableCollection<string> StrengthBreakdown { get; } = [];
        public ObservableCollection<LegendItem> TableLegend { get; } = [];

        partial void OnSpeedLevelChanged(double value) =>
            SpeedLabel = SpeedLabels[Math.Clamp((int)Math.Round(value), 0, SpeedLabels.Length - 1)];

        partial void OnSelectedResultsLeagueChanged(LeagueChoice? value) => RefreshResultGames();
        partial void OnSelectedTableLeagueChanged(LeagueChoice? value) => RefreshTable();

        partial void OnSelectedManagementFormationChanged(Formation? value)
        {
            if (value is null || _managementTeam is null)
                return;
            _managementFormation = value;
            RemapManagementLineupToFormation();
        }

        public async Task InitializeAsync()
        {
            var state = _session.State;
            var manager = _session.ManagerTeam;
            if (state is null || manager is null)
            {
                StatusText = "Kein aktives Spiel.";
                return;
            }

            try
            {
                _seasonFixtures = await _saveGame.GetFixturesAsync(state.Season);
                _matchdayCount = _seasonFixtures.Count == 0 ? 0 : _seasonFixtures.Max(f => f.Matchday);
                _teamNames = _session.Teams.ToDictionary(t => t.Id, t => t.Name);

                var next = _seasonFixtures
                    .Where(f => !f.Played &&
                                (f.HomeTeamId == manager.Id || f.AwayTeamId == manager.Id))
                    .OrderBy(f => f.Matchday)
                    .FirstOrDefault();

                if (next is null)
                {
                    StatusText = "Keine weiteren Spiele in dieser Saison.";
                    SetPhase(MatchdayUiPhase.PreMatch);
                    return;
                }

                _humanFixture = next;
                _matchday = next.Matchday;
                _isHumanHome = next.HomeTeamId == manager.Id;
                _homeTeam = _session.Teams.First(t => t.Id == next.HomeTeamId);
                _awayTeam = _session.Teams.First(t => t.Id == next.AwayTeamId);

                // Keep the manager's chosen XI (recover only); auto-prepare the opponent.
                var humanTeam = _isHumanHome ? _homeTeam : _awayTeam;
                var aiTeam = _isHumanHome ? _awayTeam : _homeTeam;
                await MatchDayService.NotifyInjuryRecoveriesAsync(_messages, humanTeam, state.CurrentDate);
                MatchDayService.RecoverForMatch(humanTeam, state.CurrentDate);
                MatchDayService.PrepareForMatch(aiTeam, state.CurrentDate);

                // Self-heal: guard against ever kicking off with an incomplete human XI.
                var formation = FormationCatalog.GetByName(humanTeam.FormationName);
                if (humanTeam.Players.Count(p => p.Status == PlayerStatus.InStartingXI) < formation.Slots.Count)
                    LineupSelector.FillMissingStarters(humanTeam, formation);

                BuildTactics();
                BuildScoutingReport(humanTeam, aiTeam);

                HeaderText = $"Spieltag {_matchday} · {_homeTeam.Name} – {_awayTeam.Name}";
                HomeTeamShortName = _homeTeam.ShortName;
                AwayTeamShortName = _awayTeam.ShortName;
                HomeTeamFullName = _homeTeam.Name;
                AwayTeamFullName = _awayTeam.Name;
                HomeTeamLogoPath = _homeTeam.LogoPath;
                AwayTeamLogoPath = _awayTeam.LogoPath;
                ScoreText = $"0 : 0";
                MinuteText = "0'";
                StatusText = $"{_homeTeam.Name} gegen {_awayTeam.Name}";
                SetPhase(MatchdayUiPhase.PreMatch);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to prepare matchday.", ex);
                StatusText = "Fehler beim Laden des Spieltags.";
            }
        }

        private void BuildTactics()
        {
            PlayingStyles.Clear();
            foreach (var s in Enum.GetValues<PlayingStyle>())
                PlayingStyles.Add(new PlayingStyleOption(s, PlayingStyleOption.LabelFor(s)));

            Orientations.Clear();
            foreach (var o in Enum.GetValues<TacticalOrientation>())
                Orientations.Add(new OrientationOption(o, OrientationOption.LabelFor(o)));

            TacklingOptions.Clear();
            foreach (var ti in Enum.GetValues<TacklingIntensity>())
                TacklingOptions.Add(new TacklingOption(ti, TacklingOption.LabelFor(ti)));

            ManagementFormations.Clear();
            foreach (var f in FormationCatalog.All)
                ManagementFormations.Add(f);
        }

        private void BuildScoutingReport(Team ownTeam, Team opponent)
        {
            int tier = opponent.LeagueTier;
            var leagueFixtures = _seasonFixtures.Where(f => f.LeagueTier == tier).ToList();
            var standings = StandingsCalculator.Calculate(leagueFixtures, _teamNames);
            var leagueTeams = _session.Teams.Where(t => t.LeagueTier == tier).ToList();

            int? analysisAbility = ownTeam.Employees
                .Where(e => e.EmployeeType == EmployeeType.Analyst)
                .Select(e => (int?)e.AnalysisAbility)
                .Max();

            var report = ScoutingReportService.BuildReport(ownTeam, opponent, standings, leagueTeams, analysisAbility);

            OpponentTableText = $"Tabellenplatz {report.OpponentPosition} · Ø-Rating {report.OpponentAverageRating:0.0}";
            OpponentFormText = string.IsNullOrEmpty(report.OpponentForm) ? "Keine Form-Daten" : $"Form: {report.OpponentForm}";
            HasAnalyst = analysisAbility is not null;

            StrengthBreakdown.Clear();
            WeaknessText = string.Empty;
            StrengthText = string.Empty;
            TacticSuggestionText = string.Empty;

            if (report.OpponentProfile is not { } profile || report.LeagueAverageProfile is not { } avg)
                return;

            StrengthBreakdown.Add($"Angriff: {profile.Attack:0} (Liga-Ø {avg.Attack:0})");
            StrengthBreakdown.Add($"Abwehr: {profile.Defense:0} (Liga-Ø {avg.Defense:0})");
            StrengthBreakdown.Add($"Mittelfeld: {profile.Midfield:0} (Liga-Ø {avg.Midfield:0})");
            StrengthBreakdown.Add($"Pressing: {profile.Pressing:0} (Liga-Ø {avg.Pressing:0})");

            if (report.WeaknessCategory is not null)
                WeaknessText = $"Schwäche: {report.WeaknessCategory}";
            if (report.StrengthCategory is not null)
                StrengthText = $"Stärke: {report.StrengthCategory}";

            if (report.TacticSuggestion is { } suggestion)
            {
                TacticSuggestionText =
                    $"Empfehlung: {PlayingStyleOption.LabelFor(suggestion.Style)} / " +
                    $"{OrientationOption.LabelFor(suggestion.Orientation)} - nutzt die Gegner-Schwäche " +
                    $"im Bereich {suggestion.ExploitedCategory}.";
            }
        }

        [RelayCommand]
        private async Task KickOff()
        {
            if (_homeTeam is null || _awayTeam is null)
                return;

            _match = new Match(_homeTeam, _awayTeam, new Random())
            {
                // Only the opponent is AI-controlled; the player manages their own side live.
                HomeCoach = _isHumanHome ? null : new AiMatchCoach(),
                AwayCoach = _isHumanHome ? new AiMatchCoach() : null,
            };
            _match.Begin();
            _halfTimeShown = false;
            _isPaused = false;
            _tickerCursor = 0;
            Ticker.Clear();
            SetPhase(MatchdayUiPhase.Live);
            UpdateSubsRemaining();

            await RunLoopAsync();
        }

        private async Task RunLoopAsync()
        {
            if (_match is null) return;

            try
            {
                while (!_match.IsFinished)
                {
                    await WaitWhilePausedAsync();

                    _match.AdvanceMinute();
                    var (sawRedCard, sawInjury) = UpdateLiveState();

                    if (_match.Phase == MatchPhase.HalfTime && !_halfTimeShown)
                    {
                        _halfTimeShown = true;
                        _isPaused = true;
                        SetPhase(MatchdayUiPhase.HalfTime);
                        // Give the player the same tactics/lineup/subs panel at halftime,
                        // confirming or cancelling both continue into the second half.
                        OpenTeamManagement(isHalfTime: true, isRedCard: false);
                        continue;
                    }

                    if (_match.IsFinished)
                        break;

                    // A red card changes a team's available XI mid-minute - pause so the
                    // manager gets a real chance to react (fill the now-empty slot or leave
                    // it, adjust tactics) instead of the sim ploughing ahead a player short.
                    if (sawRedCard || sawInjury)
                    {
                        _isPaused = true;
                        PauseResumeLabel = "Fortsetzen";
                        OpenTeamManagement(isHalfTime: false, isRedCard: sawRedCard, isInjury: sawInjury && !sawRedCard);
                        continue;
                    }

                    int delay = SpeedDelaysMs[Math.Clamp((int)Math.Round(SpeedLevel), 0, SpeedDelaysMs.Length - 1)];
                    await Task.Delay(delay);
                }

                await OnFullTimeAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Error during live simulation.", ex);
                StatusText = "Fehler während des Spiels.";
            }
        }

        private async Task WaitWhilePausedAsync()
        {
            while (_isPaused)
            {
                _resumeSignal = new TaskCompletionSource();
                await _resumeSignal.Task;
            }
        }

        // Returns whether a red card and/or an injury was seen in this batch of events, so the
        // caller can pause the match and let the manager react.
        private (bool SawRedCard, bool SawInjury) UpdateLiveState()
        {
            if (_match is null || _homeTeam is null || _awayTeam is null)
                return (false, false);

            ScoreText = $"{_match.HomeGoals} : {_match.AwayGoals}";
            MinuteText = $"{_match.CurrentMinute}'";

            bool sawRedCard = false;
            bool sawInjury = false;
            var events = _match.Result.Events;
            for (; _tickerCursor < events.Count; _tickerCursor++)
            {
                var e = events[_tickerCursor];

                switch (e.Type)
                {
                    case GameEventType.YellowCard when e.Player is not null:
                        Cards.Add(new MatchCardEntry(e.Player.Name, e.IsHomeTeam, IsRed: false));
                        break;
                    case GameEventType.RedCard when e.Player is not null:
                        Cards.Add(new MatchCardEntry(e.Player.Name, e.IsHomeTeam, IsRed: true));
                        // Only pause for the human team's own red card - the opponent's card
                        // shouldn't interrupt play.
                        if (e.IsHomeTeam == _isHumanHome)
                            sawRedCard = true;
                        break;
                    case GameEventType.Goal when e.Player is not null:
                        AddOrUpdateScorer(e.Player.Name, e.IsHomeTeam, e.Minute);
                        break;
                    case GameEventType.Injury when e.Player is not null:
                        // Only pause for the human team's own injury - same reasoning as above.
                        if (e.IsHomeTeam == _isHumanHome)
                            sawInjury = true;
                        break;
                }

                if (e.Type is GameEventType.Shot or GameEventType.DangerousAttack)
                    continue; // keep the ticker focused on notable moments

                bool isTeamEvent = e.Type is not (GameEventType.KickOff or GameEventType.HalfTime or GameEventType.FullTime);
                Ticker.Insert(0, new TickerEntry(e.Minute, IconFor(e.Type), e.Player?.Name, e.Description, e.IsHomeTeam, isTeamEvent));
            }

            UpdateSubsRemaining();
            return (sawRedCard, sawInjury);
        }

        private void AddOrUpdateScorer(string playerName, bool isHomeTeam, int minute)
        {
            var existing = Scorers.FirstOrDefault(s => s.PlayerName == playerName && s.IsHomeTeam == isHomeTeam);
            if (existing is not null)
            {
                existing.Minutes.Add(minute);
                var index = Scorers.IndexOf(existing);
                Scorers[index] = existing with { Minutes = existing.Minutes }; // re-trigger binding refresh
            }
            else
            {
                Scorers.Add(new MatchScorerEntry(playerName, isHomeTeam, [minute]));
            }
        }

        private static string IconFor(GameEventType type) => type switch
        {
            GameEventType.Goal => "ball.png",
            GameEventType.YellowCard => "yellowcard.png",
            GameEventType.RedCard => "redcard.png",
            GameEventType.Substitution => "substitution.png",
            _ => "",
        };

        // --- Simple pause (no editing) ---

        [RelayCommand]
        private void PauseResume()
        {
            if (_match is null || _match.IsFinished || IsTeamManagementOpen)
                return;

            _isPaused = !_isPaused;
            PauseResumeLabel = _isPaused ? "Fortsetzen" : "Pause";
            if (!_isPaused)
                _resumeSignal?.TrySetResult();
        }

        private void UpdateSubsRemaining()
        {
            if (_match is null) return;
            SubsRemainingText = $"Wechsel: {_match.SubsUsed(_isHumanHome)}/{Match.MaxSubstitutions} genutzt";
        }

        // --- Team management: pause, stage changes, confirm (apply) or cancel (discard) ---

        [RelayCommand]
        private void OpenTeamManagementFromButton() => OpenTeamManagement(isHalfTime: false, isRedCard: false);

        private void OpenTeamManagement(bool isHalfTime, bool isRedCard, bool isInjury = false)
        {
            if (_match is null || _match.IsFinished)
                return;

            _managementTeam = _isHumanHome ? _homeTeam! : _awayTeam!;
            IsHalfTimeManagement = isHalfTime;
            IsRedCardManagement = isRedCard;
            ManagementStatusText = isRedCard
                ? "Rote Karte! Der Spieler ist für dieses Spiel gesperrt - reagiere mit einem Wechsel oder spiele in Unterzahl weiter."
                : isInjury
                    ? "Ein Spieler wurde verletzt! Reagiere mit einem Wechsel oder spiele weiter."
                    : string.Empty;

            if (!_isPaused)
            {
                _isPaused = true;
                PauseResumeLabel = "Fortsetzen";
            }

            StageCurrentLineup();
            IsTeamManagementOpen = true;
        }

        private void StageCurrentLineup()
        {
            if (_match is null || _managementTeam is null)
                return;

            _managementFormation = FormationCatalog.GetByName(_managementTeam.FormationName);

            var onPitch = _match.OnPitch(_isHumanHome).ToList();
            _originalOnPitchIds = onPitch.Select(p => p.Id).ToHashSet();

            // Include players sent off THIS match in the position-matching pool (by identity)
            // so a red card doesn't reshuffle everyone else's slot - a red-carded player is no
            // longer PlayerStatus.InStartingXI (excluded from OnPitch above), so without this
            // the fallback "any unused player" match below would backfill his slot with the
            // wrong player, cascading a mismatch through every slot processed afterwards.
            var matchingPool = onPitch
                .Concat(_managementTeam.Players.Where(p => p.Status is PlayerStatus.Suspended or PlayerStatus.Injured))
                .ToList();

            _managementLineup = new int?[_managementFormation.Slots.Count];
            // Explicit AssignedPosition overrides (incl. plain repositioning) are matched to their
            // slot BEFORE any natural-fit matching - see LineupSelector.MatchStartersToSlots. The
            // WB toggle itself lives on Player.UsedAsWingBack directly - nothing to restore here.
            var matched = LineupSelector.MatchStartersToSlots(matchingPool, _managementFormation);
            foreach (var (slotIndex, playerId) in matched)
            {
                var match = matchingPool.First(p => p.Id == playerId);
                if (match.Status is PlayerStatus.Suspended or PlayerStatus.Injured)
                    continue; // sent off/injured - leave this slot empty until a substitute is dragged in

                _managementLineup[slotIndex] = match.Id;
            }

            _managementSelectedId = null;
            _managementDraggedId = null;

            SelectedManagementFormation = ManagementFormations.FirstOrDefault(f => f.Name == _managementFormation.Name);
            SelectedManagementStyle = PlayingStyles.FirstOrDefault(s => s.Style == _managementTeam.PlayingStyle);
            SelectedManagementOrientation = Orientations.FirstOrDefault(o => o.Orientation == _managementTeam.TacticalOrientation);
            SelectedManagementTackling = TacklingOptions.FirstOrDefault(t => t.Value == _managementTeam.TacklingIntensity);

            RebuildManagementViews();
        }

        [RelayCommand]
        private async Task ShowProfile(int playerId)
        {
            var player = _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            var contract = _session.State is null
                ? null
                : await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
            var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
            var seasonStats = _session.State is null
                ? null
                : await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
            var careerStats = await _saveGame.GetPlayerCareerStatsAsync(player.Id);
            var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(player.Id);
            SelectedProfile = PlayerProfile.From(player, contract, listing, seasonStats, careerStats, competitionStats);
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        [RelayCommand]
        private void ManagementSelectPlayer(int playerId)
        {
            if (IsSuspendedThisMatch(playerId))
            {
                ManagementStatusText = "Für dieses Spiel gesperrt.";
                return;
            }
            if (IsInjuredThisMatch(playerId))
            {
                ManagementStatusText = "Verletzte Spieler können nicht aufgestellt werden.";
                return;
            }

            if (_managementSelectedId is null)
                _managementSelectedId = playerId;
            else if (_managementSelectedId == playerId)
                _managementSelectedId = null;
            else
            {
                ManagementSwap(_managementSelectedId.Value, playerId);
                _managementSelectedId = null;
            }
            RebuildManagementViews();
        }

        // A player sent off THIS match must not be draggable/selectable - their slot stays
        // empty until the manager brings on a substitute (or leaves it open).
        private bool IsSuspendedThisMatch(int playerId) =>
            _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId)?.Status == PlayerStatus.Suspended;

        // A player injured THIS match must not be draggable/selectable either - mirrors the
        // red-card guard above.
        private bool IsInjuredThisMatch(int playerId) =>
            _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId)?.Status == PlayerStatus.Injured;

        // Toggles the WingBack role for whoever currently occupies this slot, if the slot offers
        // one. Purely positional - doesn't cost a sub. Persisted directly on the player
        // (Player.UsedAsWingBack), same as LineupViewModel's toggle.
        [RelayCommand]
        private void ToggleManagementSlotRole(int slotIndex)
        {
            if (_managementTeam is null || slotIndex < 0 || slotIndex >= _managementFormation.Slots.Count)
                return;
            var slot = _managementFormation.Slots[slotIndex];
            if (slot.AlternateRole is null)
                return;
            if (_managementLineup[slotIndex] is not int playerId)
                return;

            var player = _managementTeam.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            player.UsedAsWingBack = !player.UsedAsWingBack;
            RebuildManagementViews();
        }

        private Position ManagementEffectiveSlotPosition(int slotIndex)
        {
            var slot = _managementFormation.Slots[slotIndex];
            var player = _managementLineup[slotIndex] is int id
                ? _managementTeam?.Players.FirstOrDefault(p => p.Id == id)
                : null;
            if (player is not null && slot.AlternateRole is Position alt && player.UsedAsWingBack)
                return alt;
            return slot.Position;
        }

        [RelayCommand]
        private void ManagementBeginDrag(int playerId)
        {
            if (IsSuspendedThisMatch(playerId) || IsInjuredThisMatch(playerId))
                return;
            _managementDraggedId = playerId;
        }

        [RelayCommand]
        private void ManagementDropOn(int targetPlayerId)
        {
            if (IsSuspendedThisMatch(targetPlayerId))
            {
                ManagementStatusText = "Für dieses Spiel gesperrt.";
                _managementDraggedId = null;
                return;
            }
            if (IsInjuredThisMatch(targetPlayerId))
            {
                ManagementStatusText = "Verletzte Spieler können nicht aufgestellt werden.";
                _managementDraggedId = null;
                return;
            }

            if (_managementDraggedId is int dragged && dragged != targetPlayerId)
            {
                ManagementSwap(dragged, targetPlayerId);
                _managementSelectedId = null;
                RebuildManagementViews();
            }
            _managementDraggedId = null;
        }

        // Freely repositions players already in the staged squad-for-this-match (original XI
        // + original bench); only bringing a bench-origin player onto the pitch counts against
        // the substitution budget, checked here BEFORE the swap is accepted.
        private void ManagementSwap(int aId, int bId)
        {
            int slotA = ManagementSlotOf(aId);
            int slotB = ManagementSlotOf(bId);
            if (slotA < 0 && slotB < 0)
                return; // both are bench players - nothing on the pitch to change

            var temp = (int?[])_managementLineup.Clone();
            if (slotA >= 0 && slotB >= 0)
                (temp[slotA], temp[slotB]) = (temp[slotB], temp[slotA]);
            else if (slotA >= 0)
                temp[slotA] = bId;
            else
                temp[slotB] = aId;

            // A red card permanently reduces the team below 11 - repositioning can move the
            // resulting gap to a different slot (net filled count unchanged), but no move may
            // bring the total back up to a full XI, same rule for every further red card. An
            // injury does NOT reduce the squad this way - a healthy sub can fill that slot back
            // up to 11, so only suspensions count here.
            int unavailableCount = _managementTeam?.Players.Count(p => p.Status is PlayerStatus.Suspended) ?? 0;
            int maxOnPitch = _managementFormation.Slots.Count - unavailableCount;
            if (temp.Count(id => id.HasValue) > maxOnPitch)
            {
                ManagementStatusText = "Nicht möglich - dein Team spielt nach der roten Karte in Unterzahl.";
                return;
            }

            int newPendingSubs = temp.Where(id => id.HasValue).Count(id => !_originalOnPitchIds.Contains(id!.Value));
            if (_match is not null && newPendingSubs > _match.SubsRemaining(_isHumanHome))
            {
                ManagementStatusText = "Nicht genug Wechsel übrig für diese Änderung.";
                return;
            }

            _managementLineup = temp;
        }

        private int ManagementSlotOf(int playerId)
        {
            if (TryDecodeEmptySlot(playerId, out int emptySlot))
                return emptySlot;
            for (int i = 0; i < _managementLineup.Length; i++)
                if (_managementLineup[i] == playerId)
                    return i;
            return -1;
        }

        // Empty pitch slots have no player to identify them by, but the select/drag/drop
        // commands only take a single int id - encode the slot index into a distinct negative
        // id (real Player.Ids are always >= 1) so each empty slot resolves to exactly one slot,
        // instead of every empty slot sharing a single "-1" that ManagementSlotOf can't map back
        // to a specific index (the bug behind "an emptied slot can't be filled again").
        private static int EncodeEmptySlot(int slotIndex) => -(slotIndex + 2);

        private static bool TryDecodeEmptySlot(int id, out int slotIndex)
        {
            slotIndex = -(id + 2);
            return id <= -2;
        }

        // Remaps the currently staged 11 onto a newly chosen formation's slots by position
        // fit. Pure repositioning of the same players - never changes the substitution count.
        private void RemapManagementLineupToFormation()
        {
            if (_managementTeam is null)
                return;

            var occupantIds = _managementLineup.Where(id => id.HasValue).Select(id => id!.Value).ToList();
            var occupants = occupantIds
                .Select(id => _managementTeam.Players.FirstOrDefault(p => p.Id == id))
                .Where(p => p is not null)
                .Select(p => p!)
                .ToList();

            var newLineup = new int?[_managementFormation.Slots.Count];
            foreach (var (slotIndex, playerId) in LineupSelector.MatchStartersToSlots(occupants, _managementFormation))
                newLineup[slotIndex] = playerId;

            _managementLineup = newLineup;
            RebuildManagementViews();
        }

        private void RebuildManagementViews()
        {
            if (_managementTeam is null || _match is null)
                return;

            var stagedIds = _managementLineup.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();

            ManagementPitch.Clear();
            for (int i = 0; i < _managementFormation.Slots.Count; i++)
            {
                var slot = _managementFormation.Slots[i];
                var effectivePos = ManagementEffectiveSlotPosition(i);
                var player = _managementLineup[i] is int id
                    ? _managementTeam.Players.FirstOrDefault(p => p.Id == id)
                    : null;
                var fit = player is null ? PositionFitLevel.Empty
                    : effectivePos == player.Position ? PositionFitLevel.Favorite
                    : player.SecondaryPositions.Any(sp => sp.Position == effectivePos) ? PositionFitLevel.Secondary
                    : PositionFitLevel.OutOfPosition;
                double multiplier = player is null ? 1.0 : PositionSkillEffects.GetMultiplier(player, effectivePos);

                int tokenId = player?.Id ?? EncodeEmptySlot(i);
                ManagementPitch.Add(new PitchToken(
                    tokenId, slot.X, slot.Y,
                    PositionDisplay.Short(effectivePos),
                    player?.Name ?? "—",
                    player is null ? 0 : Math.Round(player.Rating, 0),
                    tokenId == _managementSelectedId,
                    fit,
                    i,
                    slot.AlternateRole is not null,
                    player?.UsedAsWingBack ?? false,
                    MalusPercent: (int)Math.Round((1 - multiplier) * 100),
                    Fitness: player?.Fitness ?? 100,
                    YellowCards: player is null ? 0 : YellowCardsFor(player.Id)));
            }

            // Candidate pool for the bench list = whoever started the match on the pitch or
            // bench, minus whoever is currently staged onto the pitch. This allows free
            // repositioning/undo of the original XI while still tracking real subs.
            var candidatePool = _managementTeam.Players
                .Where(p => _originalOnPitchIds.Contains(p.Id) || p.Status == PlayerStatus.OnBench);

            ManagementBench.Clear();
            foreach (var p in candidatePool.Where(p => !stagedIds.Contains(p.Id)).OrderBy(p => p.Position))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), p.Id == _managementSelectedId,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id)));
            }

            // Players sent off THIS match (Status is reset to Available before every match,
            // so Suspended here always means "red-carded in this match") are shown, greyed
            // out, so it's visible WHY their pitch slot is empty - they cannot be selected
            // or dragged (guarded in ManagementSelectPlayer/ManagementBeginDrag).
            foreach (var p in _managementTeam.Players.Where(p => p.Status == PlayerStatus.Suspended))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), IsSelected: false,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id), IsDisabled: true));
            }

            // Same treatment for players injured THIS match - stagedIds excluded so a player
            // still occupying their pitch slot (not yet substituted off) doesn't also show
            // greyed out on the bench.
            foreach (var p in _managementTeam.Players.Where(p => p.Status == PlayerStatus.Injured && !stagedIds.Contains(p.Id)))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), IsSelected: false,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id), IsDisabled: true, DisabledLabel: "Verletzt"));
            }

            int pendingSubs = stagedIds.Count(id => !_originalOnPitchIds.Contains(id));
            int used = _match.SubsUsed(_isHumanHome);
            int remaining = Match.MaxSubstitutions - used - pendingSubs;
            ManagementSubsInfo = pendingSubs > 0
                ? $"Wechsel: {used} genutzt, {pendingSubs} geplant, {remaining} übrig"
                : $"Wechsel: {used} genutzt, {remaining} übrig";
        }

        // Yellow cards accumulated so far THIS match - needed to judge tackling-intensity risk
        // (a second yellow becomes a red) before substituting or changing a player's setting.
        private int YellowCardsFor(int playerId) =>
            _match?.Result.PlayerMatchStats.TryGetValue(playerId, out var stats) == true ? stats.YellowCards : 0;

        [RelayCommand]
        private void ConfirmTeamManagement()
        {
            if (_match is null || _managementTeam is null)
            {
                CloseTeamManagement();
                return;
            }

            var team = _managementTeam;
            var stagedIds = _managementLineup.Where(id => id.HasValue).Select(id => id!.Value).ToList();

            // Diff staged vs. original pitch occupants: who left, who newly came from the bench.
            var leaving = _originalOnPitchIds.Where(id => !stagedIds.Contains(id)).ToList();
            var entering = stagedIds.Where(id => !_originalOnPitchIds.Contains(id)).ToList();

            for (int i = 0; i < Math.Min(leaving.Count, entering.Count); i++)
            {
                var off = team.Players.FirstOrDefault(p => p.Id == leaving[i]);
                var on = team.Players.FirstOrDefault(p => p.Id == entering[i]);
                if (off is null || on is null)
                    continue;
                if (!_match.TrySubstitute(_isHumanHome, off, on))
                    Log.Warn($"Substitution {off.Name} -> {on.Name} could not be applied.");
            }

            // Final positions for everyone now on the pitch (covers pure repositioning too).
            for (int i = 0; i < _managementFormation.Slots.Count; i++)
            {
                if (_managementLineup[i] is not int id)
                    continue;
                var player = team.Players.FirstOrDefault(p => p.Id == id);
                if (player is null)
                    continue;
                var slotPos = ManagementEffectiveSlotPosition(i);
                player.AssignedPosition = slotPos == player.Position ? null : slotPos;
            }

            team.FormationName = _managementFormation.Name;
            if (SelectedManagementStyle is not null)
                _match.SetPlayingStyle(_isHumanHome, SelectedManagementStyle.Style);
            if (SelectedManagementOrientation is not null)
                _match.SetOrientation(_isHumanHome, SelectedManagementOrientation.Orientation);
            if (SelectedManagementTackling is not null)
                team.TacklingIntensity = SelectedManagementTackling.Value;

            StatusText = "Änderungen übernommen.";
            UpdateSubsRemaining();
            CloseTeamManagement();
        }

        [RelayCommand]
        private void CancelTeamManagement()
        {
            StatusText = "Änderungen verworfen.";
            CloseTeamManagement();
        }

        private void CloseTeamManagement()
        {
            IsTeamManagementOpen = false;
            IsHalfTimeManagement = false;
            IsRedCardManagement = false;

            if (_isPaused)
            {
                _isPaused = false;
                PauseResumeLabel = "Pause";
                SetPhase(MatchdayUiPhase.Live);
                _resumeSignal?.TrySetResult();
            }
        }

        // --- Full time / matchday resolution ---

        private async Task OnFullTimeAsync()
        {
            if (_match is null)
                return;

            _humanResult = _match.Result;
            SetPhase(MatchdayUiPhase.FullTime);
            StatusText = "Spiel beendet.";
        }

        [RelayCommand]
        private async Task ShowResults()
        {
            var state = _session.State;
            if (state is null || _humanFixture is null || _humanResult is null)
                return;

            if (IsBusy) return;
            IsBusy = true;
            BusyText = "Spieltag wird ausgewertet …";
            try
            {
                _summary = await _matchDayService.PlayMatchdayAsync(
                    state, _session.Teams, _matchday, _humanFixture, _humanResult);

                await _saveGame.SaveStateAsync(state);
                _seasonFixtures = await _saveGame.GetFixturesAsync(state.Season);

                BuildLeaguePickers();
                RefreshResultGames();
                SetPhase(MatchdayUiPhase.Results);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to complete matchday.", ex);
                StatusText = "Fehler beim Abschließen des Spieltags.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BuildLeaguePickers()
        {
            int myTier = _session.ManagerTeam?.LeagueTier ?? 4;
            var tiers = _seasonFixtures.Select(f => f.LeagueTier).Distinct().OrderBy(t => t).ToList();

            ResultLeagues.Clear();
            foreach (var tier in tiers)
                ResultLeagues.Add(new LeagueChoice(tier, $"Liga {tier}"));

            // Own league first.
            SelectedResultsLeague = ResultLeagues.FirstOrDefault(l => l.Tier == myTier) ?? ResultLeagues.FirstOrDefault();
            SelectedTableLeague = SelectedResultsLeague;
        }

        private void RefreshResultGames()
        {
            ResultGames.Clear();
            if (_summary is null || SelectedResultsLeague is null)
                return;

            foreach (var g in _summary.Games.Where(g => g.LeagueTier == SelectedResultsLeague.Tier))
                ResultGames.Add(new MatchResultRow(g.HomeName, g.AwayName, $"{g.HomeGoals} : {g.AwayGoals}"));
        }

        [RelayCommand]
        private void GoToTable()
        {
            RefreshTable();
            SetPhase(MatchdayUiPhase.Table);
        }

        private void RefreshTable()
        {
            TableRows.Clear();
            TableLegend.Clear();
            if (SelectedTableLeague is null)
                return;

            int tier = SelectedTableLeague.Tier;
            var leagueFixtures = _seasonFixtures.Where(f => f.LeagueTier == tier).ToList();
            var rows = StandingsCalculator.Calculate(leagueFixtures, _teamNames);

            foreach (var row in rows)
                TableRows.Add(new StandingDisplayRow(row, LeagueZoneHelper.GetZone(tier, row.Position, rows.Count)));

            foreach (var item in LegendBuilder.BuildFor(tier))
                TableLegend.Add(item);
        }

        [RelayCommand]
        private async Task Finish()
        {
            var state = _session.State;
            if (state is null)
            {
                await _navigation.GoToRootAsync("mainmenu");
                return;
            }

            // Season over? Roll to the next season and show the outcome.
            if (_matchday >= _matchdayCount && !IsSeasonEnd)
            {
                if (IsBusy) return;
                IsBusy = true;
                BusyText = "Saison wird abgeschlossen …";
                try
                {
                    // Only pass German league teams - fictional CL/Europa Cup clubs (tier 0)
                    // don't belong in the league table/fixture rebuild, see SeasonProgressionService.
                    var germanTeams = _session.Teams.Where(t => t.LeagueTier >= 1).ToList();
                    var result = await _saveGame.AdvanceToNextSeasonAsync(state, germanTeams, _career);
                    _session.Teams = await _saveGame.GetAllTeamsAsync();
                    SeasonEndText = BuildSeasonEndText(result);
                    SetPhase(MatchdayUiPhase.SeasonEnd);

                    if (result.ManagerFinalPosition == 1)
                    {
                        var trophy = TrophyMapping.FromLeagueTier(result.ManagerTier);
                        TrophyImageSource = TrophyDisplay.ImageFileName(trophy);
                        TrophyTitleText = $"Herzlichen Glückwunsch, du gewinnst {TrophyDisplay.Label(trophy)}!";
                        IsTrophyDialogOpen = true;
                    }
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to advance season.", ex);
                }
                finally
                {
                    IsBusy = false;
                }
            }

            await _navigation.GoToRootAsync("mainmenu");
        }

        [RelayCommand]
        private void CloseTrophyDialog() => IsTrophyDialogOpen = false;

        private string BuildSeasonEndText(SeasonEndResult result)
        {
            var manager = _session.ManagerTeam;
            var lines = new List<string>
            {
                $"Saison {result.Season} beendet.",
                $"{manager?.Name}: {result.ManagerOutcome}",
                $"Karrierepunkte: +{result.PointsAwarded} (gesamt {_career.Points}).",
                $"Höchste Start-Liga: Liga {_career.HighestUnlockedTier}.",
            };

            var myLeague = result.Leagues.FirstOrDefault(l => l.Tier == result.ManagerTier);
            if (myLeague is not null)
            {
                var promoted = myLeague.PromotedTeamIds
                    .Select(id => _teamNames.GetValueOrDefault(id, $"#{id}"));
                if (myLeague.PromotedTeamIds.Count > 0)
                    lines.Add($"Aufsteiger Liga {result.ManagerTier}: {string.Join(", ", promoted)}");
            }

            return string.Join("\n", lines);
        }

        // Combined flags for panels that span several phases.
        public bool IsInPlay => IsLive || IsHalfTime;
        public bool IsMatchArea => IsLive || IsHalfTime || IsFullTime;

        private void SetPhase(MatchdayUiPhase phase)
        {
            IsPreMatch = phase == MatchdayUiPhase.PreMatch;
            IsLive = phase == MatchdayUiPhase.Live;
            IsHalfTime = phase == MatchdayUiPhase.HalfTime;
            IsFullTime = phase == MatchdayUiPhase.FullTime;
            IsResults = phase == MatchdayUiPhase.Results;
            IsTable = phase == MatchdayUiPhase.Table;
            IsSeasonEnd = phase == MatchdayUiPhase.SeasonEnd;
            OnPropertyChanged(nameof(IsInPlay));
            OnPropertyChanged(nameof(IsMatchArea));
        }
    }


    public record MatchResultRow(string HomeName, string AwayName, string Score);

    // A single ticker row. Team events (goals/cards/subs/...) are attributed to a side via
    // IsHomeTeam and rendered on that side with a distinct accent color; match-level events
    // (kickoff/half-time/full-time) are centered and neutrally colored.
    public record TickerEntry(int Minute, string Icon, string? PlayerName, string Description, bool IsHomeTeam, bool IsTeamEvent)
    {
        public bool HasPlayerName => !string.IsNullOrEmpty(PlayerName);
        public bool HasIcon => !string.IsNullOrEmpty(Icon);

        // Home = green, Away = sky blue - chosen to stay clearly distinct from the fixed
        // yellow/red card colors and from each other on the dark background.
        public Color AccentColor => !IsTeamEvent
            ? Color.FromArgb("#8FA3B8")
            : IsHomeTeam ? Color.FromArgb("#22C55E") : Color.FromArgb("#38BDF8");

        public LayoutOptions RowAlignment => !IsTeamEvent
            ? LayoutOptions.Center
            : IsHomeTeam ? LayoutOptions.Start : LayoutOptions.End;
    }

    // A persistent (non-scrolling) card entry shown next to the scoreboard for the whole match.
    public record MatchCardEntry(string PlayerName, bool IsHomeTeam, bool IsRed)
    {
        public string Icon => IsRed ? "redcard.png" : "yellowcard.png";
        public Color AccentColor => IsHomeTeam ? Color.FromArgb("#22C55E") : Color.FromArgb("#38BDF8");
    }

    // A persistent goal-scorer entry; Minutes accumulates if the same player scores again.
    public record MatchScorerEntry(string PlayerName, bool IsHomeTeam, List<int> Minutes)
    {
        public string MinutesLabel => string.Join(", ", Minutes.Select(m => $"{m}'"));
        public Color AccentColor => IsHomeTeam ? Color.FromArgb("#22C55E") : Color.FromArgb("#38BDF8");
    }
}
