#if WINDOWS
using System.Runtime.InteropServices;
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
        private IntPtr _hwnd;
        private IntPtr _mauiWndProc;
        // Must be kept alive (a field, not a local) - the native side only holds the raw
        // function pointer (Marshal.GetFunctionPointerForDelegate), not a reference, so a
        // collected delegate would leave a dangling pointer.
        private WndProcDelegate? _crashGuardWndProc;

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const int GWLP_WNDPROC = -4;

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        // Guards against a known upstream MAUI bug (dotnet/maui #14518/#17041 and related):
        // certain native window messages make MAUI's own NavigationRootManager.
        // SetTitleBarVisibility call AppWindow.GetFromWindowId with a stale/invalid WindowId on
        // this unpackaged, custom-chrome window, throwing ArgumentException - which, thrown
        // directly from a native WNDPROC callback (no managed frame above it to catch it),
        // fails the whole process (confirmed: reliably crashes launched standalone, never
        // reproduces with a debugger attached - the debugger's overhead just changes timing
        // enough to avoid whatever triggers it, not an actual fix).
        //
        // MAUI installs its own WNDPROC (WindowMessageManager.NewWindowProc) on this window
        // sometime during window setup - installing ours AFTER that (same "first Activated"
        // timing already established below for EnterFullScreen - see its comment) means
        // GetWindowLongPtr returns MAUI's proc, which we save and chain to ourselves via
        // CallWindowProc wrapped in try/catch. A forward P/Invoke call that reenters managed
        // code (unlike Windows calling a WNDPROC directly) DOES let an exception thrown by the
        // callee propagate back to our catch, instead of crashing the process.
        private void InstallCrashGuardWndProc()
        {
            if (_hwnd == IntPtr.Zero || _crashGuardWndProc is not null)
                return;

            _mauiWndProc = GetWindowLongPtr(_hwnd, GWLP_WNDPROC);
            _crashGuardWndProc = CrashGuardWndProc;
            var newProcPtr = Marshal.GetFunctionPointerForDelegate(_crashGuardWndProc);
            SetWindowLongPtr(_hwnd, GWLP_WNDPROC, newProcPtr);
        }

        // Diagnostic logging during the original investigation (2026-09-06) showed the crash
        // is not tied to one specific message - WM_NCACTIVATE, WM_STYLECHANGING/CHANGED, and
        // plain WM_WINDOWPOSCHANGING each independently preceded a crash across different test
        // runs, all through the same NavigationRootManager.SetTitleBarVisibility call. So
        // instead of chasing individual message IDs (a losing game - any of these can trigger
        // it), block the whole family of "non-client/chrome negotiation" messages: they exist
        // for an app to react to pending activation/style/position/size *proposals* from
        // Windows, which is only ever relevant to code that manages its own title bar or
        // resizing - this window's chrome is permanently, manually fixed (EnterFullScreen's
        // SetBorderAndTitleBar(false,false) + IsResizable/IsMinimizable/IsMaximizable=false),
        // so MAUI's handler has nothing useful to do for any of them. Deliberately NOT
        // blocked: WM_WINDOWPOSCHANGED, WM_SIZE, WM_DPICHANGED - these report an
        // already-applied change rather than negotiate a pending one, and MAUI's content
        // layout may genuinely need them (e.g. the real initial-size handshake at startup).
        //
        // The actual root cause turned out to be a large, image-heavy CollectionView
        // triggering this ambient chrome-negotiation traffic far more often than smaller
        // pages ever do by chance - fixed at the source by switching that list to
        // BindableLayout (see TrainingPage.xaml), which doesn't have CollectionView's Windows
        // virtualization overhead. This blocklist is kept in place as a defensive safety net
        // for any other page/scenario that might still trigger the same underlying MAUI bug.
        private static readonly HashSet<uint> ChromeMessagesHandledDirectly = new()
        {
            0x0006, // WM_ACTIVATE
            0x001C, // WM_ACTIVATEAPP
            0x0086, // WM_NCACTIVATE
            0x0083, // WM_NCCALCSIZE
            0x0085, // WM_NCPAINT
            0x007C, // WM_STYLECHANGING
            0x007D, // WM_STYLECHANGED
            0x0046, // WM_WINDOWPOSCHANGING
            0x0024, // WM_GETMINMAXINFO
        };

        private IntPtr CrashGuardWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (ChromeMessagesHandledDirectly.Contains(msg))
                return DefWindowProc(hWnd, msg, wParam, lParam);

            try
            {
                return CallWindowProc(_mauiWndProc, hWnd, msg, wParam, lParam);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Swallowed exception from MAUI's window procedure (msg=0x{Msg:X}) to avoid a process crash.", msg);
                return DefWindowProc(hWnd, msg, wParam, lParam);
            }
        }

        public void Attach(Microsoft.UI.Xaml.Window nativeWindow)
        {
            _nativeWindow = nativeWindow;
            _hwnd = WindowNative.GetWindowHandle(nativeWindow);
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
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
                    InstallCrashGuardWndProc();
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
