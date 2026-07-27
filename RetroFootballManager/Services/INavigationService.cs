namespace RetroFootballManager.Services
{
    public interface INavigationService
    {
        Task GoToAsync(string route);
        Task GoToRootAsync(string route);
        Task GoBackAsync();
    }
}
