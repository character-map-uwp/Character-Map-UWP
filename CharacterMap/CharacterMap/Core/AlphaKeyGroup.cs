using CharacterMap.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.Globalization.Collation;

namespace CharacterMap.Core
{
    public class UnicodeRangeGroup : List<Character>, IGrouping<string, Character>
    {
        public string Key { get; private set; }

        public UnicodeRangeGroup(string key, IEnumerable<Character> items) : base(items)
        {
            Key = key;
        }

        public static ObservableCollection<UnicodeRangeGroup> CreateGroups(IEnumerable<Character> items)
        {
            return new(items.GroupBy(i => i.Range?.Name ?? "Misc", c => c).Select(g=> new UnicodeRangeGroup(g.Key, g)));
        }

        public override string ToString()
        {
            return Key;
        }
    }

    public class AlphaKeyGroup<T> : ObservableCollection<T>
    {
        public string Key { get; private set; }

        public AlphaKeyGroup(string key)
        {
            Key = key;
        }

        // Work around for Chinese version of Windows
        // By default, Chinese language group will create useless "拼音A-Z" groups.
        private static List<AlphaKeyGroup<T>> CreateAZGroups()
        {
            char[] alpha = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ&".ToCharArray();
            var list = alpha.Select(c => new AlphaKeyGroup<T>(c.ToString())).ToList();
            return list;
        }

        public static List<AlphaKeyGroup<T>> CreateGroups(IEnumerable<T> items, Func<T, string> keySelector)
        {
            CharacterGroupings slg = new ();
            List<AlphaKeyGroup<T>> list = CreateAZGroups(); //CreateDefaultGroups(slg);
            foreach (T item in items)
            {
                int index = 0;
                string label = slg.Lookup(keySelector(item));
                index = list.FindIndex(alphagroupkey => (alphagroupkey.Key.Equals(label, StringComparison.CurrentCulture)));
                if (index > -1 && index < list.Count)
                    list[index].Add(item);
                else
                    list.Last().Add(item);
            }
            return list;
        }
    }
}
