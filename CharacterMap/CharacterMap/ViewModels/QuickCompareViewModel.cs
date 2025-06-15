﻿using CharacterMap.Views;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.Windows.Input;
using Windows.ApplicationModel;

namespace CharacterMap.ViewModels;

public class QuickCompareArgs
{
    public FolderContents Folder { get; set; }

    public bool IsQuickCompare { get; set; }

    public bool IsFolderView => Folder is not null;

    public IFontCollection SelectedCollection { get; init; }

    public QuickCompareArgs(bool isQuickCompare, FolderContents folder = null)
    {
        IsQuickCompare = isQuickCompare;
        Folder = folder;
    }
}

public partial class QuickCompareViewModel : ViewModelBase
{
    public const string MultiToken = $"{nameof(QuickCompareViewModel)}Multi";

    public static WindowInformation QuickCompareWindow { get; set; }

    protected override bool TrackAnimation => true;

    [ObservableProperty]
    IReadOnlyList<Suggestion> _textOptions = null;

    public string Title { get => Get<string>(); set => Set(value); }

    public string Text { get => Get<string>(); set => Set(value); }

    public string FilterTitle { get => Get<string>(); set => Set(value); }

    public CMFontFamily SelectedFont { get => Get<CMFontFamily>(); set => Set(value); }

    public ObservableCollection<CMFontFamily> FontList { get => Get<ObservableCollection<CMFontFamily>>(); set => Set(value); }

    private BasicFontFilter _fontListFilter = BasicFontFilter.All;
    public BasicFontFilter FontListFilter
    {
        get => _fontListFilter;
        set { if (Set(ref _fontListFilter, value)) RefreshFontList(); }
    }

    public ObservableCollection<CharacterRenderingOptions> QuickFonts { get; }

    private IFontCollection _selectedCollection;
    public IFontCollection SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (value is UserFontCollection u && u.IsSystemSymbolCollection)
            {
                FontListFilter = BasicFontFilter.SymbolFonts;
                return;
            }

            if (Set(ref _selectedCollection, value) && value != null)
                RefreshFontList(value);
        }
    }

    public INotifyCollectionChanged ItemsSource => IsQuickCompare ? QuickFonts : FontList;


    public UserCollectionsService FontCollections { get; }

    public ICommand FilterCommand { get; }

    public ICommand CollectionSelectedCommand { get; }

    public bool IsQuickCompare { get; }

    public bool IsQuickCompareView { get; }

    public bool IsFolderMode { get; }

    FolderContents _folder = null;

    public QuickCompareViewModel(QuickCompareArgs args)
    {
        // N.B: arg.IsQuickCompare denotes the singleton QuickCompare view.
        //      IsQuickCompare controls the behaviour of this page - they are
        //      two different things. We can have many windows with IsQuickCompare
        //      behaviour, but only one can act as the main QuickCompare singleton.

        IsQuickCompareView = args.IsQuickCompare;
        IsQuickCompare = args.IsQuickCompare || (args.Folder?.UseQuickCompare is bool b && b);

        if (DesignMode.DesignModeEnabled)
            return;

        if (IsQuickCompare)
        {
            if (args.IsQuickCompare)
            {
                // This is the universal quick-compare window
                QuickFonts = new();

                Register<CharacterRenderingOptions>(m =>
                {
                    // Only add the font variant if it's not already in the list.
                    if (!QuickFonts.Any(q => m.IsCompareMatch(q)))
                        QuickFonts.Add(m);
                }, nameof(QuickCompareViewModel));

                Register<CharacterRenderingOptions>(m =>
                {
                    // Only add the font variant if it's not already in the list.
                    foreach (var f in m.Family.Variants)
                    {
                        var ops = m with { Variant = f };
                        if (!QuickFonts.Any(q => ops.IsCompareMatch(q)))
                            QuickFonts.Add(ops);
                    }
                    
                }, MultiToken);
            }
            else
            {
                // This is probably the tab bar compare window
                QuickFonts = new(args.Folder.Variants.Select(v => CharacterRenderingOptions.CreateDefault(v)));
            }
        }
        else
        {
            _folder = args.Folder;
            if (_folder is not null)
                IsFolderMode = true;

            FontCollections = Ioc.Default.GetService<UserCollectionsService>();
            SelectedCollection = args.SelectedCollection;
            RefreshFontList(SelectedCollection);

            FilterCommand = new RelayCommand<object>(e => OnFilterClick(e));
            CollectionSelectedCommand = new RelayCommand<object>(e => SelectedCollection = e as IFontCollection);
        }

        UpdateTextOptions();
        Register<RampOptionsUpdatedMessage>(m => UpdateTextOptions());

        // Set Title
        if (IsQuickCompare && args.IsQuickCompare)
            Title = Localization.Get("QuickCompareTitle/Text");
        else if (IsQuickCompare && args.Folder.IsFamilyCompare)
            Title = string.Format(Localization.Get("CompareFamilyTitle/Text"), QuickFonts.FirstOrDefault()?.Variant.FamilyName);
        else if (IsQuickCompare)
            Title = Localization.Get("CompareFontFaceTitle/Text");
        else if (IsFolderMode && _folder.Source is not null)
            Title = _folder.Source.Name;
        else
            Title = Localization.Get("CompareFontsTitle/Text");
    }

    public void Deactivated()
    {
        if (IsQuickCompareView)
            QuickCompareWindow = null;

        Messenger.UnregisterAll(this);
    }

    protected override void OnPropertyChangeNotified(string propertyName)
    {
        if (propertyName is nameof(FontList) or nameof(QuickFonts))
            OnPropertyChanged(nameof(ItemsSource));
    }

    private void UpdateTextOptions()
    {
        OnSyncContext(() => { TextOptions = GlyphService.GetRampOptions(); });
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

    internal void RefreshFontList(IFontCollection collection = null)
    {
        try
        {
            IEnumerable<CMFontFamily> fontList = _folder?.Fonts ?? FontFinder.Fonts;

            if (collection != null)
            {
                FilterTitle = collection.Name;
                fontList = fontList.Where(f => collection.ContainsFamily(f.Name));
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

            FontList = new(fontList);
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

    public void ShowEditSuggestions()
    {
        Messenger.Send(new EditSuggestionsRequested());
    }
}
