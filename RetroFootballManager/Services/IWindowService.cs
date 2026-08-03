namespace RetroFootballManager.Services
{
    public interface IWindowService
    {
        void EnterFullScreen();
        void Minimize();
        void CloseApp();
        void HideCommandBarOverflow();
    }
}
