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
    // Sets each player's training focus - one attribute they work on. The actual gain happens
    // on the weekly matchday tick (see TrainingService.ApplyWeeklyTraining), not here; this page
    // only lets the manager pick or change the focus.
    public partial class TrainingViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<TrainingViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;

        // Shared negotiation dialog - see NegotiationDialogViewModel. Bound in
        // TrainingPage.xaml via BindingContext="{Binding Negotiation}".
        public NegotiationDialogViewModel Negotiation { get; }

        public TrainingViewModel(
            IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation,
            NegotiationDialogViewModel negotiation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Negotiation = negotiation;
            Title = "Training";
        }

        public ObservableCollection<Player> SquadPlayers { get; } = [];
        public ObservableCollection<AttributeRow> Attributes { get; } = [];

        [ObservableProperty] private Player? _selectedPlayer;
        [ObservableProperty] private string _coachInfo = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _playerInfo = string.Empty;

        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;
        [ObservableProperty] private string selectedPlayerPosition;

        [ObservableProperty] private bool _canRenewContract;
        [ObservableProperty] private string _renewStatusText = string.Empty;
        private Contract? _selectedProfileContract;
        private TransferListing? _selectedProfileListing;

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            SquadPlayers.Clear();
            foreach (var p in _team.Players.OrderBy(p => p.Position).ThenByDescending(p => p.Rating))
                SquadPlayers.Add(p);

            var coach = _team.Employees.FirstOrDefault(e => e.EmployeeType == EmployeeType.AssistantCoach)
                        ?? _team.Employees.FirstOrDefault();
            CoachInfo = coach is null
                ? "Kein Co-Trainer – ohne Boost."
                : $"Co-Trainer: {coach.Name} (Off {coach.OffensiveTraining} / Def {coach.DefensiveTraining} / TW {coach.GoalkeeperTraining})";

            SelectedPlayer = SquadPlayers.FirstOrDefault();
            if (SelectedPlayer != null)
                SelectedPlayerPosition = SelectedPlayer.ShortPositionName;
        }

        partial void OnSelectedPlayerChanged(Player? value) => RebuildAttributes();

        private void RebuildAttributes()
        {
            Attributes.Clear();
            var p = SelectedPlayer;
            if (p is null || _team is null)
                return;

            PlayerInfo = $"{p.Name} · {p.Age} J · Talent {p.Talent} · Rating {p.Rating:0}";

            foreach (var attr in TrainingService.ApplicableAttributes(p.Position))
            {
                double factor = TrainingService.CoachFactor(_team, attr, p);
                bool isFocus = p.CurrentTrainingFocus == attr;
                Attributes.Add(new AttributeRow(attr, TrainingService.Label(attr), GetValue(p, attr), factor, isFocus));
            }
            SelectedPlayerPosition = p.ShortPositionName;
        }

        [RelayCommand]
        private async Task ShowProfile()
        {
            if (SelectedPlayer is null)
                return;
            _selectedProfileContract = _session.State is null
                ? null
                : await _saveGame.GetActivePlayerContractAsync(SelectedPlayer.Id, _session.State.CurrentDate);
            _selectedProfileListing = await _saveGame.GetTransferListingForPlayerAsync(SelectedPlayer.Id);
            var seasonStats = _session.State is null
                ? null
                : await _saveGame.GetPlayerSeasonStatsAsync(SelectedPlayer.Id, _session.State.Season);
            var careerStats = await _saveGame.GetPlayerCareerStatsAsync(SelectedPlayer.Id);
            var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(SelectedPlayer.Id);
            SelectedProfile = PlayerProfile.From(SelectedPlayer, _selectedProfileContract, _selectedProfileListing, seasonStats, careerStats, competitionStats);
            CanRenewContract = _selectedProfileContract is not null;
            RenewStatusText = string.Empty;
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        // Opens the negotiation dialog (personal terms directly with the player - no manager
        // involved for a renewal, see NegotiationDialogViewModel) instead of an instant flat
        // salary bump.
        [RelayCommand]
        private async Task RenewContract()
        {
            if (SelectedPlayer is null || _team is null || _session.State is null)
                return;

            string? error = await Negotiation.TryStartRenewalNegotiationAsync(
                _team, SelectedPlayer, _session.State.Season, _session.State.CurrentDate, onCompleted: ShowProfile);
            RenewStatusText = error ?? string.Empty;
        }

        [RelayCommand]
        private void SetFocus(AttributeRow row)
        {
            var p = SelectedPlayer;
            if (p is null)
                return;

            p.CurrentTrainingFocus = row.Attribute;
            StatusText = $"Trainingsfokus: {TrainingService.Label(row.Attribute)} - wird über die Saison langsam trainiert.";
            RebuildAttributes();
        }

        [RelayCommand]
        private async Task Confirm()
        {
            if (_team is null || _session.State is null)
            {
                StatusText = "Kein aktives Spiel - konnte nicht bestätigt werden.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            StatusText = "Trainingsstand wird gespeichert …";
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save training.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static int GetValue(Player p, TrainableAttribute a) => a switch
        {
            TrainableAttribute.Offensive => p.OffensivePower,
            TrainableAttribute.Defensive => p.DefensivePower,
            TrainableAttribute.GameIntelligence => p.GameIntelligence,
            TrainableAttribute.Pressing => p.PressingIntensity,
            TrainableAttribute.CounterSpeed => p.CounterSpeed,
            TrainableAttribute.Passing => p.PassingAccuracy,
            TrainableAttribute.DuelHardness => p.DuelHardness,
            TrainableAttribute.DuelEfficiency => p.DuelEfficiency,
            TrainableAttribute.Crossing => p.CrossingAccuracy,
            TrainableAttribute.GkReflexes => p.GkReflexes,
            TrainableAttribute.GkHandling => p.GkHandling,
            TrainableAttribute.GkOneOnOne => p.GkOneOnOne,
            TrainableAttribute.GkDistribution => p.GkDistribution,
            TrainableAttribute.GkAerialControl => p.GkAerialControl,
            TrainableAttribute.HeaderStrength => p.HeaderStrength,
            TrainableAttribute.Jumping => p.Jumping,
            TrainableAttribute.Dribbling => p.Dribbling,
            TrainableAttribute.LongShot => p.LongShotAccuracy,
            TrainableAttribute.PenaltyKick => p.PenaltyKick,
            TrainableAttribute.FreeKick => p.FreeKick,
            TrainableAttribute.Finishing => p.Finishing,
            TrainableAttribute.Positioning => p.Positioning,
            _ => 0,
        };
    }

    public record AttributeRow(TrainableAttribute Attribute, string Label, int Value, double CoachFactor, bool IsFocus)
    {
        public string CoachHint => CoachFactor > 1.05 ? "▲" : CoachFactor < 0.95 ? "▼" : "•";
        public string FocusButtonText => IsFocus ? "Aktueller Fokus" : "Als Fokus wählen";
        public Color FocusButtonColor => IsFocus ? Color.FromArgb("#22C55E") : Color.FromArgb("#38BDF8");
    }
}
