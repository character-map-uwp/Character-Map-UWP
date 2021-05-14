using CharacterMap.Core;
using CharacterMap.Models;
using CharacterMapCX;
using System.Collections.Generic;
using System.Linq;

namespace CharacterMap.Provider
{
    public partial class DevProviderBase
    {
        /// <summary>
        /// Creates dev providers for a glyph.
        /// Register all known providers here.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="c"></param>
        /// <returns></returns>
        public static IReadOnlyList<DevProviderBase> GetProviders(CharacterRenderingOptions o, Character c)
        {
            return new List<DevProviderBase>
            {
                new DevProviderNone(o, c),
                new XamlDevProvider(o, c),
                new CSharpDevProvider(o, c),
                new VBDevProvider(o, c),
                new CppCxDevProvider(o, c),
                new CppWinrtDevProvider(o, c),
                new XamarinFormsDevProvider(o,c)
            };
        }

        private static List<KeyValuePair<GeometryCacheEntry, string>> _geometryCache { get; } = new List<KeyValuePair<GeometryCacheEntry, string>>();

        /// <summary>
        /// Creates an SVG / XAML path syntax compatible string representing the filled geometry
        /// of a glyph.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string GetOutlineGeometry(
           Character c,
           CharacterRenderingOptions o)
        {
            /* 
             * We use a cache because creating geometry is currently a little bit more expensive than it needs to be 
             * (we're not actually reading the data from the font, but using D2D to "Draw" the geometry to a custom sink)
             * and we might be creating it multiple times for a single glyph depending on how our dev providers are
             * configured, so a small cache will help performance
             */

            if (_geometryCache.FirstOrDefault(p => p.Key is GeometryCacheEntry e && e.Options == o && e.Character == c) is KeyValuePair<GeometryCacheEntry, string> pair
                && pair.Value != null)
                return pair.Value;


            string pathIconData = null;
            if (o.Variant != null)
            {
                // We use a font size of 20 as this metrically maps to the size of SegoeMDL2 icons used
                // in FontIcon / SymbolIcon controls.
                using var geom = ExportManager.CreateGeometry(c, o with { FontSize = 20 });
                using var typo = o.CreateCanvasTypography();
                pathIconData = Utils.GetInterop().GetPathData(geom).Path;
                _geometryCache.Add(KeyValuePair.Create(new GeometryCacheEntry(c, o), pathIconData));

                // Keep the cache to a certain size
                while (_geometryCache.Count > 10)
                    _geometryCache.RemoveAt(0);
            }

            return pathIconData;
        }
    }

    /// <summary>
    /// Base class for Dev providers.
    /// Should enable lazy evaluation of dev values by only creating
    /// values when calling GetOptions()/GetContextOptions()
    /// </summary>
    public abstract partial class DevProviderBase
    {
        protected static List<DevOption> DefaultUWPOptions { get; } = new List<DevOption>
        {
            new ("TxtXamlCode/Header", null),
            new ("TxtFontIcon/Header", null),
            new ("TxtPathIcon/Text", null),
            new ("TxtSymbolIcon/Header", null),
        };

        public string ResourceKey { get; }
        public string DisplayName { get; protected init; }

        public DevProviderType Type { get; }

        protected NativeInterop Interop { get; }

        protected CharacterRenderingOptions Options { get; }

        protected Character Character { get; }

        private IReadOnlyList<DevOption> _contextOptions = null;

        private IReadOnlyList<DevOption> _previewPaneOptions = null;

        public DevProviderBase(CharacterRenderingOptions r, Character character)
        {
            Options = r;
            Character = character;
            Interop = Utils.GetInterop();
            Type = GetDevProviderType();
            ResourceKey = $"Provider{Type}";
        }

        protected abstract DevProviderType GetDevProviderType();

        /// <summary>
        /// Gets options for display in the context menu when right clicking a glyph.
        /// </summary>
        /// <returns></returns>
        protected abstract IReadOnlyList<DevOption> OnGetContextOptions();

        /// <summary>
        /// Gets options for display under the character preview window.
        /// Try not to have more than 4 options here to prevent the UI becoming too cluttered.
        /// </summary>
        /// <returns></returns>
        protected abstract IReadOnlyList<DevOption> OnGetOptions();

        /// <summary>
        /// Returns all the possible (shell) values this provider can return.
        /// </summary>
        /// <returns></returns>
        public virtual IReadOnlyList<DevOption> GetAllOptions() => DefaultUWPOptions;

        /// <summary>
        /// Gets options for display in the context menu when right clicking a glyph.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<DevOption> GetContextOptions() => _contextOptions ??= OnGetContextOptions();

        /// <summary>
        /// Gets options for display under the character preview window.
        /// Try not to have more than 4 options here to prevent the UI becoming too cluttered.
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<DevOption> GetOptions() => _previewPaneOptions ??= OnGetOptions();
    }
}
