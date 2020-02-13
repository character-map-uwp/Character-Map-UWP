using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Humanizer;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using FontFamily = Windows.UI.Xaml.Media.FontFamily;

namespace CharacterMap.Core
{
    public partial class FontVariant: IDisposable
    {

        private IReadOnlyList<KeyValuePair<string, string>> _fontInformation = null;
        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;
        private IReadOnlyList<TypographyFeatureInfo> _xamlTypographyFeatures = null;

        public IReadOnlyList<KeyValuePair<string, string>> FontInformation
            => _fontInformation ?? (_fontInformation = LoadFontInformation());

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

        public bool HasXamlTypographyFeatures => XamlTypographyFeatures.Count > 0;

        public CanvasFontFace FontFace { get; private set; }

        public string PreferredName { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

        public double CharacterHash { get; private set; }

        public bool IsImported { get; }

        public string FileName { get; }

        public string FamilyName { get; }

        public (uint,uint)[] UnicodeRanges { get; }

        public Panose Panose { get; }

        /// <summary>
        /// File-system path for DWrite / Xaml to construct a font for use in this application
        /// </summary>
        public string Source { get; }

        public string XamlFontSource =>
            (IsImported ? $"/Assets/Fonts/{FileName}#{FamilyName}" : Source);

        public FontVariant(CanvasFontFace face, string familyName, StorageFile file)
        {
            FontFace = face;
            Characters = new List<Character>();
            FamilyName = familyName;

            if (file != null)
            {
                IsImported = true;
                FileName = file.Name;
                Source = $"{FontFinder.GetAppPath(file)}#{familyName}";
            }
            else
            {
                Source = familyName;
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

            UnicodeRanges = face.UnicodeRanges.Select(r => (r.First, r.Last)).ToArray();
            PreferredName = name;
            Panose = PanoseParser.Parse(face);
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
            var xaml = _typographyFeatures.Where(f => TypographyBehavior.IsXamlSupported(f.Feature)).ToList();
            if (xaml.Count > 0)
                xaml.Insert(0, new TypographyFeatureInfo(CanvasTypographyFeatureName.None));

            _xamlTypographyFeatures = xaml;
        }

        private List<KeyValuePair<string, string>> LoadFontInformation()
        {
            KeyValuePair<string, string> Get(CanvasFontInformation info)
            {
                var infos = FontFace.GetInformationalStrings(info);
                if (infos.Count == 0)
                    return new KeyValuePair<string, string>();

                var name = info.Humanize().Transform(To.TitleCase);
                var dic = infos.ToDictionary(k => k.Key, k => k.Value);
                if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
                    || infos.TryGetValue("en-us", out value))
                    return KeyValuePair.Create(name, value);
                else
                    return KeyValuePair.Create(name, infos.First().Value);
            }

            return INFORMATIONS.Select(Get).Where(s => s.Key != null).ToList();
        }

        public void Dispose()
        {
            FontFace.Dispose();
            FontFace = null;
        }

        public override string ToString()
        {
            return PreferredName;
        }
    }


    public partial class FontVariant
    {
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

        private static CanvasFontInformation[] INFORMATIONS { get; } = new[]
        {
            CanvasFontInformation.FullName,
            CanvasFontInformation.Description,
            CanvasFontInformation.Designer,
            CanvasFontInformation.DesignerUrl,
            CanvasFontInformation.VersionStrings,
            CanvasFontInformation.FontVendorUrl,
            CanvasFontInformation.Manufacturer,
            CanvasFontInformation.Trademark,
            CanvasFontInformation.CopyrightNotice,
            CanvasFontInformation.LicenseInfoUrl,
            CanvasFontInformation.LicenseDescription,
        };
    }
}
