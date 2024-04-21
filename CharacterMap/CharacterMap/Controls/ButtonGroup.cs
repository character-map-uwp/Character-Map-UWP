using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace CharacterMap.Controls;

[DependencyProperty<string>("Shortcut")]
[DependencyProperty<string>("Glyph")]
[DependencyProperty<string>("Title")]
[DependencyProperty<string>("Description")]
public partial class ButtonLabel : DependencyObject { }

class ButtonGroupBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Thickness t && parameter is string s)
            return s switch
            {
                "Left" => new Thickness(t.Left, t.Top, t.Right / 2d, t.Bottom),
                "Right" => new Thickness(t.Left / 2d, t.Top, t.Right, t.Bottom),
                "Top" => new Thickness(t.Left, t.Top, t.Right, t.Bottom / 2d),
                "Bottom" => new Thickness(t.Left, t.Top / 2d, t.Right, t.Bottom),
                "Vert" => new Thickness(t.Left, t.Top / 2d, t.Right, t.Bottom / 2d),
                _ => new Thickness(t.Left / 2d, t.Top, t.Right / 2d, t.Bottom)
            };

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

class ButtonGroupCornerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is CornerRadius r && parameter is string s)
            return s switch
            {
                "Left" => new CornerRadius(r.TopLeft, 0, 0, r.BottomLeft),
                "Right" => new CornerRadius(0, r.TopRight, r.BottomRight, 0),
                "Top" => new CornerRadius(r.TopLeft, r.TopRight, 0, 0),
                "Bottom" => new CornerRadius(0, 0, r.BottomRight, r.BottomLeft),
                _ => new CornerRadius(0),
            };

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

[DependencyProperty<Orientation>("Orientation", Orientation.Horizontal)]
public sealed partial class ButtonGroup : ItemsControl
{
    private static ButtonGroupBorderConverter _bConv { get; } = new();
    private static ButtonGroupCornerConverter _cConv { get; } = new();

    private long _bToken = 0;
    private long _cToken = 0;

    public ButtonGroup()
    {
        this.DefaultStyleKey = typeof(ButtonGroup);

        this.Loaded += ButtonGroup_Loaded;
        this.Unloaded += ButtonGroup_Unloaded;
    }

    private void ButtonGroup_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateBindings();

        _cToken = this.RegisterPropertyChangedCallback(ItemsControl.CornerRadiusProperty, OnDPChanged);
        _bToken = this.RegisterPropertyChangedCallback(ItemsControl.BorderThicknessProperty, OnDPChanged);
    }

    private void ButtonGroup_Unloaded(object sender, RoutedEventArgs e)
    {
        this.UnregisterPropertyChangedCallback(ItemsControl.CornerRadiusProperty, _cToken);
        this.UnregisterPropertyChangedCallback(ItemsControl.BorderThicknessProperty, _bToken);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateBindings();
    }

    private void OnDPChanged(DependencyObject sender, DependencyProperty dp)
    {
        UpdateBindings();
    }

    protected override void OnItemsChanged(object e)
    {
        UpdateBindings();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var measure = base.MeasureOverride(availableSize);

        if (ItemsPanelRoot is Grid g)
        {
            if (Orientation == Orientation.Horizontal)
            {
                g.RowDefinitions.Clear();

                if (g.ColumnDefinitions.Count != Items.Count)
                {
                    while (g.ColumnDefinitions.Count > Items.Count)
                        g.ColumnDefinitions.RemoveAt(0);

                    while (g.ColumnDefinitions.Count < Items.Count)
                        g.ColumnDefinitions.Add(new());
                }

                for (int i = 0; i < Items.Count; i++)
                    if (Items[i] is FrameworkElement f)
                        Grid.SetColumn(f, i);
            }
            else
            {
                g.ColumnDefinitions.Clear();

                if (g.RowDefinitions.Count != Items.Count)
                {
                    while (g.RowDefinitions.Count > Items.Count)
                        g.RowDefinitions.RemoveAt(0);

                    while (g.RowDefinitions.Count < Items.Count)
                        g.RowDefinitions.Add(new());
                }

                for (int i = 0; i < Items.Count; i++)
                    if (Items[i] is FrameworkElement f)
                        Grid.SetRow(f, i);
            }

        }

        return measure;
    }

    void UpdateBindings()
    {
        void Bind(Control c, string param = null)
        {
            if (c == null)
                return;

            c.SetBinding(Control.BorderBrushProperty, new Binding
            {
                Source = this,
                Path = new(nameof(BorderBrush))
            });

            c.SetBinding(Control.BorderThicknessProperty, new Binding
            {
                Source = this,
                Path = new(nameof(BorderThickness)),
                ConverterParameter = param,
                Converter = param is not null ? _bConv : null
            });

            c.SetBinding(Control.CornerRadiusProperty, new Binding
            {
                Source = this,
                Path = new(nameof(CornerRadius)),
                ConverterParameter = param,
                Converter = param is not null ? _cConv : null
            });
        }

        bool h = Orientation == Orientation.Horizontal;

        // 1. Set bindings for children
        if (Items.Count == 1 && Items[0] is Control c)
            Bind(c);
        else if (Items.Count > 1)
        {
            // Set First element
            if (Items[0] is Control c1)
                Bind(c1, h ? "Left" : "Top");

            // Handle last element
            if (Items[^1] is Control c2)
                Bind(c2, h ? "Right" : "Bottom");

            // Handle everything in the middle
            if (Items.Count > 2)
                for (int i = 1; i < Items.Count - 1; i++)
                    Bind(Items[i] as Control, h ? "Mid" : "Vert");
        }
    }
}
