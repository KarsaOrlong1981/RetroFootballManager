#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetroFootballManager.Services;
using WinRT.Interop;

namespace RetroFootballManager.WinUI
{
    // Unpackaged Windows app -> AppWindow must be resolved via the native HWND
    // (no packaged-app activation shortcuts available).
    //
    // Note: AppWindowPresenterKind.FullScreen renders unreliably in this MAUI/WinUI
    // combination (blank white window, or the window vanishing entirely), so
    // "fullscreen" here means: a maximized Overlapped window with its border/title bar
    // hidden via SetBorderAndTitleBar(false, false) instead of a true FullScreen presenter.
    public class WindowService : IWindowService
    {
        private AppWindow? _appWindow;
        private bool _hasEnteredFullScreenOnce;
        private bool _wasMinimized;
        private Microsoft.UI.Xaml.Window? _nativeWindow;

        public void Attach(Microsoft.UI.Xaml.Window nativeWindow)
        {
            _nativeWindow = nativeWindow;
            var hwnd = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            nativeWindow.Activated += OnActivated;
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            try
            {
                // MAUI's own WinUI window setup runs after CreateWindow/HandlerChanged and
                // resets window/title-bar state, so applying any of this there gets silently
                // overwritten. Applying it on the first Activated (after MAUI's setup has
                // already run) sticks reliably instead.
                if (!_hasEnteredFullScreenOnce)
                {
                    _hasEnteredFullScreenOnce = true;

                    if (_nativeWindow is not null)
                    {
                        _nativeWindow.Title = string.Empty;
                    }
                    if (_appWindow is not null)
                    {
                        _appWindow.Title = string.Empty;
                    }

                    EnterFullScreen();
                    HideCommandBarOverflow();
                    return;
                }

                // Restoring from the taskbar re-maximizes via normal Windows semantics,
                // which respects the work area (leaves the taskbar visible) instead of the
                // borderless full-monitor coverage we set up initially. Re-apply just the
                // maximize - NOT the border/title-bar/button style changes: those style
                // changes fire native WM_STYLECHANGING messages that MAUI's own title-bar
                // handling reacts to (NavigationRootManager.SetTitleBarVisibility ->
                // AppWindow.GetFromWindowId), which throws ArgumentException on this window
                // and crashes the process. Since the border/button styles are still applied
                // from the first EnterFullScreen() call, only re-maximizing is needed here.
                if (_wasMinimized && _appWindow?.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Minimized } op)
                {
                    _wasMinimized = false;
                    op.Maximize();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to set up window chrome on activation.");
            }
        }

        public void EnterFullScreen()
        {
            // A true AppWindowPresenterKind.FullScreen presenter renders unreliably in
            // this MAUI/WinUI combination (blank white window, or the window vanishing
            // entirely with SetDragRectangles applied first). SetBorderAndTitleBar(false,
            // false) + Maximize() on the OverlappedPresenter is the community-verified
            // substitute (see blog.verslu.is MAUI Windows fullscreen article) - it removes
            // the border/title bar at a lower level than ExtendsContentIntoTitleBar, which
            // still left a residual native caption sliver in this app.
            if (_appWindow?.Presenter is OverlappedPresenter op)
            {
                op.SetBorderAndTitleBar(false, false);
                op.IsMinimizable = false;
                op.IsMaximizable = false;
                op.IsResizable = false;
                op.Maximize();

                // With no icon/title bar, Windows adds a fallback "..." system-menu button
                // (Move/Size/Minimize/Close via tooltip "Weitere Informationen" etc.) -
                // this is the actual switch that removes it.
                if (_appWindow is not null)
                {
                    _appWindow.TitleBar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
                }
            }
        }

        public void Minimize()
        {
            if (_appWindow?.Presenter is OverlappedPresenter op)
            {
                _wasMinimized = true;
                op.Minimize();
            }
        }

        public void CloseApp() => _appWindow?.Destroy();

        public void HideCommandBarOverflow()
        {
            // MAUI wraps Shell.TitleView in an internal CommandBar on Windows that always
            // shows a "..." overflow button whenever a TitleView is set (no MAUI-level
            // property to suppress it). Reaching into the native visual tree and toggling
            // the real WinUI CommandBar.OverflowButtonVisibility is the only supported hook.
            if (_nativeWindow?.Content is null) return;

            try
            {
                CollapseCommandBarOverflow(_nativeWindow.Content);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to collapse CommandBar overflow button.");
            }
        }

        private static void CollapseCommandBarOverflow(DependencyObject node)
        {
            if (node is CommandBar commandBar)
            {
                commandBar.OverflowButtonVisibility = CommandBarOverflowButtonVisibility.Collapsed;
                commandBar.DefaultLabelPosition = CommandBarDefaultLabelPosition.Collapsed;
                commandBar.IsDynamicOverflowEnabled = false;
            }

            // Setting OverflowButtonVisibility alone still left a sliver clickable, and
            // clicking through it could trigger navigation with no save prompt - collapse
            // AND disable the actual named template part directly as a hard safety net.
            if (node is FrameworkElement { Name: "MoreButton" } moreButton)
            {
                moreButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                if (moreButton is Control control)
                {
                    control.IsEnabled = false;
                }
            }

            var childCount = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < childCount; i++)
            {
                CollapseCommandBarOverflow(VisualTreeHelper.GetChild(node, i));
            }
        }
    }
}
#endif
