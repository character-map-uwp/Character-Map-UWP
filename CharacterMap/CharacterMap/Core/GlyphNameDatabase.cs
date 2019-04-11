using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CharacterMap.Core
{
    public static class GlyphNameDatabase
    {
        public static Dictionary<string, string> GlyphDictionary { get; set; }

        public static async void LoadDatabase()
        {
            GlyphDictionary = new Dictionary<string, string>();

            string dbLanguage = CultureInfo.CurrentUICulture.ToString();
            string fullpath = $"ms-appx:///Assets/glyphnames.txt";

            StorageFile dbFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(fullpath));

            string dbContent = await FileIO.ReadTextAsync(dbFile);
            string[] dbLines = dbContent.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in dbLines)
            {
                if (line.StartsWith("#") || line.StartsWith(" "))
                    continue;

                string[] items = line.Split(';', StringSplitOptions.RemoveEmptyEntries);

                if (items.Length > 1)
                    if (!GlyphDictionary.ContainsKey($"U+{items[0]}"))
                        GlyphDictionary.Add($"U+{items[0]}", items[1]);              
            }
        }
    }


}
