using CharacterMapCX;
using System.Collections;

namespace CharacterMap.Models;

public class FontCharacterList : IReadOnlyList<Character>, IList
{
    private readonly DWriteUnicodeRange[] _ranges;
    private readonly int[] _prefixOffsets;
    private readonly int _count;
    private readonly IReadOnlyList<Character> _explicitList;

    /// <summary>
    /// Constructs a virtualized character list from DirectWrite Unicode ranges with 0 item allocations.
    /// </summary>
    public FontCharacterList(DWriteUnicodeRange[] ranges)
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

    private FontCharacterList(IReadOnlyList<Character> characters)
    {
        IReadOnlyList<Character> chars = characters ?? [];
        _explicitList = chars;
        characters = chars;
        _count = chars.Count;
    }

    /// <summary>
    /// Constructs from an explicit list of characters (e.g. for CMFontFace.CreateDefault).
    /// </summary>
    public static FontCharacterList CreateDefault(IReadOnlyList<Character> characters) => new(characters);

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
            DWriteUnicodeRange range = _ranges[rangeIndex];
            int offsetInRange = index - _prefixOffsets[rangeIndex];
            uint codepoint = range.First + (uint)offsetInRange;

            return Character.Get((int)codepoint);
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

    private int FindRangeForCodepoint(uint cp)
    {
        int low = 0;
        int high = _ranges.Length - 1;

        while (low <= high)
        {
            int mid = (low + high) >>> 1;
            DWriteUnicodeRange r = _ranges[mid];

            if (cp < r.First)
                high = mid - 1;
            else if (cp > r.Last)
                low = mid + 1;
            else
                return mid;
        }

        return -1;
    }

    public bool TryGetCharacter(uint codepoint, out Character character)
    {
        if (_explicitList != null)
        {
            for (int i = 0; i < _explicitList.Count; i++)
            {
                if (_explicitList[i].UnicodeIndex == codepoint)
                {
                    character = _explicitList[i];
                    return true;
                }
            }

            character = null;
            return false;
        }

        if (FindRangeForCodepoint(codepoint) >= 0)
        {
            character = Character.Get((int)codepoint);
            return true;
        }

        character = null;
        return false;
    }

    public Character GetFirstNonWhitespaceCharacter()
    {
        if (_explicitList != null)
        {
            for (int i = 0; i < _explicitList.Count; i++)
            {
                Character c = _explicitList[i];
                if (!Windows.Data.Text.UnicodeCharacters.IsWhitespace(c.UnicodeIndex))
                    return c;
            }

            return _explicitList.Count > 0 ? _explicitList[0] : null;
        }

        for (int i = 0; i < _ranges.Length; i++)
        {
            DWriteUnicodeRange r = _ranges[i];
            for (uint cp = r.First; cp <= r.Last; cp++)
            {
                if (!Windows.Data.Text.UnicodeCharacters.IsWhitespace(cp))
                    return Character.Get((int)cp);
            }
        }

        return _count > 0 ? this[0] : null;
    }

    public bool Contains(Character item)
    {
        if (item is null)
            return false;

        if (_explicitList != null)
            return _explicitList.Contains(item);

        return FindRangeForCodepoint(item.UnicodeIndex) >= 0;
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
        int rangeIdx = FindRangeForCodepoint(cp);
        if (rangeIdx >= 0)
            return _prefixOffsets[rangeIdx] + (int)(cp - _ranges[rangeIdx].First);

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
            DWriteUnicodeRange r = _ranges[i];
            for (uint cp = r.First; cp <= r.Last; cp++)
                yield return Character.Get((int)cp);
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
