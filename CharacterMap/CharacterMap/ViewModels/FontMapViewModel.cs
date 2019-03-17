using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMapCX;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels
{
    public class FontMapViewModel : ViewModelBase
    {
        private Interop Interop { get; }

        private StorageFile _sourceFile { get; set; }

        public ExportStyle BlackColor { get; } = ExportStyle.Black;
        public ExportStyle WhiteColor { get; } = ExportStyle.White;
        public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

        public IDialogService DialogService { get; }
        public RelayCommand<ExportStyle> CommandSavePng { get; }
        public RelayCommand<bool> CommandSaveSvg { get; }

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
                    LoadChars(value);
                    RaisePropertyChanged();
                }
            }
        }

        private FontFamily _fontFamily;
        public FontFamily FontFamily
        {
            get => _fontFamily;
            private set { Set(ref _fontFamily, value); }
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

        private CanvasTextLayoutAnalysis _selectedVariantAnalysis;
        public CanvasTextLayoutAnalysis SelectedVariantAnalysis
        {
            get => _selectedVariantAnalysis;
            set => Set(ref _selectedVariantAnalysis, value);
        }

        private CanvasTextLayoutAnalysis _selectedCharAnalysis;
        public CanvasTextLayoutAnalysis SelectedCharAnalysis
        {
            get => _selectedCharAnalysis;
            set => Set(ref _selectedCharAnalysis, value);
        }

        private IReadOnlyList<Character> _chars;
        public IReadOnlyList<Character> Chars
        {
            get => _chars;
            set => Set(ref _chars, value);
        }

        private Character _selectedChar;
        public Character SelectedChar
        {
            get => _selectedChar;
            set
            {
                if (_selectedChar != value)
                {
                    _selectedChar = value;
                    if (null != value)
                    {
                        App.AppSettings.LastSelectedCharIndex = value.UnicodeIndex;
                    }
                    RaisePropertyChanged();
                    UpdateCharAnalysis();
                    UpdateDevValues();
                }
            }
        }

        private string _xamlCode;
        public string XamlCode
        {
            get => _xamlCode;
            set => Set(ref _xamlCode, value);
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

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (value != _selectedFont)
                {
                    _selectedFont = value;
                    TitleBarHelper.SetTitle(value?.Name);
                    RaisePropertyChanged();
                    if (null != _selectedFont)
                    {
                        TitlePrefix = value.Name + " -";
                        SelectedVariant = _selectedFont.DefaultVariant;

                        var lastSelectedChar = Chars.FirstOrDefault((i => i.UnicodeIndex == App.AppSettings.LastSelectedCharIndex));
                        if (null != lastSelectedChar)
                        {
                            this.SelectedChar = lastSelectedChar;
                        }
                    }
                }
                
            }
        }

        private TypographyFeatureInfo _selectedTypography;
        public TypographyFeatureInfo SelectedTypography
        {
            get => _selectedTypography;
            set => Set(ref _selectedTypography, value);
        }

        public FontMapViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            CommandSavePng = new RelayCommand<ExportStyle>(async (b) => await SavePngAsync(b));
            CommandSaveSvg = new RelayCommand<bool>(async (b) => await SaveSvgAsync(b));

            Interop = SimpleIoc.Default.GetInstance<Interop>();
        }

        private void LoadChars(FontVariant variant)
        {
            Chars = variant?.GetCharacters();
            if (variant != null)
            {
                using (CanvasTextLayout layout = new CanvasTextLayout(
                    Utils.CanvasDevice,
                    TypographyAnalyzer.GetCharString(variant), 
                    new CanvasTextFormat
                    {
                        FontSize = 8,
                        FontFamily = variant.Source,
                        FontStretch = variant.FontFace.Stretch,
                        FontWeight = variant.FontFace.Weight,
                        FontStyle = variant.FontFace.Style,
                        HorizontalAlignment = CanvasHorizontalAlignment.Left,
                    }, 10000, 10000))
                {
                    layout.Options = CanvasDrawTextOptions.EnableColorFont;
                    ApplyEffectiveTypography(layout);
                    SelectedVariantAnalysis = Interop.AnalyzeFontLayout(layout);
                    HasFontOptions = SelectedVariantAnalysis.ContainsVectorColorGlyphs || SelectedVariant.HasXamlTypographyFeatures;
                }
            }
            else
            {
                SelectedVariantAnalysis = new CanvasTextLayoutAnalysis();
                HasFontOptions = false;
            }
        }

        private void UpdateCharAnalysis()
        {
            if (SelectedChar == null)
            {
                SelectedCharAnalysis = new CanvasTextLayoutAnalysis();
                return;
            }

            using (CanvasTextLayout layout = new CanvasTextLayout(Utils.CanvasDevice, $"{SelectedChar.Char}", new CanvasTextFormat
            {
                FontSize = 20,
                FontFamily = SelectedVariant.Source,
                FontStretch = SelectedVariant.FontFace.Stretch,
                FontWeight = SelectedVariant.FontFace.Weight,
                FontStyle = SelectedVariant.FontFace.Style,
                HorizontalAlignment = CanvasHorizontalAlignment.Center,
            }, 100, 100))
            {
                layout.Options = CanvasDrawTextOptions.EnableColorFont;
                ApplyEffectiveTypography(layout);
                SelectedCharAnalysis = Interop.AnalyzeCharacterLayout(layout);
            }
        }

        private CanvasTypography GetEffectiveTypography()
        {
            CanvasTypography typo = new CanvasTypography();
            if (SelectedTypography != null && SelectedTypography.Feature != CanvasTypographyFeatureName.None)
            {
                typo.AddFeature(SelectedTypography.Feature, 1u);
            }
            return typo;
        }

        private void ApplyEffectiveTypography(CanvasTextLayout layout)
        {
            using (var type = GetEffectiveTypography())
            {
                layout.SetTypography(0, 1, type);
            }
        }

        private void UpdateDevValues()
        {
            if (SelectedVariant == null || SelectedChar == null)
            {
                XamlCode = FontIcon = SymbolIcon = null;
            }
            else
            {
                var uni = SelectedChar.UnicodeIndex.ToString("x").ToUpper();
                XamlCode = $"&#x{uni};";
                FontIcon = $@"<FontIcon FontFamily=""{SelectedVariant.XamlFontSource}"" Glyph=""&#x{uni};"" />";
                SymbolIcon = $"(Symbol)0x{uni}";
            }
        }

        private Task SavePngAsync(ExportStyle style)
        {
            return ExportManager.ExportPngAsync(
                style,
                SelectedFont,
                SelectedVariant,
                SelectedChar,
                GetEffectiveTypography());
        }

        private Task SaveSvgAsync(bool isBlackText)
        {
            return ExportManager.ExportSvgAsync(
                isBlackText ? ExportStyle.Black : ExportStyle.White,
                SelectedFont,
                SelectedVariant,
                SelectedChar,
                GetEffectiveTypography());
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
                    _sourceFile = file;
                    SelectedFont = font;
                    return true;
                }

                await DialogService.ShowMessage(
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
                var items = new List<StorageFile> { _sourceFile };
                if (await FontFinder.ImportFontsAsync(items) is FontImportResult result
                    && (result.Imported.Count > 0 || result.Existing.Count > 0))
                {
                    await WindowService.ActivateMainWindowAsync();
                    await Task.Delay(100);
                    await CoreApplication.MainView.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
                    {
                        MessengerInstance.Send(new ImportMessage(result));
                    });
                }
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
