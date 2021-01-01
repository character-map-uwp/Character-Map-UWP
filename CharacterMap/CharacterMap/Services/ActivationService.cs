using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Activation;
using CharacterMap.Helpers;
using CharacterMap.Views;
using Microsoft.Toolkit.Mvvm.DependencyInjection;

namespace CharacterMap.Services
{
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
                // Initialize things like registering background task before the app is loaded
                await InitializeAsync();

                // We spawn a separate Window for files.
                if (activationArgs is FileActivatedEventArgs fileArgs)
                {
                    bool mainView = Window.Current.Content == null;
                    void CreateView()
                    {
                        FontMapView map = new FontMapView
                        {
                            IsStandalone = true,
                        };
                        _ = map.ViewModel.LoadFromFileArgsAsync(fileArgs);

                        // You have to activate the window in order to show it later.
                        Window.Current.Content = map;
                        Window.Current.Activate();
                    }

                    var view = await WindowService.CreateViewAsync(CreateView, false);
                    await WindowService.TrySwitchToWindowAsync(view, mainView);

                    return;
                }

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (WindowService.MainWindow == null)
                {
                    void CreateMainView()
                    {
                        // Create a Frame to act as the navigation context and navigate to the first page
                        Window.Current.Content = new Frame();
                        NavigationService.Frame.NavigationFailed += (sender, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
                        NavigationService.Frame.Navigated += OnFrameNavigated;

                        if (SystemNavigationManager.GetForCurrentView() != null)
                            SystemNavigationManager.GetForCurrentView().BackRequested += OnAppViewBackButtonRequested;
                    }

                    var view = await WindowService.CreateViewAsync(CreateMainView, true);
                    await WindowService.TrySwitchToWindowAsync(view, true);
                }
                else
                {
                    /* Main Window exists, make it show */
                    _ = ApplicationViewSwitcher.TryShowAsStandaloneAsync(WindowService.MainWindow.View.Id);
                    WindowService.MainWindow.CoreView.CoreWindow.Activate();
                }
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
