using System.Collections;
using System.Collections.ObjectModel;

namespace CharacterMap.Models;

public class UnicodeRangeGroup : IReadOnlyList<Character>, IList, IGrouping<NamedUnicodeRange, Character>
{
    private readonly IReadOnlyList<Character> _source;
    private readonly int _startIndex;
    private readonly int _count;

    public NamedUnicodeRange Key { get; }

    public int Count => _count;

    public UnicodeRangeGroup(NamedUnicodeRange key, IReadOnlyList<Character> source, int startIndex, int count)
    {
        Key = key;
        _source = source;
        _startIndex = startIndex;
        _count = count;
    }

    public Character this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _source[_startIndex + index];
        }
    }

    /// <summary>
    /// Creates a <see cref="GroupedCharacterSource"/> from a <paramref name="face"/>.
    /// The returned object implements <see cref="Windows.UI.Xaml.Data.ICollectionView"/>
    /// (group headers) and <see cref="Windows.UI.Xaml.Data.IItemsRangeInfo"/>
    /// (virtualisation) and can be set directly as <c>GridView.ItemsSource</c>.
    /// </summary>
    public static GroupedCharacterSource CreateGroups(CMFontFace face)
        => GroupedCharacterSource.Create(face);


    public override string ToString() => Key.Name;

    public IEnumerator<Character> GetEnumerator()
    {
        int end = _startIndex + _count;
        for (int i = _startIndex; i < end; i++)
            yield return _source[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #region IList Read-Only Implementation for WinUI XAML Virtualization
    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
    int IList.Add(object value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object value) => value is Character c && IndexOf(c) >= 0;
    public int IndexOf(object value)
    {
        if (value is Character c)
        {
            int end = _startIndex + _count;
            for (int i = _startIndex; i < end; i++)
                if (_source[i].Equals(c))
                    return i - _startIndex;
        }
        return -1;
    }
    void IList.Insert(int index, object value) => throw new NotSupportedException();
    void IList.Remove(object value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index)
    {
        int end = _startIndex + _count;
        for (int i = _startIndex; i < end; i++)
            array.SetValue(_source[i], index + (i - _startIndex));
    }
    #endregion
}
