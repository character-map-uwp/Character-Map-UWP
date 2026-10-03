using Microsoft.Graphics.Canvas.Text;
using System.Collections;
using System.Collections.Specialized;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Models;

/// <summary>
/// A flat, grouped, optionally-virtualised ItemsSource for use directly as
/// GridView/ListView.ItemsSource (no CollectionViewSource needed).
///
/// Implements <see cref="ICollectionView"/> so that group headers render through the
/// standard <c>GridView.GroupStyle</c> pipeline, and implements <see cref="IItemsRangeInfo"/>
/// so the virtualising panel can drive cache lifetime via <c>RangesChanged</c>.
///
/// Items are created on demand synchronously from the underlying
/// <see cref="IReadOnlyList{Character}"/> source.  Group metadata (keys, start offsets,
/// counts) is computed once during <see cref="Create"/>.
///
/// Virtualisation activates when <see cref="Count"/> >= <see cref="VirtualizationThreshold"/>.
/// Below the threshold items are returned directly from the source with no cache overhead.
/// </summary>
public sealed class GroupedCharacterSource
    : IList,                     // non-generic IList for XAML binding machinery
      IList<object>,             // required by IObservableVector<object>
      IObservableVector<object>, // required by ICollectionView
      INotifyCollectionChanged,  // change notifications (never fired; static collection)
      ICollectionView,           // grouped display without CollectionViewSource
      IItemsRangeInfo            // virtualising-panel cache management
{
    /// <summary>
    /// Collections with fewer items than this threshold are returned directly from the
    /// source without any cache management.  At or above the threshold, RangesChanged
    /// evicts slots that leave the panel's tracked window.
    /// </summary>
    public const int VirtualizationThreshold = 40_000;

    // --- Private state --------------------------------------------------------

    private readonly record struct GroupMeta(NamedUnicodeRange Range, int StartIndex, int Count);

    private readonly IReadOnlyList<Character> _source;
    private readonly GroupMeta[] _groupMeta;
    private readonly bool _isVirtualized;
    private Dictionary<int, Character> _cache;
    private IObservableVector<object> _collectionGroups;  // lazy

    // --- Construction ---------------------------------------------------------

    private GroupedCharacterSource(IReadOnlyList<Character> source, GroupMeta[] groupMeta)
    {
        _source    = source;
        _groupMeta = groupMeta;
        Count      = source.Count;
        _isVirtualized = Count >= VirtualizationThreshold;
        if (_isVirtualized)
            _cache = new(capacity: 512);
    }

    /// <summary>
    /// Builds a <see cref="GroupedCharacterSource"/> from a <see cref="CMFontFace"/> without
    /// ever calling <c>GetCharacters()</c>.  Groups are derived by walking the face's raw
    /// <see cref="Microsoft.Graphics.Canvas.Text.CanvasUnicodeRange"/> spans and mapping each
    /// codepoint to its <see cref="NamedUnicodeRange"/> via a binary search.
    /// Characters are created on demand through <see cref="FontCharacterList"/>'s virtual indexer.
    /// </summary>
    public static GroupedCharacterSource Create(CMFontFace face)
    {
        if (face is null)
            return new(new FontCharacterList([]), []);

        CanvasUnicodeRange[] fontRanges = face.UnicodeRanges;
        if (fontRanges.Length == 0)
            return new(new FontCharacterList(fontRanges), []);

        bool mdl2 = FontFinder.IsMDL2(face);

        // Build GroupMeta by walking raw CanvasUnicodeRange spans.
        // Each span [First..Last] may straddle multiple NamedUnicodeRanges, so we advance
        // through All named ranges as we go.  We track the current flat index (i.e. the
        // position within the FontCharacterList that backs this source) separately.

        List<GroupMeta> groups = [];
        int flatIndex = 0; // current offset into the FontCharacterList

        for (int ri = 0; ri < fontRanges.Length; ri++)
        {
            uint cp    = fontRanges[ri].First;
            uint cpEnd = fontRanges[ri].Last;

            while (cp <= cpEnd)
            {
                NamedUnicodeRange named = GetNamedRange(cp, mdl2, out uint rangeEnd);

                // How many codepoints in this named range are still covered by [cp..cpEnd]?
                uint segEnd = Math.Min(cpEnd, rangeEnd);
                int  segLen = (int)(segEnd - cp + 1);

                // Merge with previous group if it's the same named range
                if (groups.Count > 0 && groups[^1].Range == named)
                {
                    GroupMeta prev = groups[^1];
                    groups[^1] = prev with { Count = prev.Count + segLen };
                }
                else
                    groups.Add(new(named, flatIndex, segLen));

                flatIndex += segLen;
                cp = segEnd + 1;
            }
        }

        return new(new FontCharacterList(fontRanges), [.. groups]);
    }

    private static NamedUnicodeRange GetNamedRange(uint cp, bool mdl2, out uint rangeEnd)
    {
        if (mdl2)
        {
            if (UnicodeRanges.MDL2Deprecated.Contains(cp))
            {
                rangeEnd = UnicodeRanges.MDL2Deprecated.End;
                return UnicodeRanges.MDL2Deprecated;
            }
            if (UnicodeRanges.PrivateUseAreaMDL2.Contains(cp))
            {
                rangeEnd = UnicodeRanges.PrivateUseAreaMDL2.End;
                return UnicodeRanges.PrivateUseAreaMDL2;
            }
        }

        return UnicodeRanges.GetRange(cp, out rangeEnd);
    }

    private static NamedUnicodeRange GetNamedRange(uint cp, bool mdl2)
        => GetNamedRange(cp, mdl2, out _);

    /// <summary>
    /// Builds a <see cref="GroupedCharacterSource"/> from a flat character list using
    /// the same grouping logic that <c>UnicodeRangeGroup.CreateGroups</c> previously applied.
    /// Prefer the <see cref="Create(CMFontFace)"/> overload when the full character set is needed.
    /// </summary>
    public static GroupedCharacterSource Create(IReadOnlyList<Character> items, bool mdl2 = false)
    {
        if (items == null || items.Count == 0)
            return new(items ?? [], []);

        List<GroupMeta> groups = [];
        int start = 0;
        int total = items.Count;

        while (start < total)
        {
            NamedUnicodeRange range = GetRange(items[start], mdl2);
            int end = start + 1;

            while (end < total && range.Contains(items[end].UnicodeIndex))
                end++;

            groups.Add(new(range, start, end - start));
            start = end;
        }

        return new(items, [.. groups]);
    }

    private static NamedUnicodeRange GetRange(Character c, bool mdl2)
        => GetNamedRange(c.UnicodeIndex, mdl2);


    // --- Item access ----------------------------------------------------------

    /// <summary>Returns the character at <paramref name="index"/>, caching it when virtualised.</summary>
    internal Character GetCharacter(int index)
    {
        if (!_isVirtualized)
            return _source[index];

        if (!_cache.TryGetValue(index, out Character item))
            _cache[index] = item = _source[index];
        return item;
    }

    private int IndexOf(Character c)
    {
        if (_source is FontCharacterList fcl)
            return fcl.IndexOf(c);

        for (int i = 0; i < Count; i++)
            if (_source[i] == c)
                return i;
        return -1;
    }

    // --- IItemsRangeInfo ------------------------------------------------------

    /// <summary>
    /// Called by the virtualising panel each time its tracked window changes.
    /// Evicts cache slots that are no longer within any tracked range.
    /// No-op when the collection is below the virtualisation threshold.
    /// </summary>
    public void RangesChanged(ItemIndexRange visibleRange, IReadOnlyList<ItemIndexRange> trackedItems)
    {
        if (!_isVirtualized) return;

        HashSet<int> keep = [];
        foreach (ItemIndexRange r in trackedItems)
            for (uint i = 0; i < r.Length; i++)
                keep.Add((int)(r.FirstIndex + i));

        foreach (int i in _cache.Keys.Except(keep).ToList())
            _cache.Remove(i);
    }

    public void Dispose() => _cache?.Clear();

    // --- ICollectionView ------------------------------------------------------

    /// <summary>
    /// Group descriptors consumed by the GridView to position group headers.
    /// Lazily constructed once; never mutated after that.
    /// Each element implements both <see cref="ICollectionViewGroup"/> (for the header
    /// key) and <see cref="IObservableVector{object}"/> (for GroupItems).
    /// </summary>
    public IObservableVector<object> CollectionGroups
    {
        get
        {
            if (_collectionGroups is not null)
                return _collectionGroups;

            GroupItemsVector[] groups = new GroupItemsVector[_groupMeta.Length];
            for (int i = 0; i < _groupMeta.Length; i++)
            {
                GroupMeta g = _groupMeta[i];
                groups[i] = new(g.Range, this, g.StartIndex, g.Count);
            }
            _collectionGroups = new StaticObservableVector(groups);
            return _collectionGroups;
        }
    }

    // Current-item tracking (required by ICollectionView; not used by the app)
    public object CurrentItem      => (uint)CurrentPosition < (uint)Count ? GetCharacter(CurrentPosition) : null;
    public int    CurrentPosition  { get; private set; } = -1;
    public bool   HasMoreItems     => false;
    public bool   IsCurrentAfterLast   => CurrentPosition >= Count;
    public bool   IsCurrentBeforeFirst => CurrentPosition < 0;

    // Never fired - items/groups are static after construction.
    public event NotifyCollectionChangedEventHandler CollectionChanged;
    public event VectorChangedEventHandler<object>   VectorChanged;
    public event EventHandler<object>                CurrentChanged;
    public event CurrentChangingEventHandler         CurrentChanging;

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count) => throw new NotImplementedException();

    public bool MoveCurrentTo(object item)  => item is Character c && MoveCurrentToPosition(IndexOf(c));
    public bool MoveCurrentToFirst()        => MoveCurrentToPosition(0);
    public bool MoveCurrentToLast()         => MoveCurrentToPosition(Count - 1);
    public bool MoveCurrentToNext()         => MoveCurrentToPosition(CurrentPosition + 1);
    public bool MoveCurrentToPrevious()     => MoveCurrentToPosition(CurrentPosition - 1);

    public bool MoveCurrentToPosition(int index)
    {
        if ((uint)index >= (uint)Count) return false;
        CurrentPosition = index;
        CurrentChanged?.Invoke(this, null);
        return true;
    }

    // --- IList<object> / IObservableVector<object> ----------------------------
    //     ICollectionView extends IObservableVector<object> which extends IList<object>.

    bool   ICollection<object>.IsReadOnly => true;
    object IList<object>.this[int index]  { get => GetCharacter(index); set => throw new NotSupportedException(); }

    int  IList<object>.IndexOf(object item)              => item is Character c ? IndexOf(c) : -1;
    bool ICollection<object>.Contains(object item)       => item is Character c && IndexOf(c) >= 0;
    void ICollection<object>.CopyTo(object[] array, int idx)
    {
        for (int i = 0; i < Count; i++)
            array[idx + i] = GetCharacter(i);
    }
    void ICollection<object>.Add(object item)    => throw new NotSupportedException();
    bool ICollection<object>.Remove(object item) => throw new NotSupportedException();
    void ICollection<object>.Clear()             => throw new NotSupportedException();
    void IList<object>.Insert(int index, object item) => throw new NotSupportedException();
    void IList<object>.RemoveAt(int index)            => throw new NotSupportedException();

    public IEnumerator<object> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return GetCharacter(i);
    }

    // --- IList (non-generic -- satisfies XAML binding machinery / IBindableVector) --

    object IList.this[int index] { get => GetCharacter(index); set => throw new NotSupportedException(); }
    bool   IList.IsFixedSize  => true;
    bool   IList.IsReadOnly   => true;
    bool   ICollection.IsSynchronized => false;
    object ICollection.SyncRoot       => this;

    public int Count { get; }

    int  IList.Add(object value)               => throw new NotSupportedException();
    void IList.Clear()                         => throw new NotSupportedException();
    bool IList.Contains(object value)          => value is Character c && IndexOf(c) >= 0;
    int  IList.IndexOf(object value)           => value is Character c ? IndexOf(c) : -1;
    void IList.Insert(int index, object value) => throw new NotSupportedException();
    void IList.Remove(object value)            => throw new NotSupportedException();
    void IList.RemoveAt(int index)             => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int idx)
    {
        for (int i = 0; i < Count; i++)
            array.SetValue(GetCharacter(i), idx + i);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // --- Nested: GroupItemsVector ---------------------------------------------

    /// <summary>
    /// Represents a single group.  Implements both <see cref="ICollectionViewGroup"/>
    /// (consumed by the GridView header pipeline) and <see cref="IObservableVector{object}"/>
    /// (typed as <c>GroupItems</c>).  Item access delegates to the owning source so that the
    /// virtualised cache is shared across the flat and per-group access paths.
    /// </summary>
    private sealed class GroupItemsVector : ICollectionViewGroup, IObservableVector<object>
    {
        private readonly GroupedCharacterSource _owner;
        private readonly int _start;

        /// <summary>
        /// The group key exposed as <see cref="ICollectionViewGroup.Group"/>.
        /// Using the plain name string ensures <c>ContentPresenter</c> renders it cleanly
        /// without the record-class auto-generated ToString noise of <see cref="NamedUnicodeRange"/>.
        /// </summary>
        public object Group { get; }  // NamedUnicodeRange.Name (string)

        public IObservableVector<object> GroupItems => this;
        public int Count { get; }

        // Never fired - group contents are static.
        public event VectorChangedEventHandler<object> VectorChanged;

        public GroupItemsVector(NamedUnicodeRange range, GroupedCharacterSource owner, int start, int count)
        {
            Group  = range.Name;
            _owner = owner;
            _start = start;
            Count  = count;
        }

        public override string ToString() => (string)Group;

        bool   ICollection<object>.IsReadOnly => true;
        object IList<object>.this[int index]  { get => _owner.GetCharacter(_start + index); set => throw new NotSupportedException(); }

        int  IList<object>.IndexOf(object item)        => item is Character c ? LocalIndexOf(c) : -1;
        bool ICollection<object>.Contains(object item) => item is Character c && LocalIndexOf(c) >= 0;
        void ICollection<object>.CopyTo(object[] array, int idx)
        {
            for (int i = 0; i < Count; i++)
                array[idx + i] = _owner.GetCharacter(_start + i);
        }
        void ICollection<object>.Add(object item)    => throw new NotSupportedException();
        bool ICollection<object>.Remove(object item) => throw new NotSupportedException();
        void ICollection<object>.Clear()             => throw new NotSupportedException();
        void IList<object>.Insert(int index, object item) => throw new NotSupportedException();
        void IList<object>.RemoveAt(int index)            => throw new NotSupportedException();

        public IEnumerator<object> GetEnumerator()
        {
            int end = _start + Count;
            for (int i = _start; i < end; i++)
                yield return _owner.GetCharacter(i);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int LocalIndexOf(Character c)
        {
            int end = _start + Count;
            for (int i = _start; i < end; i++)
                if (_owner._source[i] == c)
                    return i - _start;
            return -1;
        }
    }

    // --- Nested: StaticObservableVector ---------------------------------------

    /// <summary>
    /// Immutable <see cref="IObservableVector{object}"/> backed by a fixed array.
    /// Used as the return value of <see cref="CollectionGroups"/>.
    /// </summary>
    private sealed class StaticObservableVector : IObservableVector<object>
    {
        private readonly GroupItemsVector[] _items;

        public int Count => _items.Length;

        // Never fired - groups are fixed after construction.
        public event VectorChangedEventHandler<object> VectorChanged;

        public StaticObservableVector(GroupItemsVector[] items) => _items = items;

        bool   ICollection<object>.IsReadOnly => true;
        object IList<object>.this[int index]  { get => _items[index]; set => throw new NotSupportedException(); }

        int  IList<object>.IndexOf(object item)        => item is GroupItemsVector g ? Array.IndexOf(_items, g) : -1;
        bool ICollection<object>.Contains(object item) => item is GroupItemsVector g && Array.IndexOf(_items, g) >= 0;
        void ICollection<object>.CopyTo(object[] array, int idx)
        {
            for (int i = 0; i < _items.Length; i++)
                array[idx + i] = _items[i];
        }
        void ICollection<object>.Add(object item)    => throw new NotSupportedException();
        bool ICollection<object>.Remove(object item) => throw new NotSupportedException();
        void ICollection<object>.Clear()             => throw new NotSupportedException();
        void IList<object>.Insert(int index, object item) => throw new NotSupportedException();
        void IList<object>.RemoveAt(int index)            => throw new NotSupportedException();

        public IEnumerator<object> GetEnumerator()
        {
            foreach (GroupItemsVector g in _items)
                yield return g;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
