using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CharacterMap.Wpf.Controls;

public class LabelButton : Button
{
    static LabelButton() => DefaultStyleKeyProperty.OverrideMetadata(typeof(LabelButton), new FrameworkPropertyMetadata(typeof(LabelButton)));
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(nameof(Icon), typeof(string), typeof(LabelButton), new PropertyMetadata(""));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(LabelButton), new PropertyMetadata(""));
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(nameof(Description), typeof(string), typeof(LabelButton), new PropertyMetadata(""));
    public static readonly DependencyProperty ShortcutProperty = DependencyProperty.Register(nameof(Shortcut), typeof(string), typeof(LabelButton), new PropertyMetadata(""));
    public string Icon { get => (string)GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Description { get => (string)GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string Shortcut { get => (string)GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }
}
public class ButtonGroup : ItemsControl
{
    static ButtonGroup() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonGroup), new FrameworkPropertyMetadata(typeof(ButtonGroup)));
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ButtonGroup), new PropertyMetadata(Orientation.Horizontal));
    public Orientation Orientation { get => (Orientation)GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }
}
public class SuggestionBox : TextBox
{
    static SuggestionBox() => DefaultStyleKeyProperty.OverrideMetadata(typeof(SuggestionBox), new FrameworkPropertyMetadata(typeof(SuggestionBox)));
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(SuggestionBox), new PropertyMetadata("搜索"));
    public static readonly DependencyProperty QueryCommandProperty = DependencyProperty.Register(nameof(QueryCommand), typeof(ICommand), typeof(SuggestionBox));
    public string PlaceholderText { get => (string)GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }
    public ICommand? QueryCommand { get => (ICommand?)GetValue(QueryCommandProperty); set => SetValue(QueryCommandProperty, value); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && QueryCommand?.CanExecute(Text) == true) { QueryCommand.Execute(Text); e.Handled = true; }
        else if (e.Key == Key.Escape) { Clear(); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
public class ExtendedListView : ListBox
{
    static ExtendedListView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedListView), new FrameworkPropertyMetadata(typeof(ExtendedListView)));
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (SelectedItem != null) Dispatcher.BeginInvoke(() => ScrollIntoView(SelectedItem));
    }
}
public class ExtendedTabView : ListBox
{
    static ExtendedTabView() => DefaultStyleKeyProperty.OverrideMetadata(typeof(ExtendedTabView), new FrameworkPropertyMetadata(typeof(ExtendedTabView)));
}
