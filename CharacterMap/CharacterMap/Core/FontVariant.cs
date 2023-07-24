using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMapCX;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Windows.Storage;

namespace CharacterMap.Core
{
    [System.Diagnostics.DebuggerDisplay("{FamilyName} {PreferredName}")]
    public partial class FontVariant : IDisposable
    {
        /* Using a character cache avoids a lot of unnecessary allocations */
        private static Dictionary<int, Character> _characters { get; } = new ();

        private IReadOnlyList<NamedUnicodeRange> _ranges = null;
        private IReadOnlyList<KeyValuePair<string, string>> _fontInformation = null;
        private IReadOnlyList<TypographyFeatureInfo> _typographyFeatures = null;
        private IReadOnlyList<TypographyFeatureInfo> _xamlTypographyFeatures = null;
        private FontAnalysis _analysis = null;

        public IReadOnlyList<KeyValuePair<string, string>> FontInformation
            => _fontInformation ??= LoadFontInformation();

        public IReadOnlyList<TypographyFeatureInfo> TypographyFeatures
        {
            get
            {
                if (_typographyFeatures == null)
                    LoadTypographyFeatures();
                return _typographyFeatures;
            }
        }

        /// <summary>
        /// Supported XAML typographer features for A SINGLE GLYPH. 
        /// Does not include features like Alternates which are used for strings of text.
        /// </summary>
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

        public CanvasFontFace FontFace => Face.FontFace;

        public string PreferredName { get; private set; }

        public IReadOnlyList<Character> Characters { get; private set; }

        public double CharacterHash { get; private set; }

        public bool IsImported { get; }

        public string FileName { get; }

        public string FamilyName { get; }

        public CanvasUnicodeRange[] UnicodeRanges => Face.GetUnicodeRanges();

        private Panose _panose = null;
        public Panose Panose => _panose ??= PanoseParser.Parse(Face.Properties);

        public DWriteProperties DirectWriteProperties { get; }

        /// <summary>
        /// File-system path for DWrite / XAML to construct a font for use in this application
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// A FontFamily source for XAML that includes a custom fallback font.
        /// This results in XAML *only* rendering the characters included in the font.
        /// Use when you may have a scenario where characters not inside a font's glyph
        /// range might be displayed, otherwise use <see cref="Source"/> for better performance.
        /// </summary>
        public string DisplaySource => $"{Source}, /Assets/AdobeBlank.otf#Adobe Blank";

        /// <summary>
        /// Font source that external applications should use to display this font in XAML
        /// </summary>
        public string XamlFontSource =>
            (IsImported ? $"/Assets/Fonts/{FileName}#{FamilyName}" : Source);

        public DWriteFontFace Face { get; }

        public FontVariant(DWriteFontFace face, StorageFile file)
        {
            DWriteProperties dwProps = face.Properties;
            Face = face;
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
        }

        public string GetProviderName()
        {
            //if (!String.IsNullOrEmpty(DirectWriteProperties.RemoteProviderName))
            //    return DirectWriteProperties.RemoteProviderName;

            if (IsImported)
                return Localization.Get("InstallTypeImported");

            return Localization.Get($"DWriteSource{DirectWriteProperties.Source}");
        }

        public IReadOnlyList<NamedUnicodeRange> GetRanges()
        {
            return _ranges ??=
                GetCharacters().GroupBy(c => c.Range).Select(g => g.Key).ToList();
        }

        public IReadOnlyList<Character> GetCharacters()
        {
            if (Characters == null)
            {
                List<Character> characters = new ();
                foreach (var range in UnicodeRanges)
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

        public int GetGlyphIndex(Character c)
        {
            return Face.GetGlyphIndice(c.UnicodeIndex);
        }

        public uint[] GetGlyphUnicodeIndexes()
        {
            return GetCharacters().Select(c => c.UnicodeIndex).ToArray();
        }

        public FontAnalysis GetAnalysis()
        {
            return _analysis ??= TypographyAnalyzer.Analyze(this);
        }

        /// <summary>
        /// Load an analysis without a glyph search map. Callers later using the cached analysis and expecting a search map should
        /// take care to ensure it's created by manually calling <see cref="TypographyAnalyzer.PrepareSearchMap(FontVariant, FontAnalysis)"/>
        /// </summary>
        /// <returns></returns>
        private FontAnalysis GetAnalysisInternal()
        {
            return _analysis ??= TypographyAnalyzer.Analyze(this, false);
        }

        /// <summary>
        /// Used temporarily to allow insider builds to access COLRv1. Do not use elsewhere. Very expensive.
        /// </summary>
        public bool SupportsCOLRv1Rendering => Utils.Supports23H2 && DirectWriteProperties.IsColorFont && GetAnalysisInternal().SupportsCOLRv1;

        /// <summary>
        /// Hack used for QuickCompare - we show ALL colour fonts using manual DirectWrite rendering (using DirectText control) rather than 
        /// XAML TextBlock. We cannot use the flag above to filter only COLRv1 fonts as the FontAnalysis object requires actually opening and 
        /// manually parsing the font file headers - too expensive an operation to perform when scrolling the entire font list on the UI thread.
        /// /// </summary>
        public bool SupportsColourRendering => Utils.Supports23H2 && DirectWriteProperties.IsColorFont;

        public string TryGetSampleText()
        {
            return GetInfoKey(Face, CanvasFontInformation.SampleText).Value;
        }

        private void LoadTypographyFeatures()
        {
            var features = TypographyAnalyzer.GetSupportedTypographyFeatures(this);

            var xaml = features.Where(f => TypographyBehavior.IsXamlSingleGlyphSupported(f.Feature)).ToList();
            if (xaml.Count > 0)
                xaml.Insert(0, TypographyFeatureInfo.None);
            _xamlTypographyFeatures = xaml;

            if (features.Count > 0)
                features.Insert(0, TypographyFeatureInfo.None);
            _typographyFeatures = features;
        }

        private List<KeyValuePair<string, string>> LoadFontInformation()
        {
            //KeyValuePair<string, string> Get(CanvasFontInformation info)
            //{
            //    var infos = FontFace.GetInformationalStrings(info);
            //    if (infos.Count == 0)
            //        return new KeyValuePair<string, string>();

            //    var name = info.Humanise();
            //    var dic = infos.ToDictionary(k => k.Key, k => k.Value);
            //    if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
            //        || infos.TryGetValue("en-us", out value))
            //        return KeyValuePair.Create(name, value);
            //    return KeyValuePair.Create(name, infos.First().Value);
            //}

            return INFORMATIONS.Select(i => GetInfoKey(Face, i)).Where(s => s.Key != null).ToList();
        }

        private static KeyValuePair<string, string> GetInfoKey(DWriteFontFace fontFace, CanvasFontInformation info)
        {
            var infos = fontFace.GetInformationalStrings(info);
            if (infos.Count == 0)
                return new();

            var name = info.Humanise();
            var dic = infos.ToDictionary(k => k.Key, k => k.Value);
            if (infos.TryGetValue(CultureInfo.CurrentCulture.Name, out string value)
                || infos.TryGetValue("en-us", out value))
                return KeyValuePair.Create(name, value);
            return KeyValuePair.Create(name, infos.First().Value);
        }




        /* SEARCHING */

        public Dictionary<Character, string> SearchMap { get; set; }

        public string GetDescription(Character c)
        {
            if (SearchMap == null 
                || !SearchMap.TryGetValue(c, out string mapping)
                || string.IsNullOrWhiteSpace(mapping))
                return GlyphService.GetCharacterDescription(c.UnicodeIndex, this);

            return GlyphService.TryGetAGLFNName(mapping);
        }




        /* .NET */

        public void Dispose()
        {
            FontFace?.Dispose();
            //FontFace = null;
        }

        public override string ToString()
        {
            return PreferredName;
        }
    }


    public partial class FontVariant
    {
        public static FontVariant CreateDefault(DWriteFontFace face)
        {
            return new FontVariant(face, null)
            {
                PreferredName = "",
                Characters = new List<Character> { new (0) }
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
