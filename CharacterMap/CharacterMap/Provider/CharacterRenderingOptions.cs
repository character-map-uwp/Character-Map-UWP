using CharacterMap.Core;
using CharacterMap.Provider;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace CharacterMap.Models
{
    public record CharacterRenderingOptions
    {
        public FontVariant Variant { get; }
        public IReadOnlyList<TypographyFeatureInfo> Typography { get; init; }
        public float FontSize { get; init; }
        public CanvasTextLayoutAnalysis Analysis { get; init; }

        public CharacterRenderingOptions(FontVariant variant, List<TypographyFeatureInfo> typography, float fontSize, CanvasTextLayoutAnalysis analysis)
        {
            Variant = variant;
            Typography = typography;
            FontSize = fontSize;
            Analysis = analysis;
        }

        public IReadOnlyList<DevProviderBase> GetDevProviders(Models.Character c) => DevProviderBase.GetProviders(this, c);

        public CanvasTypography CreateCanvasTypography()
        {
            CanvasTypography t = new CanvasTypography();
            foreach (var f in Typography)
            {
                if (f.Feature != CanvasTypographyFeatureName.None)
                    t.AddFeature(f.Feature, 1u);
            }
            return t;
        }
    }
}
