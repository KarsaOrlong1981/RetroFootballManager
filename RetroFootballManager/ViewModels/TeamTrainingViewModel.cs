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
    // Sets the team-wide training focus (e.g. Pressing, Tiki-Taka) - applies a smaller boost to
    // every player's relevant attributes on top of their individual focus (see
    // TrainingService.ApplyWeeklyTraining), progressing on the same weekly tick.
    public partial class TeamTrainingViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<TeamTrainingViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;

        public TeamTrainingViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Team-Training";
        }

        public ObservableCollection<TeamFocusRow> Focuses { get; } = [];

        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private ObservableCollection<Employee> _coTrainers;
        [ObservableProperty] private ManagerProfile? _manager;

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            
            if (_team is null)
                return;

            Manager = _team.ManagerProfile;
            var emplyoees = _team.Employees.Where(e => e.EmployeeType == EmployeeType.AssistantCoach || e.EmployeeType == EmployeeType.FitnessCoach || e.EmployeeType == EmployeeType.GoalkeeperCoach).ToList();
            CoTrainers = new ObservableCollection<Employee>(emplyoees);
            RebuildFocuses();
        }

        private void RebuildFocuses()
        {
            Focuses.Clear();
            if (_team is null)
                return;

            foreach (var focus in Enum.GetValues<TeamTrainingFocus>())
                Focuses.Add(new TeamFocusRow(focus, TrainingService.Label(focus), _team.TeamTrainingFocus == focus));
        }

        [RelayCommand]
        private async Task SetFocus(TeamFocusRow row)
        {
            if (_team is null)
                return;

            _team.TeamTrainingFocus = row.Focus;
            StatusText = $"Team-Trainingsfokus: {row.Label} - wirkt über die Saison langsam auf das ganze Team.";
            RebuildFocuses();
            await Confirm();
        }

        private async Task Confirm()
        {
            if (_team is null || _session.State is null)
            {
                StatusText = "Kein aktives Spiel - konnte nicht bestätigt werden.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            StatusText = "Team-Trainingsfokus wird gespeichert …";
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save team training.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public record TeamFocusRow(TeamTrainingFocus Focus, string Label, bool IsActive)
    {
        public string ButtonText => IsActive ? "Aktueller Fokus" : "Als Fokus wählen";
        public Color ButtonColor => IsActive ? Color.FromArgb("#22C55E") : Color.FromArgb("#38BDF8");
    }
}
