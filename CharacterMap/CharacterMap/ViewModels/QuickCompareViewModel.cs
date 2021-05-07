using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.Views;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.ApplicationModel;
using System.Collections.ObjectModel;

namespace CharacterMap.ViewModels
{
    public class QuickCompareViewModel : ViewModelBase
    {
        public static WindowInformation QuickCompareWindow { get; set; }

        public string Text { get => Get<string>(); set => Set(value); }

        public string FilterTitle { get => Get<string>(); set => Set(value); }

        public List<InstalledFont> FontList { get => Get<List<InstalledFont>>(); set => Set(value); }

        public InstalledFont SelectedFont { get => Get<InstalledFont>(); set => Set(value); }

        private BasicFontFilter _fontListFilter = BasicFontFilter.All;
        public BasicFontFilter FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }

        public ObservableCollection<CharacterRenderingOptions> QuickFonts { get; }

        private UserFontCollection _selectedCollection;
        public UserFontCollection SelectedCollection
        {
            get => _selectedCollection;
            set
            {
                if (value != null && value.IsSystemSymbolCollection)
                {
                    FontListFilter = BasicFontFilter.SymbolFonts;
                    return;
                }

                if (Set(ref _selectedCollection, value) && value != null)
                    RefreshFontList(value);
            }
        }

        public object ItemsSource => IsQuickCompare ? QuickFonts : FontList;

        public IReadOnlyList<string> TextOptions { get; } = GlyphService.DefaultTextOptions;

        public UserCollectionsService FontCollections { get; }

        public ICommand FilterCommand { get; }

        public bool IsQuickCompare { get;  }

        public Action<Action> Dispatch { get; set; }

        public QuickCompareViewModel(bool isQuickCompare)
        {
            IsQuickCompare = isQuickCompare;
            if (DesignMode.DesignModeEnabled)
                return;

            RefreshFontList();
            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            FilterCommand = new RelayCommand<object>(e => OnFilterClick(e));

            if (IsQuickCompare)
            {
                QuickFonts = new ObservableCollection<CharacterRenderingOptions>();
                Register<CharacterRenderingOptions>(m =>
                {
                    Dispatch(() => QuickFonts.Add(m));
                }, nameof(QuickCompareViewModel));
            }
        }

        public void Deactivated()
        {
            if (IsQuickCompare)
                QuickCompareWindow = null;

            Messenger.UnregisterAll(this);
        }

        protected override void OnPropertyChangeNotified(string propertyName)
        {
            if (propertyName == nameof(FontList) || propertyName == nameof(QuickFonts))
                OnPropertyChanged(nameof(ItemsSource));
        }

        private void OnFilterClick(object e)
        {
            if (e is BasicFontFilter filter)
            {
                if (filter == FontListFilter)
                    RefreshFontList();
                else
                    FontListFilter = filter;
            }
        }

        private void RefreshFontList(UserFontCollection collection = null)
        {
            try
            {
                var fontList = FontFinder.Fonts.AsEnumerable();

                if (collection != null)
                {
                    FilterTitle = collection.Name;
                    fontList = fontList.Where(f => collection.Fonts.Contains(f.Name));
                }
                else
                {
                    SelectedCollection = null;
                    FilterTitle = FontListFilter.FilterTitle;

                    if (FontListFilter == BasicFontFilter.ImportedFonts)
                        fontList = FontFinder.ImportedFonts;
                    else
                        fontList = FontListFilter.Query(fontList, FontCollections);
                }

                FontList = fontList.ToList();
            }
            catch (Exception e)
            {

            }
        }

        public void OpenCurrentFont()
        {
            if (SelectedFont is not null)
                _ = FontMapView.CreateNewViewForFontAsync(SelectedFont);
        }
    }
}
