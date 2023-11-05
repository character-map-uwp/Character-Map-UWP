using System.Windows.Input;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace Microsoft.Toolkit.Uwp.UI.Controls;

/// <summary>
/// The AdaptiveGridView control allows to present information within a Grid View perfectly adjusting the
/// total display available space. It reacts to changes in the layout as well as the content so it can adapt
/// to different form factors automatically.
/// </summary>
/// <remarks>
/// The number and the width of items are calculated based on the
/// screen resolution in order to fully leverage the available screen space. The property ItemsHeight define
/// the items fixed height and the property DesiredWidth sets the minimum width for the elements to add a
/// new column.</remarks>
public class AdaptiveGridView : GridView
{
    private bool _needContainerMarginForLayout;
    private Binding _heightBinding;

    #region Properties

    /// <summary>
    /// Identifies the <see cref="ItemClickCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemClickCommandProperty =
        DependencyProperty.Register(nameof(ItemClickCommand), typeof(ICommand), typeof(AdaptiveGridView), new PropertyMetadata(null));

    /// <summary>
    /// Identifies the <see cref="ItemHeight"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemHeightProperty =
        DependencyProperty.Register(nameof(ItemHeight), typeof(double), typeof(AdaptiveGridView), new PropertyMetadata(double.NaN));

    /// <summary>
    /// Identifies the <see cref="ItemWidth"/> dependency property.
    /// </summary>
    private static readonly DependencyProperty ItemWidthProperty =
        DependencyProperty.Register(nameof(ItemWidth), typeof(double), typeof(AdaptiveGridView), new PropertyMetadata(double.NaN));

    /// <summary>
    /// Identifies the <see cref="DesiredWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DesiredWidthProperty =
        DependencyProperty.Register(nameof(DesiredWidth), typeof(double), typeof(AdaptiveGridView), new PropertyMetadata(double.NaN, DesiredWidthChanged));

    /// <summary>
    /// Identifies the <see cref="StretchContentForSingleRow"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty StretchContentForSingleRowProperty =
    DependencyProperty.Register(nameof(StretchContentForSingleRow), typeof(bool), typeof(AdaptiveGridView), new PropertyMetadata(false, OnStretchContentForSingleRowPropertyChanged));

    private static void DesiredWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = d as AdaptiveGridView;
        self.RecalculateLayout(self.ActualWidth);
    }

    private static void OnStretchContentForSingleRowPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = d as AdaptiveGridView;
        self.RecalculateLayout(self.ActualWidth);
    }

    /// <summary>
    /// Gets or sets the desired width of each item
    /// </summary>
    /// <value>The width of the desired.</value>
    public double DesiredWidth
    {
        get { return (double)GetValue(DesiredWidthProperty); }
        set { SetValue(DesiredWidthProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the control should stretch the content to fill at least one row.
    /// </summary>
    /// <remarks>
    /// If set to <c>true</c> (default) and there is only one row of items, the items will be stretched to fill the complete row.
    /// If set to <c>false</c>, items will have their normal size, which means a gap can exist at the end of the row.
    /// </remarks>
    /// <value>A value indicating whether the control should stretch the content to fill at least one row.</value>
    public bool StretchContentForSingleRow
    {
        get { return (bool)GetValue(StretchContentForSingleRowProperty); }
        set { SetValue(StretchContentForSingleRowProperty, value); }
    }

    /// <summary>
    /// Gets or sets the command to execute when an item is clicked and the IsItemClickEnabled property is true.
    /// </summary>
    /// <value>The item click command.</value>
    public ICommand ItemClickCommand
    {
        get { return (ICommand)GetValue(ItemClickCommandProperty); }
        set { SetValue(ItemClickCommandProperty, value); }
    }

    /// <summary>
    /// Gets or sets the height of each item in the grid.
    /// </summary>
    /// <value>The height of the item.</value>
    public double ItemHeight
    {
        get { return (double)GetValue(ItemHeightProperty); }
        set { SetValue(ItemHeightProperty, value); }
    }

    private double ItemWidth
    {
        get { return (double)GetValue(ItemWidthProperty); }
        set { SetValue(ItemWidthProperty, value); }
    }

    private static int CalculateColumns(double containerWidth, double itemWidth)
    {
        var columns = (int)Math.Round(containerWidth / itemWidth);
        if (columns == 0)
        {
            columns = 1;
        }

        return columns;
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveGridView"/> class.
    /// </summary>
    public AdaptiveGridView()
    {
        IsTabStop = false;
        SizeChanged += OnSizeChanged;
        ItemClick += OnItemClick;
        Items.VectorChanged += ItemsOnVectorChanged;

        _heightBinding = new Binding()
        {
            Source = this,
            Path = new (nameof(ItemHeight)),
        };

        // Prevent issues with higher DPIs and underlying panel. #1803
        UseLayoutRounding = false;
    }

    /// <summary>
    /// Prepares the specified element to display the specified item.
    /// </summary>
    /// <param name="obj">The element that's used to display the specified item.</param>
    /// <param name="item">The item to display.</param>
    protected override void PrepareContainerForItemOverride(DependencyObject obj, object item)
    {
        base.PrepareContainerForItemOverride(obj, item);
        if (obj is FrameworkElement element)
        {
            element.SetBinding(HeightProperty, _heightBinding);
            element.Width = ItemWidth;
        }

        if (obj is ContentControl contentControl)
        {
            contentControl.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            contentControl.VerticalContentAlignment = VerticalAlignment.Stretch;
        }

        if (_needContainerMarginForLayout)
        {
            _needContainerMarginForLayout = false;
            RecalculateLayout(ActualWidth);
        }
    }

    void UpdateWidths()
    {
        if (this.ItemsPanelRoot is null)
            return;

        foreach (var item in this.ItemsPanelRoot.Children.OfType<GridViewItem>())
            item.Width = this.ItemWidth;
    }

    /// <summary>
    /// Calculates the width of the grid items.
    /// </summary>
    /// <param name="containerWidth">The width of the container control.</param>
    /// <returns>The calculated item width.</returns>
    protected virtual double CalculateItemWidth(double containerWidth)
    {
        if (double.IsNaN(DesiredWidth))
        {
            return DesiredWidth;
        }

        var columns = CalculateColumns(containerWidth, DesiredWidth);

        // If there's less items than there's columns, reduce the column count (if requested);
        if (Items != null && Items.Count > 0 && Items.Count < columns && StretchContentForSingleRow)
        {
            columns = Items.Count;
        }

        // subtract the margin from the width so we place the correct width for placement
        var fallbackThickness = default(Thickness);
        var itemMargin = AdaptiveHeightValueConverter.GetItemMargin(this, fallbackThickness);
        if (itemMargin == fallbackThickness)
        {
            // No style explicitly defined, or no items or no container for the items
            // We need to get an actual margin for proper layout
            _needContainerMarginForLayout = true;
        }

        return (containerWidth / columns) - itemMargin.Left - itemMargin.Right;
    }

    private void ItemsOnVectorChanged(IObservableVector<object> sender, IVectorChangedEventArgs @event)
    {
        if (!double.IsNaN(ActualWidth))
        {
            // If the item count changes, check if more or less columns needs to be rendered,
            // in case we were having fewer items than columns.
            RecalculateLayout(ActualWidth);
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        var cmd = ItemClickCommand;
        if (cmd != null)
        {
            if (cmd.CanExecute(e.ClickedItem))
            {
                cmd.Execute(e.ClickedItem);
            }
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // If we are in center alignment, we only care about relayout if the number of columns we can display changes
        // Fixes #1737
        if (HorizontalAlignment != HorizontalAlignment.Stretch)
        {
            var prevColumns = CalculateColumns(e.PreviousSize.Width, DesiredWidth);
            var newColumns = CalculateColumns(e.NewSize.Width, DesiredWidth);

            // If the width of the internal list view changes, check if more or less columns needs to be rendered.
            if (prevColumns != newColumns)
            {
                RecalculateLayout(e.NewSize.Width);
            }
        }
        else if (e.PreviousSize.Width != e.NewSize.Width)
        {
            // We need to recalculate width as our size changes to adjust internal items.
            RecalculateLayout(e.NewSize.Width);
        }
    }

    private void RecalculateLayout(double containerWidth)
    {
        var itemsPanel = ItemsPanelRoot as Panel;
        var panelMargin = itemsPanel != null ?
                          itemsPanel.Margin.Left + itemsPanel.Margin.Right :
                          0;
        var padding = Padding.Left + Padding.Right;
        var border = BorderThickness.Left + BorderThickness.Right;

        // width should be the displayable width
        containerWidth = containerWidth - padding - panelMargin - border;
        if (containerWidth > 0)
        {
            var newWidth = CalculateItemWidth(containerWidth);
            ItemWidth = Math.Floor(newWidth);
            UpdateWidths();
        }
    }
}

internal class AdaptiveHeightValueConverter : IValueConverter
{
    private Thickness thickness = new (0, 0, 4, 4);

    public Thickness DefaultItemMargin
    {
        get { return thickness; }
        set { thickness = value; }
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value != null)
        {
            var gridView = (GridView)parameter;
            if (gridView == null)
            {
                return value;
            }

            double.TryParse(value.ToString(), out double height);

            var padding = gridView.Padding;
            var margin = GetItemMargin(gridView, DefaultItemMargin);
            height = height + margin.Top + margin.Bottom + padding.Top + padding.Bottom;

            return height;
        }

        return double.NaN;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    internal static Thickness GetItemMargin(GridView view, Thickness fallback = default(Thickness))
    {
        var setter = view.ItemContainerStyle?.Setters.OfType<Setter>().FirstOrDefault(s => s.Property == FrameworkElement.MarginProperty);
        if (setter != null)
        {
            return (Thickness)setter.Value;
        }
        else
        {
            if (view.Items.Count > 0)
            {
                var container = (GridViewItem)view.ContainerFromIndex(0);
                if (container != null)
                {
                    return container.Margin;
                }
            }

            // Use the default thickness for a GridViewItem
            return fallback;
        }
    }
}