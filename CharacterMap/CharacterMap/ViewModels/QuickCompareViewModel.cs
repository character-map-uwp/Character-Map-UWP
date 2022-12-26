using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Windows.ApplicationModel;

namespace CharacterMap.ViewModels
{
    public class QuickCompareArgs
    {
        public FolderContents Folder { get; set; }
        public bool IsQuickCompare { get; set; }

        public bool IsFolderView => Folder is not null;

        public QuickCompareArgs(bool isQuickCompare, FolderContents folder = null)
        {
            IsQuickCompare = isQuickCompare;
            Folder = folder;
        }
    }

    public class QuickCompareViewModel : ViewModelBase
    {
        public static WindowInformation QuickCompareWindow { get; set; }

        public string Title                 { get => Get<string>(); set => Set(value); }

        public string Text                  { get => Get<string>(); set => Set(value); }

        public string FilterTitle           { get => Get<string>(); set => Set(value); }
        
        public InstalledFont SelectedFont   { get => Get<InstalledFont>(); set => Set(value); }

        public List<InstalledFont> FontList { get => Get<List<InstalledFont>>(); set => Set(value); }

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

        public bool IsFolderMode { get; }

        FolderContents _folder = null;

        public QuickCompareViewModel(QuickCompareArgs args)
        {
            IsQuickCompare = args.IsQuickCompare;

            if (DesignMode.DesignModeEnabled)
                return;

            _folder = args.Folder;
            RefreshFontList();
            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            FilterCommand = new RelayCommand<object>(e => OnFilterClick(e));

            if (_folder is not null)
                IsFolderMode = true;

            if (IsQuickCompare)
            {
                QuickFonts = new ObservableCollection<CharacterRenderingOptions>();
                Register<CharacterRenderingOptions>(m =>
                {
                    // Only add the font variant if it's not already in the list.
                    // Once we start accepting custom typography this comparison
                    // will have to change.
                    if (!QuickFonts.Any(q => m.IsCompareMatch(q)))
                        QuickFonts.Add(m);
                }, nameof(QuickCompareViewModel));
            }

            if (IsQuickCompare)
                Title = Localization.Get("QuickCompareTitle/Text");
            else if (IsFolderMode && _folder.Source is not null)
                Title = _folder.Source.Name;
            else
                Title = Localization.Get("CompareFontsTitle/Text");
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

        internal void RefreshFontList(UserFontCollection collection = null)
        {
            try
            {
                IEnumerable<InstalledFont> fontList = _folder?.Fonts ?? FontFinder.Fonts;

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
