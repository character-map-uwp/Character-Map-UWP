namespace CharacterMap.Core;

/// <summary>
/// Represents an entire FontFamily (currently under the WWS definition)
/// </summary>
public class CMFontFamily : IComparable, IEquatable<CMFontFamily>
{
    private List<CMFontFace> _variants;

    private List<CMFontFace> _simulatedVariants;

    private DWriteFontFamily _dwriteFamily;


    public string Name { get; }

    public bool IsSymbolFont => Variants.Count > 0 && Variants[0].DirectWriteProperties.IsSymbolFont;

    public IList<CMFontFace> Variants
    {
        get
        {
            EnsureInflated();
            return _variants;
        }
    }

    /// <summary>
    /// Identifies if a font family has any REAL different font faces
    /// </summary>
    public bool HasVariants
    {
        get
        {
            if (_dwriteFamily is not null && _variants.Count == 0)
                return _dwriteFamily.FontCount > 1;
            return _variants.Count > 1;
        }
    }

    /// <summary>
    /// Identifies if a font family has any other font faces, included
    /// simulated faces
    /// </summary>
    public bool HasAnyVariants => AllVariants.Count > 1;

    public bool HasImportedFiles { get; private set; }

    public CMFontFace DefaultVariant => field ??= Utils.GetDefaultVariant(Variants);

    public List<object> AllVariants => field ??= CreateVariants();

    public List<object> NonSimulatedVariants => field ??= CreateVariants(true);


    private CMFontFamily(string name)
    {
        Name = name;
        _variants = [];
    }

    public CMFontFamily(string name, DWriteFontFace face) : this(name, face, (string)null) { }

    public CMFontFamily(string name, DWriteFontFace face, StorageFile file) : this(name, face, file?.Path) { }

    public CMFontFamily(string name, DWriteFontFace face, string filePath) : this(name)
    {
        AddVariant(face, filePath);
    }

    public CMFontFamily(string name, DWriteFontFamily family) : this(name)
    {
        _dwriteFamily = family;
    }

    public void EnsureInflated()
    {
        if (_dwriteFamily is not null && _variants.Count == 0)
        {
            lock (_dwriteFamily)
            {
                if (_variants.Count == 0)
                {
                    bool hideSimulated = ResourceHelper.AppSettings.HideSimulatedFontFaces;
                    foreach (DWriteFontFace font in _dwriteFamily.Fonts)
                    {
                        if (font.Properties.IsSimulated && hideSimulated)
                            continue;
                        AddVariant(font);
                    }
                    SortVariants();
                    _dwriteFamily = null;
                }
            }
        }
    }


    public void AddVariant(DWriteFontFace fontFace) => AddVariant(fontFace, (string)null);

    public void AddVariant(DWriteFontFace fontFace, StorageFile file) => AddVariant(fontFace, file?.Path);

    public void AddVariant(DWriteFontFace fontFace, string filePath)
    {
        if (fontFace.Properties.IsSimulated is false)
            _variants.Add(new(fontFace, filePath));
        else
        {
            _simulatedVariants ??= [];
            _simulatedVariants.Add(new(fontFace, filePath));
        }

        if (!string.IsNullOrEmpty(filePath))
            HasImportedFiles = true;
    }

    List<object> CreateVariants(bool excludeSimulated = false)
    {
        List<object> objs = new(Variants);
        if (_simulatedVariants is not null && _simulatedVariants.Count > 0 && !excludeSimulated)
        {
            objs.Add(Localization.Get("SimulatedHeader"));
            objs.AddRange(_simulatedVariants);
        }

        return objs;
    }

    public void SortVariants()
    {
        if (_variants.Count > 1)
            _variants.Sort((a, b) => a.DirectWriteProperties.Weight.Weight.CompareTo(b.DirectWriteProperties.Weight.Weight));
    }

    public void PrepareForDelete()
    {
        //FontFace = null;
    }

    public CMFontFamily Clone()
    {
        EnsureInflated();
        return new(this.Name)
        {
            _variants = this._variants.ToList(),
            HasImportedFiles = this.HasImportedFiles
        };
    }

    public static CMFontFamily CreateDefault(DWriteFontFace face)
    {
        CMFontFamily font = new(face.Properties.FamilyName);
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
