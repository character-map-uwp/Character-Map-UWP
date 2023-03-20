using System;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using CharacterMap.Core;
using CharacterMap.Services;
using CharacterMap.Controls;
using UnhandledExceptionEventArgs = CharacterMap.Core.UnhandledExceptionEventArgs;
using CharacterMapCX.Controls;
using CharacterMap.ViewModels;
using Windows.ApplicationModel.Core;
using CommunityToolkit.Mvvm.DependencyInjection;
using CharacterMap.Helpers;

namespace CharacterMap
{
    sealed partial class App : Application
    {
        private Lazy<ActivationService> _activationService { get; }
        internal ActivationService ActivationService => _activationService.Value;

        public new static App Current { get; private set; }

        public App()
        {
            //Set app language
            if (ResourceHelper.AppSettings.GetUserLanguageID() is string language)
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = language;

            CoreApplication.EnablePrelaunch(true);

            this.FocusVisualKind = FocusVisualKind.Reveal;
            var loc = new ViewModelLocator();
            this.InitializeComponent();

            this.UnhandledException += OnUnhandledException;
            _activationService = new Lazy<ActivationService>(CreateActivationService);
            Current = this;

            DirectText.RegisterDependencyProperties();
        }

        private void RegisterExceptionHandlingSynchronizationContext()
        {
            ExceptionHandlingSynchronizationContext
                .Register()
                .UnhandledException += SynchronizationContext_UnhandledException;
        }

        private void SynchronizationContext_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            UnhandledExceptionDialog.Show(e.Exception);
        }

        private void OnUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            UnhandledExceptionDialog.Show(e.Exception);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            if (!e.PrelaunchActivated)
            {
                await ActivationService.ActivateAsync(e);
            }
            else
            {
                await FontFinder.LoadFontsAsync();
                await Ioc.Default.GetService<UserCollectionsService>().LoadCollectionsAsync();
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
