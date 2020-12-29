using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMap.Views;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using CharacterMap.Models;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;

namespace CharacterMap.ViewModels
{
    public enum FontDisplayMode
    { 
        CharacterMap,
        GlyphMap,
        TypeRamp
    }


    public class FontMapViewModel : ViewModelBase
    {
        #region Properties

        private NativeInterop _interop { get; }

        private Debouncer _searchDebouncer { get; }

        private ConcurrencyToken.ConcurrencyTokenGenerator _searchTokenFactory { get; }

        public AppSettings Settings { get; }

        public StorageFile SourceFile { get; set; }

        public ExportStyle BlackColor { get; } = ExportStyle.Black;
        public ExportStyle WhiteColor { get; } = ExportStyle.White;
        public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

        public IDialogService DialogService { get; }
        public RelayCommand<ExportParameters> CommandSavePng { get; }
        public RelayCommand<ExportParameters> CommandSaveSvg { get; }

        internal bool IsLoadingCharacters { get; private set; }

        public bool IsDarkAccent => Utils.IsAccentColorDark();

        public DWriteFallbackFont FallbackFont => FontFinder.Fallback;

        public bool IsExternalFile { get; set; }


        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => Set(ref _isLoading, value);
        }

        private string _titlePrefix;
        public string TitlePrefix
        {
            get => _titlePrefix;
            set => Set(ref _titlePrefix, value);
        }

        private IReadOnlyList<IGlyphData> _searchResults;
        public IReadOnlyList<IGlyphData> SearchResults
        {
            get => _searchResults;
            set => Set(ref _searchResults, value);
        }

        private FontDisplayMode _displayMode = FontDisplayMode.CharacterMap;
        public FontDisplayMode DisplayMode
        {
            get => _displayMode;
            set { if (Set(ref _displayMode, value)) { UpdateTypography(); } }
        }

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value == _selectedFont) return;
                _selectedFont = value;
                TitleBarHelper.SetTitle(value?.Name);
                OnPropertyChanged();
                if (null != _selectedFont)
                {
                    if (value != null) TitlePrefix = value.Name + " -";
                    SelectedVariant = _selectedFont.DefaultVariant;
                }
                else
                {
                    SelectedVariant = null;
                }
            }
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
                    LoadVariant(value);
                    OnPropertyChanged();
                    UpdateTypography();
                    SelectedTypography = TypographyFeatures.FirstOrDefault() ?? TypographyFeatureInfo.None;
                    SetDefaultChar();
                }
            }
        }

        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures;
        public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures
        {
            get => _typographyFeatures;
            private set => Set(ref _typographyFeatures, value);
        }

        private IReadOnlyList<Character> _chars;
        public IReadOnlyList<Character> Chars
        {
            get => _chars;
            set => Set(ref _chars, value);
        }

        private IReadOnlyList<DWriteFontAxis> _variationAxis;
        public IReadOnlyList<DWriteFontAxis> VariationAxis
        {
            get => _variationAxis;
            set => Set(ref _variationAxis, value);
        }

        private FontFamily _fontFamily;
        public FontFamily FontFamily
        {
            get => _fontFamily;
            private set => Set(ref _fontFamily, value);
        }

        private TypographyFeatureInfo _selectedTypography = TypographyFeatureInfo.None;
        public TypographyFeatureInfo SelectedTypography
        {
            get => _selectedTypography;
            set => Set(ref _selectedTypography, value ?? TypographyFeatureInfo.None);
        }

        private FontAnalysis _selectedVariantAnalysis;
        public FontAnalysis SelectedVariantAnalysis
        {
            get => _selectedVariantAnalysis;
            set { if (Set(ref _selectedVariantAnalysis, value)) { UpdateVariations(); } }
        }

        private CanvasTextLayoutAnalysis _selectedCharAnalysis;
        public CanvasTextLayoutAnalysis SelectedCharAnalysis
        {
            get => _selectedCharAnalysis;
            set => Set(ref _selectedCharAnalysis, value);
        }

        private List<TypographyFeatureInfo> _selectedCharVariations;
        public List<TypographyFeatureInfo> SelectedCharVariations
        {
            get => _selectedCharVariations;
            set => Set(ref _selectedCharVariations, value);
        }

        private Character _selectedChar;
        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                if (_selectedChar == value) return;
                _selectedChar = value;
                if (null != value)
                {
                    Settings.LastSelectedCharIndex = (int)value.UnicodeIndex;
                }
                OnPropertyChanged();
                UpdateCharAnalysis();
                UpdateDevValues();
            }
        }

        private bool _showColorGlyphs = true;
        public bool ShowColorGlyphs
        {
            get => _showColorGlyphs;
            set => Set(ref _showColorGlyphs, value);
        }

        private bool _importButtonEnabled = true;
        public bool ImportButtonEnabled
        {
            get => _importButtonEnabled;
            set => Set(ref _importButtonEnabled, value);
        }

        private bool _hasFontOptions = false;
        public bool HasFontOptions
        {
            get => _hasFontOptions;
            set => Set(ref _hasFontOptions, value);
        }

        private bool _isSvgChar = false;
        public bool IsSvgChar
        {
            get => _isSvgChar;
            set => Set(ref _isSvgChar, value);
        }

        private bool _isLongGeometry = true;
        public bool IsLongGeometry
        {
            get => _isLongGeometry;
            set => Set(ref _isLongGeometry, value);
        }

        private string _xamlPath;
        public string XamlPath
        {
            get => _xamlPath;
            set => Set(ref _xamlPath, value);
        }

        private string _xamlCode;
        public string XamlCode
        {
            get => _xamlCode;
            set => Set(ref _xamlCode, value);
        }

        private string _xamlPathGeom;
        public string XamlPathGeom
        {
            get => _xamlPathGeom;
            set { if (Set(ref _xamlPathGeom, value)) IsLongGeometry = value != null && value.Length > 2048; }
        }

        private string _symbolIcon;
        public string SymbolIcon
        {
            get => _symbolIcon;
            set => Set(ref _symbolIcon, value);
        }

        private string _fontIcon;
        public string FontIcon
        {
            get => _fontIcon;
            set => Set(ref _fontIcon, value);
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

        private bool _isSequenceRootVisible = false;
        public bool IsSequenceRootVisible
        {
            get => _isSequenceRootVisible;
            set => Set(ref _isSequenceRootVisible, value);
        }

        private string _sequence;
        public string Sequence
        {
            get => _sequence;
            set => Set(ref _sequence, value);
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

        private IReadOnlyList<string> _rampOptions;
        public IReadOnlyList<string> RampOptions
        {
            get => _rampOptions;
            set => Set(ref _rampOptions, value);
        }

        // todo : refactor into classes with description + writing direction
        private IReadOnlyList<string> DefaultRampOptions { get; } = new List<string>
        {
            "The quick brown dog jumps over a lazy fox. 1234567890",
            Localization.Get("CultureSpecificPangram/Text"),
            "abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "абвгдеёжзийклмнопрстуфхцчшщъыьэюя АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ", // Cyrillic Alphabet
            "1234567890.:,; ' \" (!?) +-*/= #@£$€%^& {~¬} [<>] |\\/",
            "Do bạch kim rất quý nên sẽ dùng để lắp vô xương.", // Vietnamese
            "Ταχίστη αλώπηξ βαφής ψημένη γη, δρασκελίζει υπέρ νωθρού κυνός", // Greek
            "עטלף אבק נס דרך מזגן שהתפוצץ כי חם", // Hebrew
            "نص حكيم له سر قاطع وذو شأن عظيم مكتوب على ثوب أخضر ومغلف بجلد أزرق" // Arabic
        };

        #endregion



        public int[] RampSizes { get; } = new[] { 12, 18, 24, 48, 72, 96, 110, 134 };

        public FontMapViewModel(IDialogService dialogService, AppSettings settings)
        {
            DialogService = dialogService;
            Settings = settings;

            CommandSavePng = new RelayCommand<ExportParameters>(async (b) => await SavePngAsync(b));
            CommandSaveSvg = new RelayCommand<ExportParameters>(async (b) => await SaveSvgAsync(b));

            _interop = Utils.GetInterop();

            _searchDebouncer = new Debouncer();
            _searchTokenFactory = new ConcurrencyToken.ConcurrencyTokenGenerator();
        }

        private void LoadVariant(FontVariant variant)
        {
            try
            {
                IsLoadingCharacters = true;
                Chars = variant?.GetCharacters();
                if (variant != null)
                {
                    SelectedVariantAnalysis = new FontAnalysis(variant.FontFace);
                    //if (SelectedVariantAnalysis.GlyphNames != null)
                    //{
                    //    var name = SelectedVariantAnalysis.GlyphNames.Where(n => n != "MacPost").ToList();
                    //    if (name.Count > 0)
                    //    {

                    //    }
                    //}
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
        private IReadOnlyList<String> GetRampOptions(FontVariant variant)
        {
            if (variant == null)
                return new List<string>();

            var list = DefaultRampOptions.ToList();
            
            if (variant?.TryGetSampleText() is String s)
            {
                list.Insert(0, s);
            }

            if (Unicode.ContainsRange(variant, UnicodeRange.Emoticons))
            {
                string emoji = "😂😍😭💁👍💋🐱🦉🌺🌲🍓🍕🎂🏰🏠🚄🚒🛫🛍";
                if (!list.Contains(emoji))
                    list.Add(emoji);
            }

            return list.Count == DefaultRampOptions.Count ? DefaultRampOptions : list;
        }

        internal void UpdateVariations()
        {
            VariationAxis = SelectedVariantAnalysis?.Axis?.Where(a => a.Attribute == DWriteFontAxisAttribute.Variable).ToList() ?? new List<DWriteFontAxis>();
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

            this.SelectedTypography = TypographyFeatures.FirstOrDefault(t => t.Feature == current.Feature);
            OnPropertyChanged(nameof(SelectedTypography)); // Required.
        }

        private void UpdateCharAnalysis()
        {
            if (SelectedChar == null)
            {
                SelectedCharAnalysis = new CanvasTextLayoutAnalysis();
                IsSvgChar = false;
                SelectedCharVariations = new List<TypographyFeatureInfo>();
                return;
            }

            SelectedCharAnalysis = GetCharAnalysis(SelectedChar);
            SelectedCharVariations = TypographyAnalyzer.GetCharacterVariants(SelectedVariant, SelectedChar);
            IsSvgChar = SelectedCharAnalysis.GlyphFormats.Contains(GlyphImageFormat.Svg);
        }

        internal CanvasTextLayoutAnalysis GetCharAnalysis(Character c)
        {
            using CanvasTextLayout layout = new CanvasTextLayout(Utils.CanvasDevice, $"{c.Char}", new CanvasTextFormat
            {
                FontSize = (float)Core.Converters.GetFontSize(Settings.GridSize),
                FontFamily = SelectedVariant.Source,
                FontStretch = SelectedVariant.FontFace.Stretch,
                FontWeight = SelectedVariant.FontFace.Weight,
                FontStyle = SelectedVariant.FontFace.Style,
                HorizontalAlignment = CanvasHorizontalAlignment.Left,
            }, Settings.GridSize, Settings.GridSize);

            layout.Options = CanvasDrawTextOptions.EnableColorFont;
            ApplyEffectiveTypography(layout);
            return _interop.AnalyzeCharacterLayout(layout);
        }

        private CanvasTypography GetEffectiveTypography(TypographyFeatureInfo typography = null)
        {
            if (typography == null)
                typography = SelectedTypography;

            CanvasTypography typo = new CanvasTypography();
            if (typography != null && typography.Feature != CanvasTypographyFeatureName.None)
            {
                typo.AddFeature(typography.Feature, 1u);
            }
            return typo;
        }

        private void ApplyEffectiveTypography(CanvasTextLayout layout)
        {
            using var type = GetEffectiveTypography();
            layout.SetTypography(0, 1, type);
        }

        internal void UpdateDevValues()
        {
            if (SelectedVariant == null || SelectedChar == null || !Settings.ShowDevUtils)
            {
                XamlPath = XamlPathGeom = XamlCode = FontIcon = SymbolIcon = null;
            }
            else
            {
                var data = GlyphService.GetDevValues(SelectedChar, SelectedVariant, SelectedCharAnalysis, GetEffectiveTypography(), Settings.DevToolsLanguage == 0);
                XamlCode = data.Hex;
                FontIcon = data.FontIcon;
                XamlPathGeom = data.Path;
                SymbolIcon = data.Symbol;

                XamlPath = $"{SelectedVariant.FileName}#{SelectedVariant.FamilyName}";
            }
        }

        public void SetDefaultChar()
        {
            if (Chars == null)
                return;

            if (Chars.FirstOrDefault(i => i.UnicodeIndex == Settings.LastSelectedCharIndex)
                            is Character lastSelectedChar
                            && SelectedVariant.FontFace.HasCharacter((uint)lastSelectedChar.UnicodeIndex))
            {
                SelectedChar = lastSelectedChar;
            }
            else
            {
                SelectedChar = Chars?.FirstOrDefault(
                    c => !Windows.Data.Text.UnicodeCharacters.IsWhitespace((uint)c.UnicodeIndex)) ?? Chars.FirstOrDefault();
            }
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

            return GlyphService.GetCharacterDescription(c.UnicodeIndex, SelectedVariant);
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
                SearchResults = results;
            }
        }

        internal async Task SavePngAsync(ExportParameters args, Character c = null)
        {
            Character character = SelectedChar;
            CanvasTextLayoutAnalysis analysis = SelectedCharAnalysis;

            if (c != null)
            {
                character = c;
                analysis = GetCharAnalysis(c);
            }

            ExportResult result = await ExportManager.ExportPngAsync(
                args.Style,
                SelectedFont,
                SelectedVariant,
                character,
                analysis,
                GetEffectiveTypography(args.Typography),
                Settings);

            if (result.Success)
                Messenger.Send(new AppNotificationMessage(true, result));
        }

        internal async Task SaveSvgAsync(ExportParameters args, Character c = null)
        {
            Character character = SelectedChar;
            CanvasTextLayoutAnalysis analysis = SelectedCharAnalysis;

            if (c != null)
            {
                character = c;
                analysis = GetCharAnalysis(c);
            }

            ExportResult result = await ExportManager.ExportSvgAsync(
                args.Style,
                SelectedFont,
                SelectedVariant,
                character,
                analysis,
                GetEffectiveTypography(args.Typography));

            if (result.Success)
                Messenger.Send(new AppNotificationMessage(true, result));
        }

        public async Task<bool> LoadFromFileArgsAsync(FileActivatedEventArgs args)
        {
            IsExternalFile = true;
            IsLoading = true;
            try
            {
                if (args.Files.FirstOrDefault() is StorageFile file
                    && await FontFinder.LoadFromFileAsync(file) is InstalledFont font)
                {
                    SourceFile = file;
                    IsLoading = false;

                    SelectedFont = font;
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
                var items = new List<StorageFile> { SourceFile };
                if (await FontFinder.ImportFontsAsync(items) is FontImportResult result
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

        public void OpenSelectedFontInWindow(object sender, RoutedEventArgs e)
        {
            if (SelectedFont is InstalledFont font)
            {
                _ = FontMapView.CreateNewViewForFontAsync(font);
            }
        }

        public async Task RequestCopyToClipboardAsync(CopyToClipboardMessage message)
        {
            if (message.CopyType == DevValueType.Char)
                await Utils.TryCopyToClipboardAsync(message.RequestedItem, this);
            else
            {
                var data = GlyphService.GetDevValues(message.RequestedItem, SelectedVariant, message.Analysis, GetEffectiveTypography(), Settings.DevToolsLanguage == 0);
                switch (message.CopyType)
                {
                    case DevValueType.Glyph:
                        Utils.CopyToClipBoard(data.Hex);
                        break;
                    case DevValueType.FontIcon:
                        Utils.CopyToClipBoard(data.FontIcon);
                        break;
                    case DevValueType.PathIcon:
                        Utils.CopyToClipBoard(data.Path);
                        break;
                    case DevValueType.UnicodeValue:
                        Utils.CopyToClipBoard(message.RequestedItem.UnicodeString);
                        break;
                    default:
                        return;
                }
            }

            Messenger.Send(new AppNotificationMessage(true, Localization.Get("NotificationCopied"), 2000));
        }

        public async void CopySequence()
        {
            if (await Utils.TryCopyToClipboardAsync(Sequence, this))
                Messenger.Send(new AppNotificationMessage(true, Localization.Get("NotificationCopied"), 2000));
        }
        public void ClearSequence() => Sequence = string.Empty;
        public void AddCharToSequence() => Sequence += SelectedChar.Char;
        public void IncreaseCharacterSize() => Settings.ChangeGridSize(4);
        public void DecreaseCharacterSize() => Settings.ChangeGridSize(-4);
        public void ShowPane() => Settings.EnablePreviewPane = true;
        public void HidePane() => Settings.EnablePreviewPane = false;
        public void ShowCopyPane() => Settings.EnableCopyPane = true;
        public void HideCopyPane() => Settings.EnableCopyPane = false;
    }

    public enum SearchSource
    {
        AutoProperty,
        ManualSubmit
    }
}
