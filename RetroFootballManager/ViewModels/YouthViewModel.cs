using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    // Mentor candidate for the picker: exposes position/rating so users can tell candidates
    // apart, since Player.Name alone was ambiguous when several seniors share a name/age.
    public record MentorOption(int Id, string Name, string PositionShort, double Rating)
    {
        public string Display => $"{Name} · {PositionShort} · R {Rating:0}";

        public static MentorOption From(Player p) => new(p.Id, p.Name, p.ShortPositionName, p.Rating);
    }

    // Youth academy: view prospects, assign a mentor (speeds development) and promote a
    // prospect into the senior squad so they can be fielded and gain first-team minutes.
    public partial class YouthViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<YouthViewModel>();

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;

        public YouthViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Jugend";
        }

        public ObservableCollection<Player> Youth { get; } = [];
        public ObservableCollection<MentorOption> Mentors { get; } = [];

        [ObservableProperty] private Player? _selectedYouth;
        [ObservableProperty] private MentorOption? _selectedMentor;
        [ObservableProperty] private string _youthInfo = string.Empty;
        [ObservableProperty] private string _currentMentorInfo = string.Empty;
        [ObservableProperty] private bool _hasMentor;
        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private ObservableCollection<Employee> _coTrainers;
        [ObservableProperty] private bool _isPlayerProfileOpen;
        [ObservableProperty] private PlayerProfile? _selectedProfile;
        [ObservableProperty] private string positionShort;

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            var emplyoees = _team.Employees.Where(e => e.EmployeeType == EmployeeType.YouthCoach).ToList();
            CoTrainers = new ObservableCollection<Employee>(emplyoees);

            RebuildYouth();
            SelectedYouth = Youth.FirstOrDefault();
            RefreshMentorOptions();
        }

        // Rebuilt per selected youth player - mentors must match the selected youth's home
        // position, not just any senior, otherwise a goalkeeper could "mentor" a striker.
        private void RefreshMentorOptions()
        {
            if (_team is null)
                return;

            Mentors.Clear();
            if (SelectedYouth is not null)
            {
                foreach (var p in _team.Players
                    .Where(p => p.Age >= 22 && p.Position == SelectedYouth.Position)
                    .OrderByDescending(p => p.Rating))
                    Mentors.Add(MentorOption.From(p));
            }

            SelectedMentor = SelectedYouth?.MentorId is int id ? Mentors.FirstOrDefault(m => m.Id == id) : null;
        }

        private void RebuildYouth()
        {
            if (_team is null)
                return;
            Youth.Clear();
            foreach (var y in _team.YouthPlayers.OrderByDescending(p => p.Talent))
                Youth.Add(y);
        }

        partial void OnSelectedYouthChanged(Player? value)
        {
            if (value is null || _team is null)
            {
                YouthInfo = string.Empty;
                RefreshMentorOptions();
                return;
            }

            YouthInfo = $"{value.Name} · {value.Age} J · {value.ShortPositionName} · Rating {value.Rating:0} · Talent {value.Talent}";
            RefreshMentorOptions();
            UpdateCurrentMentorInfo();
        }

        private void UpdateCurrentMentorInfo()
        {
            var current = SelectedYouth?.MentorId is int id ? Mentors.FirstOrDefault(m => m.Id == id) : null;
            HasMentor = current is not null;
            CurrentMentorInfo = current is null ? "Kein Mentor zugewiesen." : $"Aktueller Mentor: {current.Display}";
        }

        [RelayCommand]
        private async Task ShowProfile()
        {
            if (SelectedYouth is null)
                return;
            var seasonStats = _session.State is null
                ? null
                : await _saveGame.GetPlayerSeasonStatsAsync(SelectedYouth.Id, _session.State.Season);
            var careerStats = await _saveGame.GetPlayerCareerStatsAsync(SelectedYouth.Id);
            var competitionStats = await _saveGame.GetPlayerCompetitionBreakdownAsync(SelectedYouth.Id);
            SelectedProfile = PlayerProfile.From(SelectedYouth, seasonStats: seasonStats, careerStats: careerStats, competitionStats: competitionStats);
            IsPlayerProfileOpen = true;
        }

        [RelayCommand]
        private void CloseProfile() => IsPlayerProfileOpen = false;

        [RelayCommand]
        private async Task AssignMentor()
        {
            if (SelectedYouth is null)
                return;
            if (SelectedMentor is null)
            {
                StatusText = "Bitte zuerst einen Mentor auswählen.";
                return;
            }
            SelectedYouth.MentorId = SelectedMentor.Id;
            StatusText = $"Mentor: {SelectedMentor.Display} zugewiesen.";
            UpdateCurrentMentorInfo();
            await Confirm();
        }

        [RelayCommand]
        private void RemoveMentor()
        {
            if (SelectedYouth is null)
                return;
            SelectedYouth.MentorId = null;
            SelectedMentor = null;
            StatusText = "Mentor entfernt.";
            UpdateCurrentMentorInfo();
        }

        [RelayCommand]
        private void Promote()
        {
            if (SelectedYouth is null || _team is null)
                return;

            var youth = SelectedYouth;
            youth.IsYouthProspect = false;
            youth.MentorId = null;
            youth.Status = PlayerStatus.OnBench;
            _team.YouthPlayers.Remove(youth);
            _team.Players.Add(youth);

            RebuildYouth();
            SelectedYouth = Youth.FirstOrDefault();
            StatusText = $"{youth.Name} in die 1. Mannschaft hochgezogen.";
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
            StatusText = "Wird gespeichert …";
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save youth squad.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
