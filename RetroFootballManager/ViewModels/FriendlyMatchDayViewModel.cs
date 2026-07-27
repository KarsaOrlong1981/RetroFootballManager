using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Core.Models;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public enum FriendlyMatchdayUiPhase { PreMatch, Live, HalfTime, FullTime }

    public partial class FriendlyMatchDayViewModel : BaseViewModel, IQueryAttributable
    {
        private static readonly ILog Log = LogManager.GetLogger<FriendlyMatchDayViewModel>();

        private static readonly int[] SpeedDelaysMs = [1300, 850, 500, 180, 80];
        private static readonly string[] SpeedLabels = ["Sehr langsam", "Langsam", "Normal", "Schnell", "Ultra"];

        private readonly GameSession _session;
        private readonly FixtureRepository _fixtures;
        private readonly TeamRepository _teams;
        private readonly MessageService _messages;
        private readonly INavigationService _navigation;

        private int _fixtureId;
        private Match? _match;
        private Fixture? _fixture;
        private Team? _homeTeam;
        private Team? _awayTeam;
        private bool _isHumanHome;

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

        public FriendlyMatchDayViewModel(
            IDispatcher dispatcher,
            GameSession session,
            FixtureRepository fixtures,
            TeamRepository teams,
            MessageService messages,
            INavigationService navigation,
            AppSettingsService appSettings)
            : base(dispatcher)
        {
            _session = session;
            _fixtures = fixtures;
            _teams = teams;
            _messages = messages;
            _navigation = navigation;
            Title = "Freundschaftsspiel";
            SpeedLevel = appSettings.DefaultMatchSpeed;
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("fixtureId", out var value) && int.TryParse(value?.ToString(), out int id))
                _fixtureId = id;
        }

        [ObservableProperty] private bool _isPreMatch;
        [ObservableProperty] private bool _isLive;
        [ObservableProperty] private bool _isHalfTime;
        [ObservableProperty] private bool _isFullTime;

        [ObservableProperty] private string _headerText = string.Empty;
        [ObservableProperty] private string _homeTeamShortName = string.Empty;
        [ObservableProperty] private string _awayTeamShortName = string.Empty;
        [ObservableProperty] private string? _homeTeamLogoPath;
        [ObservableProperty] private string? _awayTeamLogoPath;
        [ObservableProperty] private string _scoreText = string.Empty;
        [ObservableProperty] private string _minuteText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _subsRemainingText = string.Empty;
        [ObservableProperty] private string _pauseResumeLabel = "Pause";
        [ObservableProperty] private double _speedLevel = 2;
        [ObservableProperty] private string _speedLabel = "Normal";
        [ObservableProperty] private string _busyText = string.Empty;

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

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;

        public ObservableCollection<TickerEntry> Ticker { get; } = [];
        public ObservableCollection<MatchCardEntry> Cards { get; } = [];
        public ObservableCollection<MatchScorerEntry> Scorers { get; } = [];
        public ObservableCollection<PlayingStyleOption> PlayingStyles { get; } = [];
        public ObservableCollection<OrientationOption> Orientations { get; } = [];
        public ObservableCollection<TacklingOption> TacklingOptions { get; } = [];
        public ObservableCollection<Formation> ManagementFormations { get; } = [];
        public ObservableCollection<PitchToken> ManagementPitch { get; } = [];
        public ObservableCollection<SquadToken> ManagementBench { get; } = [];

        partial void OnSpeedLevelChanged(double value) =>
            SpeedLabel = SpeedLabels[Math.Clamp((int)Math.Round(value), 0, SpeedLabels.Length - 1)];

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
                _fixture = await _fixtures.GetByIdAsync(_fixtureId);
                if (_fixture is null || _fixture.Played)
                {
                    StatusText = "Kein Freundschaftsspiel fällig.";
                    SetPhase(FriendlyMatchdayUiPhase.PreMatch);
                    return;
                }

                _isHumanHome = _fixture.HomeTeamId == manager.Id;
                _homeTeam = _session.Teams.First(t => t.Id == _fixture.HomeTeamId);
                _awayTeam = _session.Teams.First(t => t.Id == _fixture.AwayTeamId);

                var humanTeam = _isHumanHome ? _homeTeam : _awayTeam;
                var aiTeam = _isHumanHome ? _awayTeam : _homeTeam;
                await MatchDayService.NotifyInjuryRecoveriesAsync(_messages, humanTeam, state.CurrentDate);
                MatchDayService.RecoverForMatch(humanTeam, state.CurrentDate);
                MatchDayService.PrepareForMatch(aiTeam, state.CurrentDate);

                var formation = FormationCatalog.GetByName(humanTeam.FormationName);
                if (humanTeam.Players.Count(p => p.Status == PlayerStatus.InStartingXI) < formation.Slots.Count)
                    LineupSelector.FillMissingStarters(humanTeam, formation);

                BuildTactics();

                HeaderText = $"Freundschaftsspiel · {_homeTeam.ShortName} – {_awayTeam.ShortName}";
                HomeTeamShortName = _homeTeam.ShortName;
                AwayTeamShortName = _awayTeam.ShortName;
                HomeTeamLogoPath = _homeTeam.LogoPath;
                AwayTeamLogoPath = _awayTeam.LogoPath;
                ScoreText = $"{_homeTeam.ShortName} 0 : 0 {_awayTeam.ShortName}";
                MinuteText = "0'";
                StatusText = $"{_homeTeam.Name} gegen {_awayTeam.Name} (Freundschaftsspiel)";
                SetPhase(FriendlyMatchdayUiPhase.PreMatch);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to prepare friendly match.", ex);
                StatusText = "Fehler beim Laden des Freundschaftsspiels.";
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

        [RelayCommand]
        private async Task KickOff()
        {
            if (_homeTeam is null || _awayTeam is null || _fixture is null)
                return;

            _match = new Match(_homeTeam, _awayTeam, new Random())
            {
                HomeCoach = _isHumanHome ? null : new AiMatchCoach(),
                AwayCoach = _isHumanHome ? new AiMatchCoach() : null,
            };
            _match.Begin();
            _halfTimeShown = false;
            _isPaused = false;
            _tickerCursor = 0;
            Ticker.Clear();
            SetPhase(FriendlyMatchdayUiPhase.Live);
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
                        SetPhase(FriendlyMatchdayUiPhase.HalfTime);
                        OpenTeamManagement(isHalfTime: true, isRedCard: false);
                        continue;
                    }

                    if (_match.IsFinished)
                        break;

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
                Log.Error("Error during friendly match live simulation.", ex);
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

        private (bool SawRedCard, bool SawInjury) UpdateLiveState()
        {
            if (_match is null || _homeTeam is null || _awayTeam is null)
                return (false, false);

            ScoreText = $"{_homeTeam.ShortName} {_match.HomeGoals} : {_match.AwayGoals} {_awayTeam.ShortName}";
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
                        sawRedCard = true;
                        break;
                    case GameEventType.Goal when e.Player is not null:
                        AddOrUpdateScorer(e.Player.Name, e.IsHomeTeam, e.Minute);
                        break;
                    case GameEventType.Injury when e.Player is not null:
                        sawInjury = true;
                        break;
                }

                if (e.Type is GameEventType.Shot or GameEventType.DangerousAttack)
                    continue;

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
                Scorers[index] = existing with { Minutes = existing.Minutes };
            }
            else
            {
                Scorers.Add(new MatchScorerEntry(playerName, isHomeTeam, [minute]));
            }
        }

        private static string IconFor(GameEventType type) => type switch
        {
            GameEventType.Goal => "⚽",
            GameEventType.YellowCard => "🟨",
            GameEventType.RedCard => "🟥",
            GameEventType.Substitution => "🔄",
            _ => "•",
        };

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
                    continue;

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
        private void ShowProfile(int playerId)
        {
            var player = _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            SelectedProfile = PlayerProfile.From(player, contract: null, listing: null);
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

        private bool IsSuspendedThisMatch(int playerId) =>
            _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId)?.Status == PlayerStatus.Suspended;

        private bool IsInjuredThisMatch(int playerId) =>
            _managementTeam?.Players.FirstOrDefault(p => p.Id == playerId)?.Status == PlayerStatus.Injured;

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

        private void ManagementSwap(int aId, int bId)
        {
            int slotA = ManagementSlotOf(aId);
            int slotB = ManagementSlotOf(bId);
            if (slotA < 0 && slotB < 0)
                return;

            var temp = (int?[])_managementLineup.Clone();
            if (slotA >= 0 && slotB >= 0)
                (temp[slotA], temp[slotB]) = (temp[slotB], temp[slotA]);
            else if (slotA >= 0)
                temp[slotA] = bId;
            else
                temp[slotB] = aId;

            int unavailableCount = _managementTeam?.Players.Count(p => p.Status is PlayerStatus.Suspended or PlayerStatus.Injured) ?? 0;
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

        private static int EncodeEmptySlot(int slotIndex) => -(slotIndex + 2);

        private static bool TryDecodeEmptySlot(int id, out int slotIndex)
        {
            slotIndex = -(id + 2);
            return id <= -2;
        }

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

            var candidatePool = _managementTeam.Players
                .Where(p => _originalOnPitchIds.Contains(p.Id) || p.Status == PlayerStatus.OnBench);

            ManagementBench.Clear();
            foreach (var p in candidatePool.Where(p => !stagedIds.Contains(p.Id)).OrderBy(p => p.Position))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), p.Id == _managementSelectedId,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id)));
            }

            foreach (var p in _managementTeam.Players.Where(p => p.Status == PlayerStatus.Suspended))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), IsSelected: false,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id), IsDisabled: true));
            }

            foreach (var p in _managementTeam.Players.Where(p => p.Status == PlayerStatus.Injured && !stagedIds.Contains(p.Id)))
            {
                ManagementBench.Add(new SquadToken(
                    p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), IsSelected: false,
                    Fitness: p.Fitness, YellowCards: YellowCardsFor(p.Id), IsDisabled: true));
            }

            int pendingSubs = stagedIds.Count(id => !_originalOnPitchIds.Contains(id));
            int used = _match.SubsUsed(_isHumanHome);
            int remaining = Match.MaxSubstitutions - used - pendingSubs;
            ManagementSubsInfo = pendingSubs > 0
                ? $"Wechsel: {used} genutzt, {pendingSubs} geplant, {remaining} übrig"
                : $"Wechsel: {used} genutzt, {remaining} übrig";
        }

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
                SetPhase(FriendlyMatchdayUiPhase.Live);
                _resumeSignal?.TrySetResult();
            }
        }

        private async Task OnFullTimeAsync()
        {
            if (_match is null || _fixture is null || _homeTeam is null || _awayTeam is null)
                return;

            _fixture.HomeGoals = _match.HomeGoals;
            _fixture.AwayGoals = _match.AwayGoals;
            _fixture.Played = true;
            _match.Result.ApplyInjuryDurations(_fixture.Date);
            int humanTeamId = _isHumanHome ? _homeTeam.Id : _awayTeam.Id;
            await MatchDayService.NotifyInjuriesAsync(_messages, _match.Result, _homeTeam, _awayTeam, humanTeamId, _fixture.Date);
            MatchDayService.ApplyCareerMinutes(_match.Result, _homeTeam, _awayTeam);
            FriendlyService.ApplyFriendlyIncome(_homeTeam, _awayTeam);

            SetPhase(FriendlyMatchdayUiPhase.FullTime);
            StatusText = "Freundschaftsspiel beendet.";
        }

        [RelayCommand]
        private async Task Continue()
        {
            if (IsBusy) return;
            IsBusy = true;
            BusyText = "Freundschaftsspiel wird gespeichert …";
            try
            {
                if (_fixture is not null)
                    await _fixtures.SaveAsync(_fixture);
                if (_homeTeam is not null)
                    await _teams.SaveTeamAsync(_homeTeam, includeYouth: false);
                if (_awayTeam is not null)
                    await _teams.SaveTeamAsync(_awayTeam, includeYouth: false);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save friendly match.", ex);
            }
            finally
            {
                IsBusy = false;
            }

            await _navigation.GoToRootAsync("mainmenu");
        }

        public bool IsInPlay => IsLive || IsHalfTime;
        public bool IsMatchArea => IsLive || IsHalfTime || IsFullTime;

        private void SetPhase(FriendlyMatchdayUiPhase phase)
        {
            IsPreMatch = phase == FriendlyMatchdayUiPhase.PreMatch;
            IsLive = phase == FriendlyMatchdayUiPhase.Live;
            IsHalfTime = phase == FriendlyMatchdayUiPhase.HalfTime;
            IsFullTime = phase == FriendlyMatchdayUiPhase.FullTime;
            OnPropertyChanged(nameof(IsInPlay));
            OnPropertyChanged(nameof(IsMatchArea));
        }
    }
}
