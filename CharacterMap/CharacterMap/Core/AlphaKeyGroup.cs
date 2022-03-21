using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Windows.Globalization.Collation;

namespace CharacterMap.Core
{
    public class AlphaKeyCollection<T> : ObservableCollection<AlphaKeyGroup<T>>
    {
        public AlphaKeyCollection() { }
        public AlphaKeyCollection(List<AlphaKeyGroup<T>> items) : base(items) { }
        public AlphaKeyCollection(IEnumerable<AlphaKeyGroup<T>> items) : base(items) { }

        public bool TryRemove(T item)
        {
            foreach (var i in Items)
                if (i.Remove(item))
                {
                    if (i.Count == 0)
                        this.Remove(i);

                    return true;
                }

            return false;
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
