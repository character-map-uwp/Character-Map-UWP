#pragma once

namespace CharacterMapCX
{
	public enum class DWriteFontSource : int
	{
		/// <summary>
		/// The font source is unknown or is not any of the other defined font source types.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// The font source is a font file, which is installed for all users on the device.
		/// </summary>
		PerMachine = 1,

		/// <summary>
		/// The font source is a font file, which is installed for the current user.
		/// </summary>
		PerUser = 2,

		/// <summary>
		/// The font source is an APPX package, which includes one or more font files.
		/// The font source name is the full name of the package.
		/// </summary>
		AppxPackage = 3,

		/// <summary>
		/// The font source is a font provider for downloadable fonts.
		/// </summary>
		RemoteFontProvider = 4
	};

    public enum class DWriteFontInformation
    {
        //
        // Summary:
        //     Indicates the string containing the unspecified name ID.
        None,
        //
        // Summary:
        //     Indicates the string containing the copyright notice provided by the font.
        CopyrightNotice,
        //
        // Summary:
        //     Indicates the string containing a version number.
        VersionStrings,
        //
        // Summary:
        //     Indicates the string containing the trademark information provided by the font.
        Trademark,
        //
        // Summary:
        //     Indicates the string containing the name of the font manufacturer.
        Manufacturer,
        //
        // Summary:
        //     Indicates the string containing the name of the font designer.
        Designer,
        //
        // Summary:
        //     Indicates the string containing the URL of the font designer (with protocol,
        //     e.g., http://, ftp://).
        DesignerUrl,
        //
        // Summary:
        //     Indicates the string containing the description of the font. This may also contain
        //     revision information, usage recommendations, history, features, and so on.
        Description,
        //
        // Summary:
        //     Indicates the string containing the URL of the font vendor (with protocol, e.g.,
        //     http://, ftp://). If a unique serial number is embedded in the URL, it can be
        //     used to register the font.
        FontVendorUrl,
        //
        // Summary:
        //     Indicates the string containing the description of how the font may be legally
        //     used, or different example scenarios for licensed use.
        LicenseDescription,
        //
        // Summary:
        //     Indicates the string containing the URL where additional licensing information
        //     can be found.
        LicenseInfoUrl,
        //
        // Summary:
        //     Indicates the string containing the GDI-compatible family name. Since GDI allows
        //     a maximum of four fonts per family, fonts in the same family may have different
        //     GDI-compatible family names (e.g., "Arial", "Arial Narrow", "Arial Black").
        Win32FamilyNames,
        //
        // Summary:
        //     Indicates the string containing a GDI-compatible subfamily name.
        Win32SubfamilyNames,
        //
        // Summary:
        //     Indicates the string containing the family name preferred by the designer. This
        //     enables font designers to group more than four fonts in a single family without
        //     losing compatibility with GDI. This name is typically only present if it differs
        //     from the GDI-compatible family name.
        PreferredFamilyNames,
        //
        // Summary:
        //     Indicates the string containing the subfamily name preferred by the designer.
        //     This name is typically only present if it differs from the GDI-compatible subfamily
        //     name.
        PreferredSubfamilyNames,
        //
        // Summary:
        //     Contains sample text for display in font lists. This can be the font name or
        //     any other text that the designer thinks is the best example to display the font
        //     in.
        SampleText,
        //
        // Summary:
        //     The full name of the font- e.g. "Arial Bold", from name id 4 in the name table.
        FullName,
        //
        // Summary:
        //     The postscript name of the font, like GillSans-Bold, from name id 6 in the name
        //     table.
        PostscriptName,
        //
        // Summary:
        //     The postscript CID findfont name, from name id 20 in the name table
        PostscriptCidName,
        //
        // Summary:
        //     Family name for the weight-width-slope model.
        WwsFamilyName,
        //
        // Summary:
        //     Script/language tag to identify the scripts or languages that the font was primarily
        //     designed to support.
        DesignScriptLanguageTag,
        //
        // Summary:
        //     Script/language tag to identify the scripts or languages that the font declares
        //     it is able to support.
        SupportedScriptLanguageTag
    };

    public enum class DWriteTypographyFeatureName
    {
        //
        // Summary:
        //     No typography feature specified.
        None = 0,
        //
        // Summary:
        //     Indicates a set of default language behaviors.
        Default = 1953261156,
        //
        // Summary:
        //     Indicates that the font is displayed vertically.
        VerticalWriting = 1953654134,
        //
        // Summary:
        //     Replaces normal figures with figures adjusted for vertical display.
        VerticalAlternatesAndRotation = 846492278,
        //
        // Summary:
        //     Replaces figures separated by a slash with an alternative form.
        AlternativeFractions = 1668441697,
        //
        // Summary:
        //     Turns capital characters into petite capitals.
        PetiteCapitalsFromCapitals = 1668297315,
        //
        // Summary:
        //     Turns capital characters into small capitals.
        SmallCapitalsFromCapitals = 1668493923,
        //
        // Summary:
        //     In some situations, replaces default glyphs with alternate forms which provide
        //     better joining behavior.
        ContextualAlternates = 1953259875,
        //
        // Summary:
        //     Shifts various punctuation marks up to a position that works better with all-capital
        //     sequences or sets of lining figures; also changes oldstyle figures to lining
        //     figures.
        CaseSensitiveForms = 1702060387,
        //
        // Summary:
        //     Allows for the decomposing of a character into two glyphs or composition of two
        //     characters into a single glyph for better glyph processing.
        GlyphCompositionDecomposition = 1886217059,
        //
        // Summary:
        //     Replaces a sequence of glyphs with a single glyph which is preferred for typographic
        //     purposes.
        ContextualLigatures = 1734962275,
        //
        // Summary:
        //     Globally adjusts inter-glyph spacing for all-capital text.
        CapitalSpacing = 1886613603,
        //
        // Summary:
        //     Replaces default character glyphs with corresponding swash glyphs in a specified
        //     context.
        ContextualSwash = 1752658787,
        //
        // Summary:
        //     In cursive scripts like Arabic, this feature cursively positions adjacent glyphs.
        CursivePositioning = 1936880995,
        //
        // Summary:
        //     Replaces a sequence of glyphs with a single glyph which is preferred for typographic
        //     purposes.
        DiscretionaryLigatures = 1734962276,
        //
        // Summary:
        //     Replaces standard forms in Japanese fonts with corresponding forms preferred
        //     by typographers.
        ExpertForms = 1953527909,
        //
        // Summary:
        //     Replaces figures separated by a slash with 'common' (diagonal) fractions.
        Fractions = 1667330662,
        //
        // Summary:
        //     Replaces glyphs set on other widths with glyphs set on full (usually em) widths.
        FullWidth = 1684633446,
        //
        // Summary:
        //     Produces the half forms of consonants in Indic scripts.
        HalfForms = 1718378856,
        //
        // Summary:
        //     Produces the halant forms of consonants in Indic scripts.
        HalantForms = 1852596584,
        //
        // Summary:
        //     Re-spaces glyphs designed to be set on full-em widths, fitting them onto half-em
        //     widths.
        AlternateHalfWidth = 1953259880,
        //
        // Summary:
        //     Replaces the default (current) forms with the historical alternates.
        HistoricalForms = 1953720680,
        //
        // Summary:
        //     Replaces standard Japanese kana with forms that have been specially designed
        //     for only horizontal writing.
        HorizontalKanaAlternates = 1634626408,
        //
        // Summary:
        //     Replaces the default (current) forms with the historical alternates.
        HistoricalLigatures = 1734962280,
        //
        // Summary:
        //     Replaces glyphs on proportional widths, or fixed widths other than half an em,
        //     with glyphs on half-em (en) widths.
        HalfWidth = 1684633448,
        //
        // Summary:
        //     Used to access the JIS X 0212-1990 glyphs for the cases when the JIS X 0213:2004
        //     form is encoded.
        HojoKanjiForms = 1869246312,
        //
        // Summary:
        //     Enables a subset of NlcKanjiForms, producing glyph forms consistant with JIS
        //     X 0213:2004.
        Jis04Forms = 875589738,
        //
        // Summary:
        //     Replaces default (JIS90) Japanese glyphs with the corresponding forms from the
        //     JIS C 6226-1978 (JIS78) specification.
        Jis78Forms = 943157354,
        //
        // Summary:
        //     Replaces default (JIS90) Japanese glyphs with the corresponding forms from the
        //     JIS X 0208-1983 (JIS83) specification.
        Jis83Forms = 859336810,
        //
        // Summary:
        //     Replaces Japanese glyphs from the JIS78 or JIS83 specifications with the corresponding
        //     forms from the JIS X 0208-1990 (JIS90) specification.
        Jis90Forms = 809070698,
        //
        // Summary:
        //     Adjusts amount of space between glyphs, generally to provide optically consistent
        //     spacing between glyphs.
        Kerning = 1852990827,
        //
        // Summary:
        //     Replaces a sequence of glyphs with a single glyph which is preferred for typographic
        //     purposes.
        StandardLigatures = 1634167148,
        //
        // Summary:
        //     Changes selected figures from oldstyle to the default lining form.
        LiningFigures = 1836412524,
        //
        // Summary:
        //     Enables localized forms of glyphs to be substituted for default forms.
        LocalizedForms = 1818455916,
        //
        // Summary:
        //     Positions mark glyphs with respect to base glyphs.
        MarkPositioning = 1802658157,
        //
        // Summary:
        //     Replaces standard typographic forms of Greek glyphs with corresponding forms
        //     commonly used in mathematical notation.
        MathematicalGreek = 1802659693,
        //
        // Summary:
        //     Positions marks with respect to other marks.
        MarkToMarkPositioning = 1802333037,
        //
        // Summary:
        //     Replaces default glyphs with various notational forms.
        AlternateAnnotationForms = 1953259886,
        //
        // Summary:
        //     Used to access glyphs made from glyph shapes defined by the National Language
        //     Council (NLC) of Japan for a number of JIS characters.
        NlcKanjiForms = 1801677934,
        //
        // Summary:
        //     Changes selected figures from the default lining style to oldstyle form.
        OldStyleFigures = 1836412527,
        //
        // Summary:
        //     Replaces default alphabetic glyphs with the corresponding ordinal forms for use
        //     after figures.
        Ordinals = 1852076655,
        //
        // Summary:
        //     Respaces glyphs designed to be set on full-em widths, fitting them onto individual
        //     (more or less proportional) horizontal widths.
        ProportionalAlternateWidth = 1953259888,
        //
        // Summary:
        //     Turns lowercase characters into petite capitals.
        PetiteCapitals = 1885430640,
        //
        // Summary:
        //     Replaces figure glyphs set on uniform (tabular) widths with corresponding glyphs
        //     set on glyph-specific (proportional) widths.
        ProportionalFigures = 1836412528,
        //
        // Summary:
        //     Replaces glyphs set on uniform widths (typically full or half-em) with proportionally
        //     spaced glyphs.
        ProportionalWidths = 1684633456,
        //
        // Summary:
        //     Replaces glyphs on other widths with glyphs set on widths of one quarter of an
        //     em (half an en).
        QuarterWidths = 1684633457,
        //
        // Summary:
        //     Replaces a sequence of glyphs with a single glyph which is preferred for typographic
        //     purposes.
        RequiredLigatures = 1734962290,
        //
        // Summary:
        //     Identifies glyphs in the font which have been designed for "ruby", from the old
        //     typesetting term for four-point-sized type.
        RubyNotationForms = 2036495730,
        //
        // Summary:
        //     Replaces the default forms with the stylistic alternates.
        StylisticAlternates = 1953259891,
        //
        // Summary:
        //     Replaces lining or oldstyle figures with inferior figures.
        ScientificInferiors = 1718511987,
        //
        // Summary:
        //     Turns lowercase characters into small capitals.
        SmallCapitals = 1885564275,
        //
        // Summary:
        //     Replaces 'traditional' Chinese or Japanese forms with the corresponding 'simplified'
        //     forms.
        SimplifiedForms = 1819307379,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet1 = 825258867,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet2 = 842036083,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet3 = 858813299,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet4 = 875590515,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet5 = 892367731,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet6 = 909144947,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet7 = 925922163,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet8 = 942699379,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet9 = 959476595,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet10 = 808547187,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet11 = 825324403,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet12 = 842101619,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet13 = 858878835,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet14 = 875656051,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet15 = 892433267,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet16 = 909210483,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet17 = 925987699,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet18 = 942764915,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet19 = 959542131,
        //
        // Summary:
        //     Enables stylistic alternatives for portions of the character set, for a visual
        //     effect chosen by the font author.
        StylisticSet20 = 808612723,
        //
        // Summary:
        //     May replace a default glyph with a subscript glyph, or it may combine a glyph
        //     substitution with positioning adjustments for proper placement.
        Subscript = 1935832435,
        //
        // Summary:
        //     Replaces lining or oldstyle figures with superior figures, and replaces lowercase
        //     letters with superior letters.
        Superscript = 1936749939,
        //
        // Summary:
        //     Replaces default character glyphs with corresponding swash glyphs.
        Swash = 1752397683,
        //
        // Summary:
        //     Replaces the default glyphs with corresponding forms designed specifically for
        //     titling.
        Titling = 1819568500,
        //
        // Summary:
        //     Replaces 'simplified' Japanese kanji or Chinese hanzi forms with the corresponding
        //     'traditional' forms.
        TraditionalNameForms = 1835101812,
        //
        // Summary:
        //     Replaces figure glyphs set on proportional widths with corresponding glyphs set
        //     on uniform (tabular) widths.
        TabularFigures = 1836412532,
        //
        // Summary:
        //     Replaces 'simplified' Chinese hanzi or Japanese kanji forms with the corresponding
        //     'traditional' forms.
        TraditionalForms = 1684107892,
        //
        // Summary:
        //     Replaces glyphs on other widths with glyphs set on widths of one third of an
        //     em unit.
        ThirdWidths = 1684633460,
        //
        // Summary:
        //     Maps uppercase and lowercase letters to a mixed set of lowercase and small capital
        //     forms, resulting in a single case alphabet.
        Unicase = 1667853941,
        //
        // Summary:
        //     Allows the user to change from the default 0 to a slashed form.
        SlashedZero = 1869768058
    };
}
