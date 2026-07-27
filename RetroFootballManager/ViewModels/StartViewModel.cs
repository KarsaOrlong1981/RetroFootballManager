using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Data;
using RetroFootballManager.Logging;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class StartViewModel : BaseViewModel
    {
        private static readonly ILog Log = LogManager.GetLogger<StartViewModel>();

        private readonly SaveGameService _saveGame;
        private readonly GameSession _session;
        private readonly INavigationService _navigation;

        public StartViewModel(
            IDispatcher dispatcher,
            SaveGameService saveGame,
            GameSession session,
            INavigationService navigation)
            : base(dispatcher)
        {
            _saveGame = saveGame;
            _session = session;
            _navigation = navigation;
            Title = "RetroFootballManager";
        }

        [ObservableProperty]
        private bool _hasSaveGame;

        [ObservableProperty]
        private bool _isDeleteConfirmationOpen;

        [ObservableProperty]
        private bool _isOverwriteConfirmationOpen;

        public bool HasNoSaveGame => !HasSaveGame;

        partial void OnHasSaveGameChanged(bool value) => OnPropertyChanged(nameof(HasNoSaveGame));

        public async Task InitializeAsync()
        {
            try
            {
                await _saveGame.InitializeAsync();
                HasSaveGame = await _saveGame.HasSaveGameAsync();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to initialize start screen.", ex);
            }
        }

        [RelayCommand]
        private Task NewGame()
        {
            if (HasSaveGame)
            {
                IsOverwriteConfirmationOpen = true;
                return Task.CompletedTask;
            }
            return _navigation.GoToAsync("teamselection");
        }

        [RelayCommand]
        private Task ConfirmOverwrite()
        {
            IsOverwriteConfirmationOpen = false;
            return _navigation.GoToAsync("teamselection");
        }

        [RelayCommand]
        private void CancelOverwrite() => IsOverwriteConfirmationOpen = false;

        [RelayCommand]
        private void DeleteSave() => IsDeleteConfirmationOpen = true;

        [RelayCommand]
        private void CancelDelete() => IsDeleteConfirmationOpen = false;

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                await _saveGame.DeleteSaveAsync();
                HasSaveGame = false;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to delete save game.", ex);
            }
            finally
            {
                IsDeleteConfirmationOpen = false;
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task Continue()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var loaded = await _saveGame.LoadGameAsync();
                if (loaded is null)
                {
                    HasSaveGame = false;
                    return;
                }

                _session.State = loaded.Value.State;
                _session.Teams = loaded.Value.Teams;
                await _navigation.GoToRootAsync("mainmenu");
            }
            catch (Exception ex)
            {
                Log.Error("Failed to load save game.", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task Load() => Continue();

        [RelayCommand]
        private Task Options() => _navigation.GoToAsync("options");
    }
}
