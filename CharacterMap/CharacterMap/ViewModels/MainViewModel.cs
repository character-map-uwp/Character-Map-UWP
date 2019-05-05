using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.ViewManagement;
using CharacterMap.Core;
using Edi.UWP.Helpers.Extensions;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using System.Collections.Generic;
using CharacterMap.Helpers;
using Windows.Storage;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using CharacterMap.Services;

namespace CharacterMap.ViewModels
{

    public class MainViewModel : ViewModelBase
    {
        #region Properties

        public event EventHandler FontListCreated;

        private Task _initialLoad { get; }

        public IDialogService DialogService { get; }

        public RelayCommand CommandToggleFullScreen { get; }

        public bool IsDarkAccent => Utils.IsAccentColorDark();

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

        private string _titlePrefix;
        public string TitlePrefix
        {
            get => _titlePrefix;
            set => Set(ref _titlePrefix, value);
        }

        private bool _isLoadingFonts;
        public bool IsLoadingFonts
        {
            get => _isLoadingFonts;
            set => Set(ref _isLoadingFonts, value);
        }

        private bool _hasFonts;
        public bool HasFonts
        {
            get => _hasFonts;
            set => Set(ref _hasFonts, value);
        }

        private List<InstalledFont> _fontList;
        public List<InstalledFont> FontList
        {
            get => _fontList;
            set
            {
                if (_fontList != value)
                {
                    _fontList = value;
                    CreateFontListGroup();
                    RaisePropertyChanged();
                }
            }
        }

        private ObservableCollection<AlphaKeyGroup<InstalledFont>> _groupedFontList;
        public ObservableCollection<AlphaKeyGroup<InstalledFont>> GroupedFontList
        {
            get => _groupedFontList;
            set => Set(ref _groupedFontList, value);
        }

        private InstalledFont _selectedFont;
        public InstalledFont SelectedFont
        {
            get => _selectedFont;
            set
            {
                _selectedFont = value;
                if (null != _selectedFont)
                {
                    TitlePrefix = value.Name + " -";
                    App.AppSettings.LastSelectedFontName = value.Name;
                }
                else
                    TitlePrefix = string.Empty;

                RaisePropertyChanged();
            }
        }

        #endregion

        public MainViewModel(IDialogService dialogService)
        {
            DialogService = dialogService;
            AppNameVersion = Utils.GetAppDescription();
            CommandToggleFullScreen = new RelayCommand(ToggleFullScreenMode);
            MessengerInstance.Register<ImportMessage>(this, OnFontImportRequest);

            _initialLoad = LoadAsync();
        }

        private async Task LoadAsync()
        {
            IsLoadingFonts = true;

            await Task.WhenAll(
                GlyphService.InitializeAsync(),
                FontFinder.LoadFontsAsync());

            RefreshFontList();
            IsLoadingFonts = false;
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
                var fontList = FontFinder.Fonts.AsEnumerable();

                if (FontListFilter == 1)
                    fontList = fontList.Where(f => f.IsSymbolFont);
                else if (FontListFilter == 2)
                    fontList = fontList.Where(f => f.HasImportedFiles);

                FontList = fontList.ToList();
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(
                    e.Message, Localization.Get("LoadingFontListError"));
            }
        }

        private void CreateFontListGroup()
        {
            try
            {
                var list = AlphaKeyGroup<InstalledFont>.CreateGroups(FontList, f => f.Name.Substring(0, 1));
                GroupedFontList = list.ToObservableCollection();
                HasFonts = FontList.Count > 0;

                if (FontList.Count == 0)
                {
                    SelectedFont = null;
                    return;
                }

                if (!string.IsNullOrEmpty(App.AppSettings.LastSelectedFontName))
                {
                    var lastSelectedFont = FontList.FirstOrDefault((i => i.Name == App.AppSettings.LastSelectedFontName));

                    if (null != lastSelectedFont)
                    {
                        this.SelectedFont = lastSelectedFont;
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

                FontListCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception e)
            {
                DialogService.ShowMessageBox(
                    e.Message, Localization.Get("LoadingFontListError"));
            }
        }

        internal void TrySetSelectionFromImport(FontImportResult result)
        {
            StorageFile file = result.Imported.FirstOrDefault() ?? result.Existing.FirstOrDefault();
            if (file != null
                && FontList.FirstOrDefault(f =>
                f.HasImportedFiles && f.DefaultVariant.FileName == file.Name) is InstalledFont font)
            {
                SelectedFont = font;
                FontListCreated?.Invoke(this, EventArgs.Empty);
            }
        }

        internal async void TryRemoveFont(InstalledFont font)
        {
            IsLoadingFonts = true;

            /* Yes, this is hack. The UI needs to time to remove references to the 
             * current Font otherwise we won't be able to delete it because the file will 
             * be "in use". 16ms works fine on my test machines, but better safe than
             * sorry - this isn't a fast operation in sum anyway because we reload
             * all fonts, so extra 150ms is nothing...
             */
            SelectedFont = FontFinder.DefaultFont;
            await Task.Delay(150);

            bool result = await FontFinder.RemoveFontAsync(font);
            RefreshFontList();

            IsLoadingFonts = false;

            if (!result)
            {
                /* looks like we couldn't delete some fonts :'(. 
                 * We'll get em next time the app launches! */

                _ = DialogService.ShowMessage(
                    Localization.Get("FontsClearedOnNextLaunchNotice"),
                    Localization.Get("NoticeLabel/Text"));
            }
        }

        private void OnFontImportRequest(ImportMessage msg)
        {
            _ = CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                IsLoadingFonts = true;
                try
                {
                    if (_initialLoad.IsCompleted)
                    {
                        RefreshFontList();
                    }
                    else
                    {
                        await _initialLoad;
                        await Task.Delay(50);
                    }

                    TrySetSelectionFromImport(msg.Result);
                }
                finally
                {
                    IsLoadingFonts = false;
                }
            });
        }

    }
}