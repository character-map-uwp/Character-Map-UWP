using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using CharacterMap.Activation;
using CharacterMap.Helpers;
using Edi.UWP.Helpers;
using GalaSoft.MvvmLight.Threading;

namespace CharacterMap.Services
{
    internal class ActivationService
    {
        private readonly App _app;
        private readonly UIElement _shell;
        private readonly Type _defaultNavItem;

        private NavigationServiceEx NavigationService => Microsoft.Practices.ServiceLocation.ServiceLocator.Current.GetInstance<NavigationServiceEx>();


        public ActivationService(App app, Type defaultNavItem, UIElement shell = null)
        {
            _app = app;
            _shell = shell ?? new Frame();
            _defaultNavItem = defaultNavItem;
        }

        public async Task ActivateAsync(object activationArgs)
        {
            if (IsInteractive(activationArgs))
            {
                // Initialize things like registering background task before the app is loaded
                await InitializeAsync();

                // Do not repeat app initialization when the Window already has content,
                // just ensure that the window is active
                if (Window.Current.Content == null)
                {
                    // Create a Frame to act as the navigation context and navigate to the first page
                    Window.Current.Content = _shell;
                    NavigationService.Frame.NavigationFailed += (sender, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
                    NavigationService.Frame.Navigated += OnFrameNavigated;
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

            if (IsInteractive(activationArgs))
            {
                var defaultHandler = new DefaultLaunchActivationHandler(_defaultNavItem);
                if (defaultHandler.CanHandle(activationArgs))
                {
                    await defaultHandler.HandleAsync(activationArgs);
                }

                DispatcherHelper.Initialize();

                // Ensure the current window is active
                Window.Current.Activate();

                var accentColor = Colors.Transparent;
                var btnHoverColor = Color.FromArgb(128,
                    (byte)(accentColor.R + 30),
                    (byte)(accentColor.G + 30),
                    (byte)(accentColor.B + 30));
                UI.ApplyColorToTitleBar(
                    accentColor,
                    Colors.White,
                    Colors.LightGray,
                    Colors.Gray);
                UI.ApplyColorToTitleButton(
                    accentColor, Colors.White,
                    btnHoverColor, Colors.White,
                    accentColor, Colors.White,
                    Colors.LightGray, Colors.Gray);

                UI.SetWindowLaunchSize(2560, 1440);

                // Tasks after activation
                await StartupAsync();
            }
        }

        private async Task InitializeAsync()
        {
            await ThemeSelectorService.InitializeAsync();
            await Task.CompletedTask;
        }

        private async Task StartupAsync()
        {
            await WhatsNewDisplayService.ShowIfAppropriateAsync();
            ThemeSelectorService.SetRequestedTheme();
            await Task.CompletedTask;
        }

        private IEnumerable<ActivationHandler> GetActivationHandlers()
        {
            yield return Singleton<ToastNotificationsService>.Instance;
        }

        private bool IsInteractive(object args)
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
