using RetroFootballManager.ViewModels;

namespace RetroFootballManager.Views.Controls
{
    public partial class NavigationBar : ContentView
    {
        public NavigationBar()
        {
            InitializeComponent();
            BindingContext = IPlatformApplication.Current!.Services.GetRequiredService<NavigationBarViewModel>();
        }
    }
}
