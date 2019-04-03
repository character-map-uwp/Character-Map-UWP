using CharacterMap.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace CharacterMap.Provider
{
    public class DictUnicodeProvider : IGlyphDataProvider
    {
        private Task InitTask { get; }

        private Dictionary<int, GlyphDescription> _data { get; } = new Dictionary<int, GlyphDescription>();

        public DictUnicodeProvider()
        {
            InitTask = InitialiseInternalAsync();
        }

        public Task InitialiseAsync() => InitTask;

        private Task InitialiseInternalAsync()
        {
            return Task.Run(async () =>
            {
                var file = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Data/UnicodeData.txt")).AsTask().ConfigureAwait(false);

                using (var stream = await file.OpenStreamForReadAsync().ConfigureAwait(false))
                using (var reader = new StreamReader(stream))
                {
                    string[] parts;
                    while (!reader.EndOfStream)
                    {
                        parts = reader.ReadLine().Split(";", 3, StringSplitOptions.None);

                        string hex = parts[0];
                        int code = Int32.Parse(hex, System.Globalization.NumberStyles.HexNumber);
                        _data.Add(code, new GlyphDescription
                        {
                            Description = parts[1],
                            UnicodeIndex = code,
                            UnicodePoint = hex
                        });
                    }
                }
            });
        }

        public string GetCharacterDescription(int unicodeIndex)
        {
            if (_data.TryGetValue(unicodeIndex, out GlyphDescription desc))
                return desc.Description;

            return null;
        }
    }
}
