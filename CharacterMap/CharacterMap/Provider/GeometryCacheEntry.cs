namespace CharacterMap.Provider;

public class GeometryCacheEntry : IEquatable<GeometryCacheEntry>
{
    public Character Character { get; }
    public CharacterRenderingOptions Options { get; }

    public GeometryCacheEntry(Character character, CharacterRenderingOptions options)
    {
        Character = character;
        Options = options;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as GeometryCacheEntry);
    }

    public bool Equals(GeometryCacheEntry other)
    {
        return other != null &&
               EqualityComparer<Character>.Default.Equals(Character, other.Character) &&
               EqualityComparer<CharacterRenderingOptions>.Default.Equals(Options, other.Options);
    }

    public override int GetHashCode()
    {
        int hashCode = 1117641077;
        hashCode = hashCode * -1521134295 + EqualityComparer<Character>.Default.GetHashCode(Character);
        hashCode = hashCode * -1521134295 + EqualityComparer<CharacterRenderingOptions>.Default.GetHashCode(Options);
        return hashCode;
    }

    public static bool operator ==(GeometryCacheEntry left, GeometryCacheEntry right)
    {
        return EqualityComparer<GeometryCacheEntry>.Default.Equals(left, right);
    }

    public static bool operator !=(GeometryCacheEntry left, GeometryCacheEntry right)
    {
        return !(left == right);
    }
}
