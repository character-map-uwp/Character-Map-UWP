namespace CharacterMap.Models;

public enum PanoseFamily
{
    /// <summary>
    /// Any typeface classification.
    /// </summary>
    Any = 0,
    /// <summary>
    /// No fit typeface classification.
    /// </summary>
    No_Fit = 1,
    /// <summary>
    /// Text display typeface classification.
    /// </summary>
    Text_Display = 2,
    /// <summary>
    /// Script (or hand written) typeface classification.
    /// </summary>
    Script = 3,
    /// <summary>
    /// Decorative typeface classification.
    /// </summary>
    Decorative = 4,
    /// <summary>
    /// Symbol typeface classification.
    /// </summary>
    Symbol = 5,
    /// <summary>
    /// Pictorial (or symbol) typeface classification.
    /// </summary>
    Pictorial = 6
}


/// <summary>
/// Appearance of the serifs.
/// Present for families: 2-text
/// </summary>
public enum PanoseSerifStyle
{
    ANY = 0,
    NO_FIT = 1,
    COVE = 2,
    OBTUSE_COVE = 3,
    SQUARE_COVE = 4,
    OBTUSE_SQUARE_COVE = 5,
    SQUARE = 6,
    THIN = 7,
    OVAL = 8,
    EXAGGERATED = 9,
    TRIANGLE = 10,
    NORMAL_SANS = 11,
    OBTUSE_SANS = 12,
    PERPENDICULAR_SANS = 13,
    FLARED = 14,
    ROUNDED = 15
};

/// <summary>
/// PANOSE font weights. These roughly correspond to the DWRITE_FONT_WEIGHT's
/// using (panose_weight - 2) * 100.
/// Present for families: 2-text, 3-script, 4-decorative, 5-symbol
/// </summary>
public enum PanoseWeight
{
    ANY = 0,
    NO_FIT = 1,
    VERY_LIGHT = 2,
    LIGHT = 3,
    THIN = 4,
    BOOK = 5,
    MEDIUM = 6,
    DEMI = 7,
    BOLD = 8,
    HEAVY = 9,
    BLACK = 10,
    EXTRA_BLACK = 11,
};

/// <summary>
/// Proportion of the glyph shape considering additional detail to standard
/// characters.
/// Present for families: 2-text
/// </summary>
public enum PanoseProportion
{
    ANY = 0,
    NO_FIT = 1,
    OLD_STYLE = 2,
    MODERN = 3,
    EVEN_WIDTH = 4,
    EXPANDED = 5,
    CONDENSED = 6,
    VERY_EXPANDED = 7,
    VERY_CONDENSED = 8,
    MONOSPACED = 9
};

/// <summary>
/// Ratio between thickest and thinnest point of the stroke for a letter such
/// as uppercase 'O'.
/// Present for families: 2-text, 3-script, 4-decorative
/// </summary>
public enum PanoseContrast
{
    ANY = 0,
    NO_FIT = 1,
    NONE = 2,
    VERY_LOW = 3,
    LOW = 4,
    MEDIUM_LOW = 5,
    MEDIUM = 6,
    MEDIUM_HIGH = 7,
    HIGH = 8,
    VERY_HIGH = 9,
    HORIZONTAL_LOW = 10,
    HORIZONTAL_MEDIUM = 11,
    HORIZONTAL_HIGH = 12,
    BROKEN = 13
};

/// <summary>
/// Relationship between thin and thick stems.
/// Present for families: 2-text
/// </summary>
public enum PanoseStrokeVariation
{
    ANY = 0,
    NO_FIT = 1,
    NO_VARIATION = 2,
    GRADUAL_DIAGONAL = 3,
    GRADUAL_TRANSITIONAL = 4,
    GRADUAL_VERTICAL = 5,
    GRADUAL_HORIZONTAL = 6,
    RAPID_VERTICAL = 7,
    RAPID_HORIZONTAL = 8,
    INSTANT_VERTICAL = 9,
    INSTANT_HORIZONTAL = 10
};

/// <summary>
/// Style of termination of stems and rounded letterforms.
/// Present for families: 2-text
/// </summary>
public enum PanoseArmStyle
{
    ANY = 0,
    NO_FIT = 1,
    STRAIGHT_ARMS_HORIZONTAL = 2,
    STRAIGHT_ARMS_WEDGE = 3,
    STRAIGHT_ARMS_VERTICAL = 4,
    STRAIGHT_ARMS_SINGLE_SERIF = 5,
    STRAIGHT_ARMS_DOUBLE_SERIF = 6,
    NONSTRAIGHT_ARMS_HORIZONTAL = 7,
    NONSTRAIGHT_ARMS_WEDGE = 8,
    NONSTRAIGHT_ARMS_VERTICAL = 9,
    NONSTRAIGHT_ARMS_SINGLE_SERIF = 10,
    NONSTRAIGHT_ARMS_DOUBLE_SERIF = 11,
};

/// <summary>
/// Roundness of letterform.
/// Present for families: 2-text
/// </summary>
public enum PanoseLetterform
{
    ANY = 0,
    NO_FIT = 1,
    NORMAL_CONTACT = 2,
    NORMAL_WEIGHTED = 3,
    NORMAL_BOXED = 4,
    NORMAL_FLATTENED = 5,
    NORMAL_ROUNDED = 6,
    NORMAL_OFF_CENTER = 7,
    NORMAL_SQUARE = 8,
    OBLIQUE_CONTACT = 9,
    OBLIQUE_WEIGHTED = 10,
    OBLIQUE_BOXED = 11,
    OBLIQUE_FLATTENED = 12,
    OBLIQUE_ROUNDED = 13,
    OBLIQUE_OFF_CENTER = 14,
    OBLIQUE_SQUARE = 15
};

/// <summary>
/// Placement of midline across uppercase characters and treatment of diagonal
/// stem apexes.
/// Present for families: 2-text
/// </summary>
public enum PanoseMidline
{
    ANY = 0,
    NO_FIT = 1,
    STANDARD_TRIMMED = 2,
    STANDARD_POINTED = 3,
    STANDARD_SERIFED = 4,
    HIGH_TRIMMED = 5,
    HIGH_POINTED = 6,
    HIGH_SERIFED = 7,
    CONSTANT_TRIMMED = 8,
    CONSTANT_POINTED = 9,
    CONSTANT_SERIFED = 10,
    LOW_TRIMMED = 11,
    LOW_POINTED = 12,
    LOW_SERIFED = 13
};

/// <summary>
/// Relative size of lowercase letters and treament of diacritic marks
/// and uppercase glyphs.
/// Present for families: 2-text
/// </summary>
public enum PanoseXHeight
{
    ANY = 0,
    NO_FIT = 1,
    CONSTANT_SMALL = 2,
    CONSTANT_STANDARD = 3,
    CONSTANT_LARGE = 4,
    DUCKING_SMALL = 5,
    DUCKING_STANDARD = 6,
    DUCKING_LARGE = 7,
};

public enum PanoseToolKind
{
    ANY = 0,
    NO_FIT = 1,
    FLAT_NIB = 2,
    PRESSURE_POINT = 3,
    ENGRAVED = 4,
    BALL = 5,
    BRUSH = 6,
    ROUGH = 7,
    FELT_PEN_BRUSH_TIP = 8,
    WILD_BRUSH = 9
};

/// <summary>
/// Monospace vs proportional.
/// Present for families: 3-script, 5-symbol
/// </summary>
public enum PanoseSpacing
{
    ANY = 0,
    NO_FIT = 1,
    PROPORTIONAL_SPACED = 2,
    MONOSPACED = 3,
};


/// <summary>
/// Ratio between width and height of the face.
/// Present for families: 3-script
/// </summary>
public enum PanoseAspectRatio
{
    ANY = 0,
    NO_FIT = 1,
    VERY_CONDENSED = 2,
    CONDENSED = 3,
    NORMAL = 4,
    EXPANDED = 5,
    VERY_EXPANDED = 6
};

/// <summary>
/// Topology of letterforms.
/// Present for families: 3-script
/// </summary>
public enum PanoseScriptTopology
{
    ANY = 0,
    NO_FIT = 1,
    ROMAN_DISCONNECTED = 2,
    ROMAN_TRAILING = 3,
    ROMAN_CONNECTED = 4,
    CURSIVE_DISCONNECTED = 5,
    CURSIVE_TRAILING = 6,
    CURSIVE_CONNECTED = 7,
    BLACKLETTER_DISCONNECTED = 8,
    BLACKLETTER_TRAILING = 9,
    BLACKLETTER_CONNECTED = 10
};

/// <summary>
/// General look of the face, considering slope and tails.
/// Present for families: 3-script
/// </summary>
public enum PanoseScriptForm
{
    ANY = 0,
    NO_FIT = 1,
    UPRIGHT_NO_WRAPPING = 2,
    UPRIGHT_SOME_WRAPPING = 3,
    UPRIGHT_MORE_WRAPPING = 4,
    UPRIGHT_EXTREME_WRAPPING = 5,
    OBLIQUE_NO_WRAPPING = 6,
    OBLIQUE_SOME_WRAPPING = 7,
    OBLIQUE_MORE_WRAPPING = 8,
    OBLIQUE_EXTREME_WRAPPING = 9,
    EXAGGERATED_NO_WRAPPING = 10,
    EXAGGERATED_SOME_WRAPPING = 11,
    EXAGGERATED_MORE_WRAPPING = 12,
    EXAGGERATED_EXTREME_WRAPPING = 13
};

/// <summary>
/// How character ends and miniscule ascenders are treated.
/// Present for families: 3-script
/// </summary>
public enum PanoseFinals
{
    ANY = 0,
    NO_FIT = 1,
    NONE_NO_LOOPS = 2,
    NONE_CLOSED_LOOPS = 3,
    NONE_OPEN_LOOPS = 4,
    SHARP_NO_LOOPS = 5,
    SHARP_CLOSED_LOOPS = 6,
    SHARP_OPEN_LOOPS = 7,
    TAPERED_NO_LOOPS = 8,
    TAPERED_CLOSED_LOOPS = 9,
    TAPERED_OPEN_LOOPS = 10,
    ROUND_NO_LOOPS = 11,
    ROUND_CLOSED_LOOPS = 12,
    ROUND_OPEN_LOOPS = 13
};

/// <summary>
/// Relative size of the lowercase letters.
/// Present for families: 3-script
/// </summary>
public enum PanoseXAscent
{
    ANY = 0,
    NO_FIT = 1,
    VERY_LOW = 2,
    LOW = 3,
    MEDIUM = 4,
    HIGH = 5,
    VERY_HIGH = 6
};

/// <summary>
/// General look of the face.
/// Present for families: 4-decorative
/// </summary>
public enum PanoseDecorativeClass
{
    ANY = 0,
    NO_FIT = 1,
    DERIVATIVE = 2,
    NONSTANDARD_TOPOLOGY = 3,
    NONSTANDARD_ELEMENTS = 4,
    NONSTANDARD_ASPECT = 5,
    INITIALS = 6,
    CARTOON = 7,
    PICTURE_STEMS = 8,
    ORNAMENTED = 9,
    TEXT_AND_BACKGROUND = 10,
    COLLAGE = 11,
    MONTAGE = 12
};

/// <summary>
/// Ratio between the width and height of the face.
/// Present for families: 4-decorative
/// </summary>
public enum PanoseAspect
{
    ANY = 0,
    NO_FIT = 1,
    SUPER_CONDENSED = 2,
    VERY_CONDENSED = 3,
    CONDENSED = 4,
    NORMAL = 5,
    EXTENDED = 6,
    VERY_EXTENDED = 7,
    SUPER_EXTENDED = 8,
    MONOSPACED = 9
};

/// <summary>
/// Type of fill/line (treatment).
/// Present for families: 4-decorative
/// </summary>
public enum PanoseFill
{
    ANY = 0,
    NO_FIT = 1,
    STANDARD_SOLID_FILL = 2,
    NO_FILL = 3,
    PATTERNED_FILL = 4,
    COMPLEX_FILL = 5,
    SHAPED_FILL = 6,
    DRAWN_DISTRESSED = 7,
};

/// <summary>
/// Outline handling.
/// Present for families: 4-decorative
/// </summary>
public enum PanoseLining
{
    ANY = 0,
    NO_FIT = 1,
    NONE = 2,
    INLINE = 3,
    OUTLINE = 4,
    ENGRAVED = 5,
    SHADOW = 6,
    RELIEF = 7,
    BACKDROP = 8
};

/// <summary>
/// Overall shape characteristics of the font.
/// Present for families: 4-decorative
/// </summary>
public enum PanoseDecorativeTopology
{
    ANY = 0,
    NO_FIT = 1,
    STANDARD = 2,
    SQUARE = 3,
    MULTIPLE_SEGMENT = 4,
    ART_DECO = 5,
    UNEVEN_WEIGHTING = 6,
    DIVERSE_ARMS = 7,
    DIVERSE_FORMS = 8,
    LOMBARDIC_FORMS = 9,
    UPPER_CASE_IN_LOWER_CASE = 10,
    IMPLIED_TOPOLOGY = 11,
    HORSESHOE_E_AND_A = 12,
    CURSIVE = 13,
    BLACKLETTER = 14,
    SWASH_VARIANCE = 15
};

/// <summary>
/// Type of characters available in the font.
/// Present for families: 4-decorative
/// </summary>
public enum PanoseCharacterRanges
{
    ANY = 0,
    NO_FIT = 1,
    EXTENDED_COLLECTION = 2,
    LITERALS = 3,
    NO_LOWER_CASE = 4,
    SMALL_CAPS = 5
};

/// <summary>
/// Kind of symbol set.
/// Present for families: 5-symbol
/// </summary>
public enum PanoseSymbolKind
{
    ANY = 0,
    NO_FIT = 1,
    MONTAGES = 2,
    PICTURES = 3,
    SHAPES = 4,
    SCIENTIFIC = 5,
    MUSIC = 6,
    EXPERT = 7,
    PATTERNS = 8,
    BOARDERS = 9,
    ICONS = 10,
    LOGOS = 11,
    INDUSTRY_SPECIFIC = 12
};

/// <summary>
/// Aspect ratio of symbolic characters.
/// Present for families: 5-symbol
/// </summary>
public enum PanoseSymbolAspectRatio
{
    ANY = 0,
    NO_FIT = 1,
    NO_WIDTH = 2,
    EXCEPTIONALLY_WIDE = 3,
    SUPER_WIDE = 4,
    VERY_WIDE = 5,
    WIDE = 6,
    NORMAL = 7,
    NARROW = 8,
    VERY_NARROW = 9
};
