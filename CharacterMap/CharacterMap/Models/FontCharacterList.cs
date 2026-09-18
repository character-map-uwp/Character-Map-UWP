using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Models;

public class FontCharacterList : IReadOnlyList<Character>, IList
{
    private readonly CanvasUnicodeRange[] _ranges;
    private readonly int[] _prefixOffsets; // Start index of each range
    private readonly int _count;
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
    public int Count => _count;
    public Character this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            // Binary search to find which range contains this index
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
