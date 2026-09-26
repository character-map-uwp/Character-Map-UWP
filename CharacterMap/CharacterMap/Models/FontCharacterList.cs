using Microsoft.Graphics.Canvas.Text;
using System.Collections;

namespace CharacterMap.Models;

public class FontCharacterList : IReadOnlyList<Character>, IList
{
    private readonly CanvasUnicodeRange[] _ranges;
    private readonly int[] _prefixOffsets;
    private readonly int _count;
    private readonly IReadOnlyList<Character> _explicitList;

    /// <summary>
    /// Constructs a virtualized character list from DirectWrite Unicode ranges with 0 item allocations.
    /// </summary>
    public FontCharacterList(CanvasUnicodeRange[] ranges)
    {
        _ranges = ranges ?? [];
        _prefixOffsets = new int[_ranges.Length];

        int total = 0;
        for (int i = 0; i < _ranges.Length; i++)
        {
            _prefixOffsets[i] = total;
            total += (int)(_ranges[i].Last - _ranges[i].First + 1);
        }

        _count = total;
    }

    /// <summary>
    /// Constructs from an explicit list of characters (e.g. for CMFontFace.CreateDefault).
    /// </summary>
    public FontCharacterList(IReadOnlyList<Character> characters)
    {
        _explicitList = characters ?? [];
        _count = _explicitList.Count;
    }

    public int Count => _count;

    public Character this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (_explicitList != null)
                return _explicitList[index];

            int rangeIndex = FindRangeIndex(index);
            CanvasUnicodeRange range = _ranges[rangeIndex];
            int offsetInRange = index - _prefixOffsets[rangeIndex];
            uint codepoint = range.First + (uint)offsetInRange;

            return CMFontFace.GetCachedCharacter((int)codepoint);
        }
    }
    private int FindRangeIndex(int index)
    {
        int low = 0;
        int high = _ranges.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            int start = _prefixOffsets[mid];
            int end = start + (int)(_ranges[mid].Last - _ranges[mid].First);

            if (index < start)
                high = mid - 1;
            else if (index > end)
                low = mid + 1;
            else
                return mid;
        }

        return low;
    }

    public bool Contains(Character item)
    {
        if (item is null)
            return false;

        if (_explicitList != null)
            return _explicitList.Contains(item);

        uint cp = item.UnicodeIndex;
        for (int i = 0; i < _ranges.Length; i++)
            if (cp >= _ranges[i].First && cp <= _ranges[i].Last)
                return true;

        return false;
    }

    public int IndexOf(Character item)
    {
        if (item is null)
            return -1;

        if (_explicitList != null)
        {
            for (int i = 0; i < _explicitList.Count; i++)
                if (_explicitList[i].Equals(item))
                    return i;

            return -1;
        }

        uint cp = item.UnicodeIndex;
        for (int i = 0; i < _ranges.Length; i++)
        {
            if (cp >= _ranges[i].First && cp <= _ranges[i].Last)
                return _prefixOffsets[i] + (int)(cp - _ranges[i].First);
        }

        return -1;
    }

    public IEnumerator<Character> GetEnumerator()
    {
        if (_explicitList != null)
        {
            foreach (Character c in _explicitList)
                yield return c;
            yield break;
        }

        for (int i = 0; i < _ranges.Length; i++)
        {
            CanvasUnicodeRange r = _ranges[i];
            for (uint cp = r.First; cp <= r.Last; cp++)
                yield return CMFontFace.GetCachedCharacter((int)cp);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();




    #region IList Read-Only Implementation 

    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
    int IList.Add(object value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object value) => value is Character c && Contains(c);
    int IList.IndexOf(object value) => value is Character c ? IndexOf(c) : -1;
    void IList.Insert(int index, object value) => throw new NotSupportedException();
    void IList.Remove(object value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index)
    {
        for (int i = 0; i < _count; i++)
            array.SetValue(this[i], index + i);
    }

    #endregion
}
