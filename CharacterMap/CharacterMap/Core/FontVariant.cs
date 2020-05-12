using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CharacterMap.Helpers;
using CharacterMapCX;
using Humanizer;
using Microsoft.Graphics.Canvas.Text;
using Windows.Storage;
using CharacterMap.Models;
using System.Text;

namespace CharacterMap.Core
{
    [System.Diagnostics.DebuggerDisplay("{FamilyName} {PreferredName}")]
    public partial class FontVariant : IDisposable
    {
        /* Using a character cache avoids a lot of unnecessary allocations */
        private static Dictionary<int, Character> _characters { get; } = new Dictionary<int, Character>();

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

        public CanvasUnicodeRange[] UnicodeRanges => FontFace.UnicodeRanges;

        public Panose Panose { get; }

        public DWriteProperties DirectWriteProperties { get; }

        /// <summary>
        /// File-system path for DWrite / XAML to construct a font for use in this application
        /// </summary>
        public string Source { get; }

        public string XamlFontSource =>
            (IsImported ? $"/Assets/Fonts/{FileName}#{FamilyName}" : Source);

        public FontVariant(CanvasFontFace face, StorageFile file, DWriteProperties dwProps)
        {
            FontFace = face;
            FamilyName = dwProps.FamilyName;

            if (file != null)
            {
                IsImported = true;
                FileName = file.Name;
                Source = $"{FontFinder.GetAppPath(file)}#{dwProps.FamilyName}";
            }
            else
            {
                Source = dwProps.FamilyName;
            }

            string name = dwProps.FaceName;
            if (String.IsNullOrEmpty(name))
                name = Utils.GetVariantDescription(face);

            DirectWriteProperties = dwProps;
            PreferredName = name;
            Panose = PanoseParser.Parse(face);
        }

        public string GetProviderName()
        {
            //if (!String.IsNullOrEmpty(DirectWriteProperties.RemoteProviderName))
            //    return DirectWriteProperties.RemoteProviderName;

            if (IsImported)
                return Localization.Get("InstallTypeImported");

            return Localization.Get($"DWriteSource{DirectWriteProperties.Source.ToString()}");
        }

        public IReadOnlyList<Character> GetCharacters()
        {
            if (Characters == null)
            {
                var characters = new List<Character>();
                foreach (var range in FontFace.UnicodeRanges)
                {
                    CharacterHash += range.First;
                    CharacterHash += range.Last;

                    int last = (int)range.Last;
                    for (int i = (int)range.First; i <= last; i++)
                    {
                        if (!_characters.TryGetValue(i, out Character c))
                        {
                            c = new Character((uint)i);
                            _characters[i] = c;
                        }

                        characters.Add(c);
                    }
                }
                Characters = characters;
            }

            return Characters;
        }

        private void LoadTypographyFeatures()
        {
            var features = TypographyAnalyzer.GetSupportedTypographyFeatures(this);

            var xaml = features.Where(f => TypographyBehavior.IsXamlSupported(f.Feature)).ToList();
            if (xaml.Count > 0)
                xaml.Insert(0, TypographyFeatureInfo.None);
            _xamlTypographyFeatures = xaml;

            if (features.Count > 0)
                features.Insert(0, TypographyFeatureInfo.None);
            _typographyFeatures = features;
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
                return KeyValuePair.Create(name, infos.First().Value);
            }

            return INFORMATIONS.Select(i => GetInfoKey(FontFace, i)).Where(s => s.Key != null).ToList();
        }

        public string TryGetSampleText()
        {
            return GetInfoKey(FontFace, CanvasFontInformation.SampleText).Value;
        }

        private static KeyValuePair<string, string> GetInfoKey(CanvasFontFace fontFace, CanvasFontInformation info)
        {
            var infos = fontFace.GetInformationalStrings(info);
            if (infos.Count == 0)
                return new KeyValuePair<string, string>();

            var name = info.Humanize().Transform(To.TitleCase);
            var dic = infos.ToDictionary(k => k.Key, k => k.Value);
            if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
                || infos.TryGetValue("en-us", out value))
                return KeyValuePair.Create(name, value);
            return KeyValuePair.Create(name, infos.First().Value);
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
            return new FontVariant(face, null, DWriteProperties.CreateDefault())
            {
                PreferredName = "",
                Characters = new List<Character>
                {
                    new Character(0)
                }
            };
        }

        private static CanvasFontInformation[] INFORMATIONS { get; } = {
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
