using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using System.Collections.Generic;

namespace CharacterMap.Services
{
    public class AddToCollectionResult
    {
        public AddToCollectionResult(bool success, IList<InstalledFont> fonts, UserFontCollection collection)
        {
            Success = success;
            Fonts = fonts;
            if (fonts is not null && fonts.Count == 1)
                Font = fonts[0];
            Collection = collection;
        }

        public InstalledFont Font { get; }
        public IList<InstalledFont> Fonts { get; }
        public bool Success { get; }
        public UserFontCollection Collection { get; }

        public string GetTitle()
        {
            if (Fonts.Count == 1 && Fonts[0] is InstalledFont font)
                return font.Name;
            else
                return $"{Fonts.Count} fonts";
        }

        public string GetMessage()
        {
            if (Font is null && Fonts is not null)
                return $"{Fonts.Count} fonts have been added to {Collection.Name}";
            else if (Collection.IsSystemSymbolCollection)
                return Localization.Get("NotificationAddedToCollection", Font.Name, Localization.Get("OptionSymbolFonts/Text"));
            else
                return Localization.Get("NotificationAddedToCollection", Font.Name, Collection.Name);
        }
    }
}
