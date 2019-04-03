using CharacterMap.Core;
using CharacterMap.Provider;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Services
{
    public class GlyphDescription
    {
        [PrimaryKey]
        public int UnicodeIndex { get; set; }

        [Indexed]
        [MaxLength(5)]
        public string UnicodePoint { get; set; }

        public string Description { get; set; }
    }

    public interface IGlyphDataProvider
    {
        Task InitialiseAsync();
        string GetCharacterDescription(int unicodeIndex);
    }

    public static partial class GlyphService
    {
        //private static IGlyphDataProvider _mdl2Provider { get; set; }
        //private static IGlyphDataProvider _unicodeProvider { get; set; }
        private static IGlyphDataProvider _provider { get; set; }

        private static Task _init { get; set; }

        public static Task InitializeAsync()
        {
            if (_init == null)
                _init = InitializeInternalAsync();

            return _init;
        }

        private static Task InitializeInternalAsync()
        {
            //_mdl2Provider = new SQLiteMDL2Provider();
            //_unicodeProvider = new DictUnicodeProvider();
            _provider = new SQLiteGlyphProvider();

            return Task.WhenAll(
                _provider.InitialiseAsync()
                //_mdl2Provider.InitialiseAsync(),
                //_unicodeProvider.InitialiseAsync()
                );
        }

        public static void Search(string query, FontVariant selectedVariant)
        {
            //var matches = _searchConnection.Query(_searchMapping, 
            //    $"select * from {SEARCH_TABLE} where {nameof(GlyphDescription.Description)} LIKE %?%", query);
        }

        internal static string GetCharacterDescription(int unicodeIndex, FontVariant selectedVariant)
        {
            if (selectedVariant == null)
                return null;

            if (!selectedVariant.FontFace.IsSymbolFont)
                return _provider.GetCharacterDescription(unicodeIndex);
            else
            {
                return null;
            }

            //if (selectedVariant.FamilyName.Contains("MDL2 Assets"))
            //    return _mdl2Provider.GetCharacterDescription(unicodeIndex);

            //return _unicodeProvider.GetCharacterDescription(unicodeIndex);
        }
    }
}
