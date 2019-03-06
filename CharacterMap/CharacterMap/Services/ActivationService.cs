using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Activation;
using CharacterMap.Helpers;
using CommonServiceLocator;
using Edi.UWP.Helpers;
using GalaSoft.MvvmLight.Threading;
using Windows.ApplicationModel.Core;
using CharacterMap.Views;

namespace CharacterMap.Services
{
    internal class ActivationService
    {
        private readonly App _app;
        private readonly UIElement _shell;
        private readonly Type _defaultNavItem;

        private NavigationServiceEx NavigationService => ServiceLocator.Current.GetInstance<NavigationServiceEx>();


        public ActivationService(App app, Type defaultNavItem, UIElement shell = null)
        {
            _app = app;
            _shell = shell ?? new Frame();
            _defaultNavItem = defaultNavItem;
        }

        public async Task ActivateAsync(object activationArgs)
        {
            if (IsActivation(activationArgs))
            {
                // Initialize things like registering background task before the app is loaded
                await InitializeAsync();

                // We spawn a seperate Window for this.
                if (activationArgs is FileActivatedEventArgs fileArgs)
                {
                    CoreApplicationView newView = CoreApplication.CreateNewView();
                    int newViewId = 0;
                    await newView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        FontMapView map = new FontMapView
                        {
                            IsStandalone = true
                        };
                        _ = map.ViewModel.LoadFromFileArgsAsync(fileArgs);

                        // You have to activate the window in order to show it later.
                        Window.Current.Content = map;
                        Window.Current.Activate();

                        newViewId = ApplicationView.GetForCurrentView().Id;
                    });
                    bool viewShown = await ApplicationViewSwitcher.TryShowAsStandaloneAsync(newViewId);
                    return;
                }

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (Window.Current.Content == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    Window.Current.Content = _shell;
                    NavigationService.Frame.NavigationFailed += (sender, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
                    NavigationService.Frame.Navigated += OnFrameNavigated;

                    TitleBarHelper.ExtendTitleBar();
                    TitleBarHelper.SetTitleBarColors();

                    if (SystemNavigationManager.GetForCurrentView() != null)
                    {
                        SystemNavigationManager.GetForCurrentView().BackRequested += OnAppViewBackButtonRequested;
                    }
                }
            }

            var activationHandler = GetActivationHandlers().FirstOrDefault(h => h.CanHandle(activationArgs));

            if (activationHandler != null)
            {
                await activationHandler.HandleAsync(activationArgs);
            }

            if (IsActivation(activationArgs))
            {
                var defaultHandler = new DefaultLaunchActivationHandler(_defaultNavItem);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }

                DispatcherHelper.Initialize();

                // Ensure the current window is active
                Window.Current.Activate();

                UI.SetWindowLaunchSize(3000, 2000);

                // Tasks after activation
                await StartupAsync();
            }
        }

        private Task InitializeAsync()
        {
            return Task.CompletedTask;
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
}
