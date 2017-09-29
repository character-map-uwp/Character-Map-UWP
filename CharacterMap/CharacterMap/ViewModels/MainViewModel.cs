using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml.Shapes;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Services;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace CharacterMap.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<Character> _chars;
        private string _fontIcon;
        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        private bool _isLightThemeEnabled;
        private Character _selectedChar;
        private InstalledFont _selectedFont;
        private string _xamlCode;
        private string _symbolIcon;

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            RefreshFontList();
            IsLightThemeEnabled = ThemeSelectorService.IsLightThemeEnabled;
            CommandSavePng = new RelayCommand(async () => await SavePng());
            SwitchThemeCommand = new RelayCommand(async () => { await ThemeSelectorService.SwitchThemeAsync(); });
        }

        public ICommand SwitchThemeCommand { get; }

        public IDialogService DialogService { get; set; }

        public RelayCommand CommandSavePng { get; set; }

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

        public ObservableCollection<Character> Chars
        {
            get => _chars;
            set
            {
                _chars = value;
                RaisePropertyChanged();
            }
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
                    var chars = _selectedFont.GetCharacters();
                    Chars = chars.ToObservableCollection();
                }

                RaisePropertyChanged();
            }
        }

        public bool IsLightThemeEnabled
        {
            get => _isLightThemeEnabled;
            set => Set(ref _isLightThemeEnabled, value);
        }

        private void RefreshFontList()
        {
            try
            {
                var fontList = InstalledFont.GetFonts();
                FontList = fontList.Where(f => f.IsSymbolFont || !ShowSymbolFontsOnly)
                    .OrderBy(f => f.Name)
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
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1), true);
                GroupedFontList = list.ToObservableCollection();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(e.Message, "Error Loading Font Group");
            }
        }

        private async Task SavePng()
        {
            try
            {
                var savePicker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop
                };
                savePicker.FileTypeChoices.Add("Png Image", new[] { ".png" });
                savePicker.SuggestedFileName = $"CharacterMap_{DateTime.Now:yyyyMMddHHmmss}.png";
                StorageFile file = await savePicker.PickSaveFileAsync();

                if (null != file)
                {
                    CachedFileManager.DeferUpdates(file);
                    CanvasDevice device = CanvasDevice.GetSharedDevice();
                    var localDpi = Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;
                    CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (float)App.AppSettings.PngSize, (float)App.AppSettings.PngSize, localDpi);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        var d = App.AppSettings.PngSize;
                        var r = App.AppSettings.PngSize / 2;

                        var textColor = ThemeSelectorService.IsLightThemeEnabled ? Colors.White : Colors.Black;

                        ds.DrawText(SelectedChar.Char, (float)r, 0, textColor, new CanvasTextFormat
                        {
                            FontFamily = SelectedFont.Name,
                            FontSize = (float)d,
                            HorizontalAlignment = CanvasHorizontalAlignment.Center
                        });
                    }

                    using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
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
    }
}