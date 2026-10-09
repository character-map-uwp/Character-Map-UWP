using Windows.UI.Xaml;
using Windows.UI.Xaml.Core.Direct;

namespace CharacterMap.Core;

public partial class TypographySetter
{
    public static void SetTypography(IXamlDirectObject o, DWriteTypographyFeatureName f, XamlDirect _xamlDirect)
    {
        /* XAML Direct Helpers. Using XD is faster than setting Dependency Properties */
        void Set(XamlPropertyIndex index, bool value)
        {
            _xamlDirect.SetBooleanProperty(o, index, value);
        }
        void SetE(XamlPropertyIndex index, uint e)
        {
            _xamlDirect.SetEnumProperty(o, index, e);
        }
        void SetI(XamlPropertyIndex index, bool val)
        {
            _xamlDirect.SetInt32Property(o, index, val ? 1 : 0);
        }

        /* TODO : ADD EASTASIAN TYPOGRAPY PROPERTIES */

        /* Set CAPTIAL SPACING */
        /* As Capital Spacing affects character spacing, it has no use when displaying single glyphs */
        //Set(XamlPropertyIndex.Typography_CapitalSpacing, f == DWriteTypographyFeatureName.CapitalSpacing);

        /* Set KERNING */
        /* As Kerning affects character spacing, it has no use when displaying single glyphs */
        //Set(XamlPropertyIndex.Typography_Kerning, f == DWriteTypographyFeatureName.Kerning);

        /* Set SWASHES */
        SetI(XamlPropertyIndex.Typography_StandardSwashes, f == DWriteTypographyFeatureName.Swash);
        SetI(XamlPropertyIndex.Typography_ContextualSwashes, f == DWriteTypographyFeatureName.ContextualSwash);

        /* Set ALTERNATES */
        SetI(XamlPropertyIndex.Typography_AnnotationAlternates, f == DWriteTypographyFeatureName.AlternateAnnotationForms);
        SetI(XamlPropertyIndex.Typography_StylisticAlternates, f == DWriteTypographyFeatureName.StylisticAlternates);
        /* Contextual Alternates applies to combinations of characters, and as such has no purpose here yet */
        Set(XamlPropertyIndex.Typography_ContextualAlternates, f == DWriteTypographyFeatureName.ContextualAlternates);

        /* Set MATHEMATICAL GREEK */
        Set(XamlPropertyIndex.Typography_MathematicalGreek, f == DWriteTypographyFeatureName.MathematicalGreek);

        /* Set FORMS */
        Set(XamlPropertyIndex.Typography_HistoricalForms, f == DWriteTypographyFeatureName.HistoricalForms);
        Set(XamlPropertyIndex.Typography_CaseSensitiveForms, f == DWriteTypographyFeatureName.CaseSensitiveForms);
        Set(XamlPropertyIndex.Typography_EastAsianExpertForms, f == DWriteTypographyFeatureName.ExpertForms);

        /* Set SLASHED ZERO */
        Set(XamlPropertyIndex.Typography_SlashedZero, f == DWriteTypographyFeatureName.SlashedZero);

        /* Set LIGATURES */
        /* Ligatures only apply to combinations of characters, and as such have no purpose here yet */
        // Set(XamlPropertyIndex.Typography_StandardLigatures, f == DWriteTypographyFeatureName.StandardLigatures);
        // Set(XamlPropertyIndex.Typography_ContextualLigatures, f == DWriteTypographyFeatureName.ContextualLigatures);
        // Set(XamlPropertyIndex.Typography_HistoricalLigatures, f == DWriteTypographyFeatureName.HistoricalLigatures);
        // Set(XamlPropertyIndex.Typography_DiscretionaryLigatures, f == DWriteTypographyFeatureName.DiscretionaryLigatures);

        /* Set CAPITALS */
        if (f == DWriteTypographyFeatureName.SmallCapitals)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.SmallCaps);
        else if (f == DWriteTypographyFeatureName.SmallCapitalsFromCapitals)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.AllSmallCaps);
        else if (f == DWriteTypographyFeatureName.PetiteCapitals)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.PetiteCaps);
        else if (f == DWriteTypographyFeatureName.PetiteCapitalsFromCapitals)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.AllPetiteCaps);
        else if (f == DWriteTypographyFeatureName.Titling)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Titling);
        else if (f == DWriteTypographyFeatureName.Unicase)
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Unicase);
        else
            SetE(XamlPropertyIndex.Typography_Capitals, (uint)FontCapitals.Normal);

        /* Set NUMERAL ALIGNMENT */
        /* Numeral Alignment only apply to combinations of characters, and as such have no purpose here yet */
        //if (f == DWriteTypographyFeatureName.ProportionalFigures)
        //    SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Proportional);
        //else if (f == DWriteTypographyFeatureName.TabularFigures)
        //    SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Tabular);
        //else
        SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Normal);

        /* Set NUMERAL STYLE */
        if (f == DWriteTypographyFeatureName.OldStyleFigures)
            SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.OldStyle);
        else if (f == DWriteTypographyFeatureName.LiningFigures)
            SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.Lining);
        else
            SetE(XamlPropertyIndex.Typography_NumeralStyle, (uint)FontNumeralStyle.Normal);

        /* Set VARIANTS */
        if (f == DWriteTypographyFeatureName.Ordinals)
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Ordinal);
        else if (f == DWriteTypographyFeatureName.Superscript)
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Superscript);
        else if (f == DWriteTypographyFeatureName.Subscript)
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Subscript);
        else if (f == DWriteTypographyFeatureName.RubyNotationForms)
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Ruby);
        else if (f == DWriteTypographyFeatureName.ScientificInferiors)
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Inferior);
        else
            SetE(XamlPropertyIndex.Typography_Variants, (uint)FontVariants.Normal);


        /* Set STLYISTIC SETS */
        Set(XamlPropertyIndex.Typography_StylisticSet1, f == DWriteTypographyFeatureName.StylisticSet1);
        Set(XamlPropertyIndex.Typography_StylisticSet2, f == DWriteTypographyFeatureName.StylisticSet2);
        Set(XamlPropertyIndex.Typography_StylisticSet3, f == DWriteTypographyFeatureName.StylisticSet3);
        Set(XamlPropertyIndex.Typography_StylisticSet4, f == DWriteTypographyFeatureName.StylisticSet4);
        Set(XamlPropertyIndex.Typography_StylisticSet5, f == DWriteTypographyFeatureName.StylisticSet5);
        Set(XamlPropertyIndex.Typography_StylisticSet6, f == DWriteTypographyFeatureName.StylisticSet6);
        Set(XamlPropertyIndex.Typography_StylisticSet7, f == DWriteTypographyFeatureName.StylisticSet7);
        Set(XamlPropertyIndex.Typography_StylisticSet8, f == DWriteTypographyFeatureName.StylisticSet8);
        Set(XamlPropertyIndex.Typography_StylisticSet9, f == DWriteTypographyFeatureName.StylisticSet9);
        Set(XamlPropertyIndex.Typography_StylisticSet10, f == DWriteTypographyFeatureName.StylisticSet10);
        Set(XamlPropertyIndex.Typography_StylisticSet11, f == DWriteTypographyFeatureName.StylisticSet11);
        Set(XamlPropertyIndex.Typography_StylisticSet12, f == DWriteTypographyFeatureName.StylisticSet12);
        Set(XamlPropertyIndex.Typography_StylisticSet13, f == DWriteTypographyFeatureName.StylisticSet13);
        Set(XamlPropertyIndex.Typography_StylisticSet14, f == DWriteTypographyFeatureName.StylisticSet14);
        Set(XamlPropertyIndex.Typography_StylisticSet15, f == DWriteTypographyFeatureName.StylisticSet15);
        Set(XamlPropertyIndex.Typography_StylisticSet16, f == DWriteTypographyFeatureName.StylisticSet16);
        Set(XamlPropertyIndex.Typography_StylisticSet17, f == DWriteTypographyFeatureName.StylisticSet17);
        Set(XamlPropertyIndex.Typography_StylisticSet18, f == DWriteTypographyFeatureName.StylisticSet18);
        Set(XamlPropertyIndex.Typography_StylisticSet19, f == DWriteTypographyFeatureName.StylisticSet19);
        Set(XamlPropertyIndex.Typography_StylisticSet20, f == DWriteTypographyFeatureName.StylisticSet20);
    }
}


public partial class TypographySetter
{
    private static HashSet<DWriteTypographyFeatureName> _supportedSingleGlyphFeatures { get; } =
    [
        DWriteTypographyFeatureName.None,
        DWriteTypographyFeatureName.StylisticSet1,
        DWriteTypographyFeatureName.StylisticSet2,
        DWriteTypographyFeatureName.StylisticSet3,
        DWriteTypographyFeatureName.StylisticSet4,
        DWriteTypographyFeatureName.StylisticSet5,
        DWriteTypographyFeatureName.StylisticSet6,
        DWriteTypographyFeatureName.StylisticSet7,
        DWriteTypographyFeatureName.StylisticSet8,
        DWriteTypographyFeatureName.StylisticSet9,
        DWriteTypographyFeatureName.StylisticSet10,
        DWriteTypographyFeatureName.StylisticSet11,
        DWriteTypographyFeatureName.StylisticSet12,
        DWriteTypographyFeatureName.StylisticSet13,
        DWriteTypographyFeatureName.StylisticSet14,
        DWriteTypographyFeatureName.StylisticSet15,
        DWriteTypographyFeatureName.StylisticSet16,
        DWriteTypographyFeatureName.StylisticSet17,
        DWriteTypographyFeatureName.StylisticSet18,
        DWriteTypographyFeatureName.StylisticSet19,
        DWriteTypographyFeatureName.StylisticSet20,
        //DWriteTypographyFeatureName.Kerning,
        //DWriteTypographyFeatureName.CapitalSpacing,
        DWriteTypographyFeatureName.MathematicalGreek,
        DWriteTypographyFeatureName.HistoricalForms,
        DWriteTypographyFeatureName.CaseSensitiveForms,
        DWriteTypographyFeatureName.ExpertForms,
        DWriteTypographyFeatureName.SlashedZero,
        //DWriteTypographyFeatureName.ContextualAlternates,
        //DWriteTypographyFeatureName.StandardLigatures,
        //DWriteTypographyFeatureName.ContextualLigatures,
        //DWriteTypographyFeatureName.HistoricalLigatures,
        //DWriteTypographyFeatureName.DiscretionaryLigatures,
        DWriteTypographyFeatureName.SmallCapitals,
        DWriteTypographyFeatureName.SmallCapitalsFromCapitals,
        DWriteTypographyFeatureName.PetiteCapitals,
        DWriteTypographyFeatureName.PetiteCapitalsFromCapitals,
        DWriteTypographyFeatureName.Titling,
        DWriteTypographyFeatureName.Unicase,
        //DWriteTypographyFeatureName.ProportionalFigures,
        //DWriteTypographyFeatureName.TabularFigures,
        DWriteTypographyFeatureName.OldStyleFigures,
        DWriteTypographyFeatureName.LiningFigures,
        DWriteTypographyFeatureName.Ordinals,
        DWriteTypographyFeatureName.Superscript,
        DWriteTypographyFeatureName.Subscript,
        DWriteTypographyFeatureName.RubyNotationForms,
        DWriteTypographyFeatureName.ScientificInferiors,
        DWriteTypographyFeatureName.Swash,
        DWriteTypographyFeatureName.ContextualSwash,
        DWriteTypographyFeatureName.AlternateAnnotationForms,
        DWriteTypographyFeatureName.StylisticAlternates
    ];

    public static bool IsXamlSingleGlyphSupported(DWriteTypographyFeatureName feature)
        => _supportedSingleGlyphFeatures.Contains(feature);
}
