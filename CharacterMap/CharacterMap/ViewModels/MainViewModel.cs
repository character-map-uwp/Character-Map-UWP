using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI.Text;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;

namespace CharacterMap.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private IReadOnlyList<Character> _chars;
        private string _fontIcon;
        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private Character _selectedChar;
        private InstalledFont _selectedFont;
        private string _xamlCode;
        private string _symbolIcon;

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            AppNameVersion = GetAppDescription();
            CommandSavePng = new RelayCommand<bool>(async (b) => await SavePng(b));
            CommandSaveSvg = new RelayCommand<bool>(async (b) => await SaveSvgAsync(b));
            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);

            Load();
        }

        private string GetAppDescription()
        {
            var package = Package.Current;
            var packageId = package.Id;
            var version = packageId.Version;

            return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision} ({Architecture})";
        }

        public string Architecture => Edi.UWP.Helpers.Utils.Architecture;

        public RelayCommand CommandToggleFullScreen { get; set; }

        private string _appNameVersion;
        public string AppNameVersion
        {
            get => _appNameVersion;
            set => Set(ref _appNameVersion, value);
        }

        private int _fontListFilter;
        public int FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }

        public string TitlePrefix
        {
            get => _titlePrefix;
            set { _titlePrefix = value; RaisePropertyChanged(); }
        }

        public IDialogService DialogService { get; set; }

        public RelayCommand<bool> CommandSavePng { get; set; }

        public RelayCommand<bool> CommandSaveSvg { get; set; }

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

        private bool _isLoadingFonts;
        public bool IsLoadingFonts
        {
            get => _isLoadingFonts;
            set => Set(ref _isLoadingFonts, value);
        }

        public ObservableCollection<InstalledFont> FontList
        {
            get => _fontList;
            set
            {
                _fontList = value;
                CreateFontListGroup();
                RaisePropertyChanged();
            }
        }

        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set
            {
                _groupedFontList = value;
                RaisePropertyChanged();
            }
        }

        public IReadOnlyList<Character> Chars
        {
            get => _chars;
            set => Set(ref _chars, value);
        }

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
            }
        }

        public string XamlCode
        {
            get => _xamlCode;
            set
            {
                _xamlCode = value;
                RaisePropertyChanged();
            }
        }

        public string SymbolIcon
        {
            get => _symbolIcon;
            set { _symbolIcon = value; RaisePropertyChanged(); }
        }

        public string FontIcon
        {
            get => _fontIcon;
            set
            {
                _fontIcon = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowSymbolFontsOnly
        {
            get => App.AppSettings.ShowSymbolFontsOnly;
            set
            {
                App.AppSettings.ShowSymbolFontsOnly = value;
                RefreshFontList();
                RaisePropertyChanged();
            }
        }

        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                if (null != _selectedFont)
                {
                    TitlePrefix = value.Name + " - ";
                    App.AppSettings.LastSelectedFontName = value.Name;
                }

                RaisePropertyChanged();

                if (null != _selectedFont)
                {
                    SelectedVariant = _selectedFont.DefaultVariant;
                }
            }
        }

        private async void Load()
        {
            IsLoadingFonts = true;
            await FontFinder.LoadFontsAsync();
            RefreshFontList();
            IsLoadingFonts = false;
        }

        private void LoadChars(FontVariant variant)
        {
            Chars = variant?.GetCharacters();
        }

        private void ToggleFullScreenMode()
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
                view.ExitFullScreenMode();
            else
                view.TryEnterFullScreenMode();
        }

        public void RefreshFontList()
        {
            try
            {
                var fontList = FontFinder.GetFonts().AsEnumerable();

                if (FontListFilter == 1)
                    fontList = fontList.Where(f => f.IsSymbolFont);
                else if (FontListFilter == 2)
                    fontList = fontList.Where(f => f.HasImportedFiles);

                FontList = fontList.OrderBy(f => f.Name)
                                   .ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font List");
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
                GroupedFontList = list.ToObservableCollection();

                if (!FontList.Any()) return;
                if (!string.IsNullOrEmpty(App.AppSettings.LastSelectedFontName))
                {
                    var lastSelectedFont = FontList.FirstOrDefault((i => i.Name == App.AppSettings.LastSelectedFontName));

                    if (null != lastSelectedFont)
                    {
                        this.SelectedFont = lastSelectedFont;

                        var lastSelectedChar = Chars.FirstOrDefault((i => i.UnicodeIndex == App.AppSettings.LastSelectedCharIndex));
                        if (null != lastSelectedChar)
                        {
                            this.SelectedChar = lastSelectedChar;
                        }
                    }
                    else
                    {
                        SelectedFont = FontList.FirstOrDefault();
                    }
                }
                else
                {
                    SelectedFont = FontList.FirstOrDefault();
                }
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font Group");
            }
        }

        private async Task SavePng(bool isBlackText)
        {
            try
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                savePicker.FileTypeChoices.Add("Png Image", new[] { ".png" });
                savePicker.SuggestedFileName = $"CharacterMap_{DateTime.Now:yyyyMMddHHmmss}.png";
                var file = await savePicker.PickSaveFileAsync();

                if (null != file)
                {
                    CachedFileManager.DeferUpdates(file);
                    var device = CanvasDevice.GetSharedDevice();
                    var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                    var canvasH = (float)App.AppSettings.PngSize;
                    var canvasW = (float)App.AppSettings.PngSize;

                    var renderTarget = new CanvasRenderTarget(device, canvasW, canvasH, localDpi);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        var d = App.AppSettings.PngSize;
                        var r = App.AppSettings.PngSize / 2;

                        var textColor = isBlackText ? Colors.Black : Colors.White;
                        var fontSize = (float)d;

                        using (CanvasTextLayout layout = new CanvasTextLayout(device, $"{SelectedChar.Char} ", new CanvasTextFormat
                        {
                            FontSize = fontSize,
                            FontFamily = SelectedVariant.XamlFontFamily.Source,
                            FontStretch = SelectedVariant.FontFace.Stretch,
                            FontWeight = SelectedVariant.FontFace.Weight,
                            FontStyle = SelectedVariant.FontFace.Style,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center,

                        }, canvasW, canvasH))
                        {
                            layout.Options = ShowColorGlyphs ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default;

                            var db = layout.DrawBounds;
                            double scale = Math.Min(1, Math.Min(canvasW / db.Width, canvasH / db.Height));
                            ds.Transform = Matrix3x2.CreateScale(new Vector2((float)scale));

                            var x = -db.Left + ((canvasW - (db.Width * scale)) / 2d);
                            var y = -db.Top + ((canvasH - (db.Height * scale)) / 2d);
                            ds.DrawTextLayout(layout, new Vector2((float)x, (float)y), textColor);
                        }
                    }

                    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        fileStream.Size = 0;
                        await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowMessageBox(ex.Message, "Error Saving Image");
            }
        }

        private async Task SaveSvgAsync(bool isBlackText)
        {
            try
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                savePicker.FileTypeChoices.Add("SVG", new[] { ".svg" });
                savePicker.SuggestedFileName = $"{SelectedFont.Name} - {SelectedVariant.PreferredName} - {SelectedChar.UnicodeString}.svg";
                var file = await savePicker.PickSaveFileAsync();

                if (null != file)
                {
                    CachedFileManager.DeferUpdates(file);
                    var device = CanvasDevice.GetSharedDevice();

                    var canvasH = (float)App.AppSettings.PngSize;
                    var canvasW = (float)App.AppSettings.PngSize;

                    var d = App.AppSettings.PngSize;
                    var r = App.AppSettings.PngSize / 2;

                    var textColor = isBlackText ? Colors.Black : Colors.White;
                    var fontSize = (float)d;

                    using (CanvasTextLayout layout = new CanvasTextLayout(device, $"{SelectedChar.Char} ", new CanvasTextFormat
                    {
                        FontSize = fontSize,
                        FontFamily = SelectedVariant.XamlFontFamily.Source,
                        FontStretch = SelectedVariant.FontFace.Stretch,
                        FontWeight = SelectedVariant.FontFace.Weight,
                        FontStyle = SelectedVariant.FontFace.Style,
                        HorizontalAlignment = CanvasHorizontalAlignment.Center,

                    }, canvasW, canvasH))
                    using (var geom = CanvasGeometry.CreateText(layout))
                    {
                        layout.Options = ShowColorGlyphs ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default;

                        var db = layout.DrawBounds;
                        double scale = Math.Min(1, Math.Min(canvasW / db.Width, canvasH / db.Height));
                        var x = -db.Left + ((canvasW - (db.Width * scale)) / 2d);
                        var y = -db.Top + ((canvasH - (db.Height * scale)) / 2d);

                        var g = geom
                            .Transform(Matrix3x2.CreateTranslation(new Vector2((float)x, (float)y)))
                            .Transform(Matrix3x2.CreateScale(new Vector2((float)scale)));

                        /* 
                         * Unfortunately this only constructs a black and white path, if we want color
                         * I'm not sure Win2D exposes the neccessary API's to get the individual glyph
                         * layers that make up a colour glyph
                         */
                        SVGPathReciever rc = new SVGPathReciever();
                        g.SendPathTo(rc);

                        string xml = $"<svg width=\"100%\" height=\"100%\" viewBox=\"0 0 {canvasW} {canvasH}\" xmlns=\"http://www.w3.org/2000/svg\"><path d=\"{rc.GetPathData()}\" /></svg>";
                        using (CanvasSvgDocument document = CanvasSvgDocument.LoadFromXml(device, xml))
                        {
                            ((CanvasSvgNamedElement)document.Root.FirstChild).SetColorAttribute("fill", textColor);

                            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                fileStream.Size = 0;
                                await document.SaveAsync(fileStream);
                            }
                        }
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await DialogService.ShowMessageBox(ex.Message, "Error Saving Image");
            }
        }

        private bool _isDarkAccent;
        private string _titlePrefix;

        public bool IsDarkAccent
        {
            get => IsAccentColorDark();
            set { _isDarkAccent = value; RaisePropertyChanged(); }
        }

        private bool IsAccentColorDark()
        {
            var uiSettings = new UISettings();
            var c = uiSettings.GetColorValue(UIColorType.Accent);
            var isDark = (5 * c.G + 2 * c.R + c.B) <= 8 * 128;
            return isDark;
        }
    }
}