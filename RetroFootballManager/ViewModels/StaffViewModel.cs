using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
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

        private Team? _team;

        public StaffViewModel(IDispatcher dispatcher, GameSession session, StaffMarketService staffMarket)
            : base(dispatcher)
        {
            _session = session;
            _staffMarket = staffMarket;
            Title = "Mitarbeiter";
        }

        public ObservableCollection<Employee> CurrentStaff { get; } = [];
        public ObservableCollection<Employee> Candidates { get; } = [];

        [ObservableProperty] private string _statusText = string.Empty;

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
    }
}
