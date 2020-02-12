using CharacterMap.Core;
using CharacterMap.Services;
using CharacterMap.Views;
using CharacterMapCX;
using CommonServiceLocator;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;

namespace CharacterMap.ViewModels
{
    public class ViewModelLocator
    {
        private readonly NavigationServiceEx _navigationService = new NavigationServiceEx();

        public ViewModelLocator()
        {
            if (!ServiceLocator.IsLocationProviderSet)
            {
                ServiceLocator.SetLocatorProvider(() => SimpleIoc.Default);

                SimpleIoc.Default.Register(() => _navigationService);
                SimpleIoc.Default.Register<IDialogService, DialogService>();
                SimpleIoc.Default.Register(() => new Interop(Utils.CanvasDevice));

                SimpleIoc.Default.Register<MainViewModel>();
                SimpleIoc.Default.Register<UserCollectionsService>();
                Register<MainViewModel, MainPage>();
            }
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public void Register<VM, V>() where VM : class
        {
            SimpleIoc.Default.Register<VM>();
            _navigationService.Configure(typeof(VM).FullName, typeof(V));
        }

        public static void Cleanup()
        {
            // TODO Clear the ViewModels
        }
    }
}