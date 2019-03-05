using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace CharacterMap.Core
{
    public class FontVariant: IDisposable
    {
        private string _fontConstructor { get; }

        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;

        private FontFamily _xamlFontFamily { get; set; }

        public FontFamily XamlFontFamily
            => _xamlFontFamily ?? (_xamlFontFamily = new FontFamily(_fontConstructor));

        public CanvasFontFace FontFace { get; private set; }

        public string PreferredName { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

        public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures
            => _typographyFeatures ?? (_typographyFeatures = TypographyAnalyzer.GetSupportedTypographyFeatures(this));

        public double CharacterHash { get; private set; }

        public bool IsImported { get; }

        public string FileName { get; }

        public string FamilyName { get; }

        public FontVariant(CanvasFontFace face, string familyName, StorageFile file)
        {
            FontFace = face;
            Characters = new List<Character>();
            FamilyName = familyName;

            if (file != null)
            {
                IsImported = true;
                FileName = file.Name;
                _fontConstructor = $"{FontFinder.GetAppPath(file)}#{familyName}";
            }
            else
            {
                _fontConstructor = familyName;
            }

            if (!face.FaceNames.TryGetValue(CultureInfo.CurrentCulture.Name, out string name))
            {
                if (!face.FaceNames.TryGetValue("en-us", out name))
                {
                    if (face.FaceNames.Any())
                        name = face.FaceNames.FirstOrDefault().Value;
                    else
                        name = Utils.GetVariantDescription(face);
                }
            }

            PreferredName = name;
        }

        public override string ToString()
        {
            return PreferredName;
        }

        public IReadOnlyList<Character> GetCharacters()
        {
            if (Characters.Count == 0)
            {
                var characters = new List<Character>();

                foreach (var range in FontFace.UnicodeRanges)
                {
                    CharacterHash += range.First;
                    CharacterHash += range.Last;

                    for (uint i = range.First; i <= range.Last; i++)
                    {
                        characters.Add(new Character
                        {
                            Char = char.ConvertFromUtf32((int)i),
                            UnicodeIndex = (int)i
                        });
                    }
                }
                Characters = characters;
                return characters;
            }

            return Characters;
        }

        public void Dispose()
        {
            _xamlFontFamily = null;
            FontFace.Dispose();
            FontFace = null;
        }

        public static FontVariant CreateDefault(CanvasFontFace face)
        {
            return new FontVariant(face, "Segoe UI", null)
            {
                PreferredName = "",
                Characters = new List<Character>
                {
                    new Character
                    {
                        Char = "",
                        UnicodeIndex = 0
                    }
                }
            };
        }

    }
}
