using CharacterMap.Activation;
using CharacterMap.Views;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace CharacterMap.Services;

internal class ActivationService
{
    private readonly App _app;
    private readonly Type _defaultNavItem;

    private NavigationServiceEx NavigationService => Ioc.Default.GetService<NavigationServiceEx>();


    public ActivationService(App app, Type defaultNavItem, UIElement shell = null)
    {
        _app = app;
        //_shell = shell ?? new Frame();
        _defaultNavItem = defaultNavItem;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        if (IsActivation(activationArgs))
        {
            IViewService windowService = Ioc.Default.ViewService();

            // We spawn a separate Window for files.
            if (activationArgs is FileActivatedEventArgs fileArgs)
            {
                bool mainView = Window.Current.Content == null;
                FrameworkElement CreateView()
                {
                    FontMapView map = new FontMapView
                    {
                        IsStandalone = true,
                        RequestedTheme = ResourceHelper.GetEffectiveTheme()
                    };
                    _ = map.ViewModel.LoadFromFileArgsAsync(fileArgs);
                    return map;
                }

                await windowService.CreateViewAsync(CreateView, false);
                return;
            }

            // Do not repeat app initialization when the Window already has content,
            // just ensure that the window is active
            if (windowService.MainView == null)
            {
                Frame CreateMainView()
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    Frame frame = NavigationService.Frame = new Frame();
                    NavigationService.Frame.NavigationFailed += (sender, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
                    NavigationService.Frame.Navigated += OnFrameNavigated;

                    if (SystemNavigationManager.GetForCurrentView() != null)
                        SystemNavigationManager.GetForCurrentView().BackRequested += OnAppViewBackButtonRequested;

                    return frame;
                }

                var view = await windowService.CreateViewAsync(CreateMainView, true);
                await windowService.TrySwitchToViewAsync(view, true);
            }
            else
            {
                /* Main Window exists, make it show */
                _ = windowService.TryShowAsync(windowService.MainView);
            }

            // Screenshots
            // ApplicationView.GetForCurrentView().TryResizeView(new Size(1594, 833));
        }



        try
        {
            var activationHandler = GetActivationHandlers()?.FirstOrDefault(h => h.CanHandle(activationArgs));
            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }
        }
        catch
        {

        }

        if (IsActivation(activationArgs))
        {
            var defaultHandler = new DefaultLaunchActivationHandler(_defaultNavItem);
            if (defaultHandler.CanHandle(activationArgs))
            {
                await defaultHandler.HandleAsync(activationArgs);
            }

            // Ensure the current window is active
            Window.Current.Activate();

            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;

            // Tasks after activation
            await StartupAsync();
        }
    }

    private void ActivationService_Consolidated(ApplicationView sender, ApplicationViewConsolidatedEventArgs args)
    {
        App.CleanUp();
    }

    private Task StartupAsync()
    {
        return Task.CompletedTask;
    }

    private IEnumerable<ActivationHandler> GetActivationHandlers()
    {
        yield return Singleton<ToastNotificationsService>.Instance;
    }

    private bool IsActivation(object args)
    {
        return args is IActivatedEventArgs;
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = (NavigationService.CanGoBack) ?
            AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
    }

    private void OnAppViewBackButtonRequested(object sender, BackRequestedEventArgs e)
    {
        if (NavigationService.CanGoBack)
        {
            NavigationService.GoBack();
            e.Handled = true;
        }
    }
}
