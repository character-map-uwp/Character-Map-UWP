using CharacterMap.Core;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System.Collections.Generic;

namespace CharacterMap.Provider
{
    public record CharacterRenderingOptions
    {
        public FontVariant Variant { get; }
        public IReadOnlyList<TypographyFeatureInfo> Typography { get; }
        public double FontSize { get; }
        public CanvasTextLayoutAnalysis Analysis { get; }

        public CharacterRenderingOptions(FontVariant variant, List<TypographyFeatureInfo> typography, double fontSize, CanvasTextLayoutAnalysis analysis)
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
