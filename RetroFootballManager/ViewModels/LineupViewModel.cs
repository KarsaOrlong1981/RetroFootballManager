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
    public enum PositionFitLevel { Empty, Favorite, Secondary, OutOfPosition }

    public partial class LineupViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<LineupViewModel>();
        private const int BenchCap = 9;

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;
        private Formation _formation = FormationCatalog.Default;
        private int?[] _lineup = new int?[11];
        private List<int> _benchIds = [];
        private int? _selectedPlayerId;
        private int? _draggedPlayerId;


        public LineupViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Aufstellung";
        }

        public ObservableCollection<PitchToken> Pitch { get; } = [];
        public ObservableCollection<SquadToken> Bench { get; } = [];
        public ObservableCollection<SquadToken> Reserves { get; } = [];
        public ObservableCollection<PlayingStyleOption> PlayingStyles { get; } = [];
        public ObservableCollection<OrientationOption> Orientations { get; } = [];
        public ObservableCollection<TacklingOption> TacklingOptions { get; } = [];
        public ObservableCollection<FormationDialogItem> FormationDialogItems { get; } = [];

        [ObservableProperty] private PlayingStyleOption? _selectedPlayingStyle;
        [ObservableProperty] private OrientationOption? _selectedOrientation;
        [ObservableProperty] private TacklingOption? _selectedTackling;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _currentFormationName = string.Empty;

        [ObservableProperty] private bool _isFormationDialogOpen;
        [ObservableProperty] private FormationDialogItem? _selectedDialogItem;

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            PlayingStyles.Clear();
            foreach (var s in Enum.GetValues<PlayingStyle>())
                PlayingStyles.Add(new PlayingStyleOption(s, PlayingStyleOption.LabelFor(s)));

            Orientations.Clear();
            foreach (var o in Enum.GetValues<TacticalOrientation>())
                Orientations.Add(new OrientationOption(o, OrientationOption.LabelFor(o)));

            TacklingOptions.Clear();
            foreach (var ti in Enum.GetValues<TacklingIntensity>())
                TacklingOptions.Add(new TacklingOption(ti, TacklingOption.LabelFor(ti)));

            _formation = FormationCatalog.GetByName(_team.FormationName);
            CurrentFormationName = _formation.Name;
            SelectedPlayingStyle = PlayingStyles.FirstOrDefault(s => s.Style == _team.PlayingStyle);
            SelectedOrientation = Orientations.FirstOrDefault(o => o.Orientation == _team.TacticalOrientation);
            SelectedTackling = TacklingOptions.FirstOrDefault(t => t.Value == _team.TacklingIntensity);

            // Self-heal: if the persisted/session lineup is incomplete (fewer starters than
            // the formation needs), auto-pick a valid position-correct XI before rendering -
            // otherwise most pitch slots would show empty and never register a swap.
            if (_team.Players.Count(p => p.Status == PlayerStatus.InStartingXI) < _formation.Slots.Count)
                LineupSelector.SelectLineup(_team, _formation);

            BuildInitialLineup();
            RebuildViews();
        }

        partial void OnSelectedPlayingStyleChanged(PlayingStyleOption? value)
        {
            if (_team is not null && value is not null)
                _team.PlayingStyle = value.Style;
        }

        partial void OnSelectedOrientationChanged(OrientationOption? value)
        {
            if (_team is not null && value is not null)
                _team.TacticalOrientation = value.Orientation;
        }

        partial void OnSelectedTacklingChanged(TacklingOption? value)
        {
            if (_team is not null && value is not null)
                _team.TacklingIntensity = value.Value;
        }

        // --- Formation dialog ---

        [RelayCommand]
        private void OpenFormationDialog()
        {
            if (_team is null)
                return;

            var best = FormationCatalog.All
                .OrderByDescending(f => LineupSelector.ScoreFormation(_team, f))
                .First();

            FormationDialogItems.Clear();
            foreach (var f in FormationCatalog.All)
                FormationDialogItems.Add(new FormationDialogItem(f, f.Name == best.Name));

            SelectedDialogItem = FormationDialogItems.FirstOrDefault(i => i.Formation.Name == _formation.Name)
                                 ?? FormationDialogItems.FirstOrDefault();
            IsFormationDialogOpen = true;
        }

        [RelayCommand]
        private void ConfirmFormation()
        {
            if (_team is not null && SelectedDialogItem is not null)
            {
                _formation = SelectedDialogItem.Formation;
                _team.FormationName = _formation.Name;
                CurrentFormationName = _formation.Name;
                LineupSelector.SelectLineup(_team, _formation);
                BuildInitialLineup();
                RebuildViews();
                StatusText = $"Formation {_formation.Name} übernommen.";
            }
            IsFormationDialogOpen = false;
        }

        [RelayCommand]
        private void CancelFormation() => IsFormationDialogOpen = false;

        // --- Player profile ---

        [RelayCommand]
        private async Task ShowProfile(int playerId)
        {
            if (_team is null || playerId < 0)
                return;
            var player = _team.Players.FirstOrDefault(p => p.Id == playerId)
                         ?? _team.YouthPlayers.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            var contract = _session.State is null
                ? null
                : await _saveGame.GetActivePlayerContractAsync(player.Id, _session.State.CurrentDate);
            var listing = await _saveGame.GetTransferListingForPlayerAsync(player.Id);
            var seasonStats = _session.State is null
                ? null
                : await _saveGame.GetPlayerSeasonStatsAsync(player.Id, _session.State.Season);
            SelectedProfile = PlayerProfile.From(player, contract, listing, seasonStats);
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        // --- Swapping (tap + drag&drop) ---

        // Injured players must never enter a pitch slot - they're excluded from auto-picks
        // (LineupSelector) already, but manual drag&drop/tap-to-swap had no guard at all.
        private bool IsInjured(int playerId) =>
            _team?.Players.FirstOrDefault(p => p.Id == playerId)?.Status == PlayerStatus.Injured;

        [RelayCommand]
        private void SelectPlayer(int playerId)
        {
            if (playerId < 0)
                return;
            if (IsInjured(playerId))
            {
                StatusText = "Verletzte Spieler können nicht aufgestellt werden.";
                return;
            }

            if (_selectedPlayerId is null)
                _selectedPlayerId = playerId;
            else if (_selectedPlayerId == playerId)
                _selectedPlayerId = null; // tap again to deselect
            else
            {
                Swap(_selectedPlayerId.Value, playerId);
                _selectedPlayerId = null;
            }
            RebuildViews();
        }

        // Toggles the WingBack role for whoever currently occupies this slot, if the slot offers
        // one. The decision is persisted directly on the PLAYER (Player.UsedAsWingBack), not in
        // transient per-slot UI state - a slot-indexed array has to be reconstructed on every
        // reload by re-deriving "was this slot's occupant toggled" from AssignedPosition, which
        // is ambiguous whenever another starter coincidentally shares the same effective position.
        // A flag directly on the player survives reload/matchday/save exactly like any other
        // Player field, no re-derivation needed.
        [RelayCommand]
        private void ToggleSlotRole(int slotIndex)
        {
            if (_team is null || slotIndex < 0 || slotIndex >= _formation.Slots.Count)
                return;
            var slot = _formation.Slots[slotIndex];
            if (slot.AlternateRole is null)
                return;
            if (_lineup[slotIndex] is not int playerId)
                return;

            var player = _team.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is null)
                return;

            player.UsedAsWingBack = !player.UsedAsWingBack;
            ApplyLineup();
            RebuildViews();
        }

        private Position EffectiveSlotPositionFor(int slotIndex, Player? player)
        {
            var slot = _formation.Slots[slotIndex];
            if (player is not null && slot.AlternateRole is Position alt && player.UsedAsWingBack)
                return alt;
            return slot.Position;
        }

        [RelayCommand]
        private void BeginDrag(int playerId)
        {
            if (IsInjured(playerId))
                return;
            _draggedPlayerId = playerId;
        }

        [RelayCommand]
        private void DropOn(int targetPlayerId)
        {
            if (IsInjured(targetPlayerId))
            {
                StatusText = "Verletzte Spieler können nicht aufgestellt werden.";
                _draggedPlayerId = null;
                return;
            }

            if (_draggedPlayerId is int dragged && dragged != targetPlayerId && dragged >= 0 && targetPlayerId >= 0)
            {
                Swap(dragged, targetPlayerId);
                _selectedPlayerId = null;
                RebuildViews();
            }
            _draggedPlayerId = null;
        }

        // Moves aId and bId between their EXACT current tiers (pitch slot / bench / reserve).
        // Never re-ranks by rating - the drop target always decides where each player ends up.
        private void Swap(int aId, int bId)
        {
            int slotA = SlotOf(aId);
            int slotB = SlotOf(bId);

            if (slotA >= 0 && slotB >= 0)
            {
                // Pitch <-> pitch: exchange positions only.
                (_lineup[slotA], _lineup[slotB]) = (_lineup[slotB], _lineup[slotA]);
            }
            else if (slotA >= 0 || slotB >= 0)
            {
                // One on the pitch, one not (bench or reserve).
                int pitchSlot = slotA >= 0 ? slotA : slotB;
                int pitchPlayer = slotA >= 0 ? aId : bId;
                int otherPlayer = slotA >= 0 ? bId : aId;

                _lineup[pitchSlot] = otherPlayer;

                int benchIndex = _benchIds.IndexOf(otherPlayer);
                if (benchIndex >= 0)
                    _benchIds[benchIndex] = pitchPlayer; // displaced starter takes that exact bench slot
                // else: other came from reserve -> displaced starter simply becomes reserve.
            }
            else
            {
                // Neither on the pitch: bench <-> reserve (the missing swap direction).
                int benchA = _benchIds.IndexOf(aId);
                int benchB = _benchIds.IndexOf(bId);

                if (benchA >= 0 && benchB < 0)
                    _benchIds[benchA] = bId;
                else if (benchB >= 0 && benchA < 0)
                    _benchIds[benchB] = aId;
                // both bench or both reserve: no functional difference, nothing to do.
            }

            ApplyLineup();
        }

        private int SlotOf(int playerId)
        {
            for (int i = 0; i < _lineup.Length; i++)
                if (_lineup[i] == playerId)
                    return i;
            return -1;
        }

        // Maps the current InStartingXI/OnBench players onto the formation's slots and an
        // explicit bench list, by position.
        private void BuildInitialLineup()
        {
            if (_team is null)
                return;

            _lineup = new int?[_formation.Slots.Count];
            var starters = _team.Players.Where(p => p.Status == PlayerStatus.InStartingXI).ToList();

            // Explicit AssignedPosition overrides (incl. plain repositioning) are matched to their
            // slot BEFORE any natural-fit matching - otherwise a different starter who just
            // naturally IS e.g. a born wingback (Position == LeftWingBack) could "coincidentally"
            // match the same slot via EffectivePosition and steal it from whoever actually
            // belongs there. See LineupSelector.MatchStartersToSlots. The WB toggle itself lives
            // on Player.UsedAsWingBack directly - no per-slot restore step needed.
            var matched = LineupSelector.MatchStartersToSlots(starters, _formation);
            foreach (var (slotIndex, playerId) in matched)
                _lineup[slotIndex] = playerId;

            _benchIds = _team.Players
                .Where(p => p.Status == PlayerStatus.OnBench)
                .OrderByDescending(p => p.Rating)
                .Take(BenchCap)
                .Select(p => p.Id)
                .ToList();
        }

        // Writes the explicit _lineup/_benchIds tiers back onto player Status/AssignedPosition.
        // No re-ranking: whoever isn't in _lineup or _benchIds is simply a reserve.
        private void ApplyLineup()
        {
            if (_team is null)
                return;

            var starterIds = _lineup.Where(id => id.HasValue).Select(id => id!.Value).ToHashSet();
            var benchSet = _benchIds.ToHashSet();

            foreach (var player in _team.Players)
            {
                if (starterIds.Contains(player.Id))
                {
                    int slotIndex = Array.IndexOf(_lineup, (int?)player.Id);
                    var slotPos = EffectiveSlotPositionFor(slotIndex, player);
                    player.Status = PlayerStatus.InStartingXI;
                    player.AssignedPosition = slotPos == player.Position ? null : slotPos;
                }
                else if (benchSet.Contains(player.Id))
                {
                    player.AssignedPosition = null;
                    player.Status = PlayerStatus.OnBench;
                }
                else
                {
                    player.AssignedPosition = null;
                    player.Status = PlayerStatus.Available;
                }
            }
        }

        private void RebuildViews()
        {
            if (_team is null)
                return;

            Pitch.Clear();
            for (int i = 0; i < _formation.Slots.Count; i++)
            {
                var slot = _formation.Slots[i];
                var player = _lineup[i] is int id ? _team.Players.FirstOrDefault(p => p.Id == id) : null;
                var effectivePos = EffectiveSlotPositionFor(i, player);
                double multiplier = player is null ? 1.0 : PositionSkillEffects.GetMultiplier(player, effectivePos);
                Pitch.Add(new PitchToken(
                    player?.Id ?? -1, slot.X, slot.Y,
                    PositionDisplay.Short(effectivePos),
                    player?.Name ?? "—",
                    player is null ? 0 : Math.Round(player.Rating, 0),
                    player?.Id == _selectedPlayerId,
                    ClassifyFit(player, effectivePos),
                    i,
                    slot.AlternateRole is not null,
                    player?.UsedAsWingBack ?? false,
                    MalusPercent: (int)Math.Round((1 - multiplier) * 100),
                    Fitness: player?.Fitness ?? 100));
            }

            Bench.Clear();
            foreach (var p in _team.Players.Where(p => p.Status == PlayerStatus.OnBench).OrderBy(p => p.Position))
                Bench.Add(ToSquadToken(p));

            Reserves.Clear();
            foreach (var p in _team.Players.Where(p => p.Status == PlayerStatus.Available).OrderBy(p => p.Position))
                Reserves.Add(ToSquadToken(p));
        }

        private static PositionFitLevel ClassifyFit(Player? player, Position slotPosition)
        {
            if (player is null)
                return PositionFitLevel.Empty;
            if (slotPosition == player.Position)
                return PositionFitLevel.Favorite;
            return player.SecondaryPositions.Any(sp => sp.Position == slotPosition)
                ? PositionFitLevel.Secondary
                : PositionFitLevel.OutOfPosition;
        }

        private SquadToken ToSquadToken(Player p) =>
            new(p.Id, p.Name, p.ShortPositionName, Math.Round(p.Rating, 0), p.Id == _selectedPlayerId,
                PlayerAttributeSummary.From(p), Fitness: p.Fitness);

        // Persists the lineup and returns to the main menu - the visible navigation IS the
        // confirmation, since a status label at the bottom of a long scrollable page is easy
        // to miss (reported: button appeared to do nothing).
        [RelayCommand]
        private async Task Confirm()
        {
            if (_team is null || _session.State is null)
            {
                StatusText = "Kein aktives Spiel - Aufstellung konnte nicht bestätigt werden.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            StatusText = "Aufstellung wird gespeichert …";
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save lineup.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public record PitchToken(
        int PlayerId, double X, double Y, string PosLabel, string Name, double Rating, bool IsSelected,
        PositionFitLevel Fit = PositionFitLevel.Empty,
        int SlotIndex = -1,
        bool HasAlternateRole = false,
        bool IsAlternateActive = false,
        int MalusPercent = 0,
        int Fitness = 100,
        int YellowCards = 0)
    {
        // Visible only when the position-fit penalty actually reduces the player's
        // contribution (0 on the home position, since PositionSkillEffects.GetMultiplier is 1.0).
        public bool HasMalus => MalusPercent > 0;
        public string MalusLabel => $"-{MalusPercent}%";

        // So a manager can substitute sensibly and judge tackling-intensity risk live.
        public bool HasYellowCard => YellowCards > 0;
        public Color FitnessColor => Fitness >= 75 ? Color.FromArgb("#22C55E")
            : Fitness >= 50 ? Color.FromArgb("#EAB308")
            : Color.FromArgb("#EF4444");
    }

    public record SquadToken(
        int PlayerId, string Name, string PosLabel, double Rating, bool IsSelected,
        PlayerAttributeSummary? Attributes = null,
        int Fitness = 100,
        int YellowCards = 0,
        bool IsDisabled = false)
    {
        public bool HasYellowCard => YellowCards > 0;
        public Color FitnessColor => Fitness >= 75 ? Color.FromArgb("#22C55E")
            : Fitness >= 50 ? Color.FromArgb("#EAB308")
            : Color.FromArgb("#EF4444");
    }

    public record FormationDialogItem(Formation Formation, bool IsRecommended)
    {
        public string Name => Formation.Name;
    }

    public record TacklingOption(TacklingIntensity Value, string Label)
    {
        public static string LabelFor(TacklingIntensity t) => t switch
        {
            TacklingIntensity.Cautious => "Vorsichtig",
            TacklingIntensity.Moderate => "Mittel",
            TacklingIntensity.Hard => "Hart",
            _ => "Normal",
        };
    }
}
