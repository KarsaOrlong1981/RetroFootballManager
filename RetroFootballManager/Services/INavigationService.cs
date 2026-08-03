namespace RetroFootballManager.Services
{
    public interface INavigationService
    {
        event EventHandler<ShellNavigatedEventArgs>? NavigationChanged;

        Task GoToAsync(string route);
        Task GoToRootAsync(string route);
        Task GoBackAsync();
    }
}
