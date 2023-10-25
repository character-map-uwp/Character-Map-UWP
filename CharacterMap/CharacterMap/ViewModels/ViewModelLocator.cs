using CharacterMap.Views;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Popups;

namespace CharacterMap.ViewModels
{
    public class DialogService : IDialogService
    {
        public Task ShowMessageAsync(string message, string title)
        {
            try
            {
                var md = new MessageDialog(message, title);
                return md.ShowAsync().AsTask();
            }
            catch { }

            return Task.CompletedTask;
        }

        public void ShowMessageBox(string message, string title)
        {
            _ = ShowMessageAsync(message, title);
        }
    }

    public class ViewModelLocator
    {
        static ViewModelLocator()
        {
            NavigationServiceEx _navigationService = new ();

            void Register<VM, V>(IServiceCollection services) where VM : class
            {
                //services.AddTransient<VM>();
                _navigationService.Configure(typeof(VM).FullName, typeof(V));
            }

            ServiceCollection services = new ServiceCollection();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton(s => _navigationService);
            services.AddSingleton(s => ResourceHelper.AppSettings);
            services.AddSingleton(s => new NativeInterop(Utils.CanvasDevice));
            services.AddSingleton<UserCollectionsService>();
            services.AddSingleton<MainViewModel>();
            Register<MainViewModel, MainPage>(services);
            Ioc.Default.ConfigureServices(services.BuildServiceProvider());
        }

        public MainViewModel Main => Ioc.Default.GetService<MainViewModel>();

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}