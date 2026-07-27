namespace RetroFootballManager.Services
{
    public class NavigationService : INavigationService
    {
        public Task GoToAsync(string route) =>
            Shell.Current.GoToAsync(route);

        public Task GoToRootAsync(string route) =>
            Shell.Current.GoToAsync($"//{route}");

        public Task GoBackAsync() =>
            Shell.Current.GoToAsync("..");
    }
}
