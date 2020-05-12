using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Core
{
    public static class Diagnostics
    {
#if DEBUG

        public static void ListTypographicFeatures(List<InstalledFont> fonts)
        {
            HashSet<string> tt = new HashSet<string>();

            foreach (var font in fonts)
            {
                foreach (var v in font.Variants)
                {
                    foreach (var f in v.TypographyFeatures)
                    {
                        if (!tt.Contains(f.DisplayName))
                            tt.Add(f.DisplayName);
                    }
                }
            }

            var s = string.Join("\n", tt.OrderBy(t => t));
            Debug.WriteLine(s);
        }

#endif
    }
}
