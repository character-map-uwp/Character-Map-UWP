namespace CharacterMap.Core;

/// <summary>
/// Represents an entire FontFamily (currently under the WWS definition)
/// </summary>
public class CMFontFamily : IComparable, IEquatable<CMFontFamily>
{
    private List<CMFontFace> _variants;

    private List<CMFontFace> _simulatedVariants;

    private List<object> _allVariants;

    public string Name { get; }

    public bool IsSymbolFont => _variants[0].DirectWriteProperties.IsSymbolFont;

    public IList<CMFontFace> Variants => _variants;

    /// <summary>
    /// Identifies if a font family has any REAL different font faces
    /// </summary>
    public bool HasVariants => _variants.Count > 1;

    /// <summary>
    /// Identifies if a font family has any other font faces, included
    /// simulated faces
    /// </summary>
    public bool HasAnyVariants => AllVariants.Count > 1;

    public bool HasImportedFiles { get; private set; }

    private CMFontFace _defaultVariant;
    public CMFontFace DefaultVariant => _defaultVariant ??= Utils.GetDefaultVariant(Variants);

    public List<object> AllVariants => _allVariants ??= CreateVariants();


    private CMFontFamily(string name)
    {
        Name = name;
        _variants = new();
    }

    public CMFontFamily(string name, DWriteFontFace face, StorageFile file = null) : this(name)
    {
        AddVariant(face, file);
    }


    public void AddVariant(DWriteFontFace fontFace, StorageFile file = null)
    {
        if (fontFace.Properties.IsSimulated is false)
            _variants.Add(new(fontFace, file));
        else
        {
            _simulatedVariants ??= new();
            _simulatedVariants.Add(new(fontFace, file));
        }

        if (file != null)
            HasImportedFiles = true;

        _allVariants = null;
        _defaultVariant = null;
    }

    List<object> CreateVariants()
    {
        List<object> objs = new(Variants);
        if (_simulatedVariants is not null && _simulatedVariants.Count > 0)
        {
            objs.Add(Localization.Get("SimulatedHeader"));
            objs.AddRange(_simulatedVariants);
        }

        return objs;
    }

    public void SortVariants()
    {
        _variants = _variants.OrderBy(v => v.DirectWriteProperties.Weight.Weight).ToList();
    }

    public void PrepareForDelete()
    {
        //FontFace = null;
    }

    public CMFontFamily Clone()
    {
        return new CMFontFamily(this.Name)
        {
            _variants = this._variants.ToList(),
            HasImportedFiles = this.HasImportedFiles
        };
    }

    public static CMFontFamily CreateDefault(DWriteFontFace face)
    {
        CMFontFamily font = new ("Segoe UI");
        font._variants.Add(CMFontFace.CreateDefault(face));
        return font;
    }




    #region IComparable, IEquatable

    public int CompareTo(object obj)
    {
        if (obj is CMFontFamily f)
            return Name.CompareTo(f.Name);

        return 0;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as CMFontFamily);
    }

    public bool Equals(CMFontFamily other)
    {
        return other is not null &&
               Name == other.Name;
    }

    public override int GetHashCode()
    {
        int hashCode = -1425556920;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<IList<CMFontFace>>.Default.GetHashCode(Variants);
        hashCode = hashCode * -1521134295 + HasImportedFiles.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<CMFontFace>.Default.GetHashCode(DefaultVariant);
        return hashCode;
    }

    public static bool operator ==(CMFontFamily left, CMFontFamily right)
    {
        return EqualityComparer<CMFontFamily>.Default.Equals(left, right);
    }

    public static bool operator !=(CMFontFamily left, CMFontFamily right)
    {
        return !(left == right);
    }

    #endregion
}
