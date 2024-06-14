namespace CharacterMap.Core;

public class InstalledFont : IComparable, IEquatable<InstalledFont>
{
    private List<FontVariant> _variants;

    public string Name { get; }

    public bool IsSymbolFont => _variants[0].DirectWriteProperties.IsSymbolFont;

    public IList<FontVariant> Variants => _variants;

    public bool HasVariants => _variants.Count > 1;

    public bool HasImportedFiles { get; private set; }

    private FontVariant _defaultVariant;
    public FontVariant DefaultVariant => _defaultVariant ??= Utils.GetDefaultVariant(Variants);

    private InstalledFont(string name)
    {
        Name = name;
        _variants = new();
    }

    public InstalledFont(string name, DWriteFontFace face, StorageFile file = null) : this(name)
    {
        AddVariant(face, file);
    }


    public void AddVariant(DWriteFontFace fontFace, StorageFile file = null)
    {
        _variants.Add(new(fontFace, file));
        if (file != null)
            HasImportedFiles = true;

        _defaultVariant = null;
    }

    public void SortVariants()
    {
        _variants = _variants.OrderBy(v => v.DirectWriteProperties.Weight.Weight).ToList();
    }

    public void PrepareForDelete()
    {
        //FontFace = null;
    }

    public InstalledFont Clone()
    {
        return new InstalledFont(this.Name)
        {
            _variants = this._variants.ToList(),
            HasImportedFiles = this.HasImportedFiles
        };
    }

    public static InstalledFont CreateDefault(DWriteFontFace face)
    {
        InstalledFont font = new("Segoe UI");
        font._variants.Add(FontVariant.CreateDefault(face));
        return font;
    }

    public int CompareTo(object obj)
    {
        if (obj is InstalledFont f)
            return Name.CompareTo(f.Name);

        return 0;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as InstalledFont);
    }

    public bool Equals(InstalledFont other)
    {
        return other is not null &&
               Name == other.Name;
    }

    public override int GetHashCode()
    {
        int hashCode = -1425556920;
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Name);
        hashCode = hashCode * -1521134295 + EqualityComparer<IList<FontVariant>>.Default.GetHashCode(Variants);
        hashCode = hashCode * -1521134295 + HasImportedFiles.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<FontVariant>.Default.GetHashCode(DefaultVariant);
        return hashCode;
    }

    public static bool operator ==(InstalledFont left, InstalledFont right)
    {
        return EqualityComparer<InstalledFont>.Default.Equals(left, right);
    }

    public static bool operator !=(InstalledFont left, InstalledFont right)
    {
        return !(left == right);
    }
}
