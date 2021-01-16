// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// src: Windows Community Toolkit, v6.1.0.

using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Microsoft.Toolkit.Uwp.UI.Controls
{
    /// <summary>
    /// Represents the control that redistributes space between columns or rows of a Grid control.
    /// </summary>
    public partial class GridSplitter : Control
    {
        internal const int GripperCustomCursorDefaultResource = -1;
        internal static readonly CoreCursor ColumnsSplitterCursor = new CoreCursor(CoreCursorType.SizeWestEast, 1);
        internal static readonly CoreCursor RowSplitterCursor = new CoreCursor(CoreCursorType.SizeNorthSouth, 1);

        internal CoreCursor PreviousCursor { get; set; }

        private GridResizeDirection _resizeDirection;
        private GridResizeBehavior _resizeBehavior;
        private GripperHoverWrapper _hoverWrapper;
        private TextBlock _gripperDisplay;

        private bool _pressed = false;
        private bool _dragging = false;
        private bool _pointerEntered = false;

        /// <summary>
        /// Gets the target parent grid from level
        /// </summary>
        private FrameworkElement TargetControl
        {
            get
            {
                if (ParentLevel == 0)
                {
                    return this;
                }

                var parent = Parent;
                for (int i = 2; i < ParentLevel; i++)
                {
                    if (parent is FrameworkElement frameworkElement)
                    {
                        parent = frameworkElement.Parent;
                    }
                }

                return parent as FrameworkElement;
            }
        }

        /// <summary>
        /// Gets GridSplitter Container Grid
        /// </summary>
        private Grid Resizable => TargetControl?.Parent as Grid;

        /// <summary>
        /// Gets the current Column definition of the parent Grid
        /// </summary>
        private ColumnDefinition CurrentColumn
        {
            get
            {
                if (Resizable == null)
                {
                    return null;
                }

                var gridSplitterTargetedColumnIndex = GetTargetedColumn();

                if ((gridSplitterTargetedColumnIndex >= 0)
                    && (gridSplitterTargetedColumnIndex < Resizable.ColumnDefinitions.Count))
                {
                    return Resizable.ColumnDefinitions[gridSplitterTargetedColumnIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the Sibling Column definition of the parent Grid
        /// </summary>
        private ColumnDefinition SiblingColumn
        {
            get
            {
                if (Resizable == null)
                {
                    return null;
                }

                var gridSplitterSiblingColumnIndex = GetSiblingColumn();

                if ((gridSplitterSiblingColumnIndex >= 0)
                    && (gridSplitterSiblingColumnIndex < Resizable.ColumnDefinitions.Count))
                {
                    return Resizable.ColumnDefinitions[gridSplitterSiblingColumnIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the current Row definition of the parent Grid
        /// </summary>
        private RowDefinition CurrentRow
        {
            get
            {
                if (Resizable == null)
                {
                    return null;
                }

                var gridSplitterTargetedRowIndex = GetTargetedRow();

                if ((gridSplitterTargetedRowIndex >= 0)
                    && (gridSplitterTargetedRowIndex < Resizable.RowDefinitions.Count))
                {
                    return Resizable.RowDefinitions[gridSplitterTargetedRowIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the Sibling Row definition of the parent Grid
        /// </summary>
        private RowDefinition SiblingRow
        {
            get
            {
                if (Resizable == null)
                {
                    return null;
                }

                var gridSplitterSiblingRowIndex = GetSiblingRow();

                if ((gridSplitterSiblingRowIndex >= 0)
                    && (gridSplitterSiblingRowIndex < Resizable.RowDefinitions.Count))
                {
                    return Resizable.RowDefinitions[gridSplitterSiblingRowIndex];
                }

                return null;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GridSplitter"/> class.
        /// </summary>
        public GridSplitter()
        {
            DefaultStyleKey = typeof(GridSplitter);
            Loaded += GridSplitter_Loaded;
            string automationName = "Grid Splitter";
            AutomationProperties.SetName(this, automationName);
        }

        /// <inheritdoc />
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Unhook registered events
            Loaded -= GridSplitter_Loaded;
            PointerEntered -= GridSplitter_PointerEntered;
            PointerExited -= GridSplitter_PointerExited;
            PointerPressed -= GridSplitter_PointerPressed;
            PointerReleased -= GridSplitter_PointerReleased;
            ManipulationStarted -= GridSplitter_ManipulationStarted;
            ManipulationCompleted -= GridSplitter_ManipulationCompleted;

            _hoverWrapper?.UnhookEvents();

            // Register Events
            Loaded += GridSplitter_Loaded;
            PointerEntered += GridSplitter_PointerEntered;
            PointerExited += GridSplitter_PointerExited;
            PointerPressed += GridSplitter_PointerPressed;
            PointerReleased += GridSplitter_PointerReleased;
            ManipulationStarted += GridSplitter_ManipulationStarted;
            ManipulationCompleted += GridSplitter_ManipulationCompleted;

            _hoverWrapper?.UpdateHoverElement(Element);

            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY;
        }

        private void GridSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _pressed = false;
            VisualStateManager.GoToState(this, _pointerEntered ? "PointerOver" : "Normal", true);
        }

        private void GridSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _pressed = true;
            VisualStateManager.GoToState(this, "Pressed", true);
        }

        private void GridSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _pointerEntered = false;

            if (!_pressed && !_dragging)
            {
                VisualStateManager.GoToState(this, "Normal", true);
            }
        }

        private void GridSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            _pointerEntered = true;

            if (!_pressed && !_dragging)
            {
                VisualStateManager.GoToState(this, "PointerOver", true);
            }
        }

        private void GridSplitter_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            _dragging = false;
            _pressed = false;
            VisualStateManager.GoToState(this, _pointerEntered ? "PointerOver" : "Normal", true);
        }

        private void GridSplitter_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            _dragging = true;
            VisualStateManager.GoToState(this, "Pressed", true);
        }

        #region GridSplitter.Data

        /// <summary>
        /// Enum to indicate whether GridSplitter resizes Columns or Rows
        /// </summary>
        public enum GridResizeDirection
        {
            /// <summary>
            /// Determines whether to resize rows or columns based on its Alignment and
            /// width compared to height
            /// </summary>
            Auto,

            /// <summary>
            /// Resize columns when dragging Splitter.
            /// </summary>
            Columns,

            /// <summary>
            /// Resize rows when dragging Splitter.
            /// </summary>
            Rows
        }

        /// <summary>
        /// Enum to indicate what Columns or Rows the GridSplitter resizes
        /// </summary>
        public enum GridResizeBehavior
        {
            /// <summary>
            /// Determine which columns or rows to resize based on its Alignment.
            /// </summary>
            BasedOnAlignment,

            /// <summary>
            /// Resize the current and next Columns or Rows.
            /// </summary>
            CurrentAndNext,

            /// <summary>
            /// Resize the previous and current Columns or Rows.
            /// </summary>
            PreviousAndCurrent,

            /// <summary>
            /// Resize the previous and next Columns or Rows.
            /// </summary>
            PreviousAndNext
        }

        /// <summary>
        ///  Enum to indicate the supported gripper cursor types.
        /// </summary>
        public enum GripperCursorType
        {
            /// <summary>
            /// Change the cursor based on the splitter direction
            /// </summary>
            Default = -1,

            /// <summary>
            /// Standard Arrow cursor
            /// </summary>
            Arrow,

            /// <summary>
            /// Standard Cross cursor
            /// </summary>
            Cross,

            /// <summary>
            /// Standard Custom cursor
            /// </summary>
            Custom,

            /// <summary>
            /// Standard Hand cursor
            /// </summary>
            Hand,

            /// <summary>
            /// Standard Help cursor
            /// </summary>
            Help,

            /// <summary>
            /// Standard IBeam cursor
            /// </summary>
            IBeam,

            /// <summary>
            /// Standard SizeAll cursor
            /// </summary>
            SizeAll,

            /// <summary>
            /// Standard SizeNortheastSouthwest cursor
            /// </summary>
            SizeNortheastSouthwest,

            /// <summary>
            /// Standard SizeNorthSouth cursor
            /// </summary>
            SizeNorthSouth,

            /// <summary>
            /// Standard SizeNorthwestSoutheast cursor
            /// </summary>
            SizeNorthwestSoutheast,

            /// <summary>
            /// Standard SizeWestEast cursor
            /// </summary>
            SizeWestEast,

            /// <summary>
            /// Standard UniversalNo cursor
            /// </summary>
            UniversalNo,

            /// <summary>
            /// Standard UpArrow cursor
            /// </summary>
            UpArrow,

            /// <summary>
            /// Standard Wait cursor
            /// </summary>
            Wait
        }

        /// <summary>
        ///  Enum to indicate the behavior of window cursor on grid splitter hover
        /// </summary>
        public enum SplitterCursorBehavior
        {
            /// <summary>
            /// Update window cursor on Grid Splitter hover
            /// </summary>
            ChangeOnSplitterHover,

            /// <summary>
            /// Update window cursor on Grid Splitter Gripper hover
            /// </summary>
            ChangeOnGripperHover
        }

        #endregion

        #region GridSplitter.Events

        // Symbols for GripperBar in Segoe MDL2 Assets
        private const string GripperBarVertical = "\xE784";
        private const string GripperBarHorizontal = "\xE76F";
        private const string GripperDisplayFont = "Segoe MDL2 Assets";

        private void GridSplitter_Loaded(object sender, RoutedEventArgs e)
        {
            _resizeDirection = GetResizeDirection();
            _resizeBehavior = GetResizeBehavior();

            // Adding Grip to Grid Splitter
            if (Element == default(UIElement))
            {
                CreateGripperDisplay();
                Element = _gripperDisplay;
            }

            if (_hoverWrapper == null)
            {
                var hoverWrapper = new GripperHoverWrapper(
                    CursorBehavior == SplitterCursorBehavior.ChangeOnSplitterHover
                    ? this
                    : Element,
                    _resizeDirection,
                    GripperCursor,
                    GripperCustomCursorResource);
                ManipulationStarted += hoverWrapper.SplitterManipulationStarted;
                ManipulationCompleted += hoverWrapper.SplitterManipulationCompleted;

                _hoverWrapper = hoverWrapper;
            }
        }

        private void CreateGripperDisplay()
        {
            if (_gripperDisplay == null)
            {
                _gripperDisplay = new TextBlock
                {
                    FontFamily = new FontFamily(GripperDisplayFont),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GripperForeground,
                    Text = _resizeDirection == GridResizeDirection.Columns ? GripperBarVertical : GripperBarHorizontal
                };
            }
        }

        /// <inheritdoc />
        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            var step = 1;
            var ctrl = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
            if (ctrl.HasFlag(CoreVirtualKeyStates.Down))
            {
                step = 5;
            }

            if (_resizeDirection == GridResizeDirection.Columns)
            {
                if (e.Key == VirtualKey.Left)
                {
                    HorizontalMove(-step);
                }
                else if (e.Key == VirtualKey.Right)
                {
                    HorizontalMove(step);
                }
                else
                {
                    return;
                }

                e.Handled = true;
                return;
            }

            if (_resizeDirection == GridResizeDirection.Rows)
            {
                if (e.Key == VirtualKey.Up)
                {
                    VerticalMove(-step);
                }
                else if (e.Key == VirtualKey.Down)
                {
                    VerticalMove(step);
                }
                else
                {
                    return;
                }

                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        /// <inheritdoc />
        protected override void OnManipulationStarted(ManipulationStartedRoutedEventArgs e)
        {
            // saving the previous state
            PreviousCursor = Window.Current.CoreWindow.PointerCursor;
            _resizeDirection = GetResizeDirection();
            _resizeBehavior = GetResizeBehavior();

            if (_resizeDirection == GridResizeDirection.Columns)
            {
                Window.Current.CoreWindow.PointerCursor = ColumnsSplitterCursor;
            }
            else if (_resizeDirection == GridResizeDirection.Rows)
            {
                Window.Current.CoreWindow.PointerCursor = RowSplitterCursor;
            }

            base.OnManipulationStarted(e);
        }

        /// <inheritdoc />
        protected override void OnManipulationCompleted(ManipulationCompletedRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = PreviousCursor;

            base.OnManipulationCompleted(e);
        }

        /// <inheritdoc />
        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            var horizontalChange = e.Delta.Translation.X;
            var verticalChange = e.Delta.Translation.Y;

            if (_resizeDirection == GridResizeDirection.Columns)
            {
                if (HorizontalMove(horizontalChange))
                {
                    return;
                }
            }
            else if (_resizeDirection == GridResizeDirection.Rows)
            {
                if (VerticalMove(verticalChange))
                {
                    return;
                }
            }

            base.OnManipulationDelta(e);
        }

        private bool VerticalMove(double verticalChange)
        {
            if (CurrentRow == null || SiblingRow == null)
            {
                return true;
            }

            // if current row has fixed height then resize it
            if (!IsStarRow(CurrentRow))
            {
                // No need to check for the row Min height because it is automatically respected
                if (!SetRowHeight(CurrentRow, verticalChange, GridUnitType.Pixel))
                {
                    return true;
                }
            }

            // if sibling row has fixed width then resize it
            else if (!IsStarRow(SiblingRow))
            {
                // Would adding to this column make the current column violate the MinWidth?
                if (IsValidRowHeight(CurrentRow, verticalChange) == false)
                {
                    return false;
                }

                if (!SetRowHeight(SiblingRow, verticalChange * -1, GridUnitType.Pixel))
                {
                    return true;
                }
            }

            // if both row haven't fixed height (auto *)
            else
            {
                // change current row height to the new height with respecting the auto
                // change sibling row height to the new height relative to current row
                // respect the other star row height by setting it's height to it's actual height with stars

                // We need to validate current and sibling height to not cause any unexpected behavior
                if (!IsValidRowHeight(CurrentRow, verticalChange) ||
                    !IsValidRowHeight(SiblingRow, verticalChange * -1))
                {
                    return true;
                }

                foreach (var rowDefinition in Resizable.RowDefinitions)
                {
                    if (rowDefinition == CurrentRow)
                    {
                        SetRowHeight(CurrentRow, verticalChange, GridUnitType.Star);
                    }
                    else if (rowDefinition == SiblingRow)
                    {
                        SetRowHeight(SiblingRow, verticalChange * -1, GridUnitType.Star);
                    }
                    else if (IsStarRow(rowDefinition))
                    {
                        rowDefinition.Height = new GridLength(rowDefinition.ActualHeight, GridUnitType.Star);
                    }
                }
            }

            return false;
        }

        private bool HorizontalMove(double horizontalChange)
        {
            if (CurrentColumn == null || SiblingColumn == null)
            {
                return true;
            }

            // if current column has fixed width then resize it
            if (!IsStarColumn(CurrentColumn))
            {
                // No need to check for the Column Min width because it is automatically respected
                if (!SetColumnWidth(CurrentColumn, horizontalChange, GridUnitType.Pixel))
                {
                    return true;
                }
            }

            // if sibling column has fixed width then resize it
            else if (!IsStarColumn(SiblingColumn))
            {
                // Would adding to this column make the current column violate the MinWidth?
                if (IsValidColumnWidth(CurrentColumn, horizontalChange) == false)
                {
                    return false;
                }

                if (!SetColumnWidth(SiblingColumn, horizontalChange * -1, GridUnitType.Pixel))
                {
                    return true;
                }
            }

            // if both column haven't fixed width (auto *)
            else
            {
                // change current column width to the new width with respecting the auto
                // change sibling column width to the new width relative to current column
                // respect the other star column width by setting it's width to it's actual width with stars

                // We need to validate current and sibling width to not cause any unexpected behavior
                if (!IsValidColumnWidth(CurrentColumn, horizontalChange) ||
                    !IsValidColumnWidth(SiblingColumn, horizontalChange * -1))
                {
                    return true;
                }

                foreach (var columnDefinition in Resizable.ColumnDefinitions)
                {
                    if (columnDefinition == CurrentColumn)
                    {
                        SetColumnWidth(CurrentColumn, horizontalChange, GridUnitType.Star);
                    }
                    else if (columnDefinition == SiblingColumn)
                    {
                        SetColumnWidth(SiblingColumn, horizontalChange * -1, GridUnitType.Star);
                    }
                    else if (IsStarColumn(columnDefinition))
                    {
                        columnDefinition.Width = new GridLength(columnDefinition.ActualWidth, GridUnitType.Star);
                    }
                }
            }

            return false;
        }

        #endregion

        #region GridSplitter.Heplers

        /// <summary>
        /// Represents the control that redistributes space between columns or rows of a Grid control.
        /// </summary>
        private static bool IsStarColumn(ColumnDefinition definition)
        {
            return ((GridLength)definition.GetValue(ColumnDefinition.WidthProperty)).IsStar;
        }

        private static bool IsStarRow(RowDefinition definition)
        {
            return ((GridLength)definition.GetValue(RowDefinition.HeightProperty)).IsStar;
        }

        private bool SetColumnWidth(ColumnDefinition columnDefinition, double horizontalChange, GridUnitType unitType)
        {
            var newWidth = columnDefinition.ActualWidth + horizontalChange;

            var minWidth = columnDefinition.MinWidth;
            if (!double.IsNaN(minWidth) && newWidth < minWidth)
            {
                newWidth = minWidth;
            }

            var maxWidth = columnDefinition.MaxWidth;
            if (!double.IsNaN(maxWidth) && newWidth > maxWidth)
            {
                newWidth = maxWidth;
            }

            if (newWidth > ActualWidth)
            {
                columnDefinition.Width = new GridLength(newWidth, unitType);
                return true;
            }

            return false;
        }

        private bool IsValidColumnWidth(ColumnDefinition columnDefinition, double horizontalChange)
        {
            var newWidth = columnDefinition.ActualWidth + horizontalChange;

            var minWidth = columnDefinition.MinWidth;
            if (!double.IsNaN(minWidth) && newWidth < minWidth)
            {
                return false;
            }

            var maxWidth = columnDefinition.MaxWidth;
            if (!double.IsNaN(maxWidth) && newWidth > maxWidth)
            {
                return false;
            }

            if (newWidth <= ActualWidth)
            {
                return false;
            }

            return true;
        }

        private bool SetRowHeight(RowDefinition rowDefinition, double verticalChange, GridUnitType unitType)
        {
            var newHeight = rowDefinition.ActualHeight + verticalChange;

            var minHeight = rowDefinition.MinHeight;
            if (!double.IsNaN(minHeight) && newHeight < minHeight)
            {
                newHeight = minHeight;
            }

            var maxWidth = rowDefinition.MaxHeight;
            if (!double.IsNaN(maxWidth) && newHeight > maxWidth)
            {
                newHeight = maxWidth;
            }

            if (newHeight > ActualHeight)
            {
                rowDefinition.Height = new GridLength(newHeight, unitType);
                return true;
            }

            return false;
        }

        private bool IsValidRowHeight(RowDefinition rowDefinition, double verticalChange)
        {
            var newHeight = rowDefinition.ActualHeight + verticalChange;

            var minHeight = rowDefinition.MinHeight;
            if (!double.IsNaN(minHeight) && newHeight < minHeight)
            {
                return false;
            }

            var maxHeight = rowDefinition.MaxHeight;
            if (!double.IsNaN(maxHeight) && newHeight > maxHeight)
            {
                return false;
            }

            if (newHeight <= ActualHeight)
            {
                return false;
            }

            return true;
        }

        // Return the targeted Column based on the resize behavior
        private int GetTargetedColumn()
        {
            var currentIndex = Grid.GetColumn(TargetControl);
            return GetTargetIndex(currentIndex);
        }

        // Return the sibling Row based on the resize behavior
        private int GetTargetedRow()
        {
            var currentIndex = Grid.GetRow(TargetControl);
            return GetTargetIndex(currentIndex);
        }

        // Return the sibling Column based on the resize behavior
        private int GetSiblingColumn()
        {
            var currentIndex = Grid.GetColumn(TargetControl);
            return GetSiblingIndex(currentIndex);
        }

        // Return the sibling Row based on the resize behavior
        private int GetSiblingRow()
        {
            var currentIndex = Grid.GetRow(TargetControl);
            return GetSiblingIndex(currentIndex);
        }

        // Gets index based on resize behavior for first targeted row/column
        private int GetTargetIndex(int currentIndex)
        {
            switch (_resizeBehavior)
            {
                case GridResizeBehavior.CurrentAndNext:
                    return currentIndex;
                case GridResizeBehavior.PreviousAndNext:
                    return currentIndex - 1;
                case GridResizeBehavior.PreviousAndCurrent:
                    return currentIndex - 1;
                default:
                    return -1;
            }
        }

        // Gets index based on resize behavior for second targeted row/column
        private int GetSiblingIndex(int currentIndex)
        {
            switch (_resizeBehavior)
            {
                case GridResizeBehavior.CurrentAndNext:
                    return currentIndex + 1;
                case GridResizeBehavior.PreviousAndNext:
                    return currentIndex + 1;
                case GridResizeBehavior.PreviousAndCurrent:
                    return currentIndex;
                default:
                    return -1;
            }
        }

        // Checks the control alignment and Width/Height to detect the control resize direction columns/rows
        private GridResizeDirection GetResizeDirection()
        {
            GridResizeDirection direction = ResizeDirection;

            if (direction == GridResizeDirection.Auto)
            {
                // When HorizontalAlignment is Left, Right or Center, resize Columns
                if (HorizontalAlignment != HorizontalAlignment.Stretch)
                {
                    direction = GridResizeDirection.Columns;
                }

                // When VerticalAlignment is Top, Bottom or Center, resize Rows
                else if (VerticalAlignment != VerticalAlignment.Stretch)
                {
                    direction = GridResizeDirection.Rows;
                }

                // Check Width vs Height
                else if (ActualWidth <= ActualHeight)
                {
                    direction = GridResizeDirection.Columns;
                }
                else
                {
                    direction = GridResizeDirection.Rows;
                }
            }

            return direction;
        }

        // Get the resize behavior (Which columns/rows should be resized) based on alignment and Direction
        private GridResizeBehavior GetResizeBehavior()
        {
            GridResizeBehavior resizeBehavior = ResizeBehavior;

            if (resizeBehavior == GridResizeBehavior.BasedOnAlignment)
            {
                if (_resizeDirection == GridResizeDirection.Columns)
                {
                    switch (HorizontalAlignment)
                    {
                        case HorizontalAlignment.Left:
                            resizeBehavior = GridResizeBehavior.PreviousAndCurrent;
                            break;
                        case HorizontalAlignment.Right:
                            resizeBehavior = GridResizeBehavior.CurrentAndNext;
                            break;
                        default:
                            resizeBehavior = GridResizeBehavior.PreviousAndNext;
                            break;
                    }
                }

                // resize direction is vertical
                else
                {
                    switch (VerticalAlignment)
                    {
                        case VerticalAlignment.Top:
                            resizeBehavior = GridResizeBehavior.PreviousAndCurrent;
                            break;
                        case VerticalAlignment.Bottom:
                            resizeBehavior = GridResizeBehavior.CurrentAndNext;
                            break;
                        default:
                            resizeBehavior = GridResizeBehavior.PreviousAndNext;
                            break;
                    }
                }
            }

            return resizeBehavior;
        }

        #endregion

        #region GripSplitter.Options

        /// <summary>
        /// Identifies the <see cref="Element"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ElementProperty
            = DependencyProperty.Register(
                nameof(Element),
                typeof(UIElement),
                typeof(GridSplitter),
                new PropertyMetadata(default(UIElement), OnElementPropertyChanged));

        /// <summary>
        /// Identifies the <see cref="ResizeDirection"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ResizeDirectionProperty
            = DependencyProperty.Register(
                nameof(ResizeDirection),
                typeof(GridResizeDirection),
                typeof(GridSplitter),
                new PropertyMetadata(GridResizeDirection.Auto));

        /// <summary>
        /// Identifies the <see cref="ResizeBehavior"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ResizeBehaviorProperty
            = DependencyProperty.Register(
                nameof(ResizeBehavior),
                typeof(GridResizeBehavior),
                typeof(GridSplitter),
                new PropertyMetadata(GridResizeBehavior.BasedOnAlignment));

        /// <summary>
        /// Identifies the <see cref="GripperForeground"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GripperForegroundProperty
            = DependencyProperty.Register(
                nameof(GripperForeground),
                typeof(Brush),
                typeof(GridSplitter),
                new PropertyMetadata(default(Brush), OnGripperForegroundPropertyChanged));

        /// <summary>
        /// Identifies the <see cref="ParentLevel"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ParentLevelProperty
            = DependencyProperty.Register(
                nameof(ParentLevel),
                typeof(int),
                typeof(GridSplitter),
                new PropertyMetadata(default(int)));

        /// <summary>
        /// Identifies the <see cref="GripperCursor"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GripperCursorProperty =
            DependencyProperty.RegisterAttached(
                nameof(GripperCursor),
                typeof(CoreCursorType?),
                typeof(GridSplitter),
                new PropertyMetadata(GripperCursorType.Default, OnGripperCursorPropertyChanged));

        /// <summary>
        /// Identifies the <see cref="GripperCustomCursorResource"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty GripperCustomCursorResourceProperty =
            DependencyProperty.RegisterAttached(
                nameof(GripperCustomCursorResource),
                typeof(uint),
                typeof(GridSplitter),
                new PropertyMetadata(GripperCustomCursorDefaultResource, GripperCustomCursorResourcePropertyChanged));

        /// <summary>
        /// Identifies the <see cref="CursorBehavior"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CursorBehaviorProperty =
            DependencyProperty.RegisterAttached(
                nameof(CursorBehavior),
                typeof(SplitterCursorBehavior),
                typeof(GridSplitter),
                new PropertyMetadata(SplitterCursorBehavior.ChangeOnSplitterHover, CursorBehaviorPropertyChanged));

        /// <summary>
        /// Gets or sets the visual content of this Grid Splitter
        /// </summary>
        public UIElement Element
        {
            get { return (UIElement)GetValue(ElementProperty); }
            set { SetValue(ElementProperty, value); }
        }

        /// <summary>
        /// Gets or sets whether the Splitter resizes the Columns, Rows, or Both.
        /// </summary>
        public GridResizeDirection ResizeDirection
        {
            get { return (GridResizeDirection)GetValue(ResizeDirectionProperty); }

            set { SetValue(ResizeDirectionProperty, value); }
        }

        /// <summary>
        /// Gets or sets which Columns or Rows the Splitter resizes.
        /// </summary>
        public GridResizeBehavior ResizeBehavior
        {
            get { return (GridResizeBehavior)GetValue(ResizeBehaviorProperty); }

            set { SetValue(ResizeBehaviorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the foreground color of grid splitter grip
        /// </summary>
        public Brush GripperForeground
        {
            get { return (Brush)GetValue(GripperForegroundProperty); }

            set { SetValue(GripperForegroundProperty, value); }
        }

        /// <summary>
        /// Gets or sets the level of the parent grid to resize
        /// </summary>
        public int ParentLevel
        {
            get { return (int)GetValue(ParentLevelProperty); }

            set { SetValue(ParentLevelProperty, value); }
        }

        /// <summary>
        /// Gets or sets the gripper Cursor type
        /// </summary>
        public GripperCursorType GripperCursor
        {
            get { return (GripperCursorType)GetValue(GripperCursorProperty); }
            set { SetValue(GripperCursorProperty, value); }
        }

        /// <summary>
        /// Gets or sets the gripper Custom Cursor resource number
        /// </summary>
        public int GripperCustomCursorResource
        {
            get { return (int)GetValue(GripperCustomCursorResourceProperty); }
            set { SetValue(GripperCustomCursorResourceProperty, value); }
        }

        /// <summary>
        /// Gets or sets splitter cursor on hover behavior
        /// </summary>
        public SplitterCursorBehavior CursorBehavior
        {
            get { return (SplitterCursorBehavior)GetValue(CursorBehaviorProperty); }
            set { SetValue(CursorBehaviorProperty, value); }
        }

        private static void OnGripperForegroundPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridSplitter = (GridSplitter)d;

            if (gridSplitter._gripperDisplay == null)
            {
                return;
            }

            gridSplitter._gripperDisplay.Foreground = gridSplitter.GripperForeground;
        }

        private static void OnGripperCursorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridSplitter = (GridSplitter)d;

            if (gridSplitter._hoverWrapper == null)
            {
                return;
            }

            gridSplitter._hoverWrapper.GripperCursor = gridSplitter.GripperCursor;
        }

        private static void GripperCustomCursorResourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridSplitter = (GridSplitter)d;

            if (gridSplitter._hoverWrapper == null)
            {
                return;
            }

            gridSplitter._hoverWrapper.GripperCustomCursorResource = gridSplitter.GripperCustomCursorResource;
        }

        private static void CursorBehaviorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridSplitter = (GridSplitter)d;

            gridSplitter._hoverWrapper?.UpdateHoverElement(gridSplitter.CursorBehavior ==
                                                           SplitterCursorBehavior.ChangeOnSplitterHover
                ? gridSplitter
                : gridSplitter.Element);
        }

        private static void OnElementPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var gridSplitter = (GridSplitter)d;

            gridSplitter._hoverWrapper?.UpdateHoverElement(gridSplitter.CursorBehavior ==
                                                           SplitterCursorBehavior.ChangeOnSplitterHover
                ? gridSplitter
                : gridSplitter.Element);
        }

        #endregion

    }

    internal class GripperHoverWrapper
    {
        private readonly GridSplitter.GridResizeDirection _gridSplitterDirection;

        private CoreCursor _splitterPreviousPointer;
        private CoreCursor _previousCursor;
        private GridSplitter.GripperCursorType _gripperCursor;
        private int _gripperCustomCursorResource;
        private bool _isDragging;
        private UIElement _element;

        internal GridSplitter.GripperCursorType GripperCursor
        {
            get
            {
                return _gripperCursor;
            }

            set
            {
                _gripperCursor = value;
            }
        }

        internal int GripperCustomCursorResource
        {
            get
            {
                return _gripperCustomCursorResource;
            }

            set
            {
                _gripperCustomCursorResource = value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GripperHoverWrapper"/> class that add cursor change on hover functionality for GridSplitter.
        /// </summary>
        /// <param name="element">UI element to apply cursor change on hover</param>
        /// <param name="gridSplitterDirection">GridSplitter resize direction</param>
        /// <param name="gripperCursor">GridSplitter gripper on hover cursor type</param>
        /// <param name="gripperCustomCursorResource">GridSplitter gripper custom cursor resource number</param>
        internal GripperHoverWrapper(UIElement element, GridSplitter.GridResizeDirection gridSplitterDirection, GridSplitter.GripperCursorType gripperCursor, int gripperCustomCursorResource)
        {
            _gridSplitterDirection = gridSplitterDirection;
            _gripperCursor = gripperCursor;
            _gripperCustomCursorResource = gripperCustomCursorResource;
            _element = element;
            UnhookEvents();
            _element.PointerEntered += Element_PointerEntered;
            _element.PointerExited += Element_PointerExited;
        }

        internal void UpdateHoverElement(UIElement element)
        {
            UnhookEvents();
            _element = element;
            _element.PointerEntered += Element_PointerEntered;
            _element.PointerExited += Element_PointerExited;
        }

        private void Element_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (_isDragging)
            {
                // if dragging don't update the cursor just update the splitter cursor with the last window cursor,
                // because the splitter is still using the arrow cursor and will revert to original case when drag completes
                _splitterPreviousPointer = _previousCursor;
            }
            else
            {
                Window.Current.CoreWindow.PointerCursor = _previousCursor;
            }
        }

        private void Element_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // if not dragging
            if (!_isDragging)
            {
                _previousCursor = _splitterPreviousPointer = Window.Current.CoreWindow.PointerCursor;
                UpdateDisplayCursor();
            }

            // if dragging
            else
            {
                _previousCursor = _splitterPreviousPointer;
            }
        }

        private void UpdateDisplayCursor()
        {
            if (_gripperCursor == GridSplitter.GripperCursorType.Default)
            {
                if (_gridSplitterDirection == GridSplitter.GridResizeDirection.Columns)
                {
                    Window.Current.CoreWindow.PointerCursor = GridSplitter.ColumnsSplitterCursor;
                }
                else if (_gridSplitterDirection == GridSplitter.GridResizeDirection.Rows)
                {
                    Window.Current.CoreWindow.PointerCursor = GridSplitter.RowSplitterCursor;
                }
            }
            else
            {
                var coreCursor = (CoreCursorType)((int)_gripperCursor);
                if (_gripperCursor == GridSplitter.GripperCursorType.Custom)
                {
                    if (_gripperCustomCursorResource > GridSplitter.GripperCustomCursorDefaultResource)
                    {
                        Window.Current.CoreWindow.PointerCursor = new CoreCursor(coreCursor, (uint)_gripperCustomCursorResource);
                    }
                }
                else
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(coreCursor, 1);
                }
            }
        }

        internal void SplitterManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            var splitter = sender as GridSplitter;
            if (splitter == null)
            {
                return;
            }

            _splitterPreviousPointer = splitter.PreviousCursor;
            _isDragging = true;
        }

        internal void SplitterManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            var splitter = sender as GridSplitter;
            if (splitter == null)
            {
                return;
            }

            Window.Current.CoreWindow.PointerCursor = splitter.PreviousCursor = _splitterPreviousPointer;
            _isDragging = false;
        }

        internal void UnhookEvents()
        {
            if (_element == null)
            {
                return;
            }

            _element.PointerEntered -= Element_PointerEntered;
            _element.PointerExited -= Element_PointerExited;
        }
    }
}