using CharacterMap.Views;
using CommunityToolkit.Mvvm.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Xaml;

namespace CharacterMap.ViewModels;

public enum FontDisplayMode
{
    CharacterMapState = 0,
    GlyphMapState = 1,
    TypeRampState = 2
}

public partial class RampOption : ObservableObject
{
    public int FontSize { get; set; } = 12;
    [ObservableProperty]
    CharacterRenderingOptions _option;
}


public partial class FontMapViewModel : ViewModelBase
{
    #region Properties

    private bool _blockChar = false;

    protected override bool TrackAnimation => true;

    private NativeInterop _interop { get; }

    private Debouncer _searchDebouncer { get; }

    private ConcurrencyToken.ConcurrencyTokenGenerator _searchTokenFactory { get; }

    private int[] _rampSizes { get; } = new[] { 12, 18, 24, 48, 72, 96, 110, 134 };

    public StorageFile SourceFile { get => Get<StorageFile>(); set { if (Set(value)) { OnPropertyChanged(nameof(IsInstallable)); } } }

    public ExportStyle BlackColor { get; } = ExportStyle.Black;
    public ExportStyle WhiteColor { get; } = ExportStyle.White;
    public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

    public IDialogService DialogService { get; }
    public RelayCommand<ExportParameters> CommandSaveSvg { get; }
    public DWriteFallbackFont FallbackFont => FontFinder.Fallback; // Do *not* use { get;} here
    public bool IsExternalFile { get; set; }
    internal bool IsLoadingCharacters { get; private set; }

    public TypographyVariation SelectedTypography { get => GetV(TypographyVariation.None); set => Set(value ?? TypographyVariation.None); }
    public TypographyFeatureInfo SelectedCharTypography { get => GetV(TypographyFeatureInfo.None); set => Set(value ?? TypographyFeatureInfo.None); }
    public List<UnicodeRangeModel> SelectedGlyphCategories { get => Get<List<UnicodeRangeModel>>(); private set => Set(value); }
    public List<RampOption> Ramps { get; }

    public FontMapSearchModel Search { get; } = new();

    [ObservableProperty] CharacterRenderingOptions _renderingOptions;

    [ObservableProperty] IReadOnlyList<Character> _chars;
    [ObservableProperty] IReadOnlyList<DevProviderBase> _providers;
    [ObservableProperty] IReadOnlyList<TypographyVariation> _typographyFeatures;
    [ObservableProperty] ObservableCollection<UnicodeRangeGroup> _groupedChars;

    [ObservableProperty] bool _showColorGlyphs = true;
    [ObservableProperty] bool _importButtonEnabled = true;
    [ObservableProperty] bool _showingUnihan = true;
    [ObservableProperty] bool _isLoading;
    [ObservableProperty] bool _isSequenceRootVisible;
    [ObservableProperty] bool _isFiltered;
    [ObservableProperty] string _titlePrefix;
    [ObservableProperty] string _xamlPath;
    [ObservableProperty] string _sequence = string.Empty;
    [ObservableProperty] FontItem _selectedFont;
    [ObservableProperty] FolderContents _folder;
    [ObservableProperty] DevProviderBase _selectedProvider;
    [ObservableProperty] FaceAnalysisModel _selectedFaceAnalysis;
    public FontDisplayMode DisplayMode { get => Get<FontDisplayMode>(); set { if (Set(value)) { UpdateTypography(); } } }

    public bool IsInstallable =>
        IsExternalFile
        && SourceFile is not null
        && SourceFile.FileType.ToLower() is ".woff" or ".woff2";

    partial void OnShowColorGlyphsChanged(bool value)
    {
        if (RenderingOptions is not null)
            RenderingOptions = RenderingOptions with { IsColourFontEnabled = value };
        if (DisplayMode == FontDisplayMode.TypeRampState)
            UpdateRampOptions();
    }

    partial void OnSelectedFontChanging(FontItem value)
    {
        // Remove property changed listener from old font
        if (SelectedFont is not null)
            SelectedFont.PropertyChanged -= SelectedFont_PropertyChanged;
    }

    partial void OnSelectedFontChanged(FontItem value)
    {
        TitleBarHelper.SetTitle(value?.Font?.Name);

        if (SelectedFont is not null)
        {
            // Add property changed listener to new font
            SelectedFont.PropertyChanged -= SelectedFont_PropertyChanged;
            SelectedFont.PropertyChanged += SelectedFont_PropertyChanged;

            TitlePrefix = value.Font.Name + " -";
            SelectedFace = value.Selected;

            if (Set(SelectedFont.DisplayMode, nameof(DisplayMode), false))
                UpdateTypography();
        }
        else
            SelectedFace = null;
    }

    public CMFontFace SelectedFace
    {
        get => field;
        set
        {
            if (value != field)
            {
                Chars = null;
                field = value;
                int idx = Settings.LastSelectedCharIndex;
                LoadVariant(value);
                OnPropertyChanged();
                UpdateTypography();
                SetDefaultChar(idx);
                SelectedTypography = TypographyFeatures.FirstOrDefault() ?? TypographyVariation.None;
                UpdateDevValues();

                if (value is not null)
                    SelectedFont.Selected = value;
            }
        }
    }

    public CharacterAnalysisModel SelectedChar
    {
        get => field;
        set
        {
            if (field == value || _blockChar) return;
            field = value;
            if (value is not null)
                Settings.LastSelectedCharIndex = (int)value.Char.UnicodeIndex;
            OnPropertyChanged();
            UpdateDevValues();
        }
    }

    public string TypeRampText
    {
        get => field;
        set
        {
            if (value != null && value.Length > 100)
                value = value.Substring(0, 100);

            Set(ref field, value);
        }
    }

    #endregion




    public FontMapViewModel(IDialogService dialogService)
    {
        DialogService = dialogService;
        SelectedGlyphCategories = Unicode.CreateRangesList();

        Ramps = _rampSizes.Select(r => new RampOption { FontSize = r }).ToList();

        if (DesignMode.DesignModeEnabled is false)
            _interop = Utils.GetInterop();

        _searchDebouncer = new Debouncer();
        _searchTokenFactory = new ConcurrencyToken.ConcurrencyTokenGenerator();
        Register<RampOptionsUpdatedMessage>(m => UpdateTextOptions());
    }

    public void Deactivated()
    {
        Messenger.UnregisterAll(this);
    }

    protected override void OnPropertyChangeNotified(string propertyName)
    {
        switch (propertyName)
        {
            case nameof(SelectedTypography):
                SelectedCharTypography = SelectedTypography.Feature;
                break;
            case nameof(SelectedCharTypography):
                SelectedChar?.UpdateAnalysis(SelectedCharTypography);
                UpdateDevValues();
                break;
            case nameof(DisplayMode) when SelectedFont is not null:
                SelectedFont.DisplayMode = DisplayMode;
                break;
        }
    }

    private void UpdateTextOptions() => OnSyncContext(() => { SelectedFaceAnalysis?.UpdateRampOptions(); });

    private void SelectedFont_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is FontItem item)
        {
            if (e.PropertyName == nameof(FontItem.Selected))
                SelectedFace = item.Selected;
        }
    }

    public void UpdateCategories(IList<UnicodeRangeModel> value)
    {
        SelectedGlyphCategories = value.ToList();
        Search.SetContext(SelectedFace, SelectedGlyphCategories);
        UpdateCharacters();
    }

    private void UpdateCharacters()
    {
        int last = Settings.LastSelectedCharIndex;
        _blockChar = true;
        if (!SelectedGlyphCategories.Any(c => !c.IsSelected))
        {
            // Fast path : all characters;
            Chars = SelectedFace?.GetCharacters();
            GroupedChars = UnicodeRangeGroup.CreateGroups(Chars, SelectedFaceAnalysis.IsMDL2Font);
            IsFiltered = false;
        }
        else
        {
            // Filter characters
            var items = Unicode.FilterCharacters(SelectedFace?.GetCharacters(), SelectedGlyphCategories, false);

            // Only change the character source if we actually need too
            if (Chars is null || items.Count != Chars.Count)
            {
                Chars = items;
                GroupedChars = UnicodeRangeGroup.CreateGroups(items, SelectedFaceAnalysis.IsMDL2Font);
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                    if (items[i] != Chars[i])
                    {
                        Chars = items;
                        GroupedChars = UnicodeRangeGroup.CreateGroups(items, SelectedFaceAnalysis.IsMDL2Font);
                        break;
                    }
            }

            IsFiltered = true;
        }

        SetDefaultChar(last);
    }

    private void LoadVariant(CMFontFace variant)
    {
        try
        {
            IsLoadingCharacters = true;

            // 1. Update categories
            SelectedGlyphCategories = Unicode.GetCategories(SelectedFace, FontFinder.IsMDL2(SelectedFace));

            // 2. Load variant data
            if (variant != null)
            {
                SelectedFaceAnalysis = new(SelectedFace);
                ShowColorGlyphs = SelectedFaceAnalysis.ShowColorGlyphs;
            }
            else
            {
                SelectedFaceAnalysis = new (null);
                ShowColorGlyphs = false;
                ImportButtonEnabled = false;
            }

            // 3. Update characters
            UpdateCharacters();

            SelectedTypography = TypographyVariation.None;

            Search.Clear();
            Search.SetContext(variant, SelectedGlyphCategories);
            Search.DebounceSearch(Search.Query, 100);

            IsLoadingCharacters = false;
        }
        catch
        {
            /* 
             * Hack to avoid crash.
             * When launching the app by double clicking on a font file when the app is closed,
             * creating a CanvasTextLayout can fail for some unknown reason. So we retry it.
             * If we get caught in a never ending loop here, something horrible has occurred.
             */
            IsLoadingCharacters = false;
            Window.Current.Dispatcher.Enqueue(async () =>
            {
                await Task.Delay(100);
                if (variant == SelectedFace)
                    LoadVariant(variant);
            }, Windows.UI.Core.CoreDispatcherPriority.Low);
        }
    }
 
    internal void UpdateVariations()
    {
        SelectedFaceAnalysis?.UpdateVariations();
        UpdateRampOptions();
    }

    private void UpdateTypography()
    {
        var current = this.SelectedTypography;

        if (SelectedFace == null)
            TypographyFeatures = [];
        else if (DisplayMode == FontDisplayMode.TypeRampState)
            TypographyFeatures = SelectedFace.TypographyFeatures.Select(f => new TypographyVariation(f)).ToList();
        else
            TypographyFeatures = SelectedFace.XamlTypographyFeatures.Select(f => new TypographyVariation(f)).ToList();

        // Ensure ColorFont option propagates
        if (DisplayMode is FontDisplayMode.TypeRampState)
            UpdateRampOptions();

        this.SelectedTypography = TypographyFeatures.FirstOrDefault(t => t.Feature == current.Feature) ?? TypographyVariation.None;
        OnPropertyChanged(nameof(SelectedTypography)); // Required.
    }

    internal void UpdateDevValues()
    {
        if (SelectedFace == null || SelectedChar == null)
        {
            // Do nothing.
        }
        else
        {
            var t = SelectedProvider?.Type ?? Settings.SelectedDevProvider;

            RenderingOptions = new CharacterRenderingOptions(
                SelectedFace,
                new() { SelectedCharTypography },
                64,
                SelectedChar.Analysis,
                SelectedFaceAnalysis.VariationAxis);

            UpdateRampOptions();

            Providers = RenderingOptions.GetDevProviders(SelectedChar.Char);
            SetDev(t);

            XamlPath = $"{SelectedFace.FileName}#{SelectedFace.FamilyName}";
        }
    }

    public void UpdateRampOptions()
    {
        if (RenderingOptions is null)
            return;

        //SelectedFaceAnalysis?.UpdateRampOptions();

        var ops = RenderingOptions with { Axis = SelectedFaceAnalysis.VariationAxis };
        foreach (var ramp in Ramps)
            ramp.Option = ops;
    }

    public void SetDefaultChar(int idx = -1)
    {
        if (Chars == null)
            return;

        bool set = idx >= 0;
        if (idx < 0)
            idx = Settings.LastSelectedCharIndex;

        if (Chars.FirstOrDefault(i => i.UnicodeIndex == idx)
            is Character lastSelectedChar
            && SelectedFace.Face.HasCharacter((uint)lastSelectedChar.UnicodeIndex))
        {
            SelectedChar = new(SelectedFace, lastSelectedChar, this);
        }
        else
        {
            SelectedChar = new(
                SelectedFace, Chars?.FirstOrDefault(
                c => !Windows.Data.Text.UnicodeCharacters.IsWhitespace((uint)c.UnicodeIndex)) ?? Chars.FirstOrDefault(),
                this);
        }

        if (set)
            _blockChar = false;
    }

    [RelayCommand]
    public void SetDisplayMode(int index)
    {
        DisplayMode = (FontDisplayMode)index;
    }

    public void ChangeDisplayMode()
    {
        DisplayMode = DisplayMode switch
        {
            FontDisplayMode.CharacterMapState => FontDisplayMode.GlyphMapState,
            FontDisplayMode.GlyphMapState => FontDisplayMode.TypeRampState,
            _ => FontDisplayMode.CharacterMapState
        };
    }

    public string GetCharName(Character c)
    {
        if (SelectedFace == null || c == null)
            return null;

        return SelectedFace.GetDescription(c, allowUnihan: true);
    }

    public string GetCharDescription(Character c)
    {
        if (SelectedFace == null || c == null)
            return null;

        if (GlyphService.GetCharacterKeystroke(c.UnicodeIndex) is string k)
            return $"{c.UnicodeString} - {k}";
        else
            return c.UnicodeString;
    }


    [RelayCommand]
    private void SetDev(DevProviderType type) //, bool save = true)
    {
        if (Providers?.FirstOrDefault(p => p.Type == type) is DevProviderBase p)
        {
            SelectedProvider = p;
            if (true)
                Settings.SelectedDevProvider = type;
        }
    }

    [RelayCommand]
    internal Task SavePngAsync(ExportParameters args)
    {
        return SaveGlyphAsync(ExportFormat.Png, args);
    }

    [RelayCommand]
    internal Task SaveSvgAsync(ExportParameters args)
    {
        return SaveGlyphAsync(ExportFormat.Svg, args);
    }

    internal async Task SaveGlyphAsync(ExportFormat format, ExportParameters args)
    {
        Character character = SelectedChar.Char;
        CanvasTextLayoutAnalysis analysis = SelectedChar.Analysis;

        if (args.Character != null)
        {
            character = args.Character;
            analysis = SelectedChar.GetCharAnalysis(args.Character, SelectedFace);
        }

        ExportResult result = await ExportManager.ExportGlyphAsync(
            new(format, args.Style)
            {
                Font = SelectedFont.Font,
                Options = RenderingOptions with { Analysis = analysis, Typography = new List<TypographyFeatureInfo>() { args.Typography } }
            },
            character);

        if (result.State == ExportState.Succeeded)
            Notify(result);
    }

    public async Task<bool> LoadFromFileArgsAsync(FileActivatedEventArgs args)
    {
        IsExternalFile = true;
        IsLoading = true;
        try
        {
            if (args.Files.FirstOrDefault() is StorageFile file
                && await FontImporter.LoadFromFileAsync(file) is { } font)
            {
                SourceFile = file;
                IsLoading = false;

                SelectedFont = new(font);
                SetDefaultChar();
                return true;
            }

            await DialogService.ShowMessageAsync(
                Localization.Get("InvalidFontMessage"),
                Localization.Get("InvalidFontTitle"));

            WindowService.CloseForCurrentView();

            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async void ImportFile()
    {
        ImportButtonEnabled = false;

        IsLoading = true;
        try
        {
            List<StorageFile> items = new() { SourceFile };
            if (await FontImporter.ImportFontsAsync(items) is { } result
                && (result.Imported.Count > 0 || result.Existing.Count > 0))
            {
                await WindowService.ActivateMainWindowAsync();
                await Task.Delay(100);
                await CoreApplication.MainView.Dispatcher.ExecuteAsync(() =>
                {
                    Messenger.Send(new ImportMessage(result));
                }, Windows.UI.Core.CoreDispatcherPriority.Low);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void OpenSelectedFontInWindow()
    {
        if (SelectedFont is FontItem item)
        {
            _ = FontMapView.CreateNewViewForFontAsync(item.Font, null, RenderingOptions);
        }
    }

    public void OpenQuickCompare()
    {
        _ = QuickCompareView.CreateWindowAsync(new(false));
    }

    public async Task RequestCopyToClipboardAsync(CopyToClipboardMessage message)
    {
        if (message.CopyType is DevValueType.Char
            && await Utils.TryCopyToClipboardAsync(message, this))
        {
            string key = message.DataType switch
            {
                CopyDataType.SVG => "NotificationCopiedSVG",
                CopyDataType.PNG => "NotificationCopiedPNG",
                _ => "NotificationCopied"
            };

            Notify(Localization.Get(key), 2500);
        }
    }

    public async void LaunchInstall()
    {
        var path = FontFinder.GetAppPath(SelectedFaceAnalysis.Analysis.FilePath).ToLower();
        var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(path));
        var result = await Launcher.LaunchFileAsync(file, new LauncherOptions { DisplayApplicationPicker = true });
    }

    public async void CopySequence()
    {
        if (await Utils.TryCopyToClipboardAsync(Sequence, this))
            Notify(Localization.Get("NotificationCopied"), 2500);
    }
    public void ClearSequence() => Sequence = string.Empty;
    public void IncreaseCharacterSize() => Settings.ChangeGridSize(4);
    public void DecreaseCharacterSize() => Settings.ChangeGridSize(-4);
    public void ShowPane() => Settings.EnablePreviewPane = true;
    public void HidePane() => Settings.EnablePreviewPane = false;
    public void ShowCopyPane() => Settings.EnableCopyPane = true;
    public void HideCopyPane() => Settings.EnableCopyPane = false;
    public void ToggleUnihan() => ShowingUnihan = !ShowingUnihan;

    public void AddCharToSequence(int start, int length, CharacterAnalysisModel c, TypographyVariation typography = null)
    {
        if (c is null)
            return;

        typography ??= SelectedTypography;

        var s = Sequence ?? string.Empty;
        start = Math.Min(start, s.Length);
        if (s.Length > 0)
            s = s.Remove(start, length);

        string character = c.Char.Char;

        // If we have typography applied, check to see in the glyph exists as a mapped character,
        // and if so use that instead.
        if (Settings.CopyTypographicVariantsAsMappedChars 
            && typography != TypographyVariation.None 
            && c.Variations.FirstOrDefault(v => v.Feature == typography.Feature) 
                is TypographyVariation { IsVariationMapped: true } variation)
            character = (new Character((uint)variation.FaceCharacterMapping)).Char;
        
        Sequence = s.Insert(start, character);
    }

}

public enum SearchSource
{
    AutoProperty,
    ManualSubmit
}
