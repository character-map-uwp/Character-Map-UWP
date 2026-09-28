using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Models;

public class GlyphCollection : ObservableCollection<uint>, ISupportIncrementalLoading
{
    private static PropertyChangedEventArgs _hasMoreItemsHandler = new(nameof(HasMoreItems));

    //public Uri FontUri { get; private set; }

    public bool IsLoading { get; private set; } = false;

    public bool IsLoaded { get; private set; } = true;

    private uint currentOffset = 0;
    private readonly CMFontFace _fontFace;
    private SynchronizationContext _context;

    public GlyphCollection(CMFontFace fontFace)
    {
        _fontFace = fontFace;
        _context = SynchronizationContext.Current;

        // Prepare some glyph
        var size = Math.Min(MaxCount - currentOffset, 256);
        var items = Enumerable.Range((int)currentOffset, (int)size).ToList();

        foreach (var item in items)
            base.Items.Add((uint)item);

        currentOffset = (uint)this.Count;
    }

    public uint MaxCount => _fontFace.Face.GlyphCount;

    //protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    //{
    //    _context.Post((s) => { base.OnPropertyChanged(e); }, null);
    //}

    public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
    {
        return Task.FromResult(LoadMoreInternal(count)).AsAsyncOperation();
    }

    private LoadMoreItemsResult LoadMoreInternal(uint count)
    {
        var size = Math.Min(MaxCount - currentOffset, count);
        var items = Enumerable.Range((int)currentOffset, (int)size).ToList();

        foreach (var item in items)
            base.Items.Add((uint)item);

        currentOffset = (uint)this.Count;

        //_context.Post(s =>
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        //    null);

        OnPropertyChanged(_hasMoreItemsHandler);
        return new LoadMoreItemsResult { Count = (uint)items.Count };
    }

    public async Task EnsureLoadedUpToAsync(uint index)
    {
        while (Count <= index && HasMoreItems)
        {
            uint needed = (index + 1) - (uint)Count;
            uint batch = Math.Max(needed, 1000);
            LoadMoreInternal(batch);
        }
    }

    public bool HasMoreItems => Count < MaxCount;
}
