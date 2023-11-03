using CharacterMap.Views;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graphics.Canvas.Text;
using System.Collections;
using System.Collections.ObjectModel;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels
{
    public enum FontDisplayMode
    { 
        CharacterMap = 0,
        GlyphMap = 1,
        TypeRamp = 2
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

        public AppSettings Settings { get; }

        public StorageFile SourceFile { get => Get<StorageFile>(); set { if (Set(value)) { OnPropertyChanged(nameof(IsInstallable)); } } }

        public ExportStyle BlackColor { get; } = ExportStyle.Black;
        public ExportStyle WhiteColor { get; } = ExportStyle.White;
        public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

        public IDialogService DialogService                                 { get; }
        public RelayCommand<ExportParameters> CommandSavePng                { get; }
        public RelayCommand<ExportParameters> CommandSaveSvg                { get; }
        public RelayCommand<DevProviderType>  ToggleDev                     { get; }
        public DWriteFallbackFont FallbackFont                              => FontFinder.Fallback; // Do *not* use { get;} here
        public bool IsExternalFile                                          { get; set; }
        internal bool IsLoadingCharacters                                   { get; private set; }

        public TypographyFeatureInfo                SelectedTypography      { get => GetV(TypographyFeatureInfo.None); set => Set(value ?? TypographyFeatureInfo.None); }
        public TypographyFeatureInfo                SelectedCharTypography  { get => GetV(TypographyFeatureInfo.None); set => Set(value ?? TypographyFeatureInfo.None); }
        public List<UnicodeRangeModel>              SelectedGlyphCategories { get => Get<List<UnicodeRangeModel>>(); private set => Set(value); }
        public List<RampOption>                     Ramps                   { get; }

        [ObservableProperty] CharacterRenderingOptions              _renderingOptions;
        [ObservableProperty] CanvasTextLayoutAnalysis               _selectedCharAnalysis;
        [ObservableProperty] List<TypographyFeatureInfo>            _selectedCharVariations;
        [ObservableProperty] UnihanData                             _unihanData;
        [ObservableProperty] IReadOnlyList<Suggestion>              _rampOptions;
        [ObservableProperty] IReadOnlyList<Character>               _chars;
        [ObservableProperty] IEnumerable                           _searchResults;
        [ObservableProperty] IReadOnlyList<DWriteFontAxis>          _variationAxis;
        [ObservableProperty] IReadOnlyList<DevProviderBase>         _providers;
        [ObservableProperty] IReadOnlyList<TypographyFeatureInfo>   _typographyFeatures;
        [ObservableProperty] ObservableCollection<UnicodeRangeGroup>       _groupedChars;

        [ObservableProperty] bool                   _showColorGlyphs        = true;
        [ObservableProperty] bool                   _importButtonEnabled    = true;
        [ObservableProperty] bool                   _showingUnihan          = true;
        [ObservableProperty] bool                   _isLoading;
        [ObservableProperty] bool                   _isSearchGrouped;
        [ObservableProperty] bool                   _hasFontOptions;
        [ObservableProperty] bool                   _isSvgChar;
        [ObservableProperty] bool                   _isSequenceRootVisible;
        [ObservableProperty] bool                   _isMDL2Font;
        [ObservableProperty] bool                   _isFiltered;
        [ObservableProperty] string                 _titlePrefix;
        [ObservableProperty] string                 _xamlPath;
        [ObservableProperty] string                 _sequence               = string.Empty;
        [ObservableProperty] FontItem               _selectedFont;
        [ObservableProperty] FontFamily             _fontFamily;
        [ObservableProperty] FolderContents         _folder;
        [ObservableProperty] DevProviderBase        _selectedProvider;
        public FontDisplayMode DisplayMode                                  { get => Get<FontDisplayMode>(); set { if (Set(value)) { UpdateTypography(); } } }
        public FontAnalysis SelectedVariantAnalysis                         { get => Get<FontAnalysis>(); set { if (Set(value)) { UpdateVariations(); } } }

        public bool IsInstallable => 
            IsExternalFile 
            && SourceFile is not null
            && SourceFile.FileType.ToLower() is ".woff" or ".woff2";

        partial void OnShowColorGlyphsChanged(bool value)
        {
            if (RenderingOptions is not null)
                RenderingOptions = RenderingOptions with { IsColourFontEnabled = value };
            if (DisplayMode == FontDisplayMode.TypeRamp)
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
                SelectedVariant = value.Selected;

                if (Set(SelectedFont.DisplayMode, nameof(DisplayMode), false))
                    UpdateTypography();
            }
            else
                SelectedVariant = null;
        }

        private FontVariant _selectedVariant;
        public FontVariant SelectedVariant
        {
            get => _selectedVariant;
            set
            {
                if (value != _selectedVariant)
                {
                    Chars = null;
                    _selectedVariant = value;
                    FontFamily = value == null ? null : new FontFamily(value.Source);
                    int idx = Settings.LastSelectedCharIndex;
                    LoadVariant(value);
                    OnPropertyChanged();
                    UpdateTypography();
                    SetDefaultChar(idx);
                    SelectedTypography = TypographyFeatures.FirstOrDefault() ?? TypographyFeatureInfo.None;
                    UpdateDevValues();

                    if (value is not null)
                        SelectedFont.Selected = value;
                }
            }
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (Set(ref _searchQuery, value))
                    DebounceSearch(value, Settings.InstantSearchDelay, SearchSource.AutoProperty);
            }
        }

        private Character _selectedChar;
        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                if (_selectedChar == value || _blockChar) return;
                _selectedChar = value;
                if (value is not null)
                    Settings.LastSelectedCharIndex = (int)value.UnicodeIndex;
                OnPropertyChanged();
                UpdateCharAnalysis();
                UpdateDevValues();
            }
        }

        private string _typeRampText;
        public string TypeRampText
        {
            get => _typeRampText;
            set
            {
                if (value != null && value.Length > 100)
                    value = value.Substring(0, 100);

                Set(ref _typeRampText, value);
            }
        }

        #endregion




        public FontMapViewModel(IDialogService dialogService, AppSettings settings)
        {
            DialogService = dialogService;
            Settings = settings;

            CommandSavePng = new RelayCommand<ExportParameters>(async (b) => await SavePngAsync(b));
            CommandSaveSvg = new RelayCommand<ExportParameters>(async (b) => await SaveSvgAsync(b));
            ToggleDev = new RelayCommand<DevProviderType>(t => SetDev(t));
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
                    SelectedCharTypography = SelectedTypography;
                    break;
                case nameof(SelectedCharTypography):
                    UpdateDevValues();
                    break;
                case nameof(DisplayMode) when SelectedFont is not null:
                    SelectedFont.DisplayMode = DisplayMode;
                    break;
            }
        }

        private void UpdateTextOptions()
        {
            OnSyncContext(() => { RampOptions = GlyphService.GetRampOptions(); });
        }

        private void SelectedFont_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is FontItem item)
            {
                if (e.PropertyName == nameof(FontItem.Selected))
                    SelectedVariant = item.Selected;
            }
        }

        public void UpdateCategories(IList<UnicodeRangeModel> value)
        {
            SelectedGlyphCategories = value.ToList();
            UpdateCharacters();
        }

        private void UpdateCharacters()
        {
            int last = Settings.LastSelectedCharIndex;
            _blockChar = true;
            if (!SelectedGlyphCategories.Any(c => !c.IsSelected))
            {
                // Fast path : all characters;
                Chars = SelectedVariant?.GetCharacters();
                GroupedChars = UnicodeRangeGroup.CreateGroups(Chars, IsMDL2Font);
                IsFiltered = false;
            }
            else
            {
                // Filter characters
                var items = Unicode.FilterCharacters(SelectedVariant?.GetCharacters(), SelectedGlyphCategories, false);
                
                // Only change the character source if we actually need too
                if (Chars is null || items.Count != Chars.Count)
                {
                    Chars = items;
                    GroupedChars = UnicodeRangeGroup.CreateGroups(items, IsMDL2Font);
                }
                else
                {
                    for (int i = 0; i<items.Count; i++)
                        if (items[i] != Chars[i])
                        {
                            Chars = items;
                            GroupedChars = UnicodeRangeGroup.CreateGroups(items, IsMDL2Font);
                            break;
                        }
                }

                IsFiltered = true;
            }

            SetDefaultChar(last);
        }

        private void LoadVariant(FontVariant variant)
        {
            try
            {
                IsLoadingCharacters = true;

                // 1. Update categories
                IsMDL2Font = FontFinder.IsMDL2(variant);
                SelectedGlyphCategories = Unicode.GetCategories(SelectedVariant, IsMDL2Font);

                // 2. Update characters
                UpdateCharacters();

                // 3. Load variant data
                if (variant != null)
                {
                    var analysis = variant.GetAnalysis();
                    TypographyAnalyzer.PrepareSearchMap(variant, analysis);
                    analysis.ResetVariableAxis();
                    SelectedVariantAnalysis = analysis;
                    HasFontOptions = SelectedVariantAnalysis.ContainsVectorColorGlyphs || SelectedVariant.HasXamlTypographyFeatures;
                    ShowColorGlyphs = variant.DirectWriteProperties.IsColorFont;
                }
                else
                {
                    SelectedVariantAnalysis = new FontAnalysis();
                    HasFontOptions = false;
                    ShowColorGlyphs = false;
                    ImportButtonEnabled = false;
                }

                RampOptions = GetRampOptions(variant);
                SelectedTypography = TypographyFeatureInfo.None;
                SearchResults = null;
                DebounceSearch(SearchQuery, 100);
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
                _ = Window.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, async () =>
                {
                    await Task.Delay(100);
                    if (variant == SelectedVariant)
                        LoadVariant(variant);
                });
            }
        }
        private IReadOnlyList<Suggestion> GetRampOptions(FontVariant variant)
        {
            if (variant == null)
                return new List<Suggestion>();

            var list = GlyphService.GetRampOptions();
            
            if (variant?.TryGetSampleText() is String s)
                list.Insert(0, new Suggestion(Localization.Get("SuggestOptionSample/Text"), s));

            if (Unicode.ContainsRange(variant, UnicodeRange.Emoticons))
            {
                string emoji = "😂😍😭💁👍💋🐱🦉🌺🌲🍓🍕🎂🏰🏠🚄🚒🛫🛍";
                if (!list.Any(s => s.Text == emoji))
                    list.Add(new Suggestion(Localization.Get("SuggestOptionEmoji/Text"), emoji));
            }

            return list;
        }

        internal void UpdateVariations()
        {
            VariationAxis = SelectedVariantAnalysis?.Axis?.Where(a => a.Attribute == DWriteFontAxisAttribute.Variable).ToList() ?? new List<DWriteFontAxis>();
            UpdateRampOptions();
        }

        private void UpdateTypography()
        {
            var current = this.SelectedTypography;

            if (SelectedVariant == null)
                TypographyFeatures = new List<TypographyFeatureInfo>();
            else if (DisplayMode == FontDisplayMode.TypeRamp)
                TypographyFeatures = SelectedVariant.TypographyFeatures;
            else
                TypographyFeatures = SelectedVariant.XamlTypographyFeatures;

            // Ensure ColorFont option propagates
            if (DisplayMode is FontDisplayMode.TypeRamp)
                UpdateRampOptions();

            this.SelectedTypography = TypographyFeatures.FirstOrDefault(t => t.Feature == current.Feature);
            OnPropertyChanged(nameof(SelectedTypography)); // Required.
        }

        private void UpdateCharAnalysis()
        {
            if (SelectedChar == null)
            {
                UnihanData = null;
                SelectedCharAnalysis = new ();
                IsSvgChar = false;
                SelectedCharVariations = new ();
                return;
            }

            SelectedCharAnalysis = GetCharAnalysis(SelectedChar);
            SelectedCharVariations = TypographyAnalyzer.GetCharacterVariants(SelectedVariant, SelectedChar);
            IsSvgChar = SelectedCharAnalysis.GlyphFormats.Contains(GlyphImageFormat.Svg);
            UnihanData = GlyphService.GetUnihanData(SelectedChar.UnicodeIndex);
        }

        internal CanvasTextLayoutAnalysis GetCharAnalysis(Character c)
        {
            using CanvasTextLayout layout = new (Utils.CanvasDevice, $"{c.Char}", new()
            {
                FontSize = (float)Core.Converters.GetFontSize(Settings.GridSize),
                FontFamily = SelectedVariant.Source,
                FontStretch = SelectedVariant.DirectWriteProperties.Stretch,
                FontWeight = SelectedVariant.DirectWriteProperties.Weight,
                FontStyle = SelectedVariant.DirectWriteProperties.Style,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
            }, Settings.GridSize, Settings.GridSize);

            // This doesn't work if it's set during the property constructor.
            // Leave it as a separate line.
            layout.Options = CanvasDrawTextOptions.EnableColorFont;

            ApplyEffectiveTypography(layout);
            return _interop.AnalyzeCharacterLayout(layout);
        }

        private CanvasTypography GetEffectiveTypography(TypographyFeatureInfo typography = null)
        {
            if (typography == null)
                typography = SelectedTypography;

            CanvasTypography typo = new ();
            if (typography != null && typography.Feature != CanvasTypographyFeatureName.None)
                typo.AddFeature(typography.Feature, 1u);

            return typo;
        }

        private void ApplyEffectiveTypography(CanvasTextLayout layout)
        {
            using var type = GetEffectiveTypography();
            layout.SetTypography(0, 1, type);
        }

        internal void UpdateDevValues()
        {
            if (SelectedVariant == null || SelectedChar == null)
            {
                // Do nothing.
            }
            else
            {
                var t = SelectedProvider?.Type ?? Settings.SelectedDevProvider;

                RenderingOptions = new CharacterRenderingOptions(
                    SelectedVariant, 
                    new() { SelectedCharTypography }, 
                    64, 
                    SelectedCharAnalysis, 
                    VariationAxis);

                UpdateRampOptions();

                Providers = RenderingOptions.GetDevProviders(SelectedChar);
                SetDev(t);

                XamlPath = $"{SelectedVariant.FileName}#{SelectedVariant.FamilyName}";
            }
        }

        public void UpdateRampOptions()
        {
            if (RenderingOptions is null)
                return;

            var ops = RenderingOptions with { Axis = VariationAxis };
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
                && SelectedVariant.Face.HasCharacter((uint)lastSelectedChar.UnicodeIndex))
            {
                SelectedChar = lastSelectedChar;
            }
            else
            {
                SelectedChar = Chars?.FirstOrDefault(
                    c => !Windows.Data.Text.UnicodeCharacters.IsWhitespace((uint)c.UnicodeIndex)) ?? Chars.FirstOrDefault();
            }

            if (set)
                _blockChar = false;
        }

        public void ChangeDisplayMode()
        {
            if (DisplayMode == FontDisplayMode.TypeRamp)
                DisplayMode = FontDisplayMode.CharacterMap;
            else
                DisplayMode = FontDisplayMode.TypeRamp;
        }

        public string GetCharName(Character c)
        {
            if (SelectedVariant == null || c == null)
                return null;

            return SelectedVariant.GetDescription(c, allowUnihan: true);
        }

        public string GetCharDescription(Character c)
        {
            if (SelectedVariant == null || c == null)
                return null;

            if (GlyphService.GetCharacterKeystroke(c.UnicodeIndex) is string k)
                return $"{c.UnicodeString} - {k}";
            else
                return c.UnicodeString;
        }

        public void DebounceSearch(string query, int delayMilliseconds = 500, SearchSource from = SearchSource.AutoProperty)
        {
            if (from == SearchSource.AutoProperty && !Settings.UseInstantSearch)
                return;

            if (from == SearchSource.ManualSubmit || delayMilliseconds <= 0)
                Search(query);
            else
                _searchDebouncer.Debounce(delayMilliseconds, () => Search(query));
        }

        internal async void Search(string query)
        {
            var token = _searchTokenFactory.GenerateToken();
            if (await GlyphService.SearchAsync(query, SelectedVariant) is IReadOnlyList<IGlyphData> results
                && token.IsValid())
            {
                SearchResults = null;
                IsSearchGrouped = false;

                if (results.Count == 0)
                    results = null;

                // If we have filtered out any characters from the character list we should
                // group the search results into characters that are shown and characters
                // that are hidden by filtering
                if (results != null && SelectedGlyphCategories.Any(c => c.IsSelected is false))
                {
                    if (SearchResultsGroup.CreateGroups(results, SelectedGlyphCategories) is { } groups
                        && groups.HasHiddenResults)
                    {
                        IsSearchGrouped = true;
                        SearchResults = groups;
                        return;
                    }
                }
                
                SearchResults = results;
            }
        }

        private void SetDev(DevProviderType type, bool save = true)
        {
            if (Providers?.FirstOrDefault(p => p.Type == type) is DevProviderBase p)
            {
                SelectedProvider = p;
                if (save)
                    Settings.SelectedDevProvider = type;
            }
        }

        internal Task SavePngAsync(ExportParameters args, Character c = null)
        {
            return SaveGlyphAsync(ExportFormat.Png, args, c);
        }

        internal Task SaveSvgAsync(ExportParameters args, Character c = null)
        {
            return SaveGlyphAsync(ExportFormat.Svg, args, c);
        }

        internal async Task SaveGlyphAsync(ExportFormat format, ExportParameters args, Character c = null)
        {
            Character character = SelectedChar;
            CanvasTextLayoutAnalysis analysis = SelectedCharAnalysis;

            if (c != null)
            {
                character = c;
                analysis = GetCharAnalysis(c);
            }

            ExportResult result = await ExportManager.ExportGlyphAsync(
                new(format, args.Style),
                SelectedFont.Font,
                RenderingOptions with { Analysis = analysis, Typography = new List<TypographyFeatureInfo>() { args.Typography } },
                character);

            if (result.State == ExportState.Succeeded)
                Messenger.Send(new AppNotificationMessage(true, result));
        }

        public async Task<bool> LoadFromFileArgsAsync(FileActivatedEventArgs args)
        {
            IsExternalFile = true;
            IsLoading = true;
            try
            {
                if (args.Files.FirstOrDefault() is StorageFile file
                    && await FontFinder.LoadFromFileAsync(file) is { } font)
                {
                    SourceFile = file;
                    IsLoading = false;

                    SelectedFont = new (font);
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
                if (await FontFinder.ImportFontsAsync(items) is { } result
                    && (result.Imported.Count > 0 || result.Existing.Count > 0))
                {
                    await WindowService.ActivateMainWindowAsync();
                    await Task.Delay(100);
                    await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                    {
                        Messenger.Send(new ImportMessage(result));
                    });
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
            if (message.CopyType is  DevValueType.Char
                && await Utils.TryCopyToClipboardAsync(message, this))
            {
                string key = message.DataType switch
                {
                    CopyDataType.SVG => "NotificationCopiedSVG",
                    CopyDataType.PNG => "NotificationCopiedPNG",
                    _ => "NotificationCopied"
                };

                Messenger.Send(new AppNotificationMessage(true, Localization.Get(key), 2500));
            }
        }

        public async void LaunchInstall()
        {
            var path = FontFinder.GetAppPath(SelectedVariantAnalysis.FilePath).ToLower();
            var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(path));
            var result = await Launcher.LaunchFileAsync(file, new LauncherOptions { DisplayApplicationPicker = true });
        }

        public async void CopySequence()
        {
            if (await Utils.TryCopyToClipboardAsync(Sequence, this))
                Messenger.Send(new AppNotificationMessage(true, Localization.Get("NotificationCopied"), 2500));
        }
        public void ClearSequence() => Sequence = string.Empty;
        public void IncreaseCharacterSize() => Settings.ChangeGridSize(4);
        public void DecreaseCharacterSize() => Settings.ChangeGridSize(-4);
        public void ShowPane() => Settings.EnablePreviewPane = true;
        public void HidePane() => Settings.EnablePreviewPane = false;
        public void ShowCopyPane() => Settings.EnableCopyPane = true;
        public void HideCopyPane() => Settings.EnableCopyPane = false;
        public void ToggleUnihan() => ShowingUnihan = !ShowingUnihan;

        public void AddCharToSequence(int start, int length, Character c)
        {
            if (c is null)
                return;

            var s = Sequence ?? string.Empty;
            start = Math.Min(start, s.Length);
            if (s.Length > 0)
                s = s.Remove(start, length);
                    
            Sequence = s.Insert(start, c.Char);
        }

    }

    public enum SearchSource
    {
        AutoProperty,
        ManualSubmit
    }
}
