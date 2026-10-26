using CharacterMap.Views;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.WindowManagement;
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

public interface IViewInformation
{
    Task RunOnAsync(Action a);
    WeakReferenceMessenger Messenger { get; }
    bool HasThreadAccess { get; }
    Compositor Compositor { get; }
    UIElement Content { get; }
    CoreVirtualKeyStates GetKeyState(VirtualKey key);
    CoreCursor PointerCursor { get; set; }
    Rect Bounds { get; }
}


public class UWPWindowInformation : IViewInformation
{
    private UWPWindowInformation(CoreApplicationView coreView, ApplicationView view, Window window)
    {
        CoreView = coreView;
        View = view;
        Window = window;
        Manager = ViewLifetimeManager.CreateForCurrentView();
    }

    public static UWPWindowInformation CreateForCurrentView()
    {
        var info = new UWPWindowInformation(
            CoreApplication.GetCurrentView(),
            ApplicationView.GetForCurrentView(),
            Window.Current);

        info.CoreView.CoreWindow.CustomProperties["Info"] = info;
        return info;
    }

    public CoreApplicationView CoreView { get; }

    public ApplicationView View { get; }
    public Window Window { get; }
    public ViewLifetimeManager Manager { get; }

    public UIElement Content => Window.Content;
    public Compositor Compositor => Window.Current.Compositor;

    public Task RunOnAsync(Action a) => CoreView.DispatcherQueue.EnqueueAsync(a);
    public CoreVirtualKeyStates GetKeyState(VirtualKey key) => CoreView.CoreWindow.GetKeyState(key);
    public WeakReferenceMessenger Messenger => field ??= CoreView.CoreWindow.GetMessenger();
    public bool HasThreadAccess => CoreView.DispatcherQueue.HasThreadAccess;

    public CoreCursor PointerCursor
    {
        get => CoreView.CoreWindow.PointerCursor;
        set => CoreView.CoreWindow.PointerCursor = value;
    }
    public Rect Bounds => CoreView.CoreWindow.Bounds;
}

public static class ViewService
{
    /// <summary>
    /// The current View
    /// </summary>
    public static IViewInformation Current => Ioc.Default.ViewService().CurrentView;

    public static IViewService Service => Ioc.Default.ViewService();
}

public interface IViewService
{
    /// <summary>
    /// Create and show a new window
    /// </summary>
    /// <param name="a"></param>
    /// <param name="mainView"></param>
    /// <returns></returns>
    Task<IViewInformation> CreateViewAsync(Func<FrameworkElement> a, bool mainView);
    
    /// <summary>
    /// Attempt to bring an existing window to the forefront
    /// </summary>
    /// <param name="iinfo"></param>
    /// <param name="main"></param>
    /// <returns></returns>
    Task TrySwitchToViewAsync(IViewInformation iinfo, bool main);

    /// <summary>
    /// Reopen the primary CharacterMap window view
    /// </summary>
    /// <returns></returns>
    Task ReactivateMainAsync();

    //Task RunOnViewsAsync(Action<UIElement> a);

    IViewInformation MainView { get; }

    Task TryShowAsync(IViewInformation info);

    void CloseCurrentView();

    IViewInformation CurrentView { get; }

    Dictionary<int, IViewInformation> ChildViews { get; }
}

public static class ViewServiceExtensions
{
    extension (IViewInformation view)
    {
        public void Enqueue(Action a)
        {
            view.Content.Enqueue(a);
        }
    }

    extension (CoreWindow window)
    {
        public IViewInformation GetViewInformation()
        {
            return (IViewInformation)window.CustomProperties["Info"];
        }

        public WeakReferenceMessenger GetMessenger()
        {
            if ((window.CustomProperties.TryGetValue("__Messenger", out object value) && value is WeakReferenceMessenger messenger) is false)
            {
                messenger = new WeakReferenceMessenger();
                window.CustomProperties["__Messenger"] = messenger;
            }

            return messenger;
        }
    }

    extension(IViewService service)
    {
        public Task ActivateMainWindowAsync()
        {
            return service.MainView.RunOnAsync(async () =>
            {
                await App.Current.ActivationService.ActivateAsync(new FacadeLaunchArgs());
            });
        }

        public Task RunOnViewsAsync(Action<UIElement> a)
        {
            List<Task> t = new();

            if (service.MainView is IViewInformation wi)
                t.Add(wi.RunOnAsync(() => a(wi.Content)));

            List<Task> tasks = new();
            foreach (var info in service.ChildViews.ToList())
            {
                try
                {
                    tasks.Add(info.Value.RunOnAsync(() => a(info.Value.Content)));
                }
                catch (Exception ex)
                {
                    Utils.AppendDiagnostics("WindowService RunOnViewsAsync", ex);
                    service.ChildViews.Remove(info.Key);
                }
            }

            t.AddRange(tasks);
            return Task.WhenAll(t);
        }
    }
}

public class UWPWindowService : IViewService
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

    public IViewInformation MainView { get; private set; }

    public Dictionary<int, IViewInformation> ChildViews => _childWindows;

    private Dictionary<int, IViewInformation> _childWindows { get; } = [];

    public bool HasWindows => MainView != null || _childWindows.Count > 0;

    public void AddChildWindow(UWPWindowInformation information)
    {
        if (!information.CoreView.IsMain)
            _childWindows.Add(information.View.Id, information);
    }

    public async Task<IViewInformation> CreateViewAsync(Func<FrameworkElement> a, bool mainView)
    {
        UWPWindowInformation info = null;
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

            await CoreApplication.MainView.DispatcherQueue.EnqueueAsync(() =>
            {
                if (CoreApplication.MainView.DispatcherQueue.HasThreadAccess &&
                    CoreApplication.MainView.Properties.ContainsKey(nameof(MainView)))
                    tcs.SetResult(CoreApplication.CreateNewView());
                else
                    tcs.SetResult(CoreApplication.MainView);
            });

            view = await tcs.Task;
        }

        FrameworkElement Create()
        {
            FrameworkElement content = a();
            info = UWPWindowInformation.CreateForCurrentView();
            DispatcherQueueExtensions.RegisterCurrentThread(info.CoreView);

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
                view.Properties[nameof(MainView)] = info;

                if (mainView)
                    MainView = info;
            }

            var settings = ResourceHelper.AppSettings;
            settings.UpdateTransparency(settings.IsTransparencyEnabled);
            return content;
        }

        async Task DoCreateAsync()
        {
            Window.Current.Content = Create();
            Window.Current.Activate();


            if (Window.Current.Content is FrameworkElement f)
                f.RequestedTheme = ResourceHelper.GetEffectiveTheme();

            await TrySwitchToViewAsync(info, mainView);
        }

        if (view.DispatcherQueue.HasThreadAccess)
            await DoCreateAsync();
        else
            await view.DispatcherQueue.EnqueueAsync(DoCreateAsync, CoreDispatcherPriority.High);

        return info;
    }

    public async Task ReactivateMainAsync()
    {
        if (!CoreApplication.MainView.DispatcherQueue.HasThreadAccess)
        {
            // Awaiter here is screwed without a *PROPER* dispatcher awaiter
            await CoreApplication.MainView.DispatcherQueue.EnqueueAsync(
                () => ReactivateMainAsync());

            await Task.Delay(250);
            return;
        }
        var info = ((UWPWindowInformation)MainView);
        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(info.View.Id);
        await Task.Delay(100);
        await ApplicationViewSwitcher.TryShowAsStandaloneAsync(info.View.Id);
        await Task.Delay(100);

        info.CoreView.CoreWindow.Activate();
        info.CoreView.CoreWindow.SizeChanged -= Current_SizeChanged;
        info.CoreView.CoreWindow.SizeChanged += Current_SizeChanged;
    }

    public async Task TrySwitchToViewAsync(IViewInformation iinfo, bool main)
    {
        UWPWindowInformation info = ((UWPWindowInformation)iinfo);

        if (main && !CoreApplication.MainView.DispatcherQueue.HasThreadAccess)
        {
            // Awaiter here is screwed without a *PROPER* dispatcher awaiter
            await CoreApplication.MainView.DispatcherQueue.EnqueueAsync(
                () => TrySwitchToViewAsync(info, main));

            return;
        }

        if (main && CoreApplication.MainView.CoreWindow.Visible)
        {
            await ApplicationViewSwitcher.SwitchAsync(
                info.View.Id,
                ((UWPWindowInformation)CoreApplication.MainView.Properties[nameof(MainView)]).View.Id,
                ApplicationViewSwitchingOptions.ConsolidateViews).AsTask();

            return;
        }

        if (iinfo == MainView)
            await this.ActivateMainWindowAsync();

        //await info.CoreView.Dispatcher.ExecuteAsync(() =>
        //{
        //    info.CoreView.Activated -= CoreView_Activated;
        //    info.CoreView.Activated += CoreView_Activated;
        //});

        var view = CoreApplication.Views.FirstOrDefault(v => v != CoreApplication.MainView) ?? CoreApplication.MainView;
        await view.DispatcherQueue.EnqueueAsync(async () =>
        {
            await ApplicationViewSwitcher.TryShowAsStandaloneAsync(info.View.Id, ViewSizePreference.Default);
        });
    }

    public async Task TryShowAsync(IViewInformation info)
    {
        UWPWindowInformation window = ((UWPWindowInformation)info);
        _ = ApplicationViewSwitcher.TryShowAsStandaloneAsync(window.View.Id);
        window.CoreView.CoreWindow.Activate();
    }


    public void CloseCurrentView()
    {
        var view = CoreApplication.GetCurrentView();

        CoreApplication.MainView.DispatcherQueue.Enqueue(() =>
        {
            if (CoreApplication.Views.Count == 1)
                Application.Current.Exit();
            else if (CoreApplication.Views.Count == 2 && !CoreApplication.MainView.CoreWindow.Visible)
                Application.Current.Exit();
            else
            {
                view.DispatcherQueue.Enqueue(Window.Current.Close, CoreDispatcherPriority.High);
            }
        }, CoreDispatcherPriority.High);
    }

    public IViewInformation CurrentView => Window.Current.CoreWindow.GetViewInformation();






    private static void CoreView_Activated(CoreApplicationView sender, IActivatedEventArgs args)
    {
        sender.Activated -= CoreView_Activated;
        sender.CoreWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            sender.CoreWindow.SizeChanged -= Current_SizeChanged;
            sender.CoreWindow.SizeChanged += Current_SizeChanged;
            await ApplicationViewSwitcher.SwitchAsync(ApplicationView.GetForCurrentView().Id);
        });
    }

    private static void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        if (sender is CoreWindow window)
            window.GetMessenger().Send(new WindowResizingMessage(window.DispatcherQueue));
    }

    private void Manager_Released(object sender, EventArgs e)
    {
        ViewLifetimeManager manager = (ViewLifetimeManager)sender;
        manager.Released -= Manager_Released;

        UWPWindowInformation info = (UWPWindowInformation)_childWindows[manager.Id];
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

        info.CoreView.DispatcherQueue.EnqueueAsync(async () =>
        {
            info.Messenger.Reset();

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

