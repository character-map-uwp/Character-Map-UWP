using CharacterMap.Controls.Brushes;
using CharacterMap.Helpers;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls;

[DependencyProperty<bool>("EnableShadow", false, nameof(UpdateShadow))]
[DependencyProperty<double>("ShadowBlurRadius", 24d, nameof(UpdateShadow))]
[DependencyProperty<double>("ShadowOpacity", 0.25d, nameof(UpdateShadow))]
[DependencyProperty<Vector3>("ShadowOffset", "new Vector3(0, 8, 0)", nameof(UpdateShadow))]
[DependencyProperty<Color>("ShadowColor", "Colors.Black", nameof(UpdateShadow))]
public partial class SmoothBorder : ContentControl
{
    private Border _borderPresenter;
    private ContentPresenter _presenter;

    private ContainerVisual _rootVisual;
    private SpriteVisual _shadowVisual;
    private DropShadow _dropShadow;
    private ShapeVisual _shapeVisual;

    private SpriteVisual _bgVisual;

    private CompositionSpriteShape _borderShape;
    private CompositionPathGeometry _outerPathGeometry;

    private CompositionSpriteShape _bgShape;
    private CompositionPathGeometry _innerPathGeometry;

    private Visual _presenterVisual;
    private CompositionRoundedRectangleGeometry _clipGeometry;
    private CompositionGeometricClip _geometricClip;
    private CompositionPropertySet _props;

    private CompositionBrush _fillBrush;
    private CompositionBrush _strokeBrush;

    private Vector2 _currentSize;
    private readonly List<(DependencyProperty, long)> _tokens = [];
    private bool _isInitialized;

    public SmoothBorder()
    {
        this.DefaultStyleKey = typeof(SmoothBorder);
        this.Loaded += SmoothBorder_Loaded;
        this.Unloaded += SmoothBorder_Unloaded;
        this.SizeChanged += SmoothBorder_SizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _borderPresenter = GetTemplateChild("BorderPresenter") as Border;
        _presenter = GetTemplateChild("Presenter") as ContentPresenter;

        InitializeComposition();
    }

    private void SmoothBorder_Loaded(object sender, RoutedEventArgs e)
    {
        RegisterCallbacks();
        UpdateAll();
    }

    private void SmoothBorder_Unloaded(object sender, RoutedEventArgs e)
    {
        UnregisterCallbacks();
    }

    private void RegisterCallbacks()
    {
        if (_tokens.Count > 0)
            return;

        _tokens.Add((BackgroundProperty, RegisterPropertyChangedCallback(BackgroundProperty, OnDependencyPropertyChanged)));
        _tokens.Add((BorderBrushProperty, RegisterPropertyChangedCallback(BorderBrushProperty, OnDependencyPropertyChanged)));
        _tokens.Add((BorderThicknessProperty, RegisterPropertyChangedCallback(BorderThicknessProperty, OnDependencyPropertyChanged)));
        _tokens.Add((CornerRadiusProperty, RegisterPropertyChangedCallback(CornerRadiusProperty, OnDependencyPropertyChanged)));
    }

    private void UnregisterCallbacks()
    {
        foreach ((DependencyProperty dp, long token) in _tokens)
            UnregisterPropertyChangedCallback(dp, token);
        _tokens.Clear();
    }

    private void OnDependencyPropertyChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (dp == BackgroundProperty)
            UpdateBackground();
        else if (dp == BorderBrushProperty)
            UpdateBorderBrush();
        else if (dp == BorderThicknessProperty || dp == CornerRadiusProperty)
        {
            UpdateProperties();
            UpdateGeometries(Vector2.Zero, _currentSize, false);
            UpdateBorderBrush();
        }
    }

    private void InitializeComposition()
    {
        if (_borderPresenter is null)
            return;

        Compositor c = ElementCompositionPreview.GetElementVisual(this).Compositor;

        Thickness bt = this.BorderThickness;
        CornerRadius cr = this.CornerRadius;
        GetCornerParameters(cr, out float radius, out float topOffset, out float heightExtra);

        _props = c.CreatePropertySet()
            .Insert("Left", (float)bt.Left)
            .Insert("Top", (float)bt.Top)
            .Insert("Right", (float)bt.Right)
            .Insert("Bottom", (float)bt.Bottom)
            .Insert("Radius", radius)
            .Insert("TopOffset", topOffset)
            .Insert("HeightExtra", heightExtra);

        _rootVisual = c.CreateContainerVisual();
        _shadowVisual = c.CreateSpriteVisual();
        _shapeVisual = c.CreateShapeVisual();

        // 1. Outer Shape (Border)
        _outerPathGeometry = c.CreatePathGeometry();
        _borderShape = c.CreateSpriteShape(_outerPathGeometry);
        _shapeVisual.Shapes.Add(_borderShape);

        // 2. Inner Shape (Background)
        _innerPathGeometry = c.CreatePathGeometry();
        _bgShape = c.CreateSpriteShape(_innerPathGeometry);
        _shapeVisual.Shapes.Add(_bgShape);

        // 3. Implicit Size Animation on _shapeVisual & _shadowVisual
        var size = _shapeVisual.GetCached<Vector2KeyFrameAnimation>("__SIZE_ORCHES",
            () => _shapeVisual.CreateVector2KeyFrameAnimation(nameof(Visual.Size))
                .UseOrchestration());

        _shapeVisual.SetImplicitAnimation(nameof(Visual.Size), size);
        _shadowVisual.SetImplicitAnimation(nameof(Visual.Size), size);

        _bgVisual = c.CreateSpriteVisual();
        _bgVisual.SetImplicitAnimation(nameof(Visual.Size), size);

        _rootVisual.Children.InsertAtBottom(_shadowVisual);
        _rootVisual.Children.InsertAbove(_bgVisual, _shadowVisual);
        _rootVisual.Children.InsertAtTop(_shapeVisual);

        _borderPresenter.SetChildVisual(_rootVisual);

        // 4. Presenter Content Clip with GPU ExpressionAnimation
        if (_presenter is not null)
        {
            _presenterVisual = ElementCompositionPreview.GetElementVisual(_presenter);
            _clipGeometry = c.CreateRoundedRectangleGeometry();
            _clipGeometry.CornerRadius = new Vector2(radius, radius);

            _clipGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.Size),
                c.CreateExpressionAnimation("Vector2(Max(0.0f, Visual.Size.X - props.Left - props.Right), Max(0.0f, Visual.Size.Y - props.Top - props.Bottom + props.HeightExtra))")
                    .SetParameter("Visual", _shapeVisual)
                    .SetParameter("props", _props));

            _clipGeometry.StartAnimation(nameof(CompositionRoundedRectangleGeometry.Offset),
                c.CreateExpressionAnimation("Vector2(props.Left, props.Top + props.TopOffset)")
                    .SetParameter("props", _props));

            _geometricClip = c.CreateGeometricClip(_clipGeometry);
            _presenterVisual.Clip = _geometricClip;
        }

        UpdateAll();
    }

    private void SmoothBorder_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        /* DEBUGGING THING */
        //this.CornerRadius = new (
        //   Utils.Random.Next(1, 21),
        //   Utils.Random.Next(1, 21),
        //   Utils.Random.Next(1, 21),
        //   Utils.Random.Next(1, 21));

        if (_shapeVisual is null)
            return;

        Vector2 newSize = new((float)e.NewSize.Width, (float)e.NewSize.Height);

        if (!_isInitialized)
        {
            _isInitialized = true;
            UpdateGeometries(Vector2.Zero, newSize, false);
            _shapeVisual.Size = newSize;
            if (_bgVisual is not null)
                _bgVisual.Size = newSize;
            if (_shadowVisual is not null)
                _shadowVisual.Size = newSize;
            return;
        }

        bool animate = ResourceHelper.AllowAnimation;
        UpdateGeometries(_currentSize, newSize, animate);

        _shapeVisual.Size = newSize;
        if (_bgVisual is not null)
            _bgVisual.Size = newSize;
        if (_shadowVisual is not null)
            _shadowVisual.Size = newSize;
    }

    private void UpdateAll()
    {
        UpdateProperties();
        UpdateBackground();
        UpdateBorderBrush();
        UpdateShadow();

        Vector2 size = new((float)ActualWidth, (float)ActualHeight);
        if (size.X > 0 && size.Y > 0)
        {
            UpdateGeometries(Vector2.Zero, size, false);
            _shapeVisual.Size = size;
            if (_bgVisual is not null)
                _bgVisual.Size = size;
            if (_shadowVisual is not null)
                _shadowVisual.Size = size;
            _isInitialized = true;
        }
    }

    private static void GetCornerParameters(CornerRadius cr, out float radius, out float topOffset, out float heightExtra)
    {

        bool hasTop = cr.TopLeft > 0 || cr.TopRight > 0;
        bool hasBottom = cr.BottomLeft > 0 || cr.BottomRight > 0;

        if (!hasTop && hasBottom)
        {
            // Flat top, round bottom
            radius = (float)Math.Max(cr.BottomLeft, cr.BottomRight);
            topOffset = -radius;
            heightExtra = radius;
        }
        else if (hasTop && !hasBottom)
        {
            // Round top, flat bottom
            radius = (float)Math.Max(cr.TopLeft, cr.TopRight);
            topOffset = 0f;
            heightExtra = radius;
        }
        else if (hasTop && hasBottom)
        {
            // All rounded
            radius = (float)Math.Max(Math.Max(cr.TopLeft, cr.TopRight), Math.Max(cr.BottomLeft, cr.BottomRight));
            topOffset = 0f;
            heightExtra = 0f;
        }
        else
        {
            // All flat
            radius = 0f;
            topOffset = 0f;
            heightExtra = 0f;
        }
    }

    private void UpdateProperties()
    {
        Thickness bt = this.BorderThickness;
        CornerRadius cr = this.CornerRadius;
        GetCornerParameters(cr, out float radius, out float topOffset, out float heightExtra);

        _props?.Insert("Left", (float)bt.Left)
              ?.Insert("Top", (float)bt.Top)
              ?.Insert("Right", (float)bt.Right)
              ?.Insert("Bottom", (float)bt.Bottom)
              ?.Insert("Radius", radius)
              ?.Insert("TopOffset", topOffset)
              ?.Insert("HeightExtra", heightExtra);

        if (_clipGeometry is not null)
            _clipGeometry.CornerRadius = new (radius, radius);
    }

    private void UpdateGeometries(Vector2 oldSize, Vector2 newSize, bool animate)
    {
        if (_outerPathGeometry is null || newSize.X <= 0 || newSize.Y <= 0)
            return;

        Thickness bt = this.BorderThickness;
        CornerRadius cr = this.CornerRadius;
        CornerRadius innerCr = GetInnerCornerRadius(cr, bt);
        CanvasDevice device = CanvasDevice.GetSharedDevice();
        Compositor c = _shapeVisual.Compositor;

        // Target geometries for newSize
        Rect outerRectNew = new(0, 0, newSize.X, newSize.Y);
        Rect innerRectNew = new(
            bt.Left,
            bt.Top,
            Math.Max(0, newSize.X - bt.Left - bt.Right),
            Math.Max(0, newSize.Y - bt.Top - bt.Bottom));

        using CanvasGeometry outerGeomNew = CreateRoundedRectGeometry(device, outerRectNew, cr);
        using CanvasGeometry innerGeomNew = CreateRoundedRectGeometry(device, innerRectNew, innerCr);

        CompositionPath outerPathNew = new(outerGeomNew);
        CompositionPath innerPathNew = new(innerGeomNew);

        if (animate && oldSize.X > 0 && oldSize.Y > 0 && ResourceHelper.AllowAnimation)
        {
            // Source geometries for oldSize
            Rect innerRectOld = new(
                bt.Left,
                bt.Top,
                Math.Max(0, oldSize.X - bt.Left - bt.Right),
                Math.Max(0, oldSize.Y - bt.Top - bt.Bottom));

            // Animate outer border path
            _outerPathGeometry.StartAnimation(
                _outerPathGeometry.CreatePathKeyFrameAnimation(nameof(CompositionPathGeometry.Path))
                    .UseOrchestration(outerPathNew));

            // Animate inner background path
            _innerPathGeometry.StartAnimation(
                _innerPathGeometry.CreatePathKeyFrameAnimation(nameof(CompositionPathGeometry.Path))
                    .UseOrchestration(innerPathNew));
        }
        else
        {
            _outerPathGeometry.Path = outerPathNew;
            _innerPathGeometry.Path = innerPathNew;
        }

        _currentSize = newSize;
    }

    private static CornerRadius GetInnerCornerRadius(CornerRadius cr, Thickness bt)
    {
        return new(
            Math.Max(0, cr.TopLeft - Math.Max(bt.Left, bt.Top)),
            Math.Max(0, cr.TopRight - Math.Max(bt.Right, bt.Top)),
            Math.Max(0, cr.BottomRight - Math.Max(bt.Right, bt.Bottom)),
            Math.Max(0, cr.BottomLeft - Math.Max(bt.Left, bt.Bottom)));
    }

    private static CanvasGeometry CreateRoundedRectGeometry(
        ICanvasResourceCreator resourceCreator,
        Rect rc,
        CornerRadius cr)
    {
        float left = (float)rc.Left;
        float top = (float)rc.Top;
        float right = (float)rc.Right;
        float bottom = (float)rc.Bottom;
        float width = Math.Max(0.001f, (float)rc.Width);
        float height = Math.Max(0.001f, (float)rc.Height);

        float tl = Math.Max(0.0f, (float)cr.TopLeft);
        float tr = Math.Max(0.0f, (float)cr.TopRight);
        float br = Math.Max(0.0f, (float)cr.BottomRight);
        float bl = Math.Max(0.0f, (float)cr.BottomLeft);

        // Overlap resolution (CSS/XAML standard: compute single min scale factor across all 4 sides)
        float factor = 1.0f;
        if (tl + tr > width)
            factor = Math.Min(factor, width / (tl + tr));
        if (bl + br > width)
            factor = Math.Min(factor, width / (bl + br));
        if (tl + bl > height)
            factor = Math.Min(factor, height / (tl + bl));
        if (tr + br > height)
            factor = Math.Min(factor, height / (tr + br));

        if (factor < 1.0f)
        {
            tl *= factor;
            tr *= factor;
            br *= factor;
            bl *= factor;
        }

        using CanvasPathBuilder builder = new(resourceCreator);

        // Start at top edge before top-right corner
        builder.BeginFigure(new Vector2(right - tr, top));

        // Top-right corner
        if (tr > 0)
            builder.AddArc(new Vector2(right, top + tr), tr, tr, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        else
            builder.AddLine(new Vector2(right, top));

        // Right edge line
        builder.AddLine(new Vector2(right, bottom - br));

        // Bottom-right corner
        if (br > 0)
            builder.AddArc(new Vector2(right - br, bottom), br, br, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        else
            builder.AddLine(new Vector2(right, bottom));

        // Bottom edge line
        builder.AddLine(new Vector2(left + bl, bottom));

        // Bottom-left corner
        if (bl > 0)
            builder.AddArc(new Vector2(left, bottom - bl), bl, bl, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        else
            builder.AddLine(new Vector2(left, bottom));

        // Left edge line
        builder.AddLine(new Vector2(left, top + tl));

        // Top-left corner
        if (tl > 0)
            builder.AddArc(new Vector2(left + tl, top), tl, tl, 0, CanvasSweepDirection.Clockwise, CanvasArcSize.Small);
        else
            builder.AddLine(new Vector2(left, top));

        // Top edge line back to figure start
        builder.AddLine(new Vector2(right - tr, top));

        builder.EndFigure(CanvasFigureLoop.Closed);
        return CanvasGeometry.CreatePath(builder);
    }

    private void UpdateShadow()
    {
        if (_shadowVisual is null)
            return;

        bool show = EnableShadow;
        _shadowVisual.IsVisible = show;

        if (show)
        {
            Compositor c = _shadowVisual.Compositor;
            _dropShadow ??= c.CreateDropShadow();
            _dropShadow.BlurRadius = (float)ShadowBlurRadius;
            _dropShadow.Opacity = (float)ShadowOpacity;
            _dropShadow.Offset = ShadowOffset;
            _dropShadow.Color = ShadowColor;
            _shadowVisual.Shadow = _dropShadow;
        }
        else if (_dropShadow is not null)
            _shadowVisual.Shadow = null;
    }

    private void UpdateBackground()
    {
        if (_bgShape is null)
            return;

        Compositor c = _bgShape.Compositor;

        if (Background is IXamlCompositionBrush xaml)
        {
            _bgShape.FillBrush = null;
            _bgVisual.Brush = xaml.GetCompositionBrush();
            _bgVisual.Clip = _geometricClip;
            _bgVisual.IsVisible = true;
            _fillBrush = null;
            return;
        }

        if (_bgVisual is not null)
        {
            _bgVisual.Brush = null;
            _bgVisual.IsVisible = false;
        }

        _fillBrush = CreateOrUpdateBrush(Background, _fillBrush, c);
        _bgShape.FillBrush = _fillBrush;
    }

    private void UpdateBorderBrush()
    {
        if (_borderShape is null)
            return;

        Thickness bt = this.BorderThickness;
        bool hasBorder = (bt.Left > 0 || bt.Top > 0 || bt.Right > 0 || bt.Bottom > 0) && BorderBrush is not null;

        if (hasBorder)
        {
            Compositor c = _borderShape.Compositor;
            _strokeBrush = CreateOrUpdateBrush(BorderBrush, _strokeBrush, c);
            _borderShape.FillBrush = _strokeBrush;
        }
        else
        {
            _borderShape.FillBrush = null;
        }
    }

    private static CompositionBrush CreateOrUpdateBrush(Brush brush, CompositionBrush existing, Compositor compositor)
    {
        if (brush is null)
            return null;

        if (brush is SolidColorBrush scb)
        {
            Color color = GetColor(scb);
            if (existing is CompositionColorBrush ccb)
            {
                ccb.Color = color;
                return ccb;
            }
            return compositor.CreateColorBrush(color);
        }

        if (brush is LinearGradientBrush lgb)
            return lgb.AsCompositionBrush(compositor, existing as CompositionGradientBrush);

        return null;
    }

    private static Color GetColor(SolidColorBrush b)
    {
        if (b is null)
            return Colors.Transparent;
        return b.Color with { A = (byte)((double)b.Color.A * b.Opacity) };
    }
}
