using Microsoft.Graphics.Canvas.Text;
using Microsoft.Xaml.Interactivity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Documents;

namespace CharacterMap.Core
{
    public partial class TypographyBehavior : Behavior<TextBlock>
    {
        private static XamlDirect _xamlDirect { get; } = XamlDirect.GetDefault();

        public TypographyFeatureInfo TypographyFeature
        {
            get { return (TypographyFeatureInfo)GetValue(TypographyFeatureProperty); }
            set { SetValue(TypographyFeatureProperty, value); }
        }

        public static readonly DependencyProperty TypographyFeatureProperty =
            DependencyProperty.Register(nameof(TypographyFeature), typeof(TypographyFeatureInfo), typeof(TypographyBehavior), new PropertyMetadata(null, (d,e) =>
            {
                ((TypographyBehavior)d).ApplyTypography();
            }));



        protected override void OnAttached()
        {
            base.OnAttached();
            ApplyTypography();
        }

        private void ApplyTypography()
        {
            /*
             * Here we shall map DirectWrite / Win2D typography properties to their 
             * XAML equivalents. Not all properties are supported because not all 
             * properties map directly. To support them all, we'd have to render all 
             * the characters in the GridView with Win2D;
             */

            if (AssociatedObject == null)
                return;

            /* Assign shorthands for use below */
            CanvasTypographyFeatureName f = TypographyFeature == null ? CanvasTypographyFeatureName.None : TypographyFeature.Feature;
            TextBlock t = AssociatedObject;

            /* XAML Direct Helpers. Using XD is faster than setting Dependency Properties */
            IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(t);
            void Set(XamlPropertyIndex index, bool value)
            {
                _xamlDirect.SetBooleanProperty(o, index, value);
            }
            void SetE(XamlPropertyIndex index, uint e)
            {
                _xamlDirect.SetEnumProperty(o, index, e);
            }


            /* Set CAPTIAL SPACING */
            Set(XamlPropertyIndex.Typography_CapitalSpacing, f == CanvasTypographyFeatureName.CapitalSpacing);

            /* Set KERNING */
            Set(XamlPropertyIndex.Typography_Kerning, f == CanvasTypographyFeatureName.Kerning);

            /* Set MATHEMATICAL GREEK */
            Set(XamlPropertyIndex.Typography_MathematicalGreek, f == CanvasTypographyFeatureName.MathematicalGreek);

            /* Set FORMS */
            Set(XamlPropertyIndex.Typography_HistoricalForms, f == CanvasTypographyFeatureName.HistoricalForms);
            Set(XamlPropertyIndex.Typography_CaseSensitiveForms, f == CanvasTypographyFeatureName.CaseSensitiveForms);
            Set(XamlPropertyIndex.Typography_EastAsianExpertForms, f == CanvasTypographyFeatureName.ExpertForms);

            /* Set SLASHED ZERO */
            Set(XamlPropertyIndex.Typography_SlashedZero, f == CanvasTypographyFeatureName.SlashedZero);
            Set(XamlPropertyIndex.Typography_ContextualAlternates, f == CanvasTypographyFeatureName.ContextualAlternates);

            /* Set LIGATURES */
            Set(XamlPropertyIndex.Typography_StandardLigatures, f == CanvasTypographyFeatureName.StandardLigatures);
            Set(XamlPropertyIndex.Typography_ContextualLigatures, f == CanvasTypographyFeatureName.ContextualLigatures);
            Set(XamlPropertyIndex.Typography_HistoricalLigatures, f == CanvasTypographyFeatureName.HistoricalLigatures);
            Set(XamlPropertyIndex.Typography_DiscretionaryLigatures, f == CanvasTypographyFeatureName.DiscretionaryLigatures);

            /* Set CAPITALS */
            if (f == CanvasTypographyFeatureName.SmallCapitals)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.SmallCaps);
            else if (f == CanvasTypographyFeatureName.SmallCapitalsFromCapitals)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.AllSmallCaps);
            else if (f == CanvasTypographyFeatureName.PetiteCapitals)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.PetiteCaps);
            else if (f == CanvasTypographyFeatureName.PetiteCapitalsFromCapitals)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.AllPetiteCaps);
            else if (f == CanvasTypographyFeatureName.Titling)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Titling);
            else if (f == CanvasTypographyFeatureName.Unicase)
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Unicase);
            else
                SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Normal);

            /* Set NUMERAL ALIGNMENT */
            if (f == CanvasTypographyFeatureName.ProportionalFigures)
                SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Proportional);
            else if (f == CanvasTypographyFeatureName.TabularFigures)
                SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Tabular);
            else
                SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Normal);

            /* Set NUMERAL STYLE */
            if (f == CanvasTypographyFeatureName.OldStyleFigures)
                SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.OldStyle);
            else if (f == CanvasTypographyFeatureName.LiningFigures)
                SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.Lining);
            else
                SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.Normal);

            /* Set VARIANTS */
            if (f == CanvasTypographyFeatureName.Ordinals)
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Ordinal);
            else if (f == CanvasTypographyFeatureName.Superscript)
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Superscript);
            else if (f == CanvasTypographyFeatureName.Subscript)
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Subscript);
            else if (f == CanvasTypographyFeatureName.RubyNotationForms)
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Ruby);
            else if (f == CanvasTypographyFeatureName.ScientificInferiors)
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Inferior);
            else
                SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Normal);


            /* Set STLYISTIC SETS */
            Set(XamlPropertyIndex.Typography_StylisticSet1, f == CanvasTypographyFeatureName.StylisticSet1);
            Set(XamlPropertyIndex.Typography_StylisticSet2, f == CanvasTypographyFeatureName.StylisticSet2);
            Set(XamlPropertyIndex.Typography_StylisticSet3, f == CanvasTypographyFeatureName.StylisticSet3);
            Set(XamlPropertyIndex.Typography_StylisticSet4, f == CanvasTypographyFeatureName.StylisticSet4);
            Set(XamlPropertyIndex.Typography_StylisticSet5, f == CanvasTypographyFeatureName.StylisticSet5);
            Set(XamlPropertyIndex.Typography_StylisticSet6, f == CanvasTypographyFeatureName.StylisticSet6);
            Set(XamlPropertyIndex.Typography_StylisticSet7, f == CanvasTypographyFeatureName.StylisticSet7);
            Set(XamlPropertyIndex.Typography_StylisticSet8, f == CanvasTypographyFeatureName.StylisticSet8);
            Set(XamlPropertyIndex.Typography_StylisticSet9, f == CanvasTypographyFeatureName.StylisticSet9);
            Set(XamlPropertyIndex.Typography_StylisticSet10, f == CanvasTypographyFeatureName.StylisticSet10);
            Set(XamlPropertyIndex.Typography_StylisticSet11, f == CanvasTypographyFeatureName.StylisticSet11);
            Set(XamlPropertyIndex.Typography_StylisticSet12, f == CanvasTypographyFeatureName.StylisticSet12);
            Set(XamlPropertyIndex.Typography_StylisticSet13, f == CanvasTypographyFeatureName.StylisticSet13);
            Set(XamlPropertyIndex.Typography_StylisticSet14, f == CanvasTypographyFeatureName.StylisticSet14);
            Set(XamlPropertyIndex.Typography_StylisticSet15, f == CanvasTypographyFeatureName.StylisticSet15);
            Set(XamlPropertyIndex.Typography_StylisticSet16, f == CanvasTypographyFeatureName.StylisticSet16);
            Set(XamlPropertyIndex.Typography_StylisticSet17, f == CanvasTypographyFeatureName.StylisticSet17);
            Set(XamlPropertyIndex.Typography_StylisticSet18, f == CanvasTypographyFeatureName.StylisticSet18);
            Set(XamlPropertyIndex.Typography_StylisticSet19, f == CanvasTypographyFeatureName.StylisticSet19);
            Set(XamlPropertyIndex.Typography_StylisticSet20, f == CanvasTypographyFeatureName.StylisticSet20);
        }
    }


    public partial class TypographyBehavior
    {
        private static HashSet<CanvasTypographyFeatureName> _supportedFeatures { get; } = new HashSet<CanvasTypographyFeatureName>
        {
            CanvasTypographyFeatureName.None,
            CanvasTypographyFeatureName.StylisticSet1,
            CanvasTypographyFeatureName.StylisticSet2,
            CanvasTypographyFeatureName.StylisticSet3,
            CanvasTypographyFeatureName.StylisticSet4,
            CanvasTypographyFeatureName.StylisticSet5,
            CanvasTypographyFeatureName.StylisticSet6,
            CanvasTypographyFeatureName.StylisticSet7,
            CanvasTypographyFeatureName.StylisticSet8,
            CanvasTypographyFeatureName.StylisticSet9,
            CanvasTypographyFeatureName.StylisticSet10,
            CanvasTypographyFeatureName.StylisticSet11,
            CanvasTypographyFeatureName.StylisticSet12,
            CanvasTypographyFeatureName.StylisticSet13,
            CanvasTypographyFeatureName.StylisticSet14,
            CanvasTypographyFeatureName.StylisticSet15,
            CanvasTypographyFeatureName.StylisticSet16,
            CanvasTypographyFeatureName.StylisticSet17,
            CanvasTypographyFeatureName.StylisticSet18,
            CanvasTypographyFeatureName.StylisticSet19,
            CanvasTypographyFeatureName.StylisticSet20,
            CanvasTypographyFeatureName.CapitalSpacing,
            CanvasTypographyFeatureName.Kerning,
            CanvasTypographyFeatureName.MathematicalGreek,
            CanvasTypographyFeatureName.HistoricalForms,
            CanvasTypographyFeatureName.CaseSensitiveForms,
            CanvasTypographyFeatureName.ExpertForms,
            CanvasTypographyFeatureName.SlashedZero,
            CanvasTypographyFeatureName.ContextualAlternates,
            CanvasTypographyFeatureName.StandardLigatures,
            CanvasTypographyFeatureName.ContextualLigatures,
            CanvasTypographyFeatureName.HistoricalLigatures,
            CanvasTypographyFeatureName.DiscretionaryLigatures,
            CanvasTypographyFeatureName.SmallCapitals,
            CanvasTypographyFeatureName.SmallCapitalsFromCapitals,
            CanvasTypographyFeatureName.PetiteCapitals,
            CanvasTypographyFeatureName.PetiteCapitalsFromCapitals,
            CanvasTypographyFeatureName.Titling,
            CanvasTypographyFeatureName.Unicase,
            CanvasTypographyFeatureName.ProportionalFigures,
            CanvasTypographyFeatureName.TabularFigures,
            CanvasTypographyFeatureName.OldStyleFigures,
            CanvasTypographyFeatureName.LiningFigures,
            CanvasTypographyFeatureName.Ordinals,
            CanvasTypographyFeatureName.Superscript,
            CanvasTypographyFeatureName.Subscript,
            CanvasTypographyFeatureName.RubyNotationForms,
            CanvasTypographyFeatureName.ScientificInferiors
        };

        public static bool IsXamlSupported(CanvasTypographyFeatureName feature)
            => _supportedFeatures.Contains(feature);
    }

}
