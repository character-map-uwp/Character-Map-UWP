using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace CharacterMap.Provider
{
    public class UnicodeDevProvider : DevProviderBase
    {
        public UnicodeDevProvider(CharacterRenderingOptions o, Character c) : base(o, c)
        {
            DisplayName = "Unicode";
        }

        private static List<DevOption> _allOptions { get; } = new ()
        {
            new ("TxtUniCodepoint/Header", null),
            new ("TxtUniHexValue/Text", null),
            new ("TxtUTF16/Header", null)
        };

        protected override DevProviderType GetDevProviderType() => DevProviderType.Unicode;
        protected override IReadOnlyList<DevOption> OnGetContextOptions() => Inflate();
        protected override IReadOnlyList<DevOption> OnGetOptions() => Inflate();
        public override IReadOnlyList<DevOption> GetAllOptions() => _allOptions;

        IReadOnlyList<DevOption> Inflate()
        {
            var v = Options.Variant;
            var c = Character;

            string hex = c.UnicodeIndex.ToString("x4").ToUpper();
            string utf = null;

            if (Unicode.RequiresSurrogates(c))
            {
                Windows.Data.Text.UnicodeCharacters.GetSurrogatePairFromCodepoint(c.UnicodeIndex, out char high, out char low);
                utf =@$"\u{(uint)high:x4}\u{(uint)low:x4}";
            }
            else
            {
                utf = @$"\u{(uint)c.Char[0]:x4}";
            }

            List<DevOption> ops = new ()
            {
                new ("TxtUniCodepoint/Header", $"{c.UnicodeIndex}"),
                new ("TxtUniHexValue/Text", c.UnicodeString),
                new ("TxtUTF16/Header", $"{utf}"),
            };

            return ops;
        }
    }
}
