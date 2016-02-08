using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CharacterMap.Core;
using Edi.UWP.Helpers;
using GalaSoft.MvvmLight;

namespace CharacterMap.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private ObservableCollection<InstalledFont> _fontList;
        private ObservableCollection<Character> _chars;
        private InstalledFont _selectedFont;

        public ObservableCollection<InstalledFont> FontList
        {
            get { return _fontList; }
            set { _fontList = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<Character> Chars
        {
            get { return _chars; }
            set { _chars = value; RaisePropertyChanged(); }
        }

        public InstalledFont SelectedFont
        {
            get { return _selectedFont; }
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

        public MainViewModel()
        {
            var fontList = InstalledFont.GetFonts();
            FontList = fontList.OrderBy(f => f.Name).ToObservableCollection();
        }
    }
}
