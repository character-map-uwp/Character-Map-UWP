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

    public bool Contains(uint index) => index >= Start && index <= End;
}

public static class UnicodeRanges
{
    public static NamedUnicodeRange BasicLatin = new("Basic Latin", 0, 128);
    public static NamedUnicodeRange Latin1Supplement = new("Latin 1 Supplement", 128, 128);
    public static NamedUnicodeRange LatinExtendedA = new("Latin Extended A", 256, 128);
    public static NamedUnicodeRange LatinExtendedB = new("Latin Extended B", 384, 208);
    public static NamedUnicodeRange IpaExtensions = new("Ipa Extensions", 592, 96);
    public static NamedUnicodeRange SpacingModifierLetters = new("Spacing Modifier Letters", 688, 80);
    public static NamedUnicodeRange CombiningDiacriticalMarks = new("Combining Diacritical Marks", 768, 112);
    public static NamedUnicodeRange GreekandCoptic = new("Greek and Coptic", 880, 144);
    public static NamedUnicodeRange Cyrillic = new("Cyrillic", 1024, 256);
    public static NamedUnicodeRange CyrillicSupplement = new("Cyrillic Supplement", 1280, 48);
    public static NamedUnicodeRange Armenian = new("Armenian", 1328, 96);
    public static NamedUnicodeRange Hebrew = new("Hebrew", 1424, 112);
    public static NamedUnicodeRange Arabic = new("Arabic", 1536, 256);
    public static NamedUnicodeRange Syriac = new("Syriac", 1792, 80);
    public static NamedUnicodeRange ArabicSupplement = new("Arabic Supplement", 1872, 48);
    public static NamedUnicodeRange Thaana = new("Thaana", 1920, 64);
    public static NamedUnicodeRange NKo = new("N Ko", 1984, 64);
    public static NamedUnicodeRange Samaritan = new("Samaritan", 2048, 64);
    public static NamedUnicodeRange Mandaic = new("Mandaic", 2112, 32);
    public static NamedUnicodeRange SyriacSupplement = new("Syriac Supplement", 2144, 16);
    public static NamedUnicodeRange ArabicExtendedB = new("Arabic Extended B", 2160, 48);
    public static NamedUnicodeRange ArabicExtendedA = new("Arabic Extended A", 2208, 96);
    public static NamedUnicodeRange Devanagari = new("Devanagari", 2304, 128);
    public static NamedUnicodeRange Bengali = new("Bengali", 2432, 128);
    public static NamedUnicodeRange Gurmukhi = new("Gurmukhi", 2560, 128);
    public static NamedUnicodeRange Gujarati = new("Gujarati", 2688, 128);
    public static NamedUnicodeRange Oriya = new("Oriya", 2816, 128);
    public static NamedUnicodeRange Tamil = new("Tamil", 2944, 128);
    public static NamedUnicodeRange Telugu = new("Telugu", 3072, 128);
    public static NamedUnicodeRange Kannada = new("Kannada", 3200, 128);
    public static NamedUnicodeRange Malayalam = new("Malayalam", 3328, 128);
    public static NamedUnicodeRange Sinhala = new("Sinhala", 3456, 128);
    public static NamedUnicodeRange Thai = new("Thai", 3584, 128);
    public static NamedUnicodeRange Lao = new("Lao", 3712, 128);
    public static NamedUnicodeRange Tibetan = new("Tibetan", 3840, 256);
    public static NamedUnicodeRange Myanmar = new("Myanmar", 4096, 160);
    public static NamedUnicodeRange Georgian = new("Georgian", 4256, 96);
    public static NamedUnicodeRange HangulJamo = new("Hangul Jamo", 4352, 256);
    public static NamedUnicodeRange Ethiopic = new("Ethiopic", 4608, 384);
    public static NamedUnicodeRange EthiopicSupplement = new("Ethiopic Supplement", 4992, 32);
    public static NamedUnicodeRange Cherokee = new("Cherokee", 5024, 96);
    public static NamedUnicodeRange UnifiedCanadianAboriginalSyllabics = new("Unified Canadian Aboriginal Syllabics", 5120, 640);
    public static NamedUnicodeRange Ogham = new("Ogham", 5760, 32);
    public static NamedUnicodeRange Runic = new("Runic", 5792, 96);
    public static NamedUnicodeRange Tagalog = new("Tagalog", 5888, 32);
    public static NamedUnicodeRange Hanunoo = new("Hanunoo", 5920, 32);
    public static NamedUnicodeRange Buhid = new("Buhid", 5952, 32);
    public static NamedUnicodeRange Tagbanwa = new("Tagbanwa", 5984, 32);
    public static NamedUnicodeRange Khmer = new("Khmer", 6016, 128);
    public static NamedUnicodeRange Mongolian = new("Mongolian", 6144, 176);
    public static NamedUnicodeRange UnifiedCanadianAboriginalSyllabicsExtended = new("Unified Canadian Aboriginal Syllabics Extended", 6320, 80);
    public static NamedUnicodeRange Limbu = new("Limbu", 6400, 80);
    public static NamedUnicodeRange TaiLe = new("Tai Le", 6480, 48);
    public static NamedUnicodeRange NewTaiLue = new("New Tai Lue", 6528, 96);
    public static NamedUnicodeRange KhmerSymbols = new("Khmer Symbols", 6624, 32);
    public static NamedUnicodeRange Buginese = new("Buginese", 6656, 32);
    public static NamedUnicodeRange TaiTham = new("Tai Tham", 6688, 144);
    public static NamedUnicodeRange CombiningDiacriticalMarksExtended = new("Combining Diacritical Marks Extended", 6832, 80);
    public static NamedUnicodeRange Balinese = new("Balinese", 6912, 128);
    public static NamedUnicodeRange Sundanese = new("Sundanese", 7040, 64);
    public static NamedUnicodeRange Batak = new("Batak", 7104, 64);
    public static NamedUnicodeRange Lepcha = new("Lepcha", 7168, 80);
    public static NamedUnicodeRange OlChiki = new("Ol Chiki", 7248, 48);
    public static NamedUnicodeRange CyrillicExtendedC = new("Cyrillic Extended C", 7296, 16);
    public static NamedUnicodeRange GeorgianExtended = new("Georgian Extended", 7312, 48);
    public static NamedUnicodeRange SundaneseSupplement = new("Sundanese Supplement", 7360, 16);
    public static NamedUnicodeRange VedicExtensions = new("Vedic Extensions", 7376, 48);
    public static NamedUnicodeRange PhoneticExtensions = new("Phonetic Extensions", 7424, 128);
    public static NamedUnicodeRange PhoneticExtensionsSupplement = new("Phonetic Extensions Supplement", 7552, 64);
    public static NamedUnicodeRange CombiningDiacriticalMarksSupplement = new("Combining Diacritical Marks Supplement", 7616, 64);
    public static NamedUnicodeRange LatinExtendedAdditional = new("Latin Extended Additional", 7680, 256);
    public static NamedUnicodeRange GreekExtended = new("Greek Extended", 7936, 256);
    public static NamedUnicodeRange GeneralPunctuation = new("General Punctuation", 8192, 112);
    public static NamedUnicodeRange SuperscriptsandSubscripts = new("Superscripts and Subscripts", 8304, 48);
    public static NamedUnicodeRange CurrencySymbols = new("Currency Symbols", 8352, 48);
    public static NamedUnicodeRange CombiningDiacriticalMarksforSymbols = new("Combining Diacritical Marks for Symbols", 8400, 48);
    public static NamedUnicodeRange LetterlikeSymbols = new("Letter-like Symbols", 8448, 80);
    public static NamedUnicodeRange NumberForms = new("Number Forms", 8528, 64);
    public static NamedUnicodeRange Arrows = new("Arrows", 8592, 112);
    public static NamedUnicodeRange MathematicalOperators = new("Mathematical Operators", 8704, 256);
    public static NamedUnicodeRange MiscellaneousTechnical = new("Miscellaneous Technical", 8960, 256);
    public static NamedUnicodeRange ControlPictures = new("Control Pictures", 9216, 64);
    public static NamedUnicodeRange OpticalCharacterRecognition = new("Optical Character Recognition", 9280, 32);
    public static NamedUnicodeRange EnclosedAlphanumerics = new("Enclosed Alphanumerics", 9312, 160);
    public static NamedUnicodeRange BoxDrawing = new("Box Drawing", 9472, 128);
    public static NamedUnicodeRange BlockElements = new("Block Elements", 9600, 32);
    public static NamedUnicodeRange GeometricShapes = new("Geometric Shapes", 9632, 96);
    public static NamedUnicodeRange MiscellaneousSymbols = new("Miscellaneous Symbols", 9728, 256);
    public static NamedUnicodeRange Dingbats = new("Dingbats", 9984, 192);
    public static NamedUnicodeRange MiscellaneousMathematicalSymbolsA = new("Miscellaneous Mathematical Symbols A", 10176, 48);
    public static NamedUnicodeRange SupplementalArrowsA = new("Supplemental Arrows A", 10224, 16);
    public static NamedUnicodeRange BraillePatterns = new("Braille Patterns", 10240, 256);
    public static NamedUnicodeRange SupplementalArrowsB = new("Supplemental Arrows B", 10496, 128);
    public static NamedUnicodeRange MiscellaneousMathematicalSymbolsB = new("Miscellaneous Mathematical Symbols B", 10624, 128);
    public static NamedUnicodeRange SupplementalMathematicalOperators = new("Supplemental Mathematical Operators", 10752, 256);
    public static NamedUnicodeRange MiscellaneousSymbolsandArrows = new("Miscellaneous Symbols and Arrows", 11008, 256);
    public static NamedUnicodeRange Glagolitic = new("Glagolitic", 11264, 96);
    public static NamedUnicodeRange LatinExtendedC = new("Latin Extended C", 11360, 32);
    public static NamedUnicodeRange Coptic = new("Coptic", 11392, 128);
    public static NamedUnicodeRange GeorgianSupplement = new("Georgian Supplement", 11520, 48);
    public static NamedUnicodeRange Tifinagh = new("Tifinagh", 11568, 80);
    public static NamedUnicodeRange EthiopicExtended = new("Ethiopic Extended", 11648, 96);
    public static NamedUnicodeRange CyrillicExtendedA = new("Cyrillic Extended A", 11744, 32);
    public static NamedUnicodeRange SupplementalPunctuation = new("Supplemental Punctuation", 11776, 128);
    public static NamedUnicodeRange CjkRadicalsSupplement = new("Cjk Radicals Supplement", 11904, 128);
    public static NamedUnicodeRange KangxiRadicals = new("Kangxi Radicals", 12032, 224);
    public static NamedUnicodeRange IdeographicDescriptionCharacters = new("Ideographic Description Characters", 12272, 16);
    public static NamedUnicodeRange CjkSymbolsandPunctuation = new("Cjk Symbols and Punctuation", 12288, 64);
    public static NamedUnicodeRange Hiragana = new("Hiragana", 12352, 96);
    public static NamedUnicodeRange Katakana = new("Katakana", 12448, 96);
    public static NamedUnicodeRange Bopomofo = new("Bopomofo", 12544, 48);
    public static NamedUnicodeRange HangulCompatibilityJamo = new("Hangul Compatibility Jamo", 12592, 96);
    public static NamedUnicodeRange Kanbun = new("Kanbun", 12688, 16);
    public static NamedUnicodeRange BopomofoExtended = new("Bopomofo Extended", 12704, 32);
    public static NamedUnicodeRange CjkStrokes = new("CJK Strokes", 12736, 48);
    public static NamedUnicodeRange KatakanaPhoneticExtensions = new("Katakana Phonetic Extensions", 12784, 16);
    public static NamedUnicodeRange EnclosedCjkLettersandMonths = new("Enclosed CJK Letters and Months", 12800, 256);
    public static NamedUnicodeRange CjkCompatibility = new("CJK Compatibility", 13056, 256);
    public static NamedUnicodeRange CjkUnifiedIdeographsExtensionA = new("Cjk Unified Ideographs Extension A", 13312, 6592);
    public static NamedUnicodeRange YijingHexagramSymbols = new("Yijing Hexagram Symbols", 19904, 64);
    public static NamedUnicodeRange CjkUnifiedIdeographs = new("CJK Unified Ideographs", 19968, 20992);
    public static NamedUnicodeRange YiSyllables = new("Yi Syllables", 40960, 1168);
    public static NamedUnicodeRange YiRadicals = new("Yi Radicals", 42128, 64);
    public static NamedUnicodeRange Lisu = new("Lisu", 42192, 48);
    public static NamedUnicodeRange Vai = new("Vai", 42240, 320);
    public static NamedUnicodeRange CyrillicExtendedB = new("Cyrillic Extended B", 42560, 96);
    public static NamedUnicodeRange Bamum = new("Bamum", 42656, 96);
    public static NamedUnicodeRange ModifierToneLetters = new("Modifier Tone Letters", 42752, 32);
    public static NamedUnicodeRange LatinExtendedD = new("Latin Extended D", 42784, 224);
    public static NamedUnicodeRange SylotiNagri = new("Syloti Nagri", 43008, 48);
    public static NamedUnicodeRange CommonIndicNumberForms = new("Common Indic Number Forms", 43056, 16);
    public static NamedUnicodeRange Phagspa = new("Phagspa", 43072, 64);
    public static NamedUnicodeRange Saurashtra = new("Saurashtra", 43136, 96);
    public static NamedUnicodeRange DevanagariExtended = new("Devanagari Extended", 43232, 32);
    public static NamedUnicodeRange KayahLi = new("Kayah Li", 43264, 48);
    public static NamedUnicodeRange Rejang = new("Rejang", 43312, 48);
    public static NamedUnicodeRange HangulJamoExtendedA = new("Hangul Jamo Extended A", 43360, 32);
    public static NamedUnicodeRange Javanese = new("Javanese", 43392, 96);
    public static NamedUnicodeRange MyanmarExtendedB = new("Myanmar Extended B", 43488, 32);
    public static NamedUnicodeRange Cham = new("Cham", 43520, 96);
    public static NamedUnicodeRange MyanmarExtendedA = new("Myanmar Extended A", 43616, 32);
    public static NamedUnicodeRange TaiViet = new("Tai Viet", 43648, 96);
    public static NamedUnicodeRange MeeteiMayekExtensions = new("Meetei Mayek Extensions", 43744, 32);
    public static NamedUnicodeRange EthiopicExtendedA = new("Ethiopic Extended A", 43776, 48);
    public static NamedUnicodeRange LatinExtendedE = new("Latin Extended E", 43824, 64);
    public static NamedUnicodeRange CherokeeSupplement = new("Cherokee Supplement", 43888, 80);
    public static NamedUnicodeRange MeeteiMayek = new("Meetei Mayek", 43968, 64);
    public static NamedUnicodeRange HangulSyllables = new("Hangul Syllables", 44032, 11184);
    public static NamedUnicodeRange HangulJamoExtendedB = new("Hangul Jamo Extended B", 55216, 80);
    public static NamedUnicodeRange PrivateUseArea = new("Private Use Area", 57344, 6400);
    public static NamedUnicodeRange CjkCompatibilityIdeographs = new("CJK Compatibility Ideographs", 63744, 512);
    public static NamedUnicodeRange AlphabeticPresentationForms = new("Alphabetic Presentation Forms", 64256, 80);
    public static NamedUnicodeRange ArabicPresentationFormsA = new("Arabic Presentation Forms A", 64336, 688);
    public static NamedUnicodeRange VariationSelectors = new("Variation Selectors", 65024, 16);
    public static NamedUnicodeRange VerticalForms = new("Vertical Forms", 65040, 16);
    public static NamedUnicodeRange CombiningHalfMarks = new("Combining Half Marks", 65056, 16);
    public static NamedUnicodeRange CjkCompatibilityForms = new("CJK Compatibility Forms", 65072, 32);
    public static NamedUnicodeRange SmallFormVariants = new("Small Form Variants", 65104, 32);
    public static NamedUnicodeRange ArabicPresentationFormsB = new("Arabic Presentation Forms B", 65136, 144);
    public static NamedUnicodeRange HalfwidthandFullwidthForms = new("Half-width and Full-width Forms", 65280, 240);
    public static NamedUnicodeRange Specials = new("Specials", 65520, 16);
    public static NamedUnicodeRange Misc = new("Misc", 65536, 20000);

    /// <summary>
    /// Unicode Ranges sorted by Range
    /// </summary>
    public static IReadOnlyList<NamedUnicodeRange> All { get; } = new List<NamedUnicodeRange>()
    {
         BasicLatin,
         Latin1Supplement,
         LatinExtendedA,
         LatinExtendedB,
         IpaExtensions,
         SpacingModifierLetters,
         CombiningDiacriticalMarks,
         GreekandCoptic,
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
         SuperscriptsandSubscripts,
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
         MiscellaneousSymbolsandArrows,
         Glagolitic,
         LatinExtendedC,
         Coptic,
         GeorgianSupplement,
         Tifinagh,
         EthiopicExtended,
         CyrillicExtendedA,
         SupplementalPunctuation,
         CjkRadicalsSupplement,
         KangxiRadicals,
         IdeographicDescriptionCharacters,
         CjkSymbolsandPunctuation,
         Hiragana,
         Katakana,
         Bopomofo,
         HangulCompatibilityJamo,
         Kanbun,
         BopomofoExtended,
         CjkStrokes,
         KatakanaPhoneticExtensions,
         EnclosedCjkLettersandMonths,
         CjkCompatibility,
         CjkUnifiedIdeographsExtensionA,
         YijingHexagramSymbols,
         CjkUnifiedIdeographs,
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
         PrivateUseArea,
         CjkCompatibilityIdeographs,
         AlphabeticPresentationForms,
         ArabicPresentationFormsA,
         VariationSelectors,
         VerticalForms,
         CombiningHalfMarks,
         CjkCompatibilityForms,
         SmallFormVariants,
         ArabicPresentationFormsB,
         HalfwidthandFullwidthForms,
         Specials,
         Misc
    };
}
