using CharacterMap.Core;
using CharacterMap.Helpers;
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
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace CharacterMap.ViewModels
{
    public class FontMapViewModel : ViewModelBase
    {
        private Interop Interop { get; }

        public ExportStyle BlackColor { get; } = ExportStyle.Black;
        public ExportStyle WhiteColor { get; } = ExportStyle.White;
        public ExportStyle GlyphColor { get; } = ExportStyle.ColorGlyph;

        public IDialogService DialogService { get; }
        public RelayCommand<ExportStyle> CommandSavePng { get; }
        public RelayCommand<bool> CommandSaveSvg { get; }

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
            set { if (Set(ref _selectedVariant, value)) LoadChars(value); }
        }

        private bool _showColorGlyphs = true;
        public bool ShowColorGlyphs
        {
            get => _showColorGlyphs;
            set => Set(ref _showColorGlyphs, value);
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
                _selectedChar = value;
                if (null != value)
                {
                    XamlCode = $"&#x{value.UnicodeIndex.ToString("x").ToUpper()};";
                    FontIcon = $@"<FontIcon FontFamily=""{SelectedFont.Name}"" Glyph=""&#x{
                            value.UnicodeIndex.ToString("x").ToUpper()
                        };"" />";
                    SymbolIcon = $"(Symbol)0x{value.UnicodeIndex.ToString("x").ToUpper()}";

                    App.AppSettings.LastSelectedCharIndex = value.UnicodeIndex;
                }
                RaisePropertyChanged();
                UpdateCharAnalysis();
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

            Load();
        }

        private void Load()
        {

        }

        private void LoadChars(FontVariant variant)
        {
            Chars = variant?.GetCharacters();
        }

        private void UpdateCharAnalysis()
        {
            if (SelectedChar == null)
            {
                SelectedCharAnalysis = new CanvasTextLayoutAnalysis();
                return;
            }

            using (CanvasTextLayout layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), $"{SelectedChar.Char}", new CanvasTextFormat
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
                SelectedCharAnalysis = Interop.Analyze(layout);
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
            IsLoading = true;
            try
            {
                if (args.Files.FirstOrDefault() is StorageFile file
                    && await FontFinder.LoadFromFileAsync(file) is InstalledFont font)
                {
                    SelectedFont = font;
                    TitleBarHelper.SetTitle(font.Name);
                    return true;
                }

                return false;
            }
            finally
            {
                IsLoading = false;
            }
            
        }
    }
}
