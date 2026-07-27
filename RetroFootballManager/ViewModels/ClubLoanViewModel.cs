using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Common;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Models;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class ClubLoanViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<ClubLoanViewModel>();

        public static readonly int[] AmountPresets = [50_000, 100_000, 250_000, 500_000, 1_000_000];
        public static readonly int[] TermPresets = [12, 24, 36];

        public List<int> AmountOptions { get; } = [.. AmountPresets];
        public List<int> TermOptions { get; } = [.. TermPresets];

        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        private Team? _team;

        public ClubLoanViewModel(IDispatcher dispatcher, GameSession session, SaveGameService saveGame, INavigationService navigation)
            : base(dispatcher)
        {
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Kredit aufnehmen";
        }

        [ObservableProperty] private int _selectedAmount = AmountPresets[0];
        [ObservableProperty] private int _selectedTerm = TermPresets[1];
        [ObservableProperty] private string _maxAmountText = string.Empty;
        [ObservableProperty] private string _monthlyPaymentPreviewText = string.Empty;
        [ObservableProperty] private string _interestRateText = string.Empty;
        [ObservableProperty] private string _statusText = string.Empty;

        public void Initialize()
        {
            _team = _session.ManagerTeam;
            if (_team is null)
                return;

            MaxAmountText = $"Maximal möglich: {ClubLoanService.GetMaxLoanAmount(_team):N0} €";
            if (SelectedAmount > ClubLoanService.GetMaxLoanAmount(_team))
                SelectedAmount = ClubLoanService.GetMaxLoanAmount(_team);

            UpdatePreview();
        }

        partial void OnSelectedAmountChanged(int value) => UpdatePreview();
        partial void OnSelectedTermChanged(int value) => UpdatePreview();

        private void UpdatePreview()
        {
            double rate = ClubLoanService.GetRateForTerm(SelectedTerm);
            int payment = ClubLoanService.CalculateMonthlyPayment(SelectedAmount, rate, SelectedTerm);
            InterestRateText = $"Zinssatz: {rate:0.#}% p.a.";
            MonthlyPaymentPreviewText = $"Monatliche Rate: {payment:N0} €";
        }

        [RelayCommand]
        private async Task Confirm()
        {
            if (_team is null || _session.State is null)
            {
                StatusText = "Kein aktives Spiel - konnte nicht aufgenommen werden.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            try
            {
                bool ok = ClubLoanService.TryTakeLoan(_team, SelectedAmount, SelectedTerm, _session.State.CurrentDate, out string? error);
                if (!ok)
                {
                    StatusText = error ?? "Kredit konnte nicht aufgenommen werden.";
                    return;
                }

                await _saveGame.SaveTeamProgressAsync(_session.State, _team);
                await _navigation.GoBackAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to take out loan.", ex);
                StatusText = "Speichern fehlgeschlagen.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task Cancel() => _navigation.GoBackAsync();
    }
}
