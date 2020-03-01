using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using CharacterMap.Core;
using CharacterMap.Services;
using Edi.UWP.Helpers;
using CharacterMap.Helpers;
using CharacterMap.Controls;

namespace CharacterMap
{
    sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService { get; }
        internal ActivationService ActivationService => _activationService.Value;

        public static new App Current { get; private set; }

        public App()
        {
            this.FocusVisualKind = FocusVisualKind.Reveal;
            this.InitializeComponent();

            this.UnhandledException += OnUnhandledException;
            _activationService = new Lazy<ActivationService>(CreateActivationService);
            Current = this;
        }

        private void RegisterExceptionHandlingSynchronizationContext()
        {
            ExceptionHandlingSynchronizationContext
                .Register()
                .UnhandledException += SynchronizationContext_UnhandledException;
        }

        private void SynchronizationContext_UnhandledException(object sender, Edi.UWP.Helpers.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            UnhandledExceptionDialog.Show(e.Exception);

        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            UnhandledExceptionDialog.Show(e.Exception);

        }

        private string GetExceptionDetailMessage(Exception ex)
        {
            return $"{ex.Message}\r\n{ex.StackTrace}";
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

        protected override async void OnFileActivated(FileActivatedEventArgs args)
        {
            base.OnFileActivated(args);
            await ActivationService.ActivateAsync(args);
        }

        private ActivationService CreateActivationService()
        {
            return new ActivationService(this, typeof(ViewModels.MainViewModel));
        }
    }
}
