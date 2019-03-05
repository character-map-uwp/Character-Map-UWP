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
        private string _xamlFontConstructor { get; }

        private FontFamily _xamlFontFamily = null;
        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;
        private IReadOnlyList<TypographyFeatureInfo> _xamlTypographyFeatures = null;

        public FontFamily XamlFontFamily
            => _xamlFontFamily ?? (_xamlFontFamily = new FontFamily(_xamlFontConstructor));

        public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures
        {
            get
            {
                if (_typographyFeatures == null)
                    LoadTypographyFeatures();
                return _typographyFeatures;
            }
        }

        public IReadOnlyList<TypographyFeatureInfo> XamlTypographyFeatures
        {
            get
            {
                if (_xamlTypographyFeatures == null)
                    LoadTypographyFeatures();
                return _xamlTypographyFeatures;
            }
        }

        public CanvasFontFace FontFace { get; private set; }

        public string PreferredName { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

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
                _xamlFontConstructor = $"{FontFinder.GetAppPath(file)}#{familyName}";
            }
            else
            {
                _xamlFontConstructor = familyName;
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

        private void LoadTypographyFeatures()
        {
            _typographyFeatures = TypographyAnalyzer.GetSupportedTypographyFeatures(this);
            _xamlTypographyFeatures = _typographyFeatures.Where(f => TypographyBehavior.IsXamlSupported(f.Feature)).ToList();
        }

        public void Dispose()
        {
            _xamlFontFamily = null;
            FontFace.Dispose();
            FontFace = null;
        }

        public override string ToString()
        {
            return PreferredName;
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
