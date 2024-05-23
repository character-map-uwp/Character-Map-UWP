using CharacterMap.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Controls;

public class OpenFolderDialogTemplateSettings : ViewModelBase
{
    public bool CanContinue => HasFolder is true && IsLoading is false;
    public string Contents { get => Get<string>(() => "Select Folder"); set => Set(value); }
    public bool HasFolder { get => GetV(false); set => Set(value); }
    public bool IsLoading { get => GetV(false); set => Set(value); }
    public bool AllowZip { get => GetV(false); set => Set(value); }
    public bool AllowRecursive { get => GetV(false); set => Set(value); }
    public int Count { get => GetV(0); set => Set(value); }
    public StorageFolder Folder { get => Get<StorageFolder>(); set => Set(value); }

    private CancellationTokenSource _cts = new CancellationTokenSource();

    public void SetFolder(StorageFolder folder)
    {
        if (folder is not null)
        {
            HasFolder = true;
            Folder = folder;
            Contents = folder.DisplayName;
        }
        else
        {
            HasFolder = false;
            Contents = "Select Folder";
        }
    }

    protected override void OnPropertyChangeNotified(string propertyName)
    {
        if (propertyName is nameof(HasFolder) or nameof(IsLoading))
            OnPropertyChanged(nameof(CanContinue));
    }



    public FolderOpenOptions GetOptions()
    {
        var ctx = SynchronizationContext.Current;

        return new FolderOpenOptions
        {
            AllowZip = AllowZip,
            Recursive = AllowRecursive,
            Root = Folder,
            Token = _cts.Token,
            Callback = i => ctx.Post(s => Count += i, null)
        };
    }

    public void Cancel()
    {
        _cts?.Cancel(false);
    }
}

public sealed partial class OpenFolderDialog : ContentDialog
{
    public OpenFolderDialogTemplateSettings TemplateSettings { get; }

    public OpenFolderDialog()
    {
        this.InitializeComponent();
        TemplateSettings = new OpenFolderDialogTemplateSettings();
        this.Opened += OpenFolderDialog_Opened;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (this.GetTemplateChild("SecondaryButton") is Button b)
        {
            b.Click -= B_Click;
            b.Click += B_Click;
        }
    }

    private void B_Click(object sender, RoutedEventArgs e)
    {
        DoCancel();
    }

    private void OpenFolderDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        PickFolderClick();
    }

    public async void PickFolderClick()
    {
        var picker = new FolderPicker();

        picker.FileTypeFilter.Add("*");
        picker.CommitButtonText = Localization.Get("OpenFontPickerConfirm");
        var src = await picker.PickSingleFolderAsync();

        TemplateSettings.SetFolder(src);
    }

    void DoCancel()
    {
        if (TemplateSettings.IsLoading)
            TemplateSettings.Cancel();

        Hide();
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var def = args.GetDeferral();

        if (TemplateSettings.Folder is not null)
        {
            try
            {
                TemplateSettings.IsLoading = true;

                FolderOpenOptions opts = TemplateSettings.GetOptions();
                if (await FontFinder.LoadToTempFolderAsync(opts) is FolderContents folder)
                {
                    if (opts.IsCancelled)
                    {
                        _ = Utils.DeleteAsync(folder.TempFolder, true);
                    }
                    else if (folder.Fonts.Count > 0)
                    {
                        await MainPage.CreateWindowAsync(new(
                            Ioc.Default.GetService<IDialogService>(),
                            Ioc.Default.GetService<AppSettings>(),
                            folder));
                    }
                }
            }
            finally
            {
                TemplateSettings.IsLoading = false;
            }
        }

        def.Complete();
    }
}
