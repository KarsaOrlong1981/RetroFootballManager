using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data.Repositories;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class StaffViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<StaffViewModel>();

        private readonly GameSession _session;
        private readonly StaffMarketService _staffMarket;
        private readonly TeamRepository _teamRepository;

        private Team? _team;

        public StaffViewModel(
            IDispatcher dispatcher, GameSession session, StaffMarketService staffMarket, TeamRepository teamRepository)
            : base(dispatcher)
        {
            _session = session;
            _staffMarket = staffMarket;
            _teamRepository = teamRepository;
            Title = "Mitarbeiter";
        }

        public ObservableCollection<Employee> CurrentStaff { get; } = [];
        public ObservableCollection<Employee> Candidates { get; } = [];

        [ObservableProperty] private string _statusText = string.Empty;

        // Own manager profile dialog ("Mein Profil").
        [ObservableProperty] private bool _isManagerProfileDialogOpen;
        [ObservableProperty] private ManagerProfile? _ownManagerProfile;
        [ObservableProperty] private int _managerRemainingPoints;

        partial void OnManagerRemainingPointsChanged(int value) => IncreaseManagerSkillCommand.NotifyCanExecuteChanged();

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            RefreshCurrentStaff();
        }

        private void RefreshCurrentStaff()
        {
            CurrentStaff.Clear();
            foreach (var employee in _team!.Employees)
                CurrentStaff.Add(employee);
        }

        [RelayCommand]
        private void GenerateCandidates()
        {
            if (_team is null)
                return;

            Candidates.Clear();
            foreach (var candidate in _staffMarket.GenerateCandidates(_team.LeagueTier))
                Candidates.Add(candidate);
        }

        [RelayCommand]
        private async Task Hire(Employee candidate)
        {
            if (_team is null || _session.State is null)
                return;

            if (!StaffMarketService.CanHire(_team, candidate.EmployeeType, out string? capacityError))
            {
                StatusText = capacityError!;
                return;
            }

            try
            {
                await _staffMarket.HireAsync(_team, candidate, _session.State.CurrentDate);
                Candidates.Remove(candidate);
                RefreshCurrentStaff();
                StatusText = $"{candidate.Name} eingestellt.";
            }
            catch (Exception ex)
            {
                Log.Error("Einstellung fehlgeschlagen.", ex);
                StatusText = "Einstellung fehlgeschlagen.";
            }
        }

        [RelayCommand]
        private async Task Fire(Employee employee)
        {
            if (_team is null)
                return;

            try
            {
                await _staffMarket.FireAsync(_team, employee);
                RefreshCurrentStaff();
                StatusText = $"{employee.Name} entlassen.";
            }
            catch (Exception ex)
            {
                Log.Error("Entlassung fehlgeschlagen.", ex);
                StatusText = "Entlassung fehlgeschlagen.";
            }
        }

        [RelayCommand]
        private void ShowManagerProfile()
        {
            if (_team?.ManagerProfile is null)
                return;

            OwnManagerProfile = _team.ManagerProfile;
            ManagerRemainingPoints = OwnManagerProfile.UnspentSkillPoints;
            IsManagerProfileDialogOpen = true;
            IncreaseManagerSkillCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task CloseManagerProfile()
        {
            IsManagerProfileDialogOpen = false;
            if (_team is null || OwnManagerProfile is null)
                return;

            OwnManagerProfile.UnspentSkillPoints = ManagerRemainingPoints;
            await _teamRepository.SaveTeamAsync(_team, includeYouth: false);
        }

        // Only spends UnspentSkillPoints left over from creation - no decrease/respec, since
        // that would let the already-fixed profile (or organically grown skills, see
        // ManagerGrowthService) be freely reshuffled after the fact.
        [RelayCommand(CanExecute = nameof(CanIncreaseManagerSkill))]
        private void IncreaseManagerSkill(string skill)
        {
            SetManagerSkill(skill, GetManagerSkill(skill) + 1);
            ManagerRemainingPoints--;
            OnPropertyChanged(nameof(OwnManagerProfile));
            IncreaseManagerSkillCommand.NotifyCanExecuteChanged();
        }

        private bool CanIncreaseManagerSkill(string skill)
        {
            if (OwnManagerProfile is null || ManagerRemainingPoints <= 0)
                return false;

            var (ceiling, _) = ManagerProfileGenerator.GetBudgetForLicense(OwnManagerProfile.License);
            return GetManagerSkill(skill) < ceiling;
        }

        private int GetManagerSkill(string skill) => skill switch
        {
            "TrainingDesign" => OwnManagerProfile!.TrainingDesign,
            "Motivation" => OwnManagerProfile!.Motivation,
            "OffensiveCreation" => OwnManagerProfile!.OffensiveCreation,
            "DefensiveOrganization" => OwnManagerProfile!.DefensiveOrganization,
            "InGameCoaching" => OwnManagerProfile!.InGameCoaching,
            _ => 0,
        };

        private void SetManagerSkill(string skill, int value)
        {
            switch (skill)
            {
                case "TrainingDesign": OwnManagerProfile!.TrainingDesign = value; break;
                case "Motivation": OwnManagerProfile!.Motivation = value; break;
                case "OffensiveCreation": OwnManagerProfile!.OffensiveCreation = value; break;
                case "DefensiveOrganization": OwnManagerProfile!.DefensiveOrganization = value; break;
                case "InGameCoaching": OwnManagerProfile!.InGameCoaching = value; break;
            }
        }
    }
}
