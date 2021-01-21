using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CharacterMap.ViewModels
{
    public class QuickCompareViewModel : ViewModelBase
    {
        public string FilterTitle { get => Get<string>(); set => Set(value); }

        public List<InstalledFont> FontList { get => Get<List<InstalledFont>>(); set => Set(value); }

        private BasicFontFilter _fontListFilter = BasicFontFilter.All;
        public BasicFontFilter FontListFilter
        {
            get => _fontListFilter;
            set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
        }


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

        public UserCollectionsService FontCollections { get; }

        public ICommand FilterCommand { get; }


        public QuickCompareViewModel()
        {
            RefreshFontList();
            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            FilterCommand = new RelayCommand<object>(e => OnFilterClick(e));

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
    }
}
