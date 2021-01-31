using CharacterMap.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace CharacterMap.Services
{
    public class FacadeLaunchArgs : ILaunchActivatedEventArgs
    {
        public ActivationKind Kind => ActivationKind.Launch;

        public ApplicationExecutionState PreviousExecutionState => ApplicationExecutionState.Running;

        public SplashScreen SplashScreen => null;

        public string Arguments => string.Empty;

        public string TileId => string.Empty;
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
            }
            else
            {
                TaskCompletionSource<CoreApplicationView> tcs = new TaskCompletionSource<CoreApplicationView>();

                await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
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
                    f.RequestedTheme = ResourceHelper.AppSettings.UserRequestedTheme;
            }

            if (view.Dispatcher.HasThreadAccess)
                DoCreate();
            else
                await view.Dispatcher.RunAsync(CoreDispatcherPriority.High, DoCreate);

            return info;
        }

        private static void Manager_Released(object sender, EventArgs e)
        {
            ViewLifetimeManager manager = (ViewLifetimeManager)sender;
            manager.Released -= Manager_Released;

            WindowInformation info = _childWindows[manager.Id];
            _childWindows.Remove(manager.Id);

            _ = info.CoreView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Window.Current.Close();
                Window.Current.Content = null;
            });
        }

        public static async Task TrySwitchToWindowAsync(WindowInformation info, bool main)
        {
            if (main && !CoreApplication.MainView.Dispatcher.HasThreadAccess)
            {
                // Awaiter here is screwed without a *PROPER* dispatcher awaiter
                await CoreApplication.MainView.Dispatcher.RunAsync(
                    CoreDispatcherPriority.Normal, () =>
                {
                    _ = TrySwitchToWindowAsync(info, main);
                }).AsTask();

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

            var view = CoreApplication.Views.FirstOrDefault(v => v != CoreApplication.MainView) ?? CoreApplication.MainView;
            await view.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                _ = ApplicationViewSwitcher.TryShowAsStandaloneAsync(info.View.Id, ViewSizePreference.Default);
            }).AsTask();
        }

        public static Task ActivateMainWindowAsync()
        {
            return CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                await App.Current.ActivationService.ActivateAsync(new FacadeLaunchArgs());
            }).AsTask();
        }

        internal static void CloseForCurrentView()
        {
            var view = CoreApplication.GetCurrentView();

            _ = CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                if (CoreApplication.Views.Count == 1)
                    Application.Current.Exit();
                else if (CoreApplication.Views.Count == 2 && !CoreApplication.MainView.CoreWindow.Visible)
                    Application.Current.Exit();
                else
                {
                    _ = view.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
                    {
                        Window.Current.Close();
                    });
                }
            });
        }

        public static Task RunOnViewsAsync(DispatchedHandler a)
        {
            List<Task> t = new List<Task>
            {
                MainWindow.CoreView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, a).AsTask()
            };
            t.AddRange(_childWindows.Select(w => w.Value.CoreView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, a).AsTask()));

            return Task.WhenAll(t);
        }
    }
}
