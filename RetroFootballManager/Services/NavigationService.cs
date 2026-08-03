namespace RetroFootballManager.Services
{
    public class NavigationService : INavigationService
    {
        public event EventHandler<ShellNavigatedEventArgs>? NavigationChanged;

        public NavigationService()
        {
            // AppShell.Instance is set before any page (and thus any ViewModel needing
            // this service) is created, so the subscription is always safe here.
            AppShell.Instance!.Navigated += (s, e) => NavigationChanged?.Invoke(this, e);
        }

        public Task GoToAsync(string route) =>
            Shell.Current.GoToAsync(route);

        public Task GoToRootAsync(string route) =>
            Shell.Current.GoToAsync($"//{route}");

        public Task GoBackAsync() =>
            Shell.Current.GoToAsync("..");
    }
}
