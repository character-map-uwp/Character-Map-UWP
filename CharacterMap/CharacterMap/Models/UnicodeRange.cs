using System.Diagnostics;

namespace CharacterMap.Models;

public partial struct UnicodeRange
{
    public uint Start { get; }
    public uint End { get; }

    public UnicodeRange(uint start, uint end)
    {
        Start = start;
        End = end;
    }
}

public partial struct UnicodeRange
{
    public static UnicodeRange BasicLatinLetters { get; } = new(0x0041, 0x007A);
    public static UnicodeRange Arabic { get; } = new(0x0600, 0x06FF);
    public static UnicodeRange Cyrillic { get; } = new(0x0400, 0x052F);
    public static UnicodeRange Thai { get; } = new(0x0E00, 0x0E7F);
    public static UnicodeRange GreekAndCoptic { get; } = new(0x0370, 0x03FF);
    public static UnicodeRange Hebrew { get; } = new(0x0590, 0x05FF);
    public static UnicodeRange CJKUnifiedIdeographs { get; } = new(0x4E00, 0x9FFF);
    public static UnicodeRange KoreanHangulSyllables { get; } = new(0xAC00, 0xD7AF);
    public static UnicodeRange Dingbats { get; } = new(0x2700, 0x27BF);
    public static UnicodeRange Emoticons { get; } = new(0x1F600, 0x1F64F);
    public static UnicodeRange MiscSymbols { get; } = new(0x1F300, 0x1F5FF);
    public static UnicodeRange SupplementalSymbols { get; } = new(0x1F900, 0x1F9FF);
    public static UnicodeRange SymbolsExtended { get; } = new(0x1FA70, 0x1FAFF);
    public static UnicodeRange TransportSymbols { get; } = new(0x1F680, 0x1F6FF);
}

[DebuggerDisplay("{Name}, Start: {Start}, End: {End}")]
public class NamedUnicodeRange
{
    public string Name { get; }
    public uint Start { get; }
    public uint End { get; }

    public UnicodeRange Range { get; }

    public NamedUnicodeRange(string name, uint start, uint length)
    {
        Name = name;
        Start = start;
        End = start + length -1;
        Range = new(Start, End);
    }

    /* Constructor with a defined "END" value */
    public NamedUnicodeRange(uint start, uint end, string name)
    {
        Name = name;
        Start = start;
        End = end;
        Range = new(Start, End);
    }

    public bool Contains(uint index) => index >= Start && index <= End;
}

public static class UnicodeRanges
{
    /* Created using https://dotnetfiddle.net/jexT5V */

    public static NamedUnicodeRange BasicLatin = new(0x0000, 0x007F, "Basic Latin");
    public static NamedUnicodeRange Latin1Supplement = new(0x0080, 0x00FF, "Latin-1 Supplement");
    public static NamedUnicodeRange LatinExtendedA = new(0x0100, 0x017F, "Latin Extended-A");
    public static NamedUnicodeRange LatinExtendedB = new(0x0180, 0x024F, "Latin Extended-B");
    public static NamedUnicodeRange IPAExtensions = new(0x0250, 0x02AF, "IPA Extensions");
    public static NamedUnicodeRange SpacingModifierLetters = new(0x02B0, 0x02FF, "Spacing Modifier Letters");
    public static NamedUnicodeRange CombiningDiacriticalMarks = new(0x0300, 0x036F, "Combining Diacritical Marks");
    public static NamedUnicodeRange GreekAndCoptic = new(0x0370, 0x03FF, "Greek and Coptic");
    public static NamedUnicodeRange Cyrillic = new(0x0400, 0x04FF, "Cyrillic");
    public static NamedUnicodeRange CyrillicSupplement = new(0x0500, 0x052F, "Cyrillic Supplement");
    public static NamedUnicodeRange Armenian = new(0x0530, 0x058F, "Armenian");
    public static NamedUnicodeRange Hebrew = new(0x0590, 0x05FF, "Hebrew");
    public static NamedUnicodeRange Arabic = new(0x0600, 0x06FF, "Arabic");
    public static NamedUnicodeRange Syriac = new(0x0700, 0x074F, "Syriac");
    public static NamedUnicodeRange ArabicSupplement = new(0x0750, 0x077F, "Arabic Supplement");
    public static NamedUnicodeRange Thaana = new(0x0780, 0x07BF, "Thaana");
    public static NamedUnicodeRange NKo = new(0x07C0, 0x07FF, "NKo");
    public static NamedUnicodeRange Samaritan = new(0x0800, 0x083F, "Samaritan");
    public static NamedUnicodeRange Mandaic = new(0x0840, 0x085F, "Mandaic");
    public static NamedUnicodeRange SyriacSupplement = new(0x0860, 0x086F, "Syriac Supplement");
    public static NamedUnicodeRange ArabicExtendedB = new(0x0870, 0x089F, "Arabic Extended-B");
    public static NamedUnicodeRange ArabicExtendedA = new(0x08A0, 0x08FF, "Arabic Extended-A");
    public static NamedUnicodeRange Devanagari = new(0x0900, 0x097F, "Devanagari");
    public static NamedUnicodeRange Bengali = new(0x0980, 0x09FF, "Bengali");
    public static NamedUnicodeRange Gurmukhi = new(0x0A00, 0x0A7F, "Gurmukhi");
    public static NamedUnicodeRange Gujarati = new(0x0A80, 0x0AFF, "Gujarati");
    public static NamedUnicodeRange Oriya = new(0x0B00, 0x0B7F, "Oriya");
    public static NamedUnicodeRange Tamil = new(0x0B80, 0x0BFF, "Tamil");
    public static NamedUnicodeRange Telugu = new(0x0C00, 0x0C7F, "Telugu");
    public static NamedUnicodeRange Kannada = new(0x0C80, 0x0CFF, "Kannada");
    public static NamedUnicodeRange Malayalam = new(0x0D00, 0x0D7F, "Malayalam");
    public static NamedUnicodeRange Sinhala = new(0x0D80, 0x0DFF, "Sinhala");
    public static NamedUnicodeRange Thai = new(0x0E00, 0x0E7F, "Thai");
    public static NamedUnicodeRange Lao = new(0x0E80, 0x0EFF, "Lao");
    public static NamedUnicodeRange Tibetan = new(0x0F00, 0x0FFF, "Tibetan");
    public static NamedUnicodeRange Myanmar = new(0x1000, 0x109F, "Myanmar");
    public static NamedUnicodeRange Georgian = new(0x10A0, 0x10FF, "Georgian");
    public static NamedUnicodeRange HangulJamo = new(0x1100, 0x11FF, "Hangul Jamo");
    public static NamedUnicodeRange Ethiopic = new(0x1200, 0x137F, "Ethiopic");
    public static NamedUnicodeRange EthiopicSupplement = new(0x1380, 0x139F, "Ethiopic Supplement");
    public static NamedUnicodeRange Cherokee = new(0x13A0, 0x13FF, "Cherokee");
    public static NamedUnicodeRange UnifiedCanadianAboriginalSyllabics = new(0x1400, 0x167F, "Unified Canadian Aboriginal Syllabics");
    public static NamedUnicodeRange Ogham = new(0x1680, 0x169F, "Ogham");
    public static NamedUnicodeRange Runic = new(0x16A0, 0x16FF, "Runic");
    public static NamedUnicodeRange Tagalog = new(0x1700, 0x171F, "Tagalog");
    public static NamedUnicodeRange Hanunoo = new(0x1720, 0x173F, "Hanunoo");
    public static NamedUnicodeRange Buhid = new(0x1740, 0x175F, "Buhid");
    public static NamedUnicodeRange Tagbanwa = new(0x1760, 0x177F, "Tagbanwa");
    public static NamedUnicodeRange Khmer = new(0x1780, 0x17FF, "Khmer");
    public static NamedUnicodeRange Mongolian = new(0x1800, 0x18AF, "Mongolian");
    public static NamedUnicodeRange UnifiedCanadianAboriginalSyllabicsExtended = new(0x18B0, 0x18FF, "Unified Canadian Aboriginal Syllabics Extended");
    public static NamedUnicodeRange Limbu = new(0x1900, 0x194F, "Limbu");
    public static NamedUnicodeRange TaiLe = new(0x1950, 0x197F, "Tai Le");
    public static NamedUnicodeRange NewTaiLue = new(0x1980, 0x19DF, "New Tai Lue");
    public static NamedUnicodeRange KhmerSymbols = new(0x19E0, 0x19FF, "Khmer Symbols");
    public static NamedUnicodeRange Buginese = new(0x1A00, 0x1A1F, "Buginese");
    public static NamedUnicodeRange TaiTham = new(0x1A20, 0x1AAF, "Tai Tham");
    public static NamedUnicodeRange CombiningDiacriticalMarksExtended = new(0x1AB0, 0x1AFF, "Combining Diacritical Marks Extended");
    public static NamedUnicodeRange Balinese = new(0x1B00, 0x1B7F, "Balinese");
    public static NamedUnicodeRange Sundanese = new(0x1B80, 0x1BBF, "Sundanese");
    public static NamedUnicodeRange Batak = new(0x1BC0, 0x1BFF, "Batak");
    public static NamedUnicodeRange Lepcha = new(0x1C00, 0x1C4F, "Lepcha");
    public static NamedUnicodeRange OlChiki = new(0x1C50, 0x1C7F, "Ol Chiki");
    public static NamedUnicodeRange CyrillicExtendedC = new(0x1C80, 0x1C8F, "Cyrillic Extended-C");
    public static NamedUnicodeRange GeorgianExtended = new(0x1C90, 0x1CBF, "Georgian Extended");
    public static NamedUnicodeRange SundaneseSupplement = new(0x1CC0, 0x1CCF, "Sundanese Supplement");
    public static NamedUnicodeRange VedicExtensions = new(0x1CD0, 0x1CFF, "Vedic Extensions");
    public static NamedUnicodeRange PhoneticExtensions = new(0x1D00, 0x1D7F, "Phonetic Extensions");
    public static NamedUnicodeRange PhoneticExtensionsSupplement = new(0x1D80, 0x1DBF, "Phonetic Extensions Supplement");
    public static NamedUnicodeRange CombiningDiacriticalMarksSupplement = new(0x1DC0, 0x1DFF, "Combining Diacritical Marks Supplement");
    public static NamedUnicodeRange LatinExtendedAdditional = new(0x1E00, 0x1EFF, "Latin Extended Additional");
    public static NamedUnicodeRange GreekExtended = new(0x1F00, 0x1FFF, "Greek Extended");
    public static NamedUnicodeRange GeneralPunctuation = new(0x2000, 0x206F, "General Punctuation");
    public static NamedUnicodeRange SuperscriptsAndSubscripts = new(0x2070, 0x209F, "Superscripts and Subscripts");
    public static NamedUnicodeRange CurrencySymbols = new(0x20A0, 0x20CF, "Currency Symbols");
    public static NamedUnicodeRange CombiningDiacriticalMarksforSymbols = new(0x20D0, 0x20FF, "Combining Diacritical Marks for Symbols");
    public static NamedUnicodeRange LetterlikeSymbols = new(0x2100, 0x214F, "Letterlike Symbols");
    public static NamedUnicodeRange NumberForms = new(0x2150, 0x218F, "Number Forms");
    public static NamedUnicodeRange Arrows = new(0x2190, 0x21FF, "Arrows");
    public static NamedUnicodeRange MathematicalOperators = new(0x2200, 0x22FF, "Mathematical Operators");
    public static NamedUnicodeRange MiscellaneousTechnical = new(0x2300, 0x23FF, "Miscellaneous Technical");
    public static NamedUnicodeRange ControlPictures = new(0x2400, 0x243F, "Control Pictures");
    public static NamedUnicodeRange OpticalCharacterRecognition = new(0x2440, 0x245F, "Optical Character Recognition");
    public static NamedUnicodeRange EnclosedAlphanumerics = new(0x2460, 0x24FF, "Enclosed Alphanumerics");
    public static NamedUnicodeRange BoxDrawing = new(0x2500, 0x257F, "Box Drawing");
    public static NamedUnicodeRange BlockElements = new(0x2580, 0x259F, "Block Elements");
    public static NamedUnicodeRange GeometricShapes = new(0x25A0, 0x25FF, "Geometric Shapes");
    public static NamedUnicodeRange MiscellaneousSymbols = new(0x2600, 0x26FF, "Miscellaneous Symbols");
    public static NamedUnicodeRange Dingbats = new(0x2700, 0x27BF, "Dingbats");
    public static NamedUnicodeRange MiscellaneousMathematicalSymbolsA = new(0x27C0, 0x27EF, "Miscellaneous Mathematical Symbols-A");
    public static NamedUnicodeRange SupplementalArrowsA = new(0x27F0, 0x27FF, "Supplemental Arrows-A");
    public static NamedUnicodeRange BraillePatterns = new(0x2800, 0x28FF, "Braille Patterns");
    public static NamedUnicodeRange SupplementalArrowsB = new(0x2900, 0x297F, "Supplemental Arrows-B");
    public static NamedUnicodeRange MiscellaneousMathematicalSymbolsB = new(0x2980, 0x29FF, "Miscellaneous Mathematical Symbols-B");
    public static NamedUnicodeRange SupplementalMathematicalOperators = new(0x2A00, 0x2AFF, "Supplemental Mathematical Operators");
    public static NamedUnicodeRange MiscellaneousSymbolsAndArrows = new(0x2B00, 0x2BFF, "Miscellaneous Symbols and Arrows");
    public static NamedUnicodeRange Glagolitic = new(0x2C00, 0x2C5F, "Glagolitic");
    public static NamedUnicodeRange LatinExtendedC = new(0x2C60, 0x2C7F, "Latin Extended-C");
    public static NamedUnicodeRange Coptic = new(0x2C80, 0x2CFF, "Coptic");
    public static NamedUnicodeRange GeorgianSupplement = new(0x2D00, 0x2D2F, "Georgian Supplement");
    public static NamedUnicodeRange Tifinagh = new(0x2D30, 0x2D7F, "Tifinagh");
    public static NamedUnicodeRange EthiopicExtended = new(0x2D80, 0x2DDF, "Ethiopic Extended");
    public static NamedUnicodeRange CyrillicExtendedA = new(0x2DE0, 0x2DFF, "Cyrillic Extended-A");
    public static NamedUnicodeRange SupplementalPunctuation = new(0x2E00, 0x2E7F, "Supplemental Punctuation");
    public static NamedUnicodeRange CJKRadicalsSupplement = new(0x2E80, 0x2EFF, "CJK Radicals Supplement");
    public static NamedUnicodeRange KangxiRadicals = new(0x2F00, 0x2FDF, "Kangxi Radicals");
    public static NamedUnicodeRange IdeographicDescriptionCharacters = new(0x2FF0, 0x2FFF, "Ideographic Description Characters");
    public static NamedUnicodeRange CJKSymbolsAndPunctuation = new(0x3000, 0x303F, "CJK Symbols and Punctuation");
    public static NamedUnicodeRange Hiragana = new(0x3040, 0x309F, "Hiragana");
    public static NamedUnicodeRange Katakana = new(0x30A0, 0x30FF, "Katakana");
    public static NamedUnicodeRange Bopomofo = new(0x3100, 0x312F, "Bopomofo");
    public static NamedUnicodeRange HangulCompatibilityJamo = new(0x3130, 0x318F, "Hangul Compatibility Jamo");
    public static NamedUnicodeRange Kanbun = new(0x3190, 0x319F, "Kanbun");
    public static NamedUnicodeRange BopomofoExtended = new(0x31A0, 0x31BF, "Bopomofo Extended");
    public static NamedUnicodeRange CJKStrokes = new(0x31C0, 0x31EF, "CJK Strokes");
    public static NamedUnicodeRange KatakanaPhoneticExtensions = new(0x31F0, 0x31FF, "Katakana Phonetic Extensions");
    public static NamedUnicodeRange EnclosedCJKLettersAndMonths = new(0x3200, 0x32FF, "Enclosed CJK Letters and Months");
    public static NamedUnicodeRange CJKCompatibility = new(0x3300, 0x33FF, "CJK Compatibility");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionA = new(0x3400, 0x4DBF, "CJK Unified Ideographs Extension A");
    public static NamedUnicodeRange YijingHexagramSymbols = new(0x4DC0, 0x4DFF, "Yijing Hexagram Symbols");
    public static NamedUnicodeRange CJKUnifiedIdeographs = new(0x4E00, 0x9FFF, "CJK Unified Ideographs");
    public static NamedUnicodeRange YiSyllables = new(0xA000, 0xA48F, "Yi Syllables");
    public static NamedUnicodeRange YiRadicals = new(0xA490, 0xA4CF, "Yi Radicals");
    public static NamedUnicodeRange Lisu = new(0xA4D0, 0xA4FF, "Lisu");
    public static NamedUnicodeRange Vai = new(0xA500, 0xA63F, "Vai");
    public static NamedUnicodeRange CyrillicExtendedB = new(0xA640, 0xA69F, "Cyrillic Extended-B");
    public static NamedUnicodeRange Bamum = new(0xA6A0, 0xA6FF, "Bamum");
    public static NamedUnicodeRange ModifierToneLetters = new(0xA700, 0xA71F, "Modifier Tone Letters");
    public static NamedUnicodeRange LatinExtendedD = new(0xA720, 0xA7FF, "Latin Extended-D");
    public static NamedUnicodeRange SylotiNagri = new(0xA800, 0xA82F, "Syloti Nagri");
    public static NamedUnicodeRange CommonIndicNumberForms = new(0xA830, 0xA83F, "Common Indic Number Forms");
    public static NamedUnicodeRange Phagspa = new(0xA840, 0xA87F, "Phags-pa");
    public static NamedUnicodeRange Saurashtra = new(0xA880, 0xA8DF, "Saurashtra");
    public static NamedUnicodeRange DevanagariExtended = new(0xA8E0, 0xA8FF, "Devanagari Extended");
    public static NamedUnicodeRange KayahLi = new(0xA900, 0xA92F, "Kayah Li");
    public static NamedUnicodeRange Rejang = new(0xA930, 0xA95F, "Rejang");
    public static NamedUnicodeRange HangulJamoExtendedA = new(0xA960, 0xA97F, "Hangul Jamo Extended-A");
    public static NamedUnicodeRange Javanese = new(0xA980, 0xA9DF, "Javanese");
    public static NamedUnicodeRange MyanmarExtendedB = new(0xA9E0, 0xA9FF, "Myanmar Extended-B");
    public static NamedUnicodeRange Cham = new(0xAA00, 0xAA5F, "Cham");
    public static NamedUnicodeRange MyanmarExtendedA = new(0xAA60, 0xAA7F, "Myanmar Extended-A");
    public static NamedUnicodeRange TaiViet = new(0xAA80, 0xAADF, "Tai Viet");
    public static NamedUnicodeRange MeeteiMayekExtensions = new(0xAAE0, 0xAAFF, "Meetei Mayek Extensions");
    public static NamedUnicodeRange EthiopicExtendedA = new(0xAB00, 0xAB2F, "Ethiopic Extended-A");
    public static NamedUnicodeRange LatinExtendedE = new(0xAB30, 0xAB6F, "Latin Extended-E");
    public static NamedUnicodeRange CherokeeSupplement = new(0xAB70, 0xABBF, "Cherokee Supplement");
    public static NamedUnicodeRange MeeteiMayek = new(0xABC0, 0xABFF, "Meetei Mayek");
    public static NamedUnicodeRange HangulSyllables = new(0xAC00, 0xD7AF, "Hangul Syllables");
    public static NamedUnicodeRange HangulJamoExtendedB = new(0xD7B0, 0xD7FF, "Hangul Jamo Extended-B");
    public static NamedUnicodeRange HighSurrogates = new(0xD800, 0xDB7F, "High Surrogates");
    public static NamedUnicodeRange HighPrivateUseSurrogates = new(0xDB80, 0xDBFF, "High Private Use Surrogates");
    public static NamedUnicodeRange LowSurrogates = new(0xDC00, 0xDFFF, "Low Surrogates");
    public static NamedUnicodeRange PrivateUseArea = new(0xE000, 0xF8FF, "Private Use Area");
    public static NamedUnicodeRange CJKCompatibilityIdeographs = new(0xF900, 0xFAFF, "CJK Compatibility Ideographs");
    public static NamedUnicodeRange AlphabeticPresentationForms = new(0xFB00, 0xFB4F, "Alphabetic Presentation Forms");
    public static NamedUnicodeRange ArabicPresentationFormsA = new(0xFB50, 0xFDFF, "Arabic Presentation Forms-A");
    public static NamedUnicodeRange VariationSelectors = new(0xFE00, 0xFE0F, "Variation Selectors");
    public static NamedUnicodeRange VerticalForms = new(0xFE10, 0xFE1F, "Vertical Forms");
    public static NamedUnicodeRange CombiningHalfMarks = new(0xFE20, 0xFE2F, "Combining Half Marks");
    public static NamedUnicodeRange CJKCompatibilityForms = new(0xFE30, 0xFE4F, "CJK Compatibility Forms");
    public static NamedUnicodeRange SmallFormVariants = new(0xFE50, 0xFE6F, "Small Form Variants");
    public static NamedUnicodeRange ArabicPresentationFormsB = new(0xFE70, 0xFEFF, "Arabic Presentation Forms-B");
    public static NamedUnicodeRange HalfwidthAndFullwidthForms = new(0xFF00, 0xFFEF, "Halfwidth and Fullwidth Forms");
    public static NamedUnicodeRange Specials = new(0xFFF0, 0xFFFF, "Specials");
    public static NamedUnicodeRange LinearBSyllabary = new(0x10000, 0x1007F, "Linear B Syllabary");
    public static NamedUnicodeRange LinearBIdeograms = new(0x10080, 0x100FF, "Linear B Ideograms");
    public static NamedUnicodeRange AegeanNumbers = new(0x10100, 0x1013F, "Aegean Numbers");
    public static NamedUnicodeRange AncientGreekNumbers = new(0x10140, 0x1018F, "Ancient Greek Numbers");
    public static NamedUnicodeRange AncientSymbols = new(0x10190, 0x101CF, "Ancient Symbols");
    public static NamedUnicodeRange PhaistosDisc = new(0x101D0, 0x101FF, "Phaistos Disc");
    public static NamedUnicodeRange Lycian = new(0x10280, 0x1029F, "Lycian");
    public static NamedUnicodeRange Carian = new(0x102A0, 0x102DF, "Carian");
    public static NamedUnicodeRange CopticEpactNumbers = new(0x102E0, 0x102FF, "Coptic Epact Numbers");
    public static NamedUnicodeRange OldItalic = new(0x10300, 0x1032F, "Old Italic");
    public static NamedUnicodeRange Gothic = new(0x10330, 0x1034F, "Gothic");
    public static NamedUnicodeRange OldPermic = new(0x10350, 0x1037F, "Old Permic");
    public static NamedUnicodeRange Ugaritic = new(0x10380, 0x1039F, "Ugaritic");
    public static NamedUnicodeRange OldPersian = new(0x103A0, 0x103DF, "Old Persian");
    public static NamedUnicodeRange Deseret = new(0x10400, 0x1044F, "Deseret");
    public static NamedUnicodeRange Shavian = new(0x10450, 0x1047F, "Shavian");
    public static NamedUnicodeRange Osmanya = new(0x10480, 0x104AF, "Osmanya");
    public static NamedUnicodeRange Osage = new(0x104B0, 0x104FF, "Osage");
    public static NamedUnicodeRange Elbasan = new(0x10500, 0x1052F, "Elbasan");
    public static NamedUnicodeRange CaucasianAlbanian = new(0x10530, 0x1056F, "Caucasian Albanian");
    public static NamedUnicodeRange Vithkuqi = new(0x10570, 0x105BF, "Vithkuqi");
    public static NamedUnicodeRange LinearA = new(0x10600, 0x1077F, "Linear A");
    public static NamedUnicodeRange LatinExtendedF = new(0x10780, 0x107BF, "Latin Extended-F");
    public static NamedUnicodeRange CypriotSyllabary = new(0x10800, 0x1083F, "Cypriot Syllabary");
    public static NamedUnicodeRange ImperialAramaic = new(0x10840, 0x1085F, "Imperial Aramaic");
    public static NamedUnicodeRange Palmyrene = new(0x10860, 0x1087F, "Palmyrene");
    public static NamedUnicodeRange Nabataean = new(0x10880, 0x108AF, "Nabataean");
    public static NamedUnicodeRange Hatran = new(0x108E0, 0x108FF, "Hatran");
    public static NamedUnicodeRange Phoenician = new(0x10900, 0x1091F, "Phoenician");
    public static NamedUnicodeRange Lydian = new(0x10920, 0x1093F, "Lydian");
    public static NamedUnicodeRange MeroiticHieroglyphs = new(0x10980, 0x1099F, "Meroitic Hieroglyphs");
    public static NamedUnicodeRange MeroiticCursive = new(0x109A0, 0x109FF, "Meroitic Cursive");
    public static NamedUnicodeRange Kharoshthi = new(0x10A00, 0x10A5F, "Kharoshthi");
    public static NamedUnicodeRange OldSouthArabian = new(0x10A60, 0x10A7F, "Old South Arabian");
    public static NamedUnicodeRange OldNorthArabian = new(0x10A80, 0x10A9F, "Old North Arabian");
    public static NamedUnicodeRange Manichaean = new(0x10AC0, 0x10AFF, "Manichaean");
    public static NamedUnicodeRange Avestan = new(0x10B00, 0x10B3F, "Avestan");
    public static NamedUnicodeRange InscriptionalParthian = new(0x10B40, 0x10B5F, "Inscriptional Parthian");
    public static NamedUnicodeRange InscriptionalPahlavi = new(0x10B60, 0x10B7F, "Inscriptional Pahlavi");
    public static NamedUnicodeRange PsalterPahlavi = new(0x10B80, 0x10BAF, "Psalter Pahlavi");
    public static NamedUnicodeRange OldTurkic = new(0x10C00, 0x10C4F, "Old Turkic");
    public static NamedUnicodeRange OldHungarian = new(0x10C80, 0x10CFF, "Old Hungarian");
    public static NamedUnicodeRange HanifiRohingya = new(0x10D00, 0x10D3F, "Hanifi Rohingya");
    public static NamedUnicodeRange RumiNumeralSymbols = new(0x10E60, 0x10E7F, "Rumi Numeral Symbols");
    public static NamedUnicodeRange Yezidi = new(0x10E80, 0x10EBF, "Yezidi");
    public static NamedUnicodeRange ArabicExtendedC = new(0x10EC0, 0x10EFF, "Arabic Extended-C");
    public static NamedUnicodeRange OldSogdian = new(0x10F00, 0x10F2F, "Old Sogdian");
    public static NamedUnicodeRange Sogdian = new(0x10F30, 0x10F6F, "Sogdian");
    public static NamedUnicodeRange OldUyghur = new(0x10F70, 0x10FAF, "Old Uyghur");
    public static NamedUnicodeRange Chorasmian = new(0x10FB0, 0x10FDF, "Chorasmian");
    public static NamedUnicodeRange Elymaic = new(0x10FE0, 0x10FFF, "Elymaic");
    public static NamedUnicodeRange Brahmi = new(0x11000, 0x1107F, "Brahmi");
    public static NamedUnicodeRange Kaithi = new(0x11080, 0x110CF, "Kaithi");
    public static NamedUnicodeRange SoraSompeng = new(0x110D0, 0x110FF, "Sora Sompeng");
    public static NamedUnicodeRange Chakma = new(0x11100, 0x1114F, "Chakma");
    public static NamedUnicodeRange Mahajani = new(0x11150, 0x1117F, "Mahajani");
    public static NamedUnicodeRange Sharada = new(0x11180, 0x111DF, "Sharada");
    public static NamedUnicodeRange SinhalaArchaicNumbers = new(0x111E0, 0x111FF, "Sinhala Archaic Numbers");
    public static NamedUnicodeRange Khojki = new(0x11200, 0x1124F, "Khojki");
    public static NamedUnicodeRange Multani = new(0x11280, 0x112AF, "Multani");
    public static NamedUnicodeRange Khudawadi = new(0x112B0, 0x112FF, "Khudawadi");
    public static NamedUnicodeRange Grantha = new(0x11300, 0x1137F, "Grantha");
    public static NamedUnicodeRange Newa = new(0x11400, 0x1147F, "Newa");
    public static NamedUnicodeRange Tirhuta = new(0x11480, 0x114DF, "Tirhuta");
    public static NamedUnicodeRange Siddham = new(0x11580, 0x115FF, "Siddham");
    public static NamedUnicodeRange Modi = new(0x11600, 0x1165F, "Modi");
    public static NamedUnicodeRange MongolianSupplement = new(0x11660, 0x1167F, "Mongolian Supplement");
    public static NamedUnicodeRange Takri = new(0x11680, 0x116CF, "Takri");
    public static NamedUnicodeRange Ahom = new(0x11700, 0x1174F, "Ahom");
    public static NamedUnicodeRange Dogra = new(0x11800, 0x1184F, "Dogra");
    public static NamedUnicodeRange WarangCiti = new(0x118A0, 0x118FF, "Warang Citi");
    public static NamedUnicodeRange DivesAkuru = new(0x11900, 0x1195F, "Dives Akuru");
    public static NamedUnicodeRange Nandinagari = new(0x119A0, 0x119FF, "Nandinagari");
    public static NamedUnicodeRange ZanabazarSquare = new(0x11A00, 0x11A4F, "Zanabazar Square");
    public static NamedUnicodeRange Soyombo = new(0x11A50, 0x11AAF, "Soyombo");
    public static NamedUnicodeRange UnifiedCanadianAboriginalSyllabicsExtendedA = new(0x11AB0, 0x11ABF, "Unified Canadian Aboriginal Syllabics Extended-A");
    public static NamedUnicodeRange PauCinHau = new(0x11AC0, 0x11AFF, "Pau Cin Hau");
    public static NamedUnicodeRange DevanagariExtendedA = new(0x11B00, 0x11B5F, "Devanagari Extended-A");
    public static NamedUnicodeRange Bhaiksuki = new(0x11C00, 0x11C6F, "Bhaiksuki");
    public static NamedUnicodeRange Marchen = new(0x11C70, 0x11CBF, "Marchen");
    public static NamedUnicodeRange MasaramGondi = new(0x11D00, 0x11D5F, "Masaram Gondi");
    public static NamedUnicodeRange GunjalaGondi = new(0x11D60, 0x11DAF, "Gunjala Gondi");
    public static NamedUnicodeRange Makasar = new(0x11EE0, 0x11EFF, "Makasar");
    public static NamedUnicodeRange Kawi = new(0x11F00, 0x11F5F, "Kawi");
    public static NamedUnicodeRange LisuSupplement = new(0x11FB0, 0x11FBF, "Lisu Supplement");
    public static NamedUnicodeRange TamilSupplement = new(0x11FC0, 0x11FFF, "Tamil Supplement");
    public static NamedUnicodeRange Cuneiform = new(0x12000, 0x123FF, "Cuneiform");
    public static NamedUnicodeRange CuneiformNumbersAndPunctuation = new(0x12400, 0x1247F, "Cuneiform Numbers and Punctuation");
    public static NamedUnicodeRange EarlyDynasticCuneiform = new(0x12480, 0x1254F, "Early Dynastic Cuneiform");
    public static NamedUnicodeRange CyproMinoan = new(0x12F90, 0x12FFF, "Cypro-Minoan");
    public static NamedUnicodeRange EgyptianHieroglyphs = new(0x13000, 0x1342F, "Egyptian Hieroglyphs");
    public static NamedUnicodeRange EgyptianHieroglyphFormatControls = new(0x13430, 0x1345F, "Egyptian Hieroglyph Format Controls");
    public static NamedUnicodeRange AnatolianHieroglyphs = new(0x14400, 0x1467F, "Anatolian Hieroglyphs");
    public static NamedUnicodeRange BamumSupplement = new(0x16800, 0x16A3F, "Bamum Supplement");
    public static NamedUnicodeRange Mro = new(0x16A40, 0x16A6F, "Mro");
    public static NamedUnicodeRange Tangsa = new(0x16A70, 0x16ACF, "Tangsa");
    public static NamedUnicodeRange BassaVah = new(0x16AD0, 0x16AFF, "Bassa Vah");
    public static NamedUnicodeRange PahawhHmong = new(0x16B00, 0x16B8F, "Pahawh Hmong");
    public static NamedUnicodeRange Medefaidrin = new(0x16E40, 0x16E9F, "Medefaidrin");
    public static NamedUnicodeRange Miao = new(0x16F00, 0x16F9F, "Miao");
    public static NamedUnicodeRange IdeographicSymbolsAndPunctuation = new(0x16FE0, 0x16FFF, "Ideographic Symbols and Punctuation");
    public static NamedUnicodeRange Tangut = new(0x17000, 0x187FF, "Tangut");
    public static NamedUnicodeRange TangutComponents = new(0x18800, 0x18AFF, "Tangut Components");
    public static NamedUnicodeRange KhitanSmallScript = new(0x18B00, 0x18CFF, "Khitan Small Script");
    public static NamedUnicodeRange TangutSupplement = new(0x18D00, 0x18D7F, "Tangut Supplement");
    public static NamedUnicodeRange KanaExtendedB = new(0x1AFF0, 0x1AFFF, "Kana Extended-B");
    public static NamedUnicodeRange KanaSupplement = new(0x1B000, 0x1B0FF, "Kana Supplement");
    public static NamedUnicodeRange KanaExtendedA = new(0x1B100, 0x1B12F, "Kana Extended-A");
    public static NamedUnicodeRange SmallKanaExtension = new(0x1B130, 0x1B16F, "Small Kana Extension");
    public static NamedUnicodeRange Nushu = new(0x1B170, 0x1B2FF, "Nushu");
    public static NamedUnicodeRange Duployan = new(0x1BC00, 0x1BC9F, "Duployan");
    public static NamedUnicodeRange ShorthAndFormatControls = new(0x1BCA0, 0x1BCAF, "Shorthand Format Controls");
    public static NamedUnicodeRange ZnamennyMusicalNotation = new(0x1CF00, 0x1CFCF, "Znamenny Musical Notation");
    public static NamedUnicodeRange ByzantineMusicalSymbols = new(0x1D000, 0x1D0FF, "Byzantine Musical Symbols");
    public static NamedUnicodeRange MusicalSymbols = new(0x1D100, 0x1D1FF, "Musical Symbols");
    public static NamedUnicodeRange AncientGreekMusicalNotation = new(0x1D200, 0x1D24F, "Ancient Greek Musical Notation");
    public static NamedUnicodeRange KaktovikNumerals = new(0x1D2C0, 0x1D2DF, "Kaktovik Numerals");
    public static NamedUnicodeRange MayanNumerals = new(0x1D2E0, 0x1D2FF, "Mayan Numerals");
    public static NamedUnicodeRange TaiXuanJingSymbols = new(0x1D300, 0x1D35F, "Tai Xuan Jing Symbols");
    public static NamedUnicodeRange CountingRodNumerals = new(0x1D360, 0x1D37F, "Counting Rod Numerals");
    public static NamedUnicodeRange MathematicalAlphanumericSymbols = new(0x1D400, 0x1D7FF, "Mathematical Alphanumeric Symbols");
    public static NamedUnicodeRange SuttonSignWriting = new(0x1D800, 0x1DAAF, "Sutton SignWriting");
    public static NamedUnicodeRange LatinExtendedG = new(0x1DF00, 0x1DFFF, "Latin Extended-G");
    public static NamedUnicodeRange GlagoliticSupplement = new(0x1E000, 0x1E02F, "Glagolitic Supplement");
    public static NamedUnicodeRange CyrillicExtendedD = new(0x1E030, 0x1E08F, "Cyrillic Extended-D");
    public static NamedUnicodeRange NyiakengPuachueHmong = new(0x1E100, 0x1E14F, "Nyiakeng Puachue Hmong");
    public static NamedUnicodeRange Toto = new(0x1E290, 0x1E2BF, "Toto");
    public static NamedUnicodeRange Wancho = new(0x1E2C0, 0x1E2FF, "Wancho");
    public static NamedUnicodeRange NagMundari = new(0x1E4D0, 0x1E4FF, "Nag Mundari");
    public static NamedUnicodeRange EthiopicExtendedB = new(0x1E7E0, 0x1E7FF, "Ethiopic Extended-B");
    public static NamedUnicodeRange MendeKikakui = new(0x1E800, 0x1E8DF, "Mende Kikakui");
    public static NamedUnicodeRange Adlam = new(0x1E900, 0x1E95F, "Adlam");
    public static NamedUnicodeRange IndicSiyaqNumbers = new(0x1EC70, 0x1ECBF, "Indic Siyaq Numbers");
    public static NamedUnicodeRange OttomanSiyaqNumbers = new(0x1ED00, 0x1ED4F, "Ottoman Siyaq Numbers");
    public static NamedUnicodeRange ArabicMathematicalAlphabeticSymbols = new(0x1EE00, 0x1EEFF, "Arabic Mathematical Alphabetic Symbols");
    public static NamedUnicodeRange MahjongTiles = new(0x1F000, 0x1F02F, "Mahjong Tiles");
    public static NamedUnicodeRange DominoTiles = new(0x1F030, 0x1F09F, "Domino Tiles");
    public static NamedUnicodeRange PlayingCards = new(0x1F0A0, 0x1F0FF, "Playing Cards");
    public static NamedUnicodeRange EnclosedAlphanumericSupplement = new(0x1F100, 0x1F1FF, "Enclosed Alphanumeric Supplement");
    public static NamedUnicodeRange EnclosedIdeographicSupplement = new(0x1F200, 0x1F2FF, "Enclosed Ideographic Supplement");
    public static NamedUnicodeRange MiscellaneousSymbolsAndPictographs = new(0x1F300, 0x1F5FF, "Miscellaneous Symbols and Pictographs");
    public static NamedUnicodeRange Emoticons = new(0x1F600, 0x1F64F, "Emoticons");
    public static NamedUnicodeRange OrnamentalDingbats = new(0x1F650, 0x1F67F, "Ornamental Dingbats");
    public static NamedUnicodeRange TransportAndMapSymbols = new(0x1F680, 0x1F6FF, "Transport and Map Symbols");
    public static NamedUnicodeRange AlchemicalSymbols = new(0x1F700, 0x1F77F, "Alchemical Symbols");
    public static NamedUnicodeRange GeometricShapesExtended = new(0x1F780, 0x1F7FF, "Geometric Shapes Extended");
    public static NamedUnicodeRange SupplementalArrowsC = new(0x1F800, 0x1F8FF, "Supplemental Arrows-C");
    public static NamedUnicodeRange SupplementalSymbolsAndPictographs = new(0x1F900, 0x1F9FF, "Supplemental Symbols and Pictographs");
    public static NamedUnicodeRange ChessSymbols = new(0x1FA00, 0x1FA6F, "Chess Symbols");
    public static NamedUnicodeRange SymbolsAndPictographsExtendedA = new(0x1FA70, 0x1FAFF, "Symbols and Pictographs Extended-A");
    public static NamedUnicodeRange SymbolsforLegacyComputing = new(0x1FB00, 0x1FBFF, "Symbols for Legacy Computing");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionB = new(0x20000, 0x2A6DF, "CJK Unified Ideographs Extension B");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionC = new(0x2A700, 0x2B73F, "CJK Unified Ideographs Extension C");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionD = new(0x2B740, 0x2B81F, "CJK Unified Ideographs Extension D");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionE = new(0x2B820, 0x2CEAF, "CJK Unified Ideographs Extension E");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionF = new(0x2CEB0, 0x2EBEF, "CJK Unified Ideographs Extension F");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionI = new(0x2EBF0, 0x2EE5F, "CJK Unified Ideographs Extension I");
    public static NamedUnicodeRange CJKCompatibilityIdeographsSupplement = new(0x2F800, 0x2FA1F, "CJK Compatibility Ideographs Supplement");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionG = new(0x30000, 0x3134F, "CJK Unified Ideographs Extension G");
    public static NamedUnicodeRange CJKUnifiedIdeographsExtensionH = new(0x31350, 0x323AF, "CJK Unified Ideographs Extension H");
    public static NamedUnicodeRange Tags = new(0xE0000, 0xE007F, "Tags");
    public static NamedUnicodeRange VariationSelectorsSupplement = new(0xE0100, 0xE01EF, "Variation Selectors Supplement");
    public static NamedUnicodeRange SupplementaryPrivateUseAreaA = new(0xF0000, 0xFFFFF, "Supplementary Private Use Area-A");
    public static NamedUnicodeRange SupplementaryPrivateUseAreaB = new(0x100000, 0x10FFFF, "Supplementary Private Use Area-B");
    
    /* Manually added */
    public static NamedUnicodeRange Unassigned = new("Unassigned", 0x110000, 200000);

    /* These are special cases for MDL2 and are not included in All */
    public static NamedUnicodeRange MDL2Deprecated = new("Deprecated", 0xE000, 0xE5FF - 0xE000 + 1);
    public static NamedUnicodeRange PrivateUseAreaMDL2 = new("Private Use Area", 58880, 4864);

    /// <summary>
    /// Unicode Ranges sorted by Range
    /// </summary>
    public static IReadOnlyList<NamedUnicodeRange> All { get; } = new List<NamedUnicodeRange>()
    {
         BasicLatin,
         Latin1Supplement,
         LatinExtendedA,
         LatinExtendedB,
         IPAExtensions,
         SpacingModifierLetters,
         CombiningDiacriticalMarks,
         GreekAndCoptic,
         Cyrillic,
         CyrillicSupplement,
         Armenian,
         Hebrew,
         Arabic,
         Syriac,
         ArabicSupplement,
         Thaana,
         NKo,
         Samaritan,
         Mandaic,
         SyriacSupplement,
         ArabicExtendedB,
         ArabicExtendedA,
         Devanagari,
         Bengali,
         Gurmukhi,
         Gujarati,
         Oriya,
         Tamil,
         Telugu,
         Kannada,
         Malayalam,
         Sinhala,
         Thai,
         Lao,
         Tibetan,
         Myanmar,
         Georgian,
         HangulJamo,
         Ethiopic,
         EthiopicSupplement,
         Cherokee,
         UnifiedCanadianAboriginalSyllabics,
         Ogham,
         Runic,
         Tagalog,
         Hanunoo,
         Buhid,
         Tagbanwa,
         Khmer,
         Mongolian,
         UnifiedCanadianAboriginalSyllabicsExtended,
         Limbu,
         TaiLe,
         NewTaiLue,
         KhmerSymbols,
         Buginese,
         TaiTham,
         CombiningDiacriticalMarksExtended,
         Balinese,
         Sundanese,
         Batak,
         Lepcha,
         OlChiki,
         CyrillicExtendedC,
         GeorgianExtended,
         SundaneseSupplement,
         VedicExtensions,
         PhoneticExtensions,
         PhoneticExtensionsSupplement,
         CombiningDiacriticalMarksSupplement,
         LatinExtendedAdditional,
         GreekExtended,
         GeneralPunctuation,
         SuperscriptsAndSubscripts,
         CurrencySymbols,
         CombiningDiacriticalMarksforSymbols,
         LetterlikeSymbols,
         NumberForms,
         Arrows,
         MathematicalOperators,
         MiscellaneousTechnical,
         ControlPictures,
         OpticalCharacterRecognition,
         EnclosedAlphanumerics,
         BoxDrawing,
         BlockElements,
         GeometricShapes,
         MiscellaneousSymbols,
         Dingbats,
         MiscellaneousMathematicalSymbolsA,
         SupplementalArrowsA,
         BraillePatterns,
         SupplementalArrowsB,
         MiscellaneousMathematicalSymbolsB,
         SupplementalMathematicalOperators,
         MiscellaneousSymbolsAndArrows,
         Glagolitic,
         LatinExtendedC,
         Coptic,
         GeorgianSupplement,
         Tifinagh,
         EthiopicExtended,
         CyrillicExtendedA,
         SupplementalPunctuation,
         CJKRadicalsSupplement,
         KangxiRadicals,
         IdeographicDescriptionCharacters,
         CJKSymbolsAndPunctuation,
         Hiragana,
         Katakana,
         Bopomofo,
         HangulCompatibilityJamo,
         Kanbun,
         BopomofoExtended,
         CJKStrokes,
         KatakanaPhoneticExtensions,
         EnclosedCJKLettersAndMonths,
         CJKCompatibility,
         CJKUnifiedIdeographsExtensionA,
         YijingHexagramSymbols,
         CJKUnifiedIdeographs,
         YiSyllables,
         YiRadicals,
         Lisu,
         Vai,
         CyrillicExtendedB,
         Bamum,
         ModifierToneLetters,
         LatinExtendedD,
         SylotiNagri,
         CommonIndicNumberForms,
         Phagspa,
         Saurashtra,
         DevanagariExtended,
         KayahLi,
         Rejang,
         HangulJamoExtendedA,
         Javanese,
         MyanmarExtendedB,
         Cham,
         MyanmarExtendedA,
         TaiViet,
         MeeteiMayekExtensions,
         EthiopicExtendedA,
         LatinExtendedE,
         CherokeeSupplement,
         MeeteiMayek,
         HangulSyllables,
         HangulJamoExtendedB,
         HighSurrogates,
         HighPrivateUseSurrogates,
         LowSurrogates,
         PrivateUseArea,
         CJKCompatibilityIdeographs,
         AlphabeticPresentationForms,
         ArabicPresentationFormsA,
         VariationSelectors,
         VerticalForms,
         CombiningHalfMarks,
         CJKCompatibilityForms,
         SmallFormVariants,
         ArabicPresentationFormsB,
         HalfwidthAndFullwidthForms,
         Specials,
         LinearBSyllabary,
         LinearBIdeograms,
         AegeanNumbers,
         AncientGreekNumbers,
         AncientSymbols,
         PhaistosDisc,
         Lycian,
         Carian,
         CopticEpactNumbers,
         OldItalic,
         Gothic,
         OldPermic,
         Ugaritic,
         OldPersian,
         Deseret,
         Shavian,
         Osmanya,
         Osage,
         Elbasan,
         CaucasianAlbanian,
         Vithkuqi,
         LinearA,
         LatinExtendedF,
         CypriotSyllabary,
         ImperialAramaic,
         Palmyrene,
         Nabataean,
         Hatran,
         Phoenician,
         Lydian,
         MeroiticHieroglyphs,
         MeroiticCursive,
         Kharoshthi,
         OldSouthArabian,
         OldNorthArabian,
         Manichaean,
         Avestan,
         InscriptionalParthian,
         InscriptionalPahlavi,
         PsalterPahlavi,
         OldTurkic,
         OldHungarian,
         HanifiRohingya,
         RumiNumeralSymbols,
         Yezidi,
         ArabicExtendedC,
         OldSogdian,
         Sogdian,
         OldUyghur,
         Chorasmian,
         Elymaic,
         Brahmi,
         Kaithi,
         SoraSompeng,
         Chakma,
         Mahajani,
         Sharada,
         SinhalaArchaicNumbers,
         Khojki,
         Multani,
         Khudawadi,
         Grantha,
         Newa,
         Tirhuta,
         Siddham,
         Modi,
         MongolianSupplement,
         Takri,
         Ahom,
         Dogra,
         WarangCiti,
         DivesAkuru,
         Nandinagari,
         ZanabazarSquare,
         Soyombo,
         UnifiedCanadianAboriginalSyllabicsExtendedA,
         PauCinHau,
         DevanagariExtendedA,
         Bhaiksuki,
         Marchen,
         MasaramGondi,
         GunjalaGondi,
         Makasar,
         Kawi,
         LisuSupplement,
         TamilSupplement,
         Cuneiform,
         CuneiformNumbersAndPunctuation,
         EarlyDynasticCuneiform,
         CyproMinoan,
         EgyptianHieroglyphs,
         EgyptianHieroglyphFormatControls,
         AnatolianHieroglyphs,
         BamumSupplement,
         Mro,
         Tangsa,
         BassaVah,
         PahawhHmong,
         Medefaidrin,
         Miao,
         IdeographicSymbolsAndPunctuation,
         Tangut,
         TangutComponents,
         KhitanSmallScript,
         TangutSupplement,
         KanaExtendedB,
         KanaSupplement,
         KanaExtendedA,
         SmallKanaExtension,
         Nushu,
         Duployan,
         ShorthAndFormatControls,
         ZnamennyMusicalNotation,
         ByzantineMusicalSymbols,
         MusicalSymbols,
         AncientGreekMusicalNotation,
         KaktovikNumerals,
         MayanNumerals,
         TaiXuanJingSymbols,
         CountingRodNumerals,
         MathematicalAlphanumericSymbols,
         SuttonSignWriting,
         LatinExtendedG,
         GlagoliticSupplement,
         CyrillicExtendedD,
         NyiakengPuachueHmong,
         Toto,
         Wancho,
         NagMundari,
         EthiopicExtendedB,
         MendeKikakui,
         Adlam,
         IndicSiyaqNumbers,
         OttomanSiyaqNumbers,
         ArabicMathematicalAlphabeticSymbols,
         MahjongTiles,
         DominoTiles,
         PlayingCards,
         EnclosedAlphanumericSupplement,
         EnclosedIdeographicSupplement,
         MiscellaneousSymbolsAndPictographs,
         Emoticons,
         OrnamentalDingbats,
         TransportAndMapSymbols,
         AlchemicalSymbols,
         GeometricShapesExtended,
         SupplementalArrowsC,
         SupplementalSymbolsAndPictographs,
         ChessSymbols,
         SymbolsAndPictographsExtendedA,
         SymbolsforLegacyComputing,
         CJKUnifiedIdeographsExtensionB,
         CJKUnifiedIdeographsExtensionC,
         CJKUnifiedIdeographsExtensionD,
         CJKUnifiedIdeographsExtensionE,
         CJKUnifiedIdeographsExtensionF,
         CJKUnifiedIdeographsExtensionI,
         CJKCompatibilityIdeographsSupplement,
         CJKUnifiedIdeographsExtensionG,
         CJKUnifiedIdeographsExtensionH,
         Tags,
         VariationSelectorsSupplement,
         SupplementaryPrivateUseAreaA,
         SupplementaryPrivateUseAreaB,
         Unassigned
    };
}
