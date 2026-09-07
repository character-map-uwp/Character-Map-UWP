using CommunityToolkit.Mvvm.Input;
using Microsoft.Collections.Extensions;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.ApplicationModel.VoiceCommands;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels;

#region Models

public class SubsetterArgs
{
    public bool MDL2FluentOnly { get; set; } = true;
}

public record class CollectionChangedMessage(object Sender, NotifyCollectionChangedEventArgs Args);

public class FamilySelectionModel(CMFontFamily family, IMessenger messenger)
{
    public List<FaceSelectionModel> Faces => field
        ??= [..Family.NonSimulatedVariants.OfType<CMFontFace>().Select(
            v => new FaceSelectionModel(this, v, messenger))];

    public FaceSelectionModel Default => field ??= Faces.Where(f => f.Face == Family.DefaultVariant).FirstOrDefault();

    public CMFontFamily Family { get; } = family;

    public bool CanSelectFace => Faces?.Count > 1;

    private FamilySelectionModel This => this;
}

public partial class FaceSelectionModel : ObservableObject
{
    private readonly IMessenger _messenger;

    [ObservableProperty]
    ObservableCollection<Character> _selectedCharacters = new();

    [ObservableProperty]
    ObservableCollection<FontGlyph> _customGlyphs = new();

    public FamilySelectionModel Family { get; }

    /// <summary>
    /// Returns true if backed by a "real" FontFace, otherwise
    /// this represents important glyphs
    /// </summary>
    public bool IsPhysical { get; }

    public CMFontFace Face { get; }

    public int SelectedCount => SelectedCharacters.Count + CustomGlyphs.Count;

    public string DisplayName => IsPhysical ? Face.FamilyName : "Imported SVG Glyphs";
    
    public string DisplayVariant => IsPhysical ? Face.PreferredName : "Custom Paths";

    public IEnumerable<FontGlyph> GetGlyphs()
    {
        var normalGlyphs = IsPhysical
            ? SelectedCharacters.Select(c => new FontGlyph(Face, c) { GlyphName = GetGlyphName(c) })
            : Enumerable.Empty<FontGlyph>();

        return normalGlyphs.Concat(CustomGlyphs);
    }

    public FaceSelectionModel(FamilySelectionModel family, CMFontFace face, IMessenger messenger)
    {
        Family = family;
        Face = face;
        _messenger = messenger;

        IsPhysical = face is not null;
        Face ??= FontFinder.DefaultFont.DefaultVariant;

        // Notify when our selection changes
        SelectedCharacters.CollectionChanged += Selection_CollectionChanged;
        CustomGlyphs.CollectionChanged += Selection_CollectionChanged;
    }

    private void Selection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedCount));
        _messenger.Send(new CollectionChangedMessage(this, e));
    }

    public void SelectAll()
    {
        if (IsPhysical is false) 
            return;

        SelectedCharacters.CollectionChanged -= Selection_CollectionChanged;
        SelectedCharacters = [.. Face.Characters];
        SelectedCharacters.CollectionChanged += Selection_CollectionChanged;
        _messenger.Send(new CollectionChangedMessage(this, null));
    }

    private string GetGlyphName(Character c)
    {
        // If the font has post/name table, try to load the name from there.
        return Face.GetDefinedCharacterName(c);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="count"></param>
    /// <param name="sourceCount">use for x:Bind to re-call this method</param>
    /// <returns></returns>
    public List<Character> GetSelectedPreview(int count, int sourceCount)
    {
        return SelectedCharacters.Take(count).ToList();
    }

    public List<FontGlyph> GetCustomPreview(int count, int sourceCount)
    {
        int diff = SelectedCount - count;
        CustomFooter = diff > 0 ? Localization.Get("PlusMore", diff) : string.Empty;
        return CustomGlyphs.Take(count).ToList();
    }

    [ObservableProperty] string _customFooter;
}

#endregion

public partial class SubsetterViewModel : ViewModelBase
{
    public const string EDIT_STATE = "EditingState";
    public const string SVG_PREVIEW_STATE = "SVGImportPreviewState";
    public const string PREVIEW_STATE = "PreviewingState";
    public const string EXPORT_STATE = "ExportState";

    const string DEFAULT_FONT_NAME = "Segoe Icons Subset";
    const string DEFAULT_VERSION = "Version 1.00";

    public StrongReferenceMessenger StrongMessenger { get; } = new();

    public ObservableCollection<FamilySelectionModel> Families { get; }

    public ObservableCollection<FaceSelectionModel> SelectedFaces { get; } = new();

    public FaceSelectionModel SvgGlyphContainerFace { get; }


    public bool IsPreviewable => SelectedFaces.Count > 0 && !string.IsNullOrWhiteSpace(FamilyName);
    public bool IsExportable => IsPreviewable && !HasClashing;

    [ObservableProperty] FamilySelectionModel _selectedFamily;
    [ObservableProperty] FaceSelectionModel _selectedFace;
    [ObservableProperty] FontFamily _selectedXAMLFontFamily;
    [ObservableProperty] string _familyName = DEFAULT_FONT_NAME;
    [ObservableProperty] string _version = DEFAULT_VERSION;
    [ObservableProperty] bool _hasClashing = false;
    [ObservableProperty] GlyphCollection _previewList;
    [ObservableProperty] FontGlyph _svgPreview;

    /// <summary>
    /// Unicode indexes that appear more than once in <see cref="PreviewList"/>,
    /// meaning two glyphs from different source fonts share the same codepoint
    /// and will clash in the output font.
    /// </summary>
    [ObservableProperty] HashSet<uint> _clashingIndexes = [];

    HashSet<int> _selectedIndexes { get; } = new();


    /* Code Export Properties */
    [ObservableProperty] string _generatedCode = null;
    [ObservableProperty] string _generatedClassName = null;
    [ObservableProperty] CodeTemplateOption _codeTemplate = null;
    FontClassGenerator _generator = null;
    string _computedClassName = null;


   

    public SubsetterViewModel(SubsetterArgs args)
    {
        ViewState = EDIT_STATE;

        SvgGlyphContainerFace = new FaceSelectionModel(null, null, StrongMessenger);

        _codeTemplate = CodeTemplates.Options[0];

        Families = [..(args.MDL2FluentOnly
            ? FontFinder.Fonts.Where(f => f.Name.Contains("MDL2", StringComparison.InvariantCultureIgnoreCase) ||
                f.Name.Contains("Fluent", StringComparison.InvariantCultureIgnoreCase)).ToList()
            : FontFinder.Fonts).Select(f => new FamilySelectionModel(f, StrongMessenger))];

        SelectedFamily = Families.FirstOrDefault();

        StrongMessenger.Register<CollectionChangedMessage>(this, (o, msg) =>
        {
            if (msg.Sender is not FaceSelectionModel face)
                return;

            if (face.SelectedCount > 0)
            {
                if (SelectedFaces.Contains(face) is false)
                    SelectedFaces.Add(face);
            }
            else
                SelectedFaces.Remove(face);

            OnPropertyChanged(nameof(IsPreviewable));
            OnPropertyChanged(nameof(IsExportable));
            _clashDebouncer.Debounce(UpdateClashing);
        });
    }

    

    bool _blockFace = false;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(FamilyName))
            OnPropertyChanged(nameof(IsPreviewable));

        if (e.PropertyName == nameof(SelectedFamily) && !_blockFace)
            SelectedFace = SelectedFamily?.Default;

        if (e.PropertyName == nameof(SelectedFace))
            SelectedXAMLFontFamily = SelectedFace == null ? null : new FontFamily(SelectedFace.Face.Source);
    }




    //------------------------------------------------------
    //
    // UI Commands
    //
    //------------------------------------------------------

    public void GoBack()
    {
        if (ViewState == PREVIEW_STATE)
            ViewState = EDIT_STATE;
        else if (ViewState == SVG_PREVIEW_STATE)
            ViewState = EDIT_STATE;
    }

    public void ShowPreview()
    {
        UpdateClashing();

        PreviewList = GetExportChars();

        _generator = new();
        UpdateCodeInternal();

        ViewState = PREVIEW_STATE;
    }

    

    public void ClearSelection()
    {
        SelectedFace?.CustomGlyphs.Clear();
        SelectedFace?.SelectedCharacters.Clear();
    }

    public void SelectAll() => SelectedFace?.SelectAll();

    [RelayCommand]
    async Task OpenFontAsync()
    {
        if (await StorageHelper.PickOpenFileAsync(
                   FontImporter.ImportFormats.Where(f => !f.Equals(".zip", StringComparison.InvariantCultureIgnoreCase)),
                   Localization.Get("OpenFontPickerConfirm")) is StorageFile file)
        {
            if (await FontImporter.LoadFromFileAsync(file) is CMFontFamily font)
            {
                FamilySelectionModel family = new(font, StrongMessenger);

                Families.Add(family);
                SelectedFamily = family;

                // If we have no glyphs selected, assume we're actually intending on 
                // augmenting this font file and set font information based on this.
                if ((SelectedFaces?.Where(f => f.IsPhysical).Count() ?? 0) == 0)
                {
                    if (!string.IsNullOrWhiteSpace(font.Name)
                        && FamilyName == DEFAULT_FONT_NAME)
                        FamilyName = font.Name;

                    string ver = font.DefaultVariant?.TryGetInfo(Microsoft.Graphics.Canvas.Text.CanvasFontInformation.VersionStrings)?.Value;
                    if (!string.IsNullOrWhiteSpace(ver)
                        && Version == DEFAULT_VERSION)
                        Version = ver;

                    // If we are an existing icon font created with Character Map UWP,
                    // probably we are editing the font, so select all glyphs by default
                    if (CMSVTable.TryDecode(family.Default.Face) is { } table)
                    {
                        GeneratedClassName = table.ClassName;
                        SelectAll();
                    }
                }
            }
            else
            {
                Notify(new ActionFailedMessage("Couldn't open font"));
            }
        }
    }

    [RelayCommand]
    void RemoveGlyph(FontGlyph glyph)
    {
        PreviewList.Remove(glyph);
        if (glyph.IsPhysical)
            SelectedFaces.First(f => glyph.FontFace == f.Face).SelectedCharacters.Remove(glyph.Character);
        else
            SelectedFaces.FirstOrDefault(f => f.IsPhysical is false)?.CustomGlyphs.Remove(glyph);

        if (HasClashing)
            UpdateClashing();
    }

    [RelayCommand]
    void SetListItem(object e)
    {
        if (e is FaceSelectionModel { IsPhysical: true } face)
        {
            _blockFace = true;
            SelectedFamily = face.Family;
            SelectedFace = face;
            _blockFace = false;
        }
    }

    [RelayCommand]
    private async Task SubsetAsync()
    {
        var sourceState = ViewState;
        ViewState = $"Export{sourceState}";

        try
        {
            string fontName = FamilyName;
            string version = Version;

            // 1. Choose a file
            if (await StorageHelper.PickSaveFileAsync(fontName, Localization.Get("ExportFontFile/Text"), new[] { ".ttf" }, PickerLocationId.DocumentsLibrary)
                is not StorageFile target)
                return;


            // 3. Note: version string currently isn't supported by the subsetter table-rewritter
            var file = await FontSubsetter.CreateSubsetAsync(new(fontName, PreviewList, _computedClassName, target, version));
            if (file is not null && await FontImporter.LoadFromFileAsync(file) is CMFontFamily font)
                Notify(new SubsetResultMessage(font, file));
            else
                Notify(new SubsetResultMessage(null, file));
        }
        finally
        {
            ViewState = sourceState;
        }
    }

    

    // Start at 'Private-Use Supplmentary A' to avoid Segoe glyphs
    uint _nextCustomPua = 0xF0000;
    string prevState = EDIT_STATE;

    [RelayCommand]
    async Task AddSVGAsync()
    {
        prevState = ViewState;

        try
        {
            ViewState = "ExportPreviewingState"; // Shows Progress Ring 

            if (await StorageHelper.PickOpenFileAsync([".svg"], "Select SVG Glyph")
                    is not StorageFile file)
            {
                ViewState = prevState;
                return;
            }

            if (await SVGGlyphHelper.TryLoadFontGlyphAsync(file, _nextCustomPua) is FontGlyph glyph)
            {
                SvgPreview = glyph;

                // needed for VisualTransition to fire
                ViewState = prevState;
                ViewState = SVG_PREVIEW_STATE;
                return;
            }
            else
            {
                Notify(new ActionFailedMessage("Failed to load svg"));
            }
        }
        catch
        {
        }

        ViewState = prevState;
    }

    public void AcceptSVG()
    {
        _nextCustomPua = SvgPreview.Character.UnicodeIndex + 1;
        SvgGlyphContainerFace.CustomGlyphs.Add(SvgPreview);

        OnPropertyChanged(nameof(IsPreviewable));
        OnPropertyChanged(nameof(IsExportable));

        ViewState = prevState;
    }

    [RelayCommand]
    async Task SaveCodeAsync()
    {
        if (await StorageHelper.PickSaveFileAsync(
                $"{_computedClassName}", CodeTemplate.Language, [CodeTemplate.FileExtension], PickerLocationId.Unspecified)
            is not StorageFile file)
            return;

        await FileIO.WriteTextAsync(file, GeneratedCode);
    }




    //------------------------------------------------------
    //
    // Code Generator Handling
    //
    //------------------------------------------------------

    Debouncer _codeDebouncer { get; } = new();

    partial void OnGeneratedClassNameChanged(string value) => EnqueueCodeUpdate();

    void EnqueueCodeUpdate(bool force = false) => _codeDebouncer.Debounce(300, UpdateCodeInternal);

    public void UpdateCode() => _codeDebouncer.Debounce(33, UpdateCodeInternal);

    public void CopyCode() => Utils.CopyToClipBoard(GeneratedCode);

    partial void OnCodeTemplateChanged(CodeTemplateOption value) => UpdateCode();

    private void UpdateCodeInternal()
    {
        if (_generator is null) return;
        var generated = _generator.ProcessTemplate(
            CodeTemplate,
            FamilyName,
            GeneratedClassName,
            null,
            PreviewList,
            true);

        _computedClassName = generated.OutputClassName;
        GeneratedCode = generated.Content;
    }




    //------------------------------------------------------
    //
    // Misc Helpers
    //
    //------------------------------------------------------

    Debouncer _clashDebouncer = new(100);

    private void UpdateClashing()
    {
        // Build a map of UnicodeIndex → how many different FaceSelectionModels selected it.
        // Any index appearing in more than one face's selection is a clash.
        MultiValueDictionary<uint, Character> dic = new();
        foreach (FaceSelectionModel face in SelectedFaces)
            foreach (Character c in face.SelectedCharacters)
                dic.Add(c.UnicodeIndex, c);

        ClashingIndexes = [..dic
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => kvp.Key)];

        HasClashing = ClashingIndexes.Count > 0;
        OnPropertyChanged(nameof(IsExportable));
    }

    private GlyphCollection GetExportChars()
    {
        GlyphCollection chars = PreviewList is { Count: > 0 }
                        ? [.. PreviewList]
                        : [.. SelectedFaces.SelectMany(sf => sf.GetGlyphs())];

        // Handle custom SVGs
        uint exportPua = 0xF0000;
        HashSet<uint> usedCodepoints = new(chars.Select(c => c.Character.UnicodeIndex));

        foreach (FontGlyph custom in SvgGlyphContainerFace.CustomGlyphs)
        {
            if (chars.Contains(custom))
                continue;

            while (usedCodepoints.Contains(exportPua))
                exportPua++;

            custom.Character = new Character(exportPua);
            chars.Add(custom);
            exportPua++;
        }

        // Don't use lambdas here - they will get GC'd immediately
        return chars;
    }

    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        protected override void InsertItem(int index, T item)
        {
            base.InsertItem(index, item);
            OnItemSet(index, item);
        }

        protected override void RemoveItem(int index)
        {
            bool removed = false;
            T item = default;
            if (index < Items.Count)
            {
                item = Items[index];
                removed = true;
            }
            base.RemoveItem(index);

            if (removed)
                OnItemRemoved(index, item);
        }

        protected override void SetItem(int index, T item)
        {
            bool removed = false;
            T old = default;
            if (index < Items.Count)
            {
                old = Items[index];
                removed = true;
            }

            base.SetItem(index, item);
            if (removed)
                OnItemRemoved(index, old);

            OnItemSet(index, item);
        }

        protected override void ClearItems()
        {
            foreach (var item in Items)
                OnItemRemoved(-1, item);

            base.ClearItems();
        }

        protected virtual void OnItemSet(int index, T item) { }
        protected virtual void OnItemRemoved(int index, T item) { }

        protected bool Set<V>(ref V field, V value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<V>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(new(propertyName));
            return true;
        }
    }

    public partial class GlyphCollection : ObservableCollectionEx<FontGlyph>
    {
        Debouncer _debouncer = new Debouncer(16);
                                                                                                                        
        public bool HasNamedGlyphs
        {
            get => field;
            set => Set(ref field, value);
        }

        protected override void OnItemSet(int index, FontGlyph item) => TrackItem(item);

        protected override void OnItemRemoved(int index, FontGlyph item)
        {
            Untrack(item);
            if (item.HasName)
                _debouncer.Debounce(Recheck);
        }

        protected override void ClearItems()
        {
            base.ClearItems();
            HasNamedGlyphs = false;
        }

        void TrackItem(FontGlyph item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
            item.PropertyChanged += Item_PropertyChanged;
            HasNamedGlyphs = HasNamedGlyphs || item.HasName;
        }

        void Untrack(FontGlyph item)
        {
            item.PropertyChanged -= Item_PropertyChanged;
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is not FontGlyph g) return;

            if (g.HasName)
                HasNamedGlyphs = true;
            else
                _debouncer.Debounce(Recheck);
        }

        void Recheck() => HasNamedGlyphs = this.Items.Any(g => g.HasName);
    }
}
