using CharacterMap.Core;
using CharacterMap.Helpers;
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
        public IReadOnlyList<DWriteFontAxis> Axis { get; init; }
        public bool IsVariation { get; }

        public TypographyFeatureInfo DefaultTypography { get; }
        public TypographyFeatureInfo DXTypography { get; }

        /// <summary>
        /// If set to true, XAML rendering cannot properly display all variations of this rendering
        /// </summary>
        public bool RequiresNativeRender { get; }

        public static CharacterRenderingOptions CreateDefault(InstalledFont font)
        {
            CharacterRenderingOptions options = new(
                font.DefaultVariant,
                new List<TypographyFeatureInfo> { TypographyFeatureInfo.None },
                64,
                null, 
                null);

            return options;
        }

        public CharacterRenderingOptions(FontVariant variant, List<TypographyFeatureInfo> typography, float fontSize, CanvasTextLayoutAnalysis analysis, IReadOnlyList<DWriteFontAxis> axis)
        {
            Variant = variant;
            Typography = typography;
            FontSize = fontSize;
            Analysis = analysis;

            DefaultTypography = typography?.Where(t => t.Feature != CanvasTypographyFeatureName.None).FirstOrDefault();
            DXTypography = typography.FirstOrDefault();

            Axis = axis?.Copy();

            //IsVariation = Axis != null && Axis.Where(a => a.Value != a.DefaultValue).ToList() is List<DWriteFontAxis> a && a.Count > 0;
            RequiresNativeRender = Variant.DirectWriteProperties.HasVariations;
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
                (o.Variant == this.Variant 
                    && o.DefaultTypography == this.DefaultTypography
                    && o.Axis == this.Axis);
            
            // && o.IsColourFontEnabled == this.IsColourFontEnabled);
        }
    }
}
