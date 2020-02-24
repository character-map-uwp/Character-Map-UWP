//#define GENERATE_DATA

using CharacterMap.Core;
using CharacterMap.Provider;
using SQLite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CharacterMap.Services
{
    public interface IGlyphData
    {
        [Column("Ix")]
        int UnicodeIndex { get; }
        [Column("Hx")]
        string UnicodeHex { get; }
        string Description { get; }
    }

    public class GlyphDescription : IGlyphData
    {
        [PrimaryKey, Column("Ix")]
        public int UnicodeIndex { get; set; }

        [Indexed, MaxLength(5), Column("Hx")]
        public string UnicodeHex { get; set; }

        public string Description { get; set; }
    }

    public class MDL2Glyph : GlyphDescription { }
    public class MaterialDesignIconsGlyph : GlyphDescription { }
    public class WebdingsGlyph : GlyphDescription { }
    public class WingdingsGlyph : GlyphDescription { }
    public class Wingdings2Glyph : GlyphDescription { }
    public class Wingdings3Glyph : GlyphDescription { }
    public class FontAwesomeGlyph : GlyphDescription { }
    public class IcoFontGlyph : GlyphDescription { }

    public interface IGlyphDataProvider
    {
        void Initialise();
        string GetCharacterDescription(int unicodeIndex, FontVariant variant);
        Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant);
    }

    public static class GlyphService
    {
        private static IGlyphDataProvider _provider { get; set; }

        private static Task _init { get; set; }

        public static IReadOnlyList<IGlyphData> EMPTY_SEARCH = new List<IGlyphData>();

        static GlyphService()
        {
            _provider = new SQLiteGlyphProvider();
            _provider.Initialise();
        }

        public static Task InitializeAsync()
        {
            return _init ?? (_init = InitializeInternalAsync());
        }

        private static Task InitializeInternalAsync()
        {
#if DEBUG && GENERATE_DATABASE
            if (_provider is SQLiteGlyphProvider p)
            {
                return p.InitialiseDatabaseAsync();
            }
#endif
            return Task.CompletedTask;
        }

        internal static string GetCharacterDescription(int unicodeIndex, FontVariant variant, bool includeKeystroke = false)
        {
            if (variant == null || _provider == null)
                return null;

            string description = _provider.GetCharacterDescription(unicodeIndex, variant);

            if (includeKeystroke)
            {
                // Add Unicode keystroke details
                if (unicodeIndex >= 128 && unicodeIndex <= 255)
                    description += $" - Alt + {unicodeIndex:0000}";
            }

            return description;
        }

        internal static Task<IReadOnlyList<IGlyphData>> SearchAsync(string query, FontVariant variant)
        {
            if (variant == null)
                return Task.FromResult(EMPTY_SEARCH);

            return _provider.SearchAsync(query, variant);
        }
    }
}
