using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;

namespace CharacterMap.Controls;

[DependencyProperty<Duration>("Duration")]
[DependencyProperty<KeySpline>("KeySpline")]
public partial class CompositionTransition : DependencyObject
{
    public bool IsValid => Duration.HasTimeSpan && KeySpline is not null;

    public CompositionEasingFunction GetEase(Compositor c)
    {
        return Composition.GetCached<CompositionEasingFunction>(c, $"__ctEa{KeySpline}", () =>
        {
            return c.CreateCubicBezierEasingFunction(KeySpline);
        });
    }
}

public enum SelectionVisualType
{
    PointerOver, // Should render 
    Selection,   // Should render on top of ListViewBase internal ScrollContentPresenter
    Focus        // Should be rendered on top of Window.Current.Content
}

public enum SelectorInteractionState
{
    None,
    PointerOver
}

[DependencyProperty<Point>("VisualCornerRadius", default)]
[DependencyProperty<Point>("VisualOffset", default)]
[DependencyProperty<HorizontalAlignment>("VisualHorizontalAlignment", "HorizontalAlignment.Stretch", nameof(Update))]
[DependencyProperty<VerticalAlignment>("VisualVerticalAlignment", "VerticalAlignment.Stretch", nameof(Update))]
[DependencyProperty<Thickness>("VisualInset", default, nameof(Update))]
[DependencyProperty<CompositionTransition>("SizeTransition")]
[DependencyProperty<CompositionTransition>("CornerTransition")]
[DependencyProperty<CompositionTransition>("OffsetTransition")]
[DependencyProperty<SolidColorBrush>("Fill")]
[DependencyProperty<SolidColorBrush>("Stroke")]
[DependencyProperty<SolidColorBrush>("BarFill")]
[DependencyProperty<SelectionVisualType>("Mode")]
[DependencyProperty<double>("StrokeThickness")]
[DependencyProperty<FrameworkElement>("Target")] // Set to a ListView or RadioButtons to automatically attach
[DependencyProperty<FrameworkElement>("DisplayTarget")]
[DependencyProperty<Point>("BarSize")]
[DependencyProperty<double>("BarPadding")]
[DependencyProperty<Point>("BarCornerRadius", "new Point(2,2)")]
[DependencyProperty<Orientation>("Orientation", "Orientation.Horizontal")]
[AttachedProperty<SelectorVisualElement>("Element")]
[AttachedProperty<DataTemplate>("ElementTemplate")]
public partial class SelectorVisualElement : FrameworkElement
{
    private ShapeVisual _containerShapes;
    private ShapeVisual _barShapes;
    private ContainerVisual _container;
    private CompositionRoundedRectangleGeometry _rect;
    private CompositionRoundedRectangleGeometry _bar;
    private CompositionColorBrush _fillBrush;
    private CompositionColorBrush _strokeBrush;
    private CompositionColorBrush _barFillBrush;
    private CompositionPropertySet _props;
    private CompositionSpriteShape _barSprite;

    private ExpressionAnimation _exp;

    string barXY_horizontal => "Vector2(" +
            "((Container.Size.X - Bar.Size.X) / 2) + 2," + // TODO: Why + 2? Is is StrokeThickness?
            "Container.Size.Y - Bar.Size.Y - props.Padding)";

    string barXY_vertical => "Vector2(" +
            "props.Padding, " +
            "((Container.Size.Y - Bar.Size.Y) / 2) + 2)";


    private List<(DependencyProperty, long)> _tokens { get; } = [];

    public SelectorVisualElement()
    {
        this.IsHitTestVisible = false;
        this.Loaded += SelectorVisual_Loaded;
        this.Unloaded += SelectorVisual_Unloaded;
        _props = this.GetElementVisual().Compositor.CreatePropertySet();
    }

    void Update()
    {
        if (VisualTreeHelper.GetParent(this) is not FrameworkElement)
            return;

        CreateVisual();
        _rect.CornerRadius = VisualCornerRadius.ToVector2();
    }

    Color GetColor(SolidColorBrush b)
    {
        if (b is null) return Colors.Transparent;
        return b.Color with { A = (byte)((double)b.Color.A * b.Opacity) };
    }

    partial void OnFillChanged(SolidColorBrush o, SolidColorBrush n)
    {
        // Note: This implementation does not support updating the brush properties.
        if (_fillBrush != null)
            _fillBrush.Color = GetColor(n);
    }

    partial void OnStrokeChanged(SolidColorBrush o, SolidColorBrush n)
    {
        // Note: This implementation does not support updating the brush properties.
        if (_strokeBrush is not null)
            _strokeBrush.Color = GetColor(n);
    }

    partial void OnBarFillChanged(SolidColorBrush o, SolidColorBrush n)
    {
        if (_barFillBrush is not null)
            _barFillBrush.Color = GetColor(n);
    }

    partial void OnStrokeThicknessChanged(double o, double n)
    {
        if (_containerShapes is not null && _containerShapes.Shapes.FirstOrDefault() is CompositionSpriteShape s)
            s.StrokeThickness = (float)n;
    }

    partial void OnBarSizeChanged(Point o, Point n)
    {
        if (_bar is not null)
        {
            _bar.Size = n.ToVector2();
            _barShapes.Size = _bar.Size;
            _props?.InsertVector2("BarSize", _bar.Size);
        }
    }

    partial void OnBarPaddingChanged(double o, double n)
    {
        _props?.InsertScalar("Padding", (float)BarPadding);
    }

    partial void OnBarCornerRadiusChanged(Point o, Point n)
    {
        if (_bar is not null)
            _bar.CornerRadius = n.ToVector2();
    }

    partial void OnVisualCornerRadiusChanged(Point o, Point n)
    {
        if (_rect is not null)
            _rect.CornerRadius = VisualCornerRadius.ToVector2();
    }

    void CreateVisual()
    {
        if (_containerShapes is not null)
            return;

        var c = this.GetElementVisual().Compositor;

        _fillBrush = c.CreateColorBrush(GetColor(Fill));
        _strokeBrush = c.CreateColorBrush(GetColor(Stroke));
        _barFillBrush = c.CreateColorBrush(GetColor(BarFill));

        _rect = c.CreateRoundedRectangleGeometry();
        _rect.CornerRadius = VisualCornerRadius.ToVector2();

        // Create background shape
        var s = c.CreateSpriteShape(_rect);

        s.StrokeBrush = _strokeBrush;
        s.StrokeThickness = (float)StrokeThickness;
        s.FillBrush = _fillBrush;

        _containerShapes = c.CreateShapeVisual();
        _containerShapes.Shapes.Add(s);

        _container = c.CreateContainerVisual();
        _container.Children.InsertAtTop(_containerShapes);

        // Create bar shape
        _bar = c.CreateRoundedRectangleGeometry();
        _bar.CornerRadius = BarCornerRadius.ToVector2();
        _bar.Size = BarSize.ToVector2();
        var bs = c.CreateSpriteShape(_bar);
        bs.FillBrush = _barFillBrush;
        bs.CenterPoint = _bar.Size / 2;
        _barSprite = bs;

        _props.Insert("Padding", (float)BarPadding);

        // Setup bar shape
        _barShapes = c.CreateShapeVisual();

        CompositionFactory.StartCentering(_barShapes);
        _barShapes.Shapes.Add(bs);
        _container.Children.InsertAtTop(_barShapes);

        // Bar scale
        _barShapes.SetImplicitAnimation("Scale",
            bs.CreateVector3KeyFrameAnimation("Scale")
                .AddKeyFrame(1, "this.FinalValue")
                .SetDuration(0.3));

        var str = this.Orientation is Orientation.Horizontal
            ? barXY_horizontal
            : barXY_vertical;

        // Position bar
        _exp = bs.CreateExpressionAnimation("Offset.XY")
            .SetExpression(str)
            .SetParameter("Container", _rect)
            .SetParameter("Bar", _bar)
            .SetParameter("props", _props);

        bs.StartAnimation(_exp);

        if (DisplayTarget is null)
            this.SetChildVisual(_container);
        else
            DisplayTarget.SetChildVisual(_container);
    }

    void UpdateSize(bool animate)
    {
        if (_rect is null)
            return;

        Vector2 size = this.ActualSize();
        size -= new Vector2((float)VisualInset.Left + (float)VisualInset.Right, (float)VisualInset.Top + (float)VisualInset.Bottom);
    }

    public void Hide()
    {
        this.Visibility = Visibility.Collapsed;
    }

    public void MoveTo(FrameworkElement target, FrameworkElement container, bool show = true)
    {
        if (target is null)
            return;

        bool animate = true;

        CreateVisual();

        if (show && this.Visibility == Visibility.Collapsed)
        {
            this.Visibility = Visibility.Visible;
            animate = false;
        }

        // TODO: listen to new target's SizeChanged event,
        //       and unhook previous target

        // 1: Get the target element's position relative to the container
        var r = target.GetBoundingRect(container).Value;
        Vector3 position = new Vector3((float)r.Left + (float)VisualInset.Left,
            (float)r.Top + (float)VisualInset.Top, 0);

        position += new Vector3(VisualOffset.ToVector2(), 0);

        // 2: Get the target element's size
        Vector2 size = target.ActualSize();
        size -= new Point(VisualInset.Left + VisualInset.Right, VisualInset.Top + VisualInset.Bottom).ToVector2();
        var size2 = size += new Vector2((float)StrokeThickness);

        if (size.X < 0 || size.Y < 0)
            size = new();

        animate &= ResourceHelper.AllowAnimation;

        // 3: Animation position
        SetOffset(animate);
        _containerShapes.Offset = position;
        _barShapes.Offset = position;

        // 4: Animation size
        SetSizeAnimation(_containerShapes, animate);
        SetSizeAnimation(_barShapes, animate);
        SetSizeAnimation(_container, animate);
        SetSizeAnimation(_rect, animate);
        SetSizeAnimation(_bar, animate);

        _containerShapes.Size = size2;
        _barShapes.Size = size2;
        _container.Size = size2;

        if (size.LengthSquared() > VisualCornerRadius.ToVector2().LengthSquared())
        {
            _rect.Size = size - VisualCornerRadius.ToVector2();
            _rect.Offset = VisualCornerRadius.ToVector2() / 2f;
        }
    }

    void SetOffset(bool animate)
    {
        if (animate && OffsetTransition is { IsValid: true })
        {
            if (_containerShapes.HasImplicitAnimation("Offset"))
                return;

            var ani = _containerShapes.CreateVector3KeyFrameAnimation("Offset")
                    .AddKeyFrame(1, "this.FinalValue", OffsetTransition.GetEase(_containerShapes.Compositor))
                    .SetDuration(OffsetTransition.Duration.TimeSpan.TotalSeconds);

            _containerShapes.SetImplicitAnimation("Offset", ani);
            _barShapes.SetImplicitAnimation("Offset", ani);
        }
        else
        { 
            _containerShapes.SetImplicitAnimation("Offset", null);
            _barShapes.SetImplicitAnimation("Offset", null);
        }
    }

    void SetSizeAnimation(CompositionObject o, bool animate)
    {
        if (animate && SizeTransition is { IsValid: true })
        {
            if (o.HasImplicitAnimation("Size"))
                return;

            o.SetImplicitAnimation("Size",
                o.CreateVector2KeyFrameAnimation("Size")
                    .AddKeyFrame(1, "this.FinalValue", SizeTransition.GetEase(o.Compositor))
                    .SetDuration(SizeTransition.Duration.TimeSpan.TotalSeconds));
        }
        else
            o.SetImplicitAnimation("Size", null);
    }




    //------------------------------------------------------
    //
    // XAML Lifecycle
    //
    //------------------------------------------------------

    #region Lifecycle

    private void SelectorVisual_Loaded(object sender, RoutedEventArgs e)
    {
        Register();
        Update();
    }

    private void SelectorVisual_Unloaded(object sender, RoutedEventArgs e)
    {
        Unregister();

        static void Dispose<T>(ref T d) where T : class, IDisposable
        {
            d?.Dispose();
            d = null;
        }

        this.SetChildVisual(null);

        Dispose(ref _container);
        Dispose(ref _containerShapes);
        Dispose(ref _barShapes);
        Dispose(ref _rect);
        Dispose(ref _bar);
        Dispose(ref _fillBrush);
        Dispose(ref _strokeBrush);
        Dispose(ref _barFillBrush);
        Dispose(ref _barSprite);


        //private ShapeVisual _containerShapes;
        //private ShapeVisual _barShapes;
        //private ContainerVisual _container;
        //private CompositionRoundedRectangleGeometry _rect;
        //private CompositionRoundedRectangleGeometry _bar;
        //private CompositionColorBrush _fillBrush;
        //private CompositionColorBrush _strokeBrush;
        //private CompositionColorBrush _barFillBrush;
        //private CompositionPropertySet _props;
        //private CompositionSpriteShape _barSprite;

    }

    void Register()
    {
        Unregister();

        Do(HorizontalAlignmentProperty);
        Do(VerticalAlignmentProperty);
        Do(MarginProperty);

        void Do(DependencyProperty p)
        {
            _tokens.Add((p, this.RegisterPropertyChangedCallback(p, callback)));
        }
    }

    void Unregister()
    {
        foreach (var token in _tokens)
        {
            this.UnregisterPropertyChangedCallback(token.Item1, token.Item2);
        }
    }

    private void callback(DependencyObject sender, DP dp)
    {
        Update();
    }

    #endregion





    //------------------------------------------------------
    //
    // Attached Properties
    //
    //------------------------------------------------------

    partial void OnDisplayTargetChanged(FrameworkElement o, FrameworkElement n)
    {
        if (o is FrameworkElement old)
        {
            old.SetChildVisual(null);
        }

        if (n is FrameworkElement newValue)
        {
            this.SetChildVisual(null);
            newValue.SetChildVisual(_container);
        }
        else
        {
            this.SetChildVisual(null);
        }
    }

    partial void OnModeChanged(SelectionVisualType o, SelectionVisualType n)
    {
        OnTargetChanged(Target, Target);
    }

    partial void OnTargetChanged(FrameworkElement o, FrameworkElement n)
    {
        if (o is ListViewBase oldList)
            OnListViewTargetChanged(oldList, null);

        if (o is RadioButtons oldRadios)
            OnRadioButtonsTargetChanged(oldRadios, null);

        if (n is ListViewBase newList)
            OnListViewTargetChanged(null, newList);

        if (n is RadioButtons newRadios)
            OnRadioButtonsTargetChanged(null, newRadios);
    }

    static partial void OnElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SelectorVisualElement oldValue)
            oldValue.Target = null;

        if (e.NewValue is SelectorVisualElement newValue)
            newValue.Target = (FrameworkElement)d;
    }

    static partial void OnElementTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement f)
        {
            if (e.OldValue is not null && e.NewValue is null)
                SetElement(f, null);

            if (e.NewValue is DataTemplate t)
                SetElement(f, (SelectorVisualElement)t.LoadContent());
        }
    }




    //------------------------------------------------------
    //
    // Drag Handling
    //
    //------------------------------------------------------

    internal void SetState(SelectorInteractionState state)
    {
        if (_barSprite is null)
            return;

        //_barSprite.CenterPoint = new Vector2(0.5f);
        //_barSprite.Offset = BarSize.ToVector2()/2f;

        if (state is SelectorInteractionState.PointerOver)
        {
            if (this.Orientation == Orientation.Horizontal)
                _bar.Size = new((float)BarSize.X * 2f, (float)BarSize.Y);
            else
                _bar.Size = new((float)BarSize.X, (float)BarSize.Y * 1.5f);

        }
        else
            _bar.Size = BarSize.ToVector2();
    }




    //------------------------------------------------------
    //
    // Drag Handling
    //
    //------------------------------------------------------

    public void StartMove()
    {
        SetOffset(false);
    }

    public void Move(Vector2 offset)
    {
        if (this.Orientation == Orientation.Horizontal)
            MoveX(offset.X);
        else
            MoveY(offset.Y);
    }

    void MoveX(float offset)
    {
        var target = _containerShapes.Offset + new Vector3(offset, 0, 0);
        if (target.X < 0)
            target = target with { X = 0 };

        if (VisualTreeHelper.GetParent(this) is FrameworkElement parent)
        {
            var max = parent.ActualWidth - _rect.Size.X;
            if (target.X > max)
                target = target with { X = (float)max };
        }

        _containerShapes.Offset = target;
    }

    void MoveY(float offset)
    {
        var target = _containerShapes.Offset + new Vector3(0, offset, 0);
        if (target.Y < 0)
            target = target with { Y = 0 };

        if (VisualTreeHelper.GetParent(this) is FrameworkElement parent)
        {
            var max = parent.ActualHeight - _rect.Size.Y;
            if (target.Y > max)
                target = target with { Y = (float)max };
        }

        _containerShapes.Offset = target;
    }

    public void EndMove()
    {
        SetOffset(true);
    }

    public Rect GetBounds()
    {
        return new Rect(
            _containerShapes.Offset.X, 
            _containerShapes.Offset.Y, 
            _containerShapes.Size.X, 
            _containerShapes.Size.Y);
    }
}




//------------------------------------------------------
//
// RadioButtons helpers
//
//------------------------------------------------------

partial class SelectorVisualElement // RADIOBUTTONS
{
    void OnRadioButtonsTargetChanged(RadioButtons o, RadioButtons n)
    {
        if (o is RadioButtons old)
        {
            old.Loaded -= Radios_Loaded;
            old.SelectionChanged -= Radios_Selection;
            old.SizeChanged -= Radios_SizeChanged;
        }

        if (n is RadioButtons nrb)
        {
            nrb.Loaded -= Radios_Loaded;
            nrb.SizeChanged -= Radios_SizeChanged;

            if (nrb.ActualSize().X <= 0)
                nrb.Loaded += Radios_Loaded;
            else
            {
                nrb.SizeChanged += Radios_SizeChanged;
                RadioMoveTo(nrb, nrb.SelectedIndex);
            }

            if (nrb.GetFirstDescendantOfType<FrameworkElement>("BorderRoot") is { } b)
                this.DisplayTarget = b;

            nrb.SelectionChanged -= Radios_Selection;
            nrb.SelectionChanged += Radios_Selection;
        }

        void Radios_Selection(object sender, SelectionChangedEventArgs e)
        {
            if (sender is RadioButtons rb)
                RadioMoveTo(rb, rb.SelectedIndex);
        }

        void Radios_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButtons rb)
            {
                rb.SizeChanged -= Radios_SizeChanged;
                rb.SizeChanged += Radios_SizeChanged;

                if (rb.GetFirstDescendantOfType<FrameworkElement>("BorderRoot") is { } b)
                    this.DisplayTarget = b;

                RadioMoveTo(rb, rb.SelectedIndex);
            }
        }

        void Radios_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is RadioButtons rb)
                RadioMoveTo(rb, rb.SelectedIndex);
        }
    }

    public void RadioMoveTo(RadioButtons b, int index)
    {
        if (VisualTreeHelper.GetParent(b) is null)
            return;

        if (index < 0)
        {
            // If anyone else uses this look at fixing this,
            // but Radios fires removed selection event before the added one,
            // not both at the same time. This will break our movement
            // animation if we handle it with this "basic" code.
            //this.Hide();
            return;
        }

        var items = b.GetFirstLevelDescendantsOfType<RadioButton>().ToList();
        var target = b.GetFirstLevelDescendantsOfType<RadioButton>().Skip(b.SelectedIndex).FirstOrDefault();
        this.MoveTo(target, b);
    }
}




//------------------------------------------------------
//
// ListView helpers
//
//------------------------------------------------------

partial class SelectorVisualElement // EXTENDEDLISTVIEW
{
    void OnListViewTargetChanged(ListViewBase o, ListViewBase n)
    {
        if (o is ListViewBase old)
        {
            old.Loaded -= Lvb_Loaded;
            old.SelectionChanged -= Lv_SelectionChanged;
        }

        if (n is ListViewBase lvb)
        {
            lvb.Loaded -= Lvb_Loaded;

            if (Mode == SelectionVisualType.Selection)
            {
                if (lvb.GetFirstDescendantOfType<ScrollContentPresenter>(s => s.Name == "ScrollContentPresenter") is { } s)
                    Attach(lvb, this, s);
                else
                    lvb.Loaded += Lvb_Loaded;
            }
        }

        void Lvb_Loaded(object sender, RoutedEventArgs e)
        {
            ListViewBase lv = (ListViewBase)sender;
            lv.Loaded -= Lvb_Loaded;

            if (this.Mode == SelectionVisualType.Selection)
            {
                if (lv.GetFirstDescendantOfType<ScrollContentPresenter>(s => s.Name == "ScrollContentPresenter") is { } s)
                    Attach(lv, SelectorVisualElement.GetElement(lv), s);
            }
        }

        static void Attach(ListViewBase lv, SelectorVisualElement sv, ScrollContentPresenter sp)
        {
            if (sp is null)
                return;

            lv.SelectionChanged -= Lv_SelectionChanged;

            // Hook item changed
            lv.SelectionChanged += Lv_SelectionChanged;
            if (sp.Content is ItemsPresenter ip)
            {
                sv.DisplayTarget = ip;
            }

        }

        static void Lv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListViewBase l = (ListViewBase)sender;
            SelectorVisualElement sve = SelectorVisualElement.GetElement(l);

            if (e.AddedItems.FirstOrDefault() is { } item)
                if (l.ContainerFromItem(item) is FrameworkElement fe)
                {
                    if (sve.DisplayTarget is FrameworkElement target)
                        sve.MoveTo(fe, target, true);
                    else
                        sve.MoveTo(fe, l, true);
                }
                else if (e.RemovedItems.Count > 0)
                    sve.Hide();
        }
    }

}
