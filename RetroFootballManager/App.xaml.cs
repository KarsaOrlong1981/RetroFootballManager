using RetroFootballManager.Services;

namespace RetroFootballManager
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

#if WINDOWS
            window.HandlerChanged += (s, e) =>
            {
                if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow) return;

                try
                {
                    var windowService = (RetroFootballManager.WinUI.WindowService)IPlatformApplication.Current!.Services.GetRequiredService<IWindowService>();
                    windowService.Attach(nativeWindow);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Failed to attach WindowService / enter fullscreen.");
                }
            };
#endif

            return window;
        }
    }
}