using CharacterMap.Controls;
using CharacterMapCX.Controls;
using SQLite;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using UnhandledExceptionEventArgs = CharacterMap.Core.UnhandledExceptionEventArgs;

namespace CharacterMap;

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
        else
            Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = string.Empty;

        CoreApplication.EnablePrelaunch(true);

        this.FocusVisualKind = FocusVisualKind.Reveal;
        var loc = new ViewModelLocator();
        this.InitializeComponent();

        this.UnhandledException += OnUnhandledException;
        this.Suspending += App_Suspending;
        _activationService = new Lazy<ActivationService>(CreateActivationService);
        Current = this;

        DirectText.RegisterDependencyProperties();
        SQLitePCL.raw.SetProvider(SQLiteConnection.Provider);
    }

    private async void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
    {
        var d = e.SuspendingOperation.GetDeferral();
        await Ioc.Default.GetService<UserCollectionsService>().FlushAsync();
        d.Complete();
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
