using Microsoft.Graphics.Canvas.Text;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace CharacterMap.Models
{
    public record CharacterRenderingOptions
    {

        private static DWriteAxisComparer _axisComparer { get; } = new();

        public CMFontFamily Family { get; init; }
        public CMFontFace Variant { get; init; }
        public float FontSize { get; init; }
        public CanvasTextLayoutAnalysis Analysis { get; init; }
        public IReadOnlyList<TypographyFeatureInfo> Typography { get; init; }
        public bool IsColourFontEnabled { get; init; } = true;
        public IReadOnlyList<DWriteFontAxis> Axis { get; init; }
        public bool IsVariation { get; }

        public TypographyFeatureInfo DefaultTypography { get; }

        /// <summary>
        /// Typography to pass to DirectText control
        /// </summary>
        public TypographyFeatureInfo DXTypography { get; }

        /// <summary>
        /// If set to true, XAML rendering cannot properly display all variations of this rendering
        /// </summary>
        public bool RequiresNativeRender { get; }

        public static CharacterRenderingOptions CreateDefault(CMFontFamily font)
        {
            return CreateDefault(font.DefaultVariant, font);
        }

        public static CharacterRenderingOptions CreateDefault(CMFontFace variant, CMFontFamily fam = null)
        {
            CharacterRenderingOptions options = new(
                variant,
                new() { TypographyFeatureInfo.None },
                64,
                null,
                null)
            {
                Family = fam
            };

            return options;
        }

        public CharacterRenderingOptions(CMFontFace variant, List<TypographyFeatureInfo> typography, float fontSize, CanvasTextLayoutAnalysis analysis, IReadOnlyList<DWriteFontAxis> axis)
        {
            Variant = variant;
            Typography = typography;
            FontSize = fontSize;
            Analysis = analysis;

            DefaultTypography = typography?.Where(t => t.Feature != CanvasTypographyFeatureName.None).FirstOrDefault();
            DXTypography = typography.FirstOrDefault();

            Axis = axis?.Copy();

            //IsVariation = Axis != null && Axis.Where(a => a.Value != a.DefaultValue).ToList() is List<DWriteFontAxis> a && a.Count > 0;
            RequiresNativeRender = Variant.DirectWriteProperties.HasVariations || Variant.SupportsCOLRv1Rendering;
        }



        public string GetName()
        {
            // Basic Name
            string name = $"{Variant.FamilyName} {Variant.PreferredName}";

            // Add OpenType features
            if (DefaultTypography is not null)
                name += $" - {DefaultTypography.DisplayName}";

            // Add variable axis
            if (Axis != null && Axis.Where(a => a.Value != a.DefaultValue).ToList() is List<DWriteFontAxis> axis && axis.Count > 0)
            {
                foreach (var a in axis)
                    name += $", {a.Label} {a.Value}";
            }

            return name;
        }

        public IReadOnlyList<DevProviderBase> GetDevProviders(Models.Character c) => DevProviderBase.GetProviders(this, c);

        public CanvasTypography CreateCanvasTypography()
        {
            CanvasTypography t = new();
            foreach (var f in Typography)
            {
                if (f.Feature != CanvasTypographyFeatureName.None)
                    t.AddFeature(f.Feature, 1u);
            }
            return t;
        }

        public bool IsCompareMatch(CharacterRenderingOptions o)
        {
            return object.ReferenceEquals(this, o) ||
                (o.Variant == this.Variant
                    && o.DefaultTypography == this.DefaultTypography
                    && AreSameAxis());

            bool AreSameAxis()
            {
                if (o.Axis.Count == 0 && this.Axis.Count == 0)
                    return true;

                if (o.Axis.Count != this.Axis.Count)
                    return false;

                bool same = true;
                for (int i = 0; i < o.Axis.Count; i++)
                {
                    var a = o.Axis[i];
                    var b = this.Axis[i];

                    //if (a != b)
                    if (_axisComparer.Equals(a, b) is false)
                    {
                        same = false;
                        break;
                    }
                }

                // TODO: this is borked
                return same;
            }
        }
    }
}

public class DWriteAxisComparer : IEqualityComparer<DWriteFontAxis>
{
    public bool Equals(DWriteFontAxis x, DWriteFontAxis y)
    {
        return x.Tag == y.Tag &&
               x.Value == y.Value;
    }

    public int GetHashCode(DWriteFontAxis obj)
    {
        int hash = 269;
        hash = (hash * 47) + obj.Tag.GetHashCode();
        hash = (hash * 47) + obj.Value.GetHashCode();
        hash = (hash * 47) + obj.Label.GetHashCode();
        return hash;
    }
}