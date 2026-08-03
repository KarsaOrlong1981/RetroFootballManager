using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RetroFootballManager.Services;

namespace RetroFootballManager.ViewModels
{
    public partial class NavigationBarViewModel : ObservableObject
    {
        private readonly INavigationService _navigation;
        private readonly IWindowService _windowService;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private bool canGoBack;

        public NavigationBarViewModel(INavigationService navigation, IWindowService windowService)
        {
            _navigation = navigation;
            _windowService = windowService;
            _navigation.NavigationChanged += OnNavigationChanged;
        }

        private void OnNavigationChanged(object? sender, ShellNavigatedEventArgs e)
        {
            Title = RouteTitles.Resolve(e.Current?.Location?.OriginalString);
            CanGoBack = (Shell.Current?.Navigation.NavigationStack.Count ?? 0) > 1;
            _windowService.HideCommandBarOverflow();
        }

        [RelayCommand]
        private Task GoBack() => _navigation.GoBackAsync();

        [RelayCommand]
        private void Minimize() => _windowService.Minimize();

        [RelayCommand]
        private void CloseApp() => _windowService.CloseApp();
    }
}
