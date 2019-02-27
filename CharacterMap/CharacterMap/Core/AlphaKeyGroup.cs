using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Globalization.Collation;

namespace CharacterMap.Core
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

        //private static List<AlphaKeyGroup<T>> CreateDefaultGroups(CharacterGroupings slg)
        //{
        //    return (from cg 
        //            in slg
        //            where cg.Label != string.Empty
        //            select cg.Label == "..." ? 
        //                new AlphaKeyGroup<T>(GlobeGroupKey) : 
        //                new AlphaKeyGroup<T>(cg.Label))
        //            .ToList();
        //}

        // Work around for Chinese version of Windows
        // By default, Chinese lanugage group will create useless "拼音A-Z" groups.
        private static List<AlphaKeyGroup<T>> CreateAZGroups()
        {
            char[] alpha = "#ABCDEFGHIJKLMNOPQRSTUVWXYZ&".ToCharArray();
            var list = alpha.Select(c => new AlphaKeyGroup<T>(c.ToString())).ToList();
            return list;
        }

        public static List<AlphaKeyGroup<T>> CreateGroups(IEnumerable<T> items, Func<T, string> keySelector)
        {
            CharacterGroupings slg = new CharacterGroupings();
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
