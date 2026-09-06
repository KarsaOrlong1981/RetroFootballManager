using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace RetroFootballManager.WinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : MauiWinUIApplication
    {
        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += (s, e) =>
            {
                // Known upstream MAUI bug (dotnet/maui #14518/#17041): a native window-style
                // message can make MAUI's own NavigationRootManager.SetTitleBarVisibility call
                // AppWindow.GetFromWindowId with a stale/invalid WindowId on this unpackaged,
                // custom-chrome (border/title-bar hidden) window, throwing ArgumentException.
                // Harmless to swallow here - we've already permanently hidden the title bar
                // ourselves (WindowService.EnterFullScreen), so MAUI's own attempt to
                // (re-)apply title-bar visibility has nothing useful left to do anyway.
                bool isKnownTitleBarBug = e.Exception is ArgumentException
                    && e.Exception.StackTrace?.Contains("SetTitleBarVisibility") == true;
                if (isKnownTitleBarBug)
                {
                    e.Handled = true;
                    Serilog.Log.Warning(e.Exception, "Swallowed known MAUI SetTitleBarVisibility bug.");
                    return;
                }

                Serilog.Log.Fatal(e.Exception, "Unhandled WinUI exception.");
            };
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }

}
