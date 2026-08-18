using CharacterMap.Helpers;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Graphics.Canvas.Effects;
using System;
using Windows.Graphics.Effects;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls.Brushes;

public interface IXamlCompositionBrush
{
    CompositionBrush GetCompositionBrush();
}

public class MicaAltBrush : XamlCompositionBrushBase, IXamlCompositionBrush
{
    private const string TINT_COLOR_PARAM = "Tint.Color";

    private UISettings _uiSettings;
    private CompositionBackdropBrush _backdropBrush;
    private CompositionEffectBrush _effectBrush;

    public MicaAltBrush()
    {
    }

    protected override void OnConnected()
    {
        base.OnConnected();

        if (this.CompositionBrush is null)
        {
            Compositor c = Window.Current?.Compositor ?? Composition.Compositor;
            if (c is not null)
                BuildBrush(c);
        }

        RegisterEvents();
        UpdateTheme();
    }

    protected override void OnDisconnected()
    {
        base.OnDisconnected();

        UnregisterEvents();

        if (this.CompositionBrush is not null)
        {
            this.CompositionBrush.Dispose();
            this.CompositionBrush = null;
        }

        if (_backdropBrush is not null)
        {
            _backdropBrush.Dispose();
            _backdropBrush = null;
        }

        _effectBrush = null;
    }

    public CompositionBrush GetCompositionBrush(Compositor compositor = null)
    {
        if (this.CompositionBrush is null)
        {
            Compositor c = compositor ?? Window.Current?.Compositor ?? Composition.Compositor;
            if (c is not null)
            {
                BuildBrush(c);
                UpdateTheme();
            }
        }

        return this.CompositionBrush;
    }

    private void BuildBrush(Compositor compositor)
    {
        _backdropBrush = compositor.CreateHostBackdropBrush();

        bool isDark = ResourceHelper.GetEffectiveTheme() == ElementTheme.Dark;
        Color initialTint = GetTintColor(isDark);

        IGraphicsEffect graphicsEffect = new ArithmeticCompositeEffect
        {
            Name = "MicaAltBlend",
            Source1 = new CompositionEffectSourceParameter("Backdrop"),
            Source2 = new ColorSourceEffect { Name = "Tint", Color = initialTint },
            MultiplyAmount = 0.0f,
            Source1Amount = 0.20f,
            Source2Amount = 0.80f,
            Offset = 0.0f
        };

        CompositionEffectFactory factory = compositor.CreateEffectFactory(
            graphicsEffect,
            [TINT_COLOR_PARAM]);

        _effectBrush = factory.CreateBrush();
        _effectBrush.SetSourceParameter("Backdrop", _backdropBrush);

        this.CompositionBrush = _effectBrush;
    }

    private void RegisterEvents()
    {
        _uiSettings ??= new();
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
        _uiSettings.ColorValuesChanged += OnColorValuesChanged;

        WeakReferenceMessenger.Default.Unregister<AppSettingsChangedMessage>(this);
        WeakReferenceMessenger.Default.Register<AppSettingsChangedMessage>(this, (r, m) =>
        {
            if (m.PropertyName == nameof(AppSettings.UserRequestedTheme))
                UpdateTheme();
        });
    }

    private void UnregisterEvents()
    {
        if (_uiSettings is not null)
        {
            _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
            _uiSettings = null;
        }

        WeakReferenceMessenger.Default.Unregister<AppSettingsChangedMessage>(this);
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        _ = Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () => UpdateTheme());
    }

    private void UpdateTheme()
    {
        bool isDark = ResourceHelper.GetEffectiveTheme() == ElementTheme.Dark;
        Color tint = GetTintColor(isDark);
        Color fallback = GetFallbackColor(isDark);

        this.FallbackColor = fallback;

        if (_effectBrush is not null)
            _effectBrush.Properties.InsertColor(TINT_COLOR_PARAM, tint);
    }

    private static Color GetTintColor(bool isDark)
    {
        return isDark
            ? Color.FromArgb(215, 32, 32, 32)
            : Color.FromArgb(215, 240, 240, 240);
    }

    private static Color GetFallbackColor(bool isDark)
    {
        return isDark
            ? Color.FromArgb(255, 32, 32, 32)
            : Color.FromArgb(255, 240, 240, 240);
    }

    public CompositionBrush GetCompositionBrush() => CompositionBrush;
}
