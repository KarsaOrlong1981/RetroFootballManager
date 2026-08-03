using RetroFootballManager.Views.Controls;

namespace RetroFootballManager.Views
{
    // Every page derives from this instead of ContentPage so the custom
    // NavigationBar (back/title/window-chrome) appears uniformly everywhere
    // without each page having to wire it up itself.
    //
    // Note: on Windows, MAUI wraps any Shell.TitleView in an internal CommandBar
    // that always shows a "..." overflow button whenever a TitleView is set, with no
    // supported way to suppress it. Reparenting each page's Content into a wrapper
    // Grid on Loaded to avoid that was tried and reverted - it broke Picker population
    // timing and gesture/command bindings elsewhere (unrelated controls only fully
    // initialize once truly loaded in the final visual tree, and reparenting after the
    // fact re-triggers Loaded/Unloaded in ways that corrupt that). The "..." button is
    // the accepted trade-off for a stable app.
    public class BaseContentPage : ContentPage
    {
        public BaseContentPage()
        {
            Shell.SetNavBarIsVisible(this, true);
            Shell.SetTitleView(this, new NavigationBar());
            Shell.SetBackButtonBehavior(this, new BackButtonBehavior { IsVisible = false, IsEnabled = false });
        }
    }
}
