using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Models;

public class GlyphCollection
    : IList<uint>,
      IList,
      IReadOnlyList<uint>,
      INotifyCollectionChanged,
      INotifyPropertyChanged,
      IItemsRangeInfo
{
    private readonly CMFontFace _fontFace;

    public ItemIndexRange VisibleRange { get; private set; }
    public IReadOnlyList<ItemIndexRange> TrackedItems { get; private set; }

    public event NotifyCollectionChangedEventHandler CollectionChanged;
    public event PropertyChangedEventHandler PropertyChanged;

    public GlyphCollection(CMFontFace fontFace)
    {
        _fontFace = fontFace;
    }

    public uint MaxCount => _fontFace?.Face?.GlyphCount ?? 0;
    public int Count => (int)MaxCount;

    public bool IsReadOnly => true;

    public uint this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return (uint)index;
        }
        set => throw new NotSupportedException();
    }

    // --- IItemsRangeInfo ---

    public void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
    {
        VisibleRange = visibleRange;
        TrackedItems = trackedItems;
    }

    public void Dispose()
    {
        VisibleRange = null;
        TrackedItems = null;
    }

    // --- IList<uint> & IReadOnlyList<uint> ---

    public int IndexOf(uint item) => item < (uint)Count ? (int)item : -1;
    public bool Contains(uint item) => item < (uint)Count;

    public void CopyTo(uint[] array, int arrayIndex)
    {
        int count = Count;
        for (int i = 0; i < count; i++)
            array[arrayIndex + i] = (uint)i;
    }

    public IEnumerator<uint> GetEnumerator()
    {
        int count = Count;
        for (int i = 0; i < count; i++)
            yield return (uint)i;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ICollection<uint>.Add(uint item) => throw new NotSupportedException();
    void ICollection<uint>.Clear() => throw new NotSupportedException();
    bool ICollection<uint>.Remove(uint item) => throw new NotSupportedException();
    void IList<uint>.Insert(int index, uint item) => throw new NotSupportedException();
    void IList<uint>.RemoveAt(int index) => throw new NotSupportedException();

    // --- IList (non-generic) ---

    object IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    int IList.Add(object value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object value) => value is uint u && Contains(u);
    int IList.IndexOf(object value) => value is uint u ? IndexOf(u) : -1;
    void IList.Insert(int index, object value) => throw new NotSupportedException();
    void IList.Remove(object value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    void ICollection.CopyTo(Array array, int index)
    {
        int count = Count;
        for (int i = 0; i < count; i++)
            array.SetValue((uint)i, index + i);
    }
}
