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
        string GetCharacterDescription(int unicodeIndex, FontVariant variant);
    }

    public static partial class GlyphService
    {
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
            _provider = new SQLiteGlyphProvider();

            return Task.WhenAll(
                _provider.InitialiseAsync()
                );
        }

        internal static string GetCharacterDescription(int unicodeIndex, FontVariant variant)
        {
            if (variant == null)
                return null;

            return _provider.GetCharacterDescription(unicodeIndex, variant);
        }
    }
}
