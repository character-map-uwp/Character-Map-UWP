using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CharacterMap.Wpf.Models;

namespace CharacterMap.Wpf.Controls;

public class CharacterGridView : ListBox
{
    static CharacterGridView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(CharacterGridView), new FrameworkPropertyMetadata(typeof(CharacterGridView)));
    public static readonly DependencyProperty ItemSizeProperty = DependencyProperty.Register(nameof(ItemSize), typeof(double), typeof(CharacterGridView),
        new FrameworkPropertyMetadata(88d, (d, e) => d.SetCurrentValue(FontSizeProperty, (double)e.NewValue * 0.42)),
        value => (double)value >= 32 && double.IsFinite((double)value));
    public static readonly DependencyProperty ItemFontFaceProperty = DependencyProperty.Register(nameof(ItemFontFace), typeof(GlyphTypeface), typeof(CharacterGridView));
    public static readonly DependencyProperty ShowAnnotationsProperty = DependencyProperty.Register(nameof(ShowAnnotations), typeof(bool), typeof(CharacterGridView));
    public static readonly DependencyProperty ItemDoubleClickCommandProperty = DependencyProperty.Register(nameof(ItemDoubleClickCommand), typeof(ICommand), typeof(CharacterGridView));
    public double ItemSize { get => (double)GetValue(ItemSizeProperty); set => SetValue(ItemSizeProperty, value); }
    public GlyphTypeface? ItemFontFace { get => (GlyphTypeface?)GetValue(ItemFontFaceProperty); set => SetValue(ItemFontFaceProperty, value); }
    public bool ShowAnnotations { get => (bool)GetValue(ShowAnnotationsProperty); set => SetValue(ShowAnnotationsProperty, value); }
    public ICommand? ItemDoubleClickCommand { get => (ICommand?)GetValue(ItemDoubleClickCommandProperty); set => SetValue(ItemDoubleClickCommandProperty, value); }
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (ItemsControl.ContainerFromElement(this, e.OriginalSource as DependencyObject) is ListBoxItem { Content: GlyphItem glyph }
            && ItemDoubleClickCommand?.CanExecute(glyph) == true) ItemDoubleClickCommand.Execute(glyph);
    }
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (SelectedItem != null) Dispatcher.BeginInvoke(() => ScrollIntoView(SelectedItem));
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        int columns = FindPanel(this)?.Columns ?? 1;
        int index = e.Key switch
        {
            Key.Left => SelectedIndex - 1, Key.Right => SelectedIndex + 1,
            Key.Up => SelectedIndex - columns, Key.Down => SelectedIndex + columns,
            Key.Home => 0, Key.End => Items.Count - 1,
            Key.PageDown => SelectedIndex + columns * Math.Max(1, (int)(ActualHeight / ItemSize)),
            Key.PageUp => SelectedIndex - columns * Math.Max(1, (int)(ActualHeight / ItemSize)), _ => -2
        };
        if (index != -2 && Items.Count > 0)
        {
            SelectedIndex = Math.Clamp(index, 0, Items.Count - 1); e.Handled = true; return;
        }
        if (e.Key == Key.Enter && ItemDoubleClickCommand?.CanExecute(SelectedItem) == true)
        {
            ItemDoubleClickCommand.Execute(SelectedItem); e.Handled = true; return;
        }
        base.OnKeyDown(e);
    }
    private static VirtualizingWrapPanel? FindPanel(DependencyObject root)
    {
        if (root is VirtualizingWrapPanel panel) return panel;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindPanel(VisualTreeHelper.GetChild(root, i)) is { } result) return result;
        return null;
    }
}
