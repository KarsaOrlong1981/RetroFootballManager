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
    public record DifficultyChoice(Difficulty Value, string Label);

    public partial class OptionsViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<OptionsViewModel>();

        private static readonly string[] SpeedLabels = ["Sehr langsam", "Langsam", "Normal", "Schnell", "Ultra"];

        private readonly AppSettingsService _appSettings;
        private readonly CareerService _career;
        private readonly GameSession _session;
        private readonly SaveGameService _saveGame;
        private readonly INavigationService _navigation;

        public OptionsViewModel(
            IDispatcher dispatcher,
            AppSettingsService appSettings,
            CareerService career,
            GameSession session,
            SaveGameService saveGame,
            INavigationService navigation)
            : base(dispatcher)
        {
            _appSettings = appSettings;
            _career = career;
            _session = session;
            _saveGame = saveGame;
            _navigation = navigation;
            Title = "Optionen";
        }

        [ObservableProperty] private double _defaultMatchSpeed;
        [ObservableProperty] private string _defaultMatchSpeedLabel = string.Empty;
        [ObservableProperty] private bool _isCareerActive;
        [ObservableProperty] private DifficultyChoice? _selectedDifficulty;
        [ObservableProperty] private bool _isResetCareerConfirmationOpen;
        [ObservableProperty] private string _statusText = string.Empty;

        public ObservableCollection<DifficultyChoice> DifficultyChoices { get; } =
        [
            new(Difficulty.Easy, "Leicht"),
            new(Difficulty.Normal, "Normal"),
            new(Difficulty.Hard, "Schwer"),
        ];

        public void Initialize()
        {
            DefaultMatchSpeed = _appSettings.DefaultMatchSpeed;

            IsCareerActive = _session.State is not null;
            if (IsCareerActive)
                SelectedDifficulty = DifficultyChoices.FirstOrDefault(d => d.Value == _session.State!.Difficulty);
        }

        partial void OnDefaultMatchSpeedChanged(double value)
        {
            int index = Math.Clamp((int)Math.Round(value), 0, SpeedLabels.Length - 1);
            DefaultMatchSpeedLabel = SpeedLabels[index];
            _appSettings.DefaultMatchSpeed = index;
        }

        partial void OnSelectedDifficultyChanged(DifficultyChoice? value)
        {
            if (value is null || _session.State is null)
                return;

            _session.State.Difficulty = value.Value;
            _ = _saveGame.SaveStateAsync(_session.State);
            StatusText = $"Schwierigkeitsgrad: {value.Label}";
        }

        [RelayCommand]
        private void RequestResetCareer() => IsResetCareerConfirmationOpen = true;

        [RelayCommand]
        private void CancelResetCareer() => IsResetCareerConfirmationOpen = false;

        [RelayCommand]
        private async Task ConfirmResetCareer()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _career.ResetAsync();
                StatusText = "Karriere-Fortschritt zurückgesetzt.";
            }
            catch (Exception ex)
            {
                Log.Error("Failed to reset career.", ex);
                StatusText = "Zurücksetzen fehlgeschlagen.";
            }
            finally
            {
                IsResetCareerConfirmationOpen = false;
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task Close() => _navigation.GoBackAsync();
    }
}
