using CommunityToolkit.Mvvm.Input;
using Microsoft.Collections.Extensions;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.UI.Xaml.Media;

namespace CharacterMap.ViewModels;

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
}


public partial class SubsetterViewModel : ViewModelBase
{
    public const string EDIT_STATE = "EditingState";
    public const string PREVIEW_STATE = "PreviewingState";
    public const string EXPORT_STATE = "ExportState";

    const string DEFAULT_FONT_NAME = "Segoe Icons Subset";
    const string DEFAULT_VERSION = "Version 1.00";

    public StrongReferenceMessenger StrongMessenger { get; } = new();

    public ObservableCollection<FamilySelectionModel> Families { get; }

    public ObservableCollection<FaceSelectionModel> SelectedFaces { get; } = new();

    public bool IsPreviewable => SelectedFaces.Count > 0 && !string.IsNullOrWhiteSpace(FamilyName);
    public bool IsExportable => IsPreviewable && !HasClashing;

    [ObservableProperty] FamilySelectionModel _selectedFamily;
    [ObservableProperty] FaceSelectionModel _selectedFace;
    [ObservableProperty] FontFamily _selectedXAMLFontFamily;
    [ObservableProperty] string _familyName = DEFAULT_FONT_NAME;
    [ObservableProperty] string _version = DEFAULT_VERSION;
    [ObservableProperty] bool _hasClashing = false;
    [ObservableProperty] string _generatedCode = null;
    [ObservableProperty] string _generatedClassName = null;

    [ObservableProperty] ObservableCollection<FontGlyph> _previewList;

    List<FontGlyph> _exportChars = null;
    FontClassGenerator _generator = null;
    string _className = null;

    public FaceSelectionModel SvgGlyphContainerFace { get; }

    HashSet<int> _selectedIndexes { get; } = new();

    /// <summary>
    /// Unicode indexes that appear more than once in <see cref="PreviewList"/>,
    /// meaning two glyphs from different source fonts share the same codepoint
    /// and will clash in the output font.
    /// </summary>
    [ObservableProperty] HashSet<uint> _clashingIndexes = [];

    public SubsetterViewModel(SubsetterArgs args)
    {
        ViewState = EDIT_STATE;

        SvgGlyphContainerFace = new FaceSelectionModel(null, null, StrongMessenger);

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
            _debouncer.Debounce(100, UpdateClashing);
        });
    }

    Debouncer _debouncer = new();

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

    [RelayCommand]
    public void GoBack()
    {
        if (ViewState == PREVIEW_STATE)
            ViewState = EDIT_STATE;
    }

    [RelayCommand]
    void ShowPreview()
    {
        UpdateClashing();

        ObservableCollection<FontGlyph> list = [..SelectedFaces
            .SelectMany(sf => sf.GetGlyphs())
            .OrderBy(fg => fg.Character.UnicodeIndex)];

        PreviewList = list;
        _exportChars = GetExportChars();
        _generator = new();
        UpdateCodeInternal();

        ViewState = PREVIEW_STATE;
    }

    [RelayCommand]
    void ClearSelection()
    {
        SelectedFace?.CustomGlyphs.Clear();
        SelectedFace?.SelectedCharacters.Clear();
    }

    [RelayCommand]
    void SelectAll() => SelectedFace?.SelectAll();

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
        if (e is FaceSelectionModel face && face.Face != null)
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

            List<FontGlyph> chars = _exportChars;

            // 3. Note: version string currently isn't supported by the subsetter table-rewritter
            var file = await FontSubsetter.CreateSubsetAsync(new(fontName, chars, _className, target, version));
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

    private List<FontGlyph> GetExportChars()
    {
        List<FontGlyph> chars = PreviewList is { Count: > 0 }
                        ? [.. PreviewList]
                        : SelectedFaces.SelectMany(sf => sf.GetGlyphs()).ToList();

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

        return chars;
    }

    // Start at Private use supplmentary A to avoid Segoe glyphs
    uint _nextCustomPua = 0xF0000;

    [RelayCommand]
    async Task AddSVGAsync()
    {
        var state = ViewState;

        try
        {
            ViewState = "ExportPreviewingState"; // Shows Progress Ring 

            if (await StorageHelper.PickOpenFileAsync([".svg"], "Select SVG Glyph")
                    is not StorageFile file)
                return;

            if (await SVGGlyphHelper.TryLoadFontGlyphAsync(file, _nextCustomPua) is FontGlyph glyph)
            {
                _nextCustomPua = glyph.Character.UnicodeIndex + 1;
                SvgGlyphContainerFace.CustomGlyphs.Add(glyph);

                OnPropertyChanged(nameof(IsPreviewable));
                OnPropertyChanged(nameof(IsExportable));
            }
            else
            {
                Notify(new ActionFailedMessage("Failed to load svg"));
            }
        }
        finally
        {
            ViewState = state;
        }
    }

    Debouncer _codeDebouncer { get; } = new();

    partial void OnGeneratedClassNameChanged(string value)
        => _codeDebouncer.Debounce(300, UpdateCodeInternal);

    public void UpdateCode() => _codeDebouncer.Debounce(33, UpdateCodeInternal);

    private void UpdateCodeInternal()
    {
        var chars = _exportChars;
        var generated = _generator.Generate(
            FamilyName, 
            GeneratedClassName, 
            chars, 
            FontCodeOutputType.CharLiteral);

        _className = generated.ClassName;
        GeneratedCode = generated.Content;
    }

    public void CopyCode() => Utils.CopyToClipBoard(GeneratedCode);

    [RelayCommand]
    async Task SaveCodeAsync()
    {
        if (await StorageHelper.PickSaveFileAsync(
                $"{_className}", "CSharp", [".cs"], PickerLocationId.Unspecified)
            is not StorageFile file)
            return;

        await FileIO.WriteTextAsync(file, GeneratedCode);
    }
}
