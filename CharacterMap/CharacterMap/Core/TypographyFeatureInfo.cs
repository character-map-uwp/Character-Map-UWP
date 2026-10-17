
namespace CharacterMap.Core;

public class TypographyFeatureInfo : ITypographyInfo, IEquatable<TypographyFeatureInfo>
{
    private static HashSet<DWriteTypographyFeatureName> _allValues { get; } = new HashSet<DWriteTypographyFeatureName>(
        Enum.GetValues(typeof(DWriteTypographyFeatureName)).Cast<DWriteTypographyFeatureName>());

    public static TypographyFeatureInfo None { get; } = new TypographyFeatureInfo(DWriteTypographyFeatureName.None, "Default");

    public DWriteTypographyFeatureName Feature { get; }

    public string DisplayName { get; }

    public TypographyFeatureInfo(DWriteTypographyFeatureName n, string displayName = null)
    {
        Feature = n;

        if (displayName != null)
        {
            DisplayName = displayName;
        }
        else if (IsNamedFeature(Feature))
        {
            DisplayName = Feature.Humanise();
        }
        else
        {
            //
            // For custom font features, we can produce the OpenType feature tag
            // using the feature name.
            //
            uint id = (uint)(Feature);
            DisplayName = DirectWrite.GetTagName(id);
        }
    }

    public override string ToString()
    {
        return DisplayName;
    }

    bool IsNamedFeature(DWriteTypographyFeatureName name)
    {
        //
        // DWrite and Win2D support a pre-defined list of typographic features.
        // However, fonts are free to expose features outside of that list.
        // In fact, many built-in fonts have such custom features. 
        // 
        // These custom features are also accessible through Win2D, and 
        // are reported by GetSupportedTypographicFeatureNames.
        //

        return _allValues.Contains(name);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TypographyFeatureInfo);
    }

    public bool Equals(TypographyFeatureInfo other)
    {
        return other != null &&
               Feature == other.Feature;
    }

    public override int GetHashCode()
    {
        return 1334695525 + Feature.GetHashCode();
    }

    public static bool operator ==(TypographyFeatureInfo left, TypographyFeatureInfo right)
    {
        return EqualityComparer<TypographyFeatureInfo>.Default.Equals(left, right);
    }

    public static bool operator !=(TypographyFeatureInfo left, TypographyFeatureInfo right)
    {
        return !(left == right);
    }
}
