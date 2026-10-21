namespace CharacterMap.Core;

/// <summary>
/// Represents an entire FontFamily (currently under the WWS definition)
/// </summary>
public class CMFontFamily : IComparable, IEquatable<CMFontFamily>
{
    private List<CMFontFace> _fontFaces;

    private List<CMFontFace> _simulatedFaces;

    private DWriteFontFamily _dwriteFamily;


    public string Name { get; }

    public bool IsSymbolFont => Variants.Count > 0 && Variants[0].DirectWriteProperties.IsSymbolFont;

    /// <summary>
    /// True if a family contains atleast ONE face
    /// </summary>
    public bool HasAnyFaces => _dwriteFamily is not null
        ? _dwriteFamily.FontCount > 0
        : (_fontFaces.Count > 0 || _simulatedFaces?.Count > 0);

    /// <summary>
    /// Real, non-simulated Font Faces of this family
    /// </summary>
    public IList<CMFontFace> Variants
    {
        get
        {
            EnsureInflated();
            return _fontFaces;
        }
    }

    /// <summary>
    /// Identifies if a font family has any REAL different font faces
    /// </summary>
    public bool HasVariants
    {
        get
        {
            if (_dwriteFamily is not null && _fontFaces.Count == 0)
                return _dwriteFamily.FontCount > 1;
            return _fontFaces.Count > 1;
        }
    }



    /// <summary>
    /// Identifies if a font family has any other font faces, included
    /// simulated faces
    /// </summary>
    public bool HasMultipleFaces => AllFaces.Count > 1;

    public bool HasImportedFiles { get; private set; }

    public CMFontFace DefaultVariant => field ??= Utils.GetDefaultFace(Variants.Count > 0 ? Variants : _simulatedFaces);

    public List<object> AllFaces => field ??= CreateFaceList();

    public List<object> NonSimulatedVariants => field ??= CreateFaceList(true);


    private CMFontFamily(string name)
    {
        Name = name;
        _fontFaces = [];
    }

    public CMFontFamily(string name, DWriteFontFace face) : this(name, face, (string)null) { }

    public CMFontFamily(string name, DWriteFontFace face, StorageFile file) : this(name, face, file?.Path) { }

    public CMFontFamily(string name, DWriteFontFace face, string filePath) : this(name)
    {
        AddFace(face, filePath);
    }

    public CMFontFamily(string name, DWriteFontFamily family) : this(name)
    {
        _dwriteFamily = family;
    }

    public void EnsureInflated()
    {
        if (_dwriteFamily is not null && _fontFaces.Count == 0)
        {
            lock (_dwriteFamily)
            {
                if (_fontFaces.Count == 0)
                {
                    bool hideSimulated = ResourceHelper.AppSettings.HideSimulatedFontFaces;
                    foreach (DWriteFontFace font in _dwriteFamily.Fonts)
                    {
                        if (font.Properties.IsSimulated && hideSimulated)
                            continue;
                        AddFace(font);
                    }
                    SortFaces();
                    _dwriteFamily = null;
                }
            }
        }
    }


    public void AddFace(DWriteFontFace fontFace) => AddFace(fontFace, (string)null);

    public void AddFace(DWriteFontFace fontFace, StorageFile file) => AddFace(fontFace, file?.Path);

    public void AddFace(DWriteFontFace fontFace, string filePath)
    {
        if (fontFace.Properties.IsSimulated is false)
            _fontFaces.Add(new(fontFace, filePath));
        else
        {
            _simulatedFaces ??= [];
            _simulatedFaces.Add(new(fontFace, filePath));
        }

        if (!string.IsNullOrEmpty(filePath))
            HasImportedFiles = true;
    }

    List<object> CreateFaceList(bool excludeSimulated = false)
    {
        List<object> objs = new(Variants);
        if (_simulatedFaces is not null && _simulatedFaces.Count > 0 && !excludeSimulated)
        {
            objs.Add(Localization.Get("SimulatedHeader"));
            objs.AddRange(_simulatedFaces);
        }

        return objs;
    }

    public void SortFaces()
    {
        if (_fontFaces.Count > 1)
            _fontFaces.Sort((a, b) => a.DirectWriteProperties.Weight.Weight.CompareTo(b.DirectWriteProperties.Weight.Weight));
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
            _fontFaces = this._fontFaces.ToList(),
            HasImportedFiles = this.HasImportedFiles
        };
    }

    public static CMFontFamily CreateDefault(DWriteFontFace face)
    {
        CMFontFamily font = new(face.Properties.FamilyName);
        font._fontFaces.Add(CMFontFace.CreateDefault(face));
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
