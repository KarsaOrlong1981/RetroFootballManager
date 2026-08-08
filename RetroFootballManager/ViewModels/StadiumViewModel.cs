using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class StadiumViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<StadiumViewModel>();

        private const int SeatUpgradeStep = 1_000;
        private const int StandingUpgradeStep = 500;
        private const int LogeUpgradeStep = 50;

        private static readonly string[] EvolutionImages =
        [
            "hintergrund_stadion_evo0_minimal.jpg",
            "hintergrund_stadion_evo1.jpg",
            "hintergrund_stadion_evo2.jpg",
            "hintergrund_stadion_evo3.jpg",
            "hintergrund_stadion_evo4.jpg",
            "hintergrund_stadion_evo5.jpg",
            "hintergrund_stadion_evo6_ultimativ.jpg",
        ];

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;

        public StadiumViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Stadion";
        }

        [ObservableProperty] private string _stadiumName = string.Empty;
        [ObservableProperty] private string _budgetText = string.Empty;
        [ObservableProperty] private string _capacitySummary = string.Empty;
        [ObservableProperty] private string _attendanceEstimateText = string.Empty;

        [ObservableProperty] private double _seatPrice;
        [ObservableProperty] private double _standingPrice;
        [ObservableProperty] private double _logePrice;

        [ObservableProperty] private string _seatUpgradeCostText = string.Empty;
        [ObservableProperty] private string _standingUpgradeCostText = string.Empty;
        [ObservableProperty] private string _logeUpgradeCostText = string.Empty;
        [ObservableProperty] private string _roofCostText = string.Empty;
        [ObservableProperty] private bool _hasRoof;

        [ObservableProperty] private string _comfortLevelText = string.Empty;
        [ObservableProperty] private string _cateringLevelText = string.Empty;
        [ObservableProperty] private string _merchandiseLevelText = string.Empty;
        [ObservableProperty] private string _infrastructureLevelText = string.Empty;
        [ObservableProperty] private string _comfortUpgradeCostText = string.Empty;
        [ObservableProperty] private string _cateringUpgradeCostText = string.Empty;
        [ObservableProperty] private string _merchandiseUpgradeCostText = string.Empty;
        [ObservableProperty] private string _infrastructureUpgradeCostText = string.Empty;

        [ObservableProperty] private string _statusText = string.Empty;
        [ObservableProperty] private string _backgroundImageSource = EvolutionImages[0];

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team?.Stadium is null)
                return;

            RefreshFromStadium();
        }

        private void RefreshFromStadium()
        {
            var stadium = _team!.Stadium!;

            StadiumName = stadium.Name;
            BudgetText = _team.Finances is not null ? $"{_team.Finances.CurrentBalance:N0} €" : "–";
            CapacitySummary = $"{stadium.Capacity:N0} Plätze " +
                $"(Sitz {stadium.SeatingCapacity:N0} · Steh {stadium.StandingCapacity:N0} · Loge {stadium.LogeCapacity:N0})";
            BackgroundImageSource = EvolutionImages[StadiumService.GetEvolutionStage(stadium.Capacity)];
            HasRoof = stadium.HasRoof;

            SeatPrice = stadium.SeatPrice;
            StandingPrice = stadium.StandingPrice;
            LogePrice = stadium.LogePrice;

            ComfortLevelText = $"Komfort: Stufe {stadium.ComfortLevel}/5";
            CateringLevelText = $"Gastronomie: Stufe {stadium.CateringLevel}/5";
            MerchandiseLevelText = $"Merchandise: Stufe {stadium.MerchandiseLevel}/5";
            InfrastructureLevelText = $"Infrastruktur: Stufe {stadium.InfrastructureLevel}/5";

            SeatUpgradeCostText = $"{StadiumService.GetSeatingUpgradeCost(stadium, SeatUpgradeStep).Amount:N0} €";
            StandingUpgradeCostText = $"{StadiumService.GetStandingUpgradeCost(stadium, StandingUpgradeStep).Amount:N0} €";
            LogeUpgradeCostText = $"{StadiumService.GetLogeUpgradeCost(stadium, LogeUpgradeStep).Amount:N0} €";
            RoofCostText = $"{StadiumService.GetRoofCost(stadium).Amount:N0} €";
            ComfortUpgradeCostText = $"{StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Comfort).Amount:N0} €";
            CateringUpgradeCostText = $"{StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Catering).Amount:N0} €";
            MerchandiseUpgradeCostText = $"{StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Merchandise).Amount:N0} €";
            InfrastructureUpgradeCostText = $"{StadiumService.GetLevelUpgradeCost(stadium, StadiumUpgradeKind.Infrastructure).Amount:N0} €";

            UpdateAttendanceEstimate();
        }

        private void UpdateAttendanceEstimate()
        {
            var stadium = _team!.Stadium!;
            double baselinePrice = 10 + (4 - _team.LeagueTier) * 8;
            var estimate = AttendanceModel.Calculate(stadium, recentFormPoints0to15: 8, leaguePosition: 9, leagueSize: 18, opponentTierRank: 2, baselinePrice);
            AttendanceEstimateText = $"Geschätzter Zuschauerschnitt: {estimate.TotalAttendance:N0} ({estimate.AvgFillRate:P0} Auslastung)";
        }

        partial void OnSeatPriceChanged(double value)
        {
            if (_team?.Stadium is null) return;
            _team.Stadium.SeatPrice = value;
            UpdateAttendanceEstimate();
        }

        partial void OnStandingPriceChanged(double value)
        {
            if (_team?.Stadium is null) return;
            _team.Stadium.StandingPrice = value;
            UpdateAttendanceEstimate();
        }

        partial void OnLogePriceChanged(double value)
        {
            if (_team?.Stadium is null) return;
            _team.Stadium.LogePrice = value;
            UpdateAttendanceEstimate();
        }

        // These four grow the stadium's capacity - "Stadionausbau" for club mood purposes.
        // Comfort/Catering/Merchandise/Infrastructure below are quality upgrades, not expansion.
        [RelayCommand]
        private void UpgradeSeating() => TryExpansionUpgrade(
            StadiumService.GetSeatingUpgradeCost(_team!.Stadium!, SeatUpgradeStep).Amount,
            s => StadiumService.ApplySeatingUpgrade(s, SeatUpgradeStep));

        [RelayCommand]
        private void UpgradeStanding() => TryExpansionUpgrade(
            StadiumService.GetStandingUpgradeCost(_team!.Stadium!, StandingUpgradeStep).Amount,
            s => StadiumService.ApplyStandingUpgrade(s, StandingUpgradeStep));

        [RelayCommand]
        private void UpgradeLoge() => TryExpansionUpgrade(
            StadiumService.GetLogeUpgradeCost(_team!.Stadium!, LogeUpgradeStep).Amount,
            s => StadiumService.ApplyLogeUpgrade(s, LogeUpgradeStep));

        [RelayCommand]
        private void BuildRoof()
        {
            if (_team?.Stadium?.HasRoof == true)
            {
                StatusText = "Das Stadion ist bereits überdacht.";
                return;
            }

            TryExpansionUpgrade(StadiumService.GetRoofCost(_team!.Stadium!).Amount, StadiumService.ApplyRoof);
        }

        private void TryExpansionUpgrade(double cost, Action<Stadium> upgrade)
        {
            if (TryUpgrade(cost, upgrade) && _team is not null)
                ClubMoodService.ApplyStadiumExpansion(_team);
        }

        [RelayCommand]
        private void UpgradeComfort() => TryUpgrade(
            StadiumService.GetLevelUpgradeCost(_team!.Stadium!, StadiumUpgradeKind.Comfort).Amount,
            s => StadiumService.ApplyLevelUpgrade(s, StadiumUpgradeKind.Comfort));

        [RelayCommand]
        private void UpgradeCatering() => TryUpgrade(
            StadiumService.GetLevelUpgradeCost(_team!.Stadium!, StadiumUpgradeKind.Catering).Amount,
            s => StadiumService.ApplyLevelUpgrade(s, StadiumUpgradeKind.Catering));

        [RelayCommand]
        private void UpgradeMerchandise() => TryUpgrade(
            StadiumService.GetLevelUpgradeCost(_team!.Stadium!, StadiumUpgradeKind.Merchandise).Amount,
            s => StadiumService.ApplyLevelUpgrade(s, StadiumUpgradeKind.Merchandise));

        [RelayCommand]
        private void UpgradeInfrastructure() => TryUpgrade(
            StadiumService.GetLevelUpgradeCost(_team!.Stadium!, StadiumUpgradeKind.Infrastructure).Amount,
            s => StadiumService.ApplyLevelUpgrade(s, StadiumUpgradeKind.Infrastructure));

        private bool TryUpgrade(double cost, Action<Stadium> upgrade)
        {
            if (_team is null)
                return false;

            bool applied = StadiumService.TryApplyUpgrade(_team, upgrade, cost);
            StatusText = applied
                ? "Ausbau durchgeführt."
                : "Nicht genug Geld für diesen Ausbau.";

            if (applied)
                RefreshFromStadium();

            return applied;
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
            StatusText = "Stadion wird gespeichert …";
            try
            {
                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to save stadium.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
