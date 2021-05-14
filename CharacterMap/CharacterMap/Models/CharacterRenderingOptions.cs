using CharacterMap.Core;
using CharacterMap.Provider;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;
using System.Linq;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace CharacterMap.Models
{
    public record CharacterRenderingOptions
    {
        public FontVariant Variant { get; }
        public float FontSize { get; init; }
        public CanvasTextLayoutAnalysis Analysis { get; init; }
        public IReadOnlyList<TypographyFeatureInfo> Typography { get; init; }
        public bool IsColourFontEnabled { get; init; } = true;
        //public IReadOnlyList<DWriteFontAxis> Axis { get; init; }

        public CharacterRenderingOptions(FontVariant variant, List<TypographyFeatureInfo> typography, float fontSize, CanvasTextLayoutAnalysis analysis)//, IReadOnlyList<DWriteFontAxis> axis)
        {
            Variant = variant;
            Typography = typography;
            FontSize = fontSize;
            Analysis = analysis;

            DefaultTypography = typography?.Where(t => t.Feature != CanvasTypographyFeatureName.None).FirstOrDefault();
            //Axis = axis.Select(a => a.WithValue(a.Value)).ToList();
        }

        public TypographyFeatureInfo DefaultTypography { get; }

        public string GetName()
        {
            //if (Axis != null && Axis.Where(a => a.Value != a.DefaultValue).ToList() is List<DWriteFontAxis> axis && axis.Count > 0)
            //{
            //}

            if (DefaultTypography == null)
                return $"{Variant.FamilyName} {Variant.PreferredName}";
            else
                return $"{Variant.FamilyName} {Variant.PreferredName} - {DefaultTypography.DisplayName}";
        }

        public IReadOnlyList<DevProviderBase> GetDevProviders(Models.Character c) => DevProviderBase.GetProviders(this, c);

        public CanvasTypography CreateCanvasTypography()
        {
            CanvasTypography t = new ();
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
                (o.Variant == this.Variant && o.DefaultTypography == this.DefaultTypography);// && o.IsColourFontEnabled == this.IsColourFontEnabled);
        }
    }
}
