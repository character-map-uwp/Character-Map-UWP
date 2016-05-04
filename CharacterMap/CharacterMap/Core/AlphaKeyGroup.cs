using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Globalization.Collation;

namespace BeijingMetro_UWP.Common
{
    public class AlphaKeyGroup<T> : List<T>
    {
        const string GlobeGroupKey = "?";
        public string Key { get; private set; }
        //public List<T> this { get; private set; }
        public AlphaKeyGroup(string key)
        {
            Key = key;
        }
        private static List<AlphaKeyGroup<T>> CreateDefaultGroups(CharacterGroupings slg)
        {
            return (from cg 
                    in slg
                    where cg.Label != string.Empty
                    select cg.Label == "..." ? 
                        new AlphaKeyGroup<T>(GlobeGroupKey) : 
                        new AlphaKeyGroup<T>(cg.Label))
                    .ToList();
        }

        public static List<AlphaKeyGroup<T>> CreateGroups(IEnumerable<T> items, Func<T, string> keySelector, bool sort)
        {
            CharacterGroupings slg = new CharacterGroupings();
            List<AlphaKeyGroup<T>> list = CreateDefaultGroups(slg);
            foreach (T item in items)
            {
                int index = 0;
                string label = slg.Lookup(keySelector(item));
                index = list.FindIndex(alphagroupkey => (alphagroupkey.Key.Equals(label, StringComparison.CurrentCulture)));
                if (index > -1 && index < list.Count) list[index].Add(item);
            }
            if (sort)
            {
                foreach (AlphaKeyGroup<T> group in list)
                {
                    group.Sort((c0, c1) => keySelector(c0).CompareTo(keySelector(c1)));
                }
            }
            return list;
        }
    }
}
