using CharacterMap.Core;
using CharacterMap.Models;
using CharacterMapCX;
using System.Collections.Generic;

namespace CharacterMap.Provider
{
    public enum DevProviderType
    {
        None,
        CSharp,
        XAML,
        CppCX
    }

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
                new XamlDevProvider(o, c),
                new CSharpDevProvider(o, c),
                new CppCxDevProvider(o, c)
            };
        }
    }

    /// <summary>
    /// Base class for Dev providers.
    /// Should enable lazy evaluation of dev values by only creating
    /// values when calling GetOptions()/GetContextOptions()
    /// </summary>
    public abstract partial class DevProviderBase
    {
        public string ResourceKey { get; }


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

        /// <summary>
        /// Creates an SVG / XAML path syntax compatible string representing the filled geometry
        /// of a glyph.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        public string GetOutlineGeometry(
            Character c,
            CharacterRenderingOptions o)
        {
            string pathIconData = null;
            if (o.Variant != null)
            {
                using var typo = o.CreateCanvasTypography();
                using var geom = ExportManager.CreateGeometry(64, o.Variant, c, o.Analysis, typo);
                pathIconData = Interop.GetPathData(geom).Path;
            }

            return pathIconData;
        }
    }
}
