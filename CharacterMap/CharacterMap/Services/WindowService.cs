using CharacterMap.Views;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Services;

public class FacadeLaunchArgs : ILaunchActivatedEventArgs
{
    public ActivationKind Kind => ActivationKind.Launch;

    public ApplicationExecutionState PreviousExecutionState => ApplicationExecutionState.Running;

    public SplashScreen SplashScreen => null;

    public string Arguments => string.Empty;

    public string TileId => string.Empty;
}

public interface IWindowContent
{
    void Cleanup();
}

public class WindowInformation
{
    private WindowInformation(CoreApplicationView coreView, ApplicationView view)
    {
        CoreView = coreView;
        View = view;
        Manager = ViewLifetimeManager.CreateForCurrentView();
    }

    public static WindowInformation CreateForCurrentView()
    {
        return new WindowInformation(
            CoreApplication.GetCurrentView(),
            ApplicationView.GetForCurrentView());
    }

    public CoreApplicationView CoreView { get; }

    public ApplicationView View { get; }

    public ViewLifetimeManager Manager { get; }
}

public static class WindowService
{
    /*
     * Our multi-window logic is based upon that of the Windows Photo app.
     * 
     * 1 - The first view to open, whether it is the main window or a secondary
     *     window claims the MainView.
     * 2 - If the next new view is a secondary window, it opens it's own view.
     * 3 - If the next new view is the *main* window, it replaces the view originally 
     *     created in step 1. That original view is now killed off.
     */

    public static WindowInformation MainWindow { get; private set; }

    private static Dictionary<int, WindowInformation> _childWindows { get; } = new Dictionary<int, WindowInformation>();

    public static bool HasWindows => MainWindow != null || _childWindows.Count > 0;

    public static void AddChildWindow(WindowInformation information)
    {
        if (!information.CoreView.IsMain)
            _childWindows.Add(information.View.Id, information);
    }

    public static async Task<WindowInformation> CreateViewAsync(Action a, bool mainView)
    {
        WindowInformation info = null;
        CoreApplicationView view = null;
        if (mainView)
        {
            view = CoreApplication.MainView;
            view.CoreWindow.SizeChanged -= Current_SizeChanged;
            view.CoreWindow.SizeChanged += Current_SizeChanged;
        }
        else
        {
            TaskCompletionSource<CoreApplicationView> tcs = new();

            await CoreApplication.MainView.Dispatcher.ExecuteAsync(() =>
            {
                if (CoreApplication.MainView.Dispatcher.HasThreadAccess &&
                    CoreApplication.MainView.Properties.ContainsKey(nameof(MainWindow)))
                    tcs.SetResult(CoreApplication.CreateNewView());
                else
                    tcs.SetResult(CoreApplication.MainView);
            });

            view = await tcs.Task;
        }

        void Create()
        {
            a();
            info = WindowInformation.CreateForCurrentView();

            if (!mainView)
                info.View.SetDesiredBoundsMode(ApplicationViewBoundsMode.UseVisible);

            if (view != CoreApplication.MainView)
            {
                info.Manager.StartViewInUse();
                info.Manager.Released += Manager_Released;
                AddChildWindow(info);
            }
            else
            {
                view.Properties[nameof(MainWindow)] = info;

                if (mainView)
                    MainWindow = info;
            }

            var settings = ResourceHelper.AppSettings;
            settings.UpdateTransparency(settings.IsTransparencyEnabled);
        }

        void DoCreate()
        {
            Create();
            if (Window.Current.Content is FrameworkElement f)
                f.RequestedTheme = ResourceHelper.GetEffectiveTheme();
        }

        if (view.Dispatcher.HasThreadAccess)
            DoCreate();
        else
            await view.Dispatcher.ExecuteAsync(DoCreate, CoreDispatcherPriority.High);

        return info;
    }

    private static void Manager_Released(object sender, EventArgs e)
    {
        ViewLifetimeManager manager = (ViewLifetimeManager)sender;
        manager.Released -= Manager_Released;

        WindowInformation info = _childWindows[manager.Id];
        _childWindows.Remove(manager.Id);

        info.CoreView.Activated -= CoreView_Activated;

        info.CoreView.DispatcherQueue.ShutdownStarting += (s, args) =>
        {
            var deferral = args.GetDeferral();
            var finalizer = new Thread(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                deferral.Complete();
            });
            finalizer.Start();
        };

        info.CoreView.Dispatcher.ExecuteAsync(async () =>
        {
            info.CoreView.CoreWindow.GetMessenger().Reset();

            if (Window.Current.Content is IWindowContent c)
                c.Cleanup();

            if (Window.Current.Content is UserControl page)
                page.Content = null;

            Window.Current.Content = null;

            // Await GC on background thread while UI thread message loop is alive & pumping
            await Task.Run(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            info.CoreView.CoreWindow.CustomProperties.Clear();
            Composition.ReleaseCache(Window.Current.Compositor);

            // Allow dispatcher to process all marshaled COM releases
            await Task.Delay(250);

            GC.Collect();

            CoreWindowCleanupHelper.PrepareForClose(Window.Current.CoreWindow);
            Window.Current.Close();
        });
    }

    private static void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        if (sender is CoreWindow window)
            window.GetMessenger().Send(new WindowResizingMessage(window.Dispatcher));
    }

    public static async Task ReactivateMainAsync()
    {
        if (!CoreApplication.MainView.Dispatcher.HasThreadAccess)
        {
            // Awaiter here is screwed without a *PROPER* dispatcher awaiter
            await CoreApplication.MainView.Dispatcher.ExecuteAsync(
                () => ReactivateMainAsync());

            await Task.Delay(250);
            return;
        }

        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(WindowService.MainWindow.View.Id);
        await Task.Delay(100);
        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(WindowService.MainWindow.View.Id);
        await Task.Delay(100);
        WindowService.MainWindow.CoreView.CoreWindow.Activate();

        WindowService.MainWindow.CoreView.CoreWindow.SizeChanged -= Current_SizeChanged;
        WindowService.MainWindow.CoreView.CoreWindow.SizeChanged += Current_SizeChanged;

    }

    public static async Task TrySwitchToWindowAsync(WindowInformation info, bool main)
    {
        if (main && !CoreApplication.MainView.Dispatcher.HasThreadAccess)
        {
            // Awaiter here is screwed without a *PROPER* dispatcher awaiter
            await CoreApplication.MainView.Dispatcher.ExecuteAsync(
                () => TrySwitchToWindowAsync(info, main));

            return;
        }

        if (main && CoreApplication.MainView.CoreWindow.Visible)
        {
            await ApplicationViewSwitcher.SwitchAsync(
                info.View.Id,
                ((WindowInformation)CoreApplication.MainView.Properties[nameof(MainWindow)]).View.Id,
                ApplicationViewSwitchingOptions.ConsolidateViews).AsTask();

            return;
        }

        if (info == MainWindow)
            await ActivateMainWindowAsync();

        //await info.CoreView.Dispatcher.ExecuteAsync(() =>
        //{
        //    info.CoreView.Activated -= CoreView_Activated;
        //    info.CoreView.Activated += CoreView_Activated;
        //});

        var view = CoreApplication.Views.FirstOrDefault(v => v != CoreApplication.MainView) ?? CoreApplication.MainView;
        await view.Dispatcher.ExecuteAsync(async () =>
        {
            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(info.View.Id, ViewSizePreference.Default);
        });
    }

    private static void CoreView_Activated(CoreApplicationView sender, IActivatedEventArgs args)
    {
        sender.Activated -= CoreView_Activated;
        sender.CoreWindow.Dispatcher.ExecuteAsync(async () =>
        {
            sender.CoreWindow.SizeChanged -= Current_SizeChanged;
            sender.CoreWindow.SizeChanged += Current_SizeChanged;
            await ApplicationViewSwitcher.SwitchAsync(ApplicationView.GetForCurrentView().Id);
        });
    }

    public static Task ActivateMainWindowAsync()
    {
        return CoreApplication.MainView.Dispatcher.ExecuteAsync(async () =>
        {
            await App.Current.ActivationService.ActivateAsync(new FacadeLaunchArgs());
        });
    }

    internal static void CloseForCurrentView()
    {
        var view = CoreApplication.GetCurrentView();

        CoreApplication.MainView.Dispatcher.Enqueue(() =>
        {
            if (CoreApplication.Views.Count == 1)
                Application.Current.Exit();
            else if (CoreApplication.Views.Count == 2 && !CoreApplication.MainView.CoreWindow.Visible)
                Application.Current.Exit();
            else
            {
                view.Dispatcher.Enqueue(Window.Current.Close, CoreDispatcherPriority.High);
            }
        }, CoreDispatcherPriority.High);
    }

    public static Task RunOnViewsAsync(DispatchedHandler a)
    {
        List<Task> t = new();

        if (MainWindow != null)
            t.Add(MainWindow.CoreView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, a).AsTask());

        List<Task> tasks = new();
        foreach (var window in _childWindows.ToList())
        {
            try
            {
                tasks.Add(window.Value.CoreView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, a).AsTask());
            }
            catch (Exception ex)
            {
                Utils.AppendDiagnostics("WindowService RunOnViewsAsync", ex);
                _childWindows.Remove(window.Key);
            }
        }

        t.AddRange(tasks);
        return Task.WhenAll(t);
    }
}

// Workaround for Windowing bug noted in 
// https://github.com/microsoft/microsoft-ui-xaml/issues/12384
public static class CoreWindowCleanupHelper
{
    private const int GWLP_WNDPROC = -4;
    private const uint WM_NULL = 0x0000;

    [ComImport]
    [Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICoreWindowInterop
    {
        IntPtr WindowHandle { get; }
        bool MessageHandled { set; }
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [ThreadStatic]
    private static WndProcDelegate _hookProc;

    [ThreadStatic]
    private static IntPtr _prevWndProc;

    [ThreadStatic]
    private static int _nestingDepth;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        if (IntPtr.Size == 8)
            return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);

        return SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    /// <summary>
    /// Installs a CoreWindow subclass proc directly in front of XAML's subclass before Window.Close().
    /// After each message at the outermost level, it sends WM_NULL down the chain so XAML clears
    /// slot 0x18, allowing DefWindowProc to process WM_UAHDESTROYWINDOW and avoiding the RoUninitialize hang.
    /// </summary>
    public static void PrepareForClose(CoreWindow coreWindow)
    {
        if (coreWindow == null)
            return;

        ICoreWindowInterop interop = (ICoreWindowInterop)(object)coreWindow;
        IntPtr hwnd = interop.WindowHandle;
        if (hwnd == IntPtr.Zero)
            return;

        _nestingDepth = 0;
        _hookProc = SubclassWndProc;
        IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookProc);
        _prevWndProc = SetWindowLongPtr(hwnd, GWLP_WNDPROC, hookPtr);
    }

    private static IntPtr SubclassWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        _nestingDepth++;
        IntPtr result = CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
        _nestingDepth--;

        if (_nestingDepth == 0 && _prevWndProc != IntPtr.Zero)
            CallWindowProc(_prevWndProc, hWnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

        return result;
    }
}

