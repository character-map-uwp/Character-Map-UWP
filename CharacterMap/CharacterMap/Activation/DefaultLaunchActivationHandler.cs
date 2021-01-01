using System;
using System.Threading.Tasks;
using CharacterMap.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Windows.ApplicationModel.Activation;

namespace CharacterMap.Activation
{
    internal class DefaultLaunchActivationHandler : ActivationHandler<ILaunchActivatedEventArgs>
    {
        private readonly string _navElement;
    
        private NavigationServiceEx NavigationService => Ioc.Default.GetService<NavigationServiceEx>();

        public DefaultLaunchActivationHandler(Type navElement)
        {
            _navElement = navElement.FullName;
        }
    
        protected override async Task HandleInternalAsync(ILaunchActivatedEventArgs args)
        {
            // When the navigation stack isn't restored navigate to the first page,
            // configuring the new page by passing required information as a navigation
            // parameter
            NavigationService.Navigate(_navElement, args.Arguments);

            // You can use this sample to create toast notifications where needed in your app.
            // Singleton<ToastNotificationsService>.Instance.ShowSavedNotification();
            await Task.CompletedTask;
        }

        protected override bool CanHandleInternal(ILaunchActivatedEventArgs args)
        {
            // None of the ActivationHandlers has handled the app activation
            return NavigationService.Frame.Content == null;
        }
    }
}
