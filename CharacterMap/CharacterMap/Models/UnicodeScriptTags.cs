using System.Globalization;
using System.Text.RegularExpressions;

namespace CharacterMap.Models;

internal class UnicodeScriptTags
{
    /* 
     * Values are not translatable yet.
     * TODO: Add all values from https://en.wikipedia.org/wiki/ISO_15924
     */

    public static IReadOnlyDictionary<string, string> Scripts { get; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "adlm", "Adlam" },
        { "ahom", "Ahom" },
        { "hluw", "Anatolian Hieroglyphs" },
        { "arab", "Arabic" },
        { "aran", "Arabic (Nastaliq variant)" },
        { "armn", "Armenian" },
        { "avst", "Avestan" },
        { "bali", "Balinese" },
        { "bamu", "Bamum" },
        { "bass", "Bassa Vah" },
        { "batk", "Batak" },
        { "beng", "Bangla" },
        { "bng2", "Bangla v.2" },
        { "bhks", "Bhaiksuki" },
        { "bopo", "Bopomofo" },
        { "brah", "Brahmi" },
        { "brai", "Braille" },
        { "bugi", "Buginese" },
        { "buhd", "Buhid" },
        { "byzm", "Byzantine Music" },
        { "cans", "Canadian Syllabics" },
        { "cari", "Carian" },
        { "aghb", "Caucasian Albanian" },
        { "cakm", "Chakma" },
        { "cham", "Cham" },
        { "cher", "Cherokee" },
        { "chrs", "Chorasmian" },
        { "hani", "CJK Ideographic (Han)" },
        { "copt", "Coptic" },
        { "cprt", "Cypriot Syllabary" },
        { "cpmn", "Cypro-Minoan" },
        { "cyrl", "Cyrillic" },
        //{ "bg-cyrl", "Cyrillic (Bulgarian)" },
        //{ "mk-cyrl", "Cyrillic (Macedonian)" },
        //{ "sr-cyrl", "Cyrillic (Serbian)" },
        { "dflt", "Default" },
        { "dsrt", "Deseret" },
        { "deva", "Devanagari" },
        { "dev2", "Devanagari v.2" },
        { "diak", "Dives Akuru" },
        { "dogr", "Dogra" },
        { "dupl", "Duployan" },
        { "egyp", "Egyptian Hieroglyphs" },
        { "elba", "Elbasan" },
        { "elym", "Elymaic" },
        { "ethi", "Ethiopic" },
        { "geor", "Georgian" },
        { "geok", "Georgian Khutsuri" },
        { "glag", "Glagolitic" },
        { "goth", "Gothic" },
        { "gran", "Grantha" },
        { "grek", "Greek" },
        { "gujr", "Gujarati" },
        { "gjr2", "Gujarati v.2" },
        { "gong", "Gunjala Gondi" },
        { "guru", "Gurmukhi" },
        { "gur2", "Gurmukhi v.2" },
        { "hang", "Hangul" },
        { "hanb", "Han with Bopomofo" },
        { "hans", "Han (Simplified)" },
        { "hant", "Han (Traditional)" },
        { "hano", "Hanunoo" },
        { "hatr", "Hatran" },
        { "hebr", "Hebrew" },
        { "hira", "Hiragana" },
        { "hrkt", "Hiragana/Katakana" },
        { "kana", "Katakana" },
        { "armi", "Imperial Aramaic" },
        { "rohg", "Hanifi Rohingya" },
        { "phli", "Inscriptional Pahlavi" },
        { "prti", "Inscriptional Parthian" },
        { "jamo", "Hangul Jamo" },
        { "java", "Javanese" },
        { "jpan", "Japanese" },
        { "kthi", "Kaithi" },
        { "knda", "Kannada" },
        { "knd2", "Kannada v.2" },
        { "kali", "Kayah Li" },
        { "khar", "Kharosthi" },
        { "kits", "Khitan Small Script" },
        { "kore", "Korean" },
        { "khmr", "Khmer" },
        { "khoj", "Khojki" },
        { "sind", "Khudawadi" },
        //{ "lao ", "Lao" },
        { "laoo", "Lao" },
        { "latn", "Latin" },
        //{ "vi-latn", "Latin (Vietnamese)" },
        { "lepc", "Lepcha" },
        { "limb", "Limbu" },
        { "lina", "Linear A" },
        { "linb", "Linear B" },
        { "lisu", "Lisu (Fraser)" },
        { "lyci", "Lycian" },
        { "lydi", "Lydian" },
        { "mahj", "Mahajani" },
        { "maka", "Makasar" },
        { "mlym", "Malayalam" },
        { "mlm2", "Malayalam v.2" },
        { "mand", "Mandaic, Mandaean" },
        { "mani", "Manichaean" },
        { "marc", "Marchen" },
        { "gonm", "Masaram Gondi" },
        { "math", "Mathematical Alphanumeric Symbols" },
        { "medf", "Medefaidrin" }, // (Oberi Okaime, Oberi Ɔkaimɛ)" },
        { "mtei", "Meitei Mayek (Meithei, Meetei)" },
        { "mend", "Mende Kikakui" },
        { "merc", "Meroitic Cursive" },
        { "mero", "Meroitic Hieroglyphs" },
        { "plrd", "Miao" },
        { "modi", "Modi" },
        { "mong", "Mongolian" },
        { "mroo", "Mro" },
        { "mult", "Multani" },
        { "musc", "Musical Symbols" },
        { "mymr", "Myanmar" },
        { "mym2", "Myanmar v.2" },
        { "nbat", "Nabataean" },
        { "nand", "Nandinagari" },
        { "newa", "Newa" },
        { "talu", "New Tai Lue" },
        //{ "nko ", "N'Ko" },
        { "nkoo", "N'Ko" },
        { "nshu", "Nüshu" },
        { "hmnp", "Nyiakeng Puachue Hmong" },
        { "orya", "Odia" },
        { "ory2", "Odia v.2" },
        { "ogam", "Ogham" },
        { "olck", "Ol Chiki" },
        { "ital", "Old Italic" },
        { "hung", "Old Hungarian" },
        { "narb", "Old North Arabian" },
        { "perm", "Old Permic" },
        { "xpeo", "Old Persian Cuneiform" },
        { "sogo", "Old Sogdian" },
        { "sarb", "Old South Arabian" },
        { "orkh", "Old Turkic, Orkhon Runic" },
        { "ougr", "Old Uyghur" },
        { "osge", "Osage" },
        { "osma", "Osmanya" },
        { "hmng", "Pahawh Hmong" },
        { "palm", "Palmyrene" },
        { "pauc", "Pau Cin Hau" },
        { "phag", "Phags-pa" },
        { "phnx", "Phoenician" },
        { "phlp", "Psalter Pahlavi" },
        { "rjng", "Rejang" },
        { "runr", "Runic" },
        { "samr", "Samaritan" },
        { "saur", "Saurashtra" },
        { "shrd", "Sharada" },
        { "shaw", "Shavian" },
        { "sidd", "Siddham" },
        { "sgnw", "Sign Writing" },
        { "sinh", "Sinhala" },
        { "sogd", "Sogdian" },
        { "sora", "Sora Sompeng" },
        { "soyo", "Soyombo" },
        { "xsux", "Sumero-Akkadian Cuneiform" },
        { "sund", "Sundanese" },
        { "sylo", "Syloti Nagri" },
        { "syrc", "Syriac" },
        { "tglg", "Tagalog" },
        { "tagb", "Tagbanwa" },
        { "tale", "Tai Le" },
        { "lana", "Tai Tham (Lanna)" },
        { "tavt", "Tai Viet" },
        { "takr", "Takri" },
        { "taml", "Tamil" },
        { "tml2", "Tamil v.2" },
        { "tnsa", "Tangsa" },
        { "tang", "Tangut" },
        { "telu", "Telugu" },
        { "tel2", "Telugu v.2" },
        { "thaa", "Thaana" },
        { "thai", "Thai" },
        { "tibt", "Tibetan" },
        { "tfng", "Tifinagh" },
        { "tirh", "Tirhuta" },
        { "toto", "Toto" },
        { "ugar", "Ugaritic Cuneiform" },
        //{ "vai ", "Vai" },
        { "vaii", "Vai" },
        { "vith", "Vithkuqi" },
        { "wcho", "Wancho" },
        { "wara", "Warang Citi" },
        { "yezi", "Yezidi" },
        //{ "yi  ", "Yi" },
        { "yiii", "Yi" },
        { "zanb", "Zanabazar Square" },
        { "zmth", "Mathematical notation" },
        { "zsye", "Emoji Style" },
        { "zsym", "Text Style emoji" }
    };

    private static Dictionary<String, string> _corrections { get; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "zann", "zanb" },      // zan -> zann -> zanb
    };

    public static string GetName(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            return tag;

        string padded = PadTag(tag);
        if (Scripts.TryGetValue(padded, out string name))
            return name;

        // Attempt to handle language/script variants
        // e.g. vi-latn would parse as 'latn' and return as 'Latin (Vietnamese)'
        // e.g. sr-cyrl would parse as 'cyrl' and return as 'Cyrillic (Serbian)'
        // e.g. Hant-HK would parse as 'hant' and return as 'Han (Traditional) (HK)'
        if (IsLangTag(padded))
        {
            var parts = padded.Split('-');
            var script = parts[0].Length == 4 ? parts[0] : parts[1];

            try
            {
                CultureInfo clt = new(padded);
                if (clt.EnglishName.StartsWith("Unknown") is false)
                    return clt.DisplayName;
            }
            catch { }

            if (Scripts.TryGetValue(script, out name))
            {
                // Try convert the ISO 639-1 language tag to full name
                var lng = parts[0].Length == 4 ? parts[1] : parts[0];
                try
                {
                    CultureInfo clt = new(lng);
                    if (clt.EnglishName.StartsWith("Unknown") is false)
                        lng = clt.DisplayName;
                }
                catch { }

                return $"{name} ({lng})";
            }
        }

        return tag;
    }

    /// <summary>
    /// Attempts to return the base script tag without language specific identifiers
    /// </summary>
    public static string GetBaseTag(string tag)
    {
        // Attempt to handle undocumented cases of language variants
        // e.g. au-latn would parse and return as 'latn'
        // e.g. Hant-HK would parse and return as 'Hant'
        tag = PadTag(tag);
        if (IsLangTag(tag))
        {
            var parts = tag.Split('-');
            return parts[0].Length == 4 ? parts[0] : parts[1];
        }

        return PadTag(tag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string PadTag(string tag)
    {
        // Tags should be 4 characters long - tags that are
        // shorter should be padded with extra spaces
        if (tag is not null && tag.Length > 0 && tag.Length < 4)
        {
            // 1. Pad
            while (tag.Length < 4)
                tag = tag + tag[tag.Length - 1];

            // 2. Use any corrections to replace a padded version
            //    with a specific OpenType tag
            if (_corrections.TryGetValue(tag, out var cor))
                tag = cor;
        }

        return tag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLangTag(string scriptTag)
        => scriptTag.Length == 7 && (scriptTag[2] is '-' || scriptTag[4] == '-');


    #region DEBUG

    // Creates a code list that could be used to convert lang tags
    // from Apple fonts into names of countries. 
    static void Generate()
    {
        Regex regex = new Regex(@"\b[a-zA-Z]{4}\b");
        var countryCodesMapping = new Dictionary<string, string>();
        CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

        foreach (var culture in cultures)
        {
            try
            {
                var region = new RegionInfo(culture.LCID);
                countryCodesMapping[region.TwoLetterISORegionName] = region.EnglishName;
            }
            catch (Exception)
            {

            }
        }

        System.Diagnostics.Debug.WriteLine(CultureInfo.CurrentCulture.EnglishName);
        System.Diagnostics.Debug.WriteLine("var countryCodesMapping = new Dictionary<string, string>() {");
        foreach (var mapping in countryCodesMapping.OrderBy(mapping => mapping.Key))
        {
            System.Diagnostics.Debug.WriteLine("   {{ \"{0}\", \"{1}\" }}", mapping.Key, mapping.Value);
        }

        System.Diagnostics.Debug.WriteLine("};");
    }



    #endregion
}
