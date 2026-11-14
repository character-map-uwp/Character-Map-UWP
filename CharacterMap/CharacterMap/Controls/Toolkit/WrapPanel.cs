// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// src: Windows Community Toolkit, v6.1.0.

using Windows.UI.Xaml;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace Microsoft.Toolkit.Uwp.UI.Controls;

/// <summary>
/// Options for how to calculate the layout of <see cref="WrapPanel"/> items.
/// </summary>
public enum StretchChild
{
    /// <summary>
    /// Don't apply any additional stretching logic
    /// </summary>
    None,

    /// <summary>
    /// Make the last child stretch to fill the available space
    /// </summary>
    Last
}

/// <summary>
/// WrapPanel is a panel that position child control vertically or horizontally based on the orientation and when max width / max height is reached a new row (in case of horizontal) or column (in case of vertical) is created to fit new controls.
/// </summary>
[DependencyProperty<double>("HorizontalSpacing", 0d, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<double>("VerticalSpacing", 0d, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<Orientation>("Orientation", Windows.UI.Xaml.Controls.Orientation.Horizontal, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<Thickness>("Padding", "new Thickness(0d)", nameof(OnLayoutPropertyChanged))]
[DependencyProperty<StretchChild>("StretchChild", StretchChild.None, nameof(OnLayoutPropertyChanged))]
[DependencyProperty<int>("MaxColumns", 0, nameof(OnLayoutPropertyChanged))]
public partial class WrapPanel : Panel
{
    void OnLayoutPropertyChanged()
    {
        InvalidateMeasure();
        InvalidateArrange();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        availableSize.Width = availableSize.Width - Padding.Left - Padding.Right;
        availableSize.Height = availableSize.Height - Padding.Top - Padding.Bottom;
        var totalMeasure = UvMeasure.Zero;
        var parentMeasure = new UvMeasure(Orientation, availableSize.Width, availableSize.Height);
        var spacingMeasure = new UvMeasure(Orientation, HorizontalSpacing, VerticalSpacing);
        var lineMeasure = UvMeasure.Zero;

        int itemsInCurrentLine = 0;
        foreach (var child in Children)
        {
            child.Measure(availableSize);
            var currentMeasure = new UvMeasure(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
            if (currentMeasure.U == 0)
            {
                continue; // ignore collapsed items
            }

            // if this is the first item, do not add spacing. Spacing is added to the "left"
            double uChange = lineMeasure.U == 0
                ? currentMeasure.U
                : currentMeasure.U + spacingMeasure.U;
            if (parentMeasure.U >= uChange + lineMeasure.U && (MaxColumns <= 0 || itemsInCurrentLine < MaxColumns))
            {
                lineMeasure.U += uChange;
                lineMeasure.V = Math.Max(lineMeasure.V, currentMeasure.V);
                itemsInCurrentLine++;
            }
            else
            {
                // new line should be added
                // to get the max U to provide it correctly to ui width ex: ---| or -----|
                totalMeasure.U = Math.Max(lineMeasure.U, totalMeasure.U);
                totalMeasure.V += lineMeasure.V + spacingMeasure.V;

                // if the next new row still can handle more controls
                if (parentMeasure.U > currentMeasure.U && (MaxColumns <= 0 || 1 < MaxColumns))
                {
                    // set lineMeasure initial values to the currentMeasure to be calculated later on the new loop
                    lineMeasure = currentMeasure;
                    itemsInCurrentLine = 1;
                }

                // the control will take one row alone
                else
                {
                    // validate the new control measures
                    totalMeasure.U = Math.Max(currentMeasure.U, totalMeasure.U);
                    totalMeasure.V += currentMeasure.V;

                    // add new empty line
                    lineMeasure = UvMeasure.Zero;
                    itemsInCurrentLine = 0;
                }
            }
        }

        // update value with the last line
        // if the the last loop is(parentMeasure.U > currentMeasure.U + lineMeasure.U) the total isn't calculated then calculate it
        // if the last loop is (parentMeasure.U > currentMeasure.U) the currentMeasure isn't added to the total so add it here
        // for the last condition it is zeros so adding it will make no difference
        // this way is faster than an if condition in every loop for checking the last item
        totalMeasure.U = Math.Max(lineMeasure.U, totalMeasure.U);
        totalMeasure.V += lineMeasure.V;

        totalMeasure.U = Math.Ceiling(totalMeasure.U);

        return Orientation == Orientation.Horizontal ? new Size(totalMeasure.U, totalMeasure.V) : new Size(totalMeasure.V, totalMeasure.U);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count > 0)
        {
            var parentMeasure = new UvMeasure(Orientation, finalSize.Width, finalSize.Height);
            var spacingMeasure = new UvMeasure(Orientation, HorizontalSpacing, VerticalSpacing);
            var paddingStart = new UvMeasure(Orientation, Padding.Left, Padding.Top);
            var paddingEnd = new UvMeasure(Orientation, Padding.Right, Padding.Bottom);

            // Track the start position of the current row
            double rowStartU = paddingStart.U;
            double rowStartV = paddingStart.V;

            // Helper to arrange a completed row
            void ArrangeRow(List<UIElement> rowChildren, List<UvMeasure> rowMeasures, double rowHeight, bool isLastRow)
            {
                double currentU = rowStartU;
                for (int idx = 0; idx < rowChildren.Count; idx++)
                {
                    var child = rowChildren[idx];
                    var desired = rowMeasures[idx];

                    // Stretch the last child in the final row if requested
                    if (isLastRow && idx == rowChildren.Count - 1 && StretchChild == StretchChild.Last)
                    {
                        desired.U = parentMeasure.U - currentU - paddingEnd.U;
                    }

                    // Determine alignment offset based on orientation
                    double offset = 0;
                    if (Orientation == Orientation.Vertical && child is FrameworkElement fe)
                    {
                        // Horizontal alignment within a column
                        switch (fe.HorizontalAlignment)
                        {
                            case HorizontalAlignment.Center:
                                offset = (rowHeight - desired.V) / 2;
                                break;
                            case HorizontalAlignment.Right:
                                offset = rowHeight - desired.V;
                                break;
                            // Left (default) results in offset = 0
                        }
                    }
                    // Arrange the child
                    if (Orientation == Orientation.Horizontal)
                    {
                        // Stretch the last child in the final row if requested
                        if (isLastRow && idx == rowChildren.Count - 1 && StretchChild == StretchChild.Last)
                            desired.U = parentMeasure.U - currentU - paddingEnd.U;

                        child.Arrange(new Rect(currentU, rowStartV, desired.U, rowHeight));
                    }
                    else
                    {
                        // For vertical orientation, give the child the full column width (rowHeight) as its width
                        if (isLastRow && idx == rowChildren.Count - 1 && StretchChild == StretchChild.Last)
                            desired.U = parentMeasure.U - currentU - paddingEnd.U;

                        child.Arrange(new Rect(rowStartV + offset, currentU, rowHeight, desired.U));
                    }

                    // Advance U position for next child in the row
                    currentU += desired.U + spacingMeasure.U;
                }
            }

            var rowChildren = new List<UIElement>();
            var rowMeasures = new List<UvMeasure>();
            double currentRowHeight = 0;
            double currentRowU = rowStartU;
            int lastIndex = Children.Count - 1;

            for (int i = 0; i <= lastIndex; i++)
            {
                var child = Children[i];
                var desired = new UvMeasure(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
                if (desired.U == 0)
                {
                    // Skip collapsed items
                    continue;
                }

                // Determine if the child fits in the current row
                double projectedU = currentRowU + (rowChildren.Count > 0 ? spacingMeasure.U : 0) + desired.U + paddingEnd.U;
                bool exceedsMax = MaxColumns > 0 && rowChildren.Count >= MaxColumns;
                if (((projectedU > parentMeasure.U && rowChildren.Count > 0) || exceedsMax) && rowChildren.Count > 0)
                {
                    // Arrange existing row before starting a new one
                    ArrangeRow(rowChildren, rowMeasures, currentRowHeight, false);

                    // Move to next row
                    rowStartV += currentRowHeight + spacingMeasure.V;
                    currentRowU = paddingStart.U;
                    rowChildren.Clear();
                    rowMeasures.Clear();
                    currentRowHeight = 0;
                }

                // Add child to current row
                rowChildren.Add(child);
                rowMeasures.Add(desired);
                currentRowHeight = Math.Max(currentRowHeight, desired.V);
                currentRowU += (rowChildren.Count > 1 ? spacingMeasure.U : 0) + desired.U;
            }

            // Arrange the final row (apply stretch if required)
            if (rowChildren.Count > 0)
            {
                ArrangeRow(rowChildren, rowMeasures, currentRowHeight, true);
            }
        }

        return finalSize;
    }



    [System.Diagnostics.DebuggerDisplay("U = {U} V = {V}")]
    private struct UvMeasure
    {
        internal static UvMeasure Zero => default;

        internal double U { get; set; }

        internal double V { get; set; }

        public UvMeasure(Orientation orientation, Size size)
            : this(orientation, size.Width, size.Height)
        {
        }

        public UvMeasure(Orientation orientation, double width, double height)
        {
            if (orientation == Orientation.Horizontal)
            {
                U = width;
                V = height;
            }
            else
            {
                U = height;
                V = width;
            }
        }

        public UvMeasure Add(double u, double v)
            => new UvMeasure { U = U + u, V = V + v };

        public UvMeasure Add(UvMeasure measure)
            => Add(measure.U, measure.V);

        public Size ToSize(Orientation orientation)
            => orientation == Orientation.Horizontal ? new Size(U, V) : new Size(V, U);
    }

    private struct UvRect
    {
        public UvMeasure Position { get; set; }

        public UvMeasure Size { get; set; }

        public Rect ToRect(Orientation orientation) => orientation switch
        {
            Orientation.Vertical => new Rect(Position.V, Position.U, Size.V, Size.U),
            Orientation.Horizontal => new Rect(Position.U, Position.V, Size.U, Size.V),
            _ => throw new NotSupportedException("unsupported orientation"),
        };
    }

    private struct Row
    {
        public Row(List<UvRect> childrenRects, UvMeasure size)
        {
            ChildrenRects = childrenRects;
            Size = size;
        }

        public List<UvRect> ChildrenRects { get; }

        public UvMeasure Size { get; set; }

        public UvRect Rect => ChildrenRects.Count > 0 ?
            new UvRect { Position = ChildrenRects[0].Position, Size = Size } :
            new UvRect { Position = UvMeasure.Zero, Size = Size };

        public void Add(UvMeasure position, UvMeasure size)
        {
            ChildrenRects.Add(new UvRect { Position = position, Size = size });
            Size = new UvMeasure
            {
                U = position.U + size.U,
                V = Math.Max(Size.V, size.V),
            };
        }
    }
}