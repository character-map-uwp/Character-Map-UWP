using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using CharacterMap.Services;
using Edi.UWP.Helpers;

namespace CharacterMap
{
    sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService;
        private ActivationService ActivationService => _activationService.Value;

        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += OnUnhandledException;
            _activationService = new Lazy<ActivationService>(CreateActivationService);
        }

        private void RegisterExceptionHandlingSynchronizationContext()
        {
            ExceptionHandlingSynchronizationContext
                .Register()
                .UnhandledException += SynchronizationContext_UnhandledException;
        }

        private async void SynchronizationContext_UnhandledException(object sender, Edi.UWP.Helpers.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await new MessageDialog("Synchronization Context Unhandled Exception:\r\n" + GetExceptionDetailMessage(e.Exception), "🙈 ERROR")
                .ShowAsync();
        }

        private async void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await new MessageDialog("Application Unhandled Exception:\r\n" + GetExceptionDetailMessage(e.Exception), "🙈 ERROR")
                .ShowAsync();
        }

        // https://github.com/ljw1004/async-exception-stacktrace
        private string GetExceptionDetailMessage(Exception ex)
        {
            return $"{ex.Message}\r\n{ex.StackTraceEx()}";
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!e.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(e);
            }
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            RegisterExceptionHandlingSynchronizationContext();
            await ActivationService.ActivateAsync(args);
        }

        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(ViewModels.MainViewModel));
        }
    }
}
