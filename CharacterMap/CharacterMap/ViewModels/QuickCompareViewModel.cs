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
using Windows.ApplicationModel;

namespace CharacterMap.ViewModels
{
    public class QuickCompareViewModel : ViewModelBase
    {
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
        
        public IReadOnlyList<string> TextOptions { get; } = new List<string>
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

        public UserCollectionsService FontCollections { get; }

        public ICommand FilterCommand { get; }


        public QuickCompareViewModel()
        {
            if (DesignMode.DesignModeEnabled)
                return;

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
