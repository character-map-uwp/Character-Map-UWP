using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Ink;
using System.Windows.Media;
using CharacterMap.Wpf.Controls;
using CharacterMap.Wpf.Models;
using CharacterMap.Wpf.Services;
using CharacterMap.Wpf.ViewModels;
using Microsoft.Win32;

namespace CharacterMap.Wpf.Views;

internal static class ViewElements
{
    public static TextBlock Label(string text, double size = 14) => new() { Text = text, FontSize = size, Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap };
    public static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 6, 12, 6) };
        button.Click += (_, _) => action(); return button;
    }
}

public sealed class QuickCompareView : UserControl
{
    public QuickCompareView(MainWindowViewModel vm)
    {
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new());
        root.Children.Add(ViewElements.Label("对比字体", 24));
        var sample = new TextBox { Text = string.IsNullOrWhiteSpace(vm.ComposerText) ? "The quick brown fox jumps over the lazy dog.\n0123456789 · 字体对比" : vm.ComposerText, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 74, Margin = new Thickness(0, 8, 0, 18) };
        System.Windows.Automation.AutomationProperties.SetName(sample, "对比文字"); Grid.SetRow(sample, 1); root.Children.Add(sample);
        var columns = new Grid(); columns.ColumnDefinitions.Add(new()); columns.ColumnDefinitions.Add(new()); Grid.SetRow(columns, 2); root.Children.Add(columns);
        for (int i = 0; i < 2; i++)
        {
            var panel = new DockPanel { Margin = new Thickness(i == 0 ? 0 : 12, 0, i == 0 ? 12 : 0, 0) };
            var fonts = new ComboBox { ItemsSource = vm.AllFonts, DisplayMemberPath = nameof(FontEntry.DisplayName), SelectedItem = i == 0 ? vm.SelectedFont : vm.AllFonts.FirstOrDefault(f => f.DisplayName == "Georgia") ?? vm.AllFonts.FirstOrDefault(), Margin = new Thickness(0, 0, 0, 10), IsTextSearchEnabled = true };
            System.Windows.Automation.AutomationProperties.SetName(fonts, i == 0 ? "左侧字体" : "右侧字体");
            DockPanel.SetDock(fonts, Dock.Top); panel.Children.Add(fonts);
            var preview = new TextBlock { FontSize = 38, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(16) };
            preview.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBox.Text)) { Source = sample });
            preview.SetBinding(TextBlock.FontFamilyProperty, new Binding("SelectedItem.Family") { Source = fonts });
            var scroll = new ScrollViewer { Content = preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            panel.Children.Add(scroll); Grid.SetColumn(panel, i); columns.Children.Add(panel);
        }
        Content = root;
    }
}

public sealed class CalligraphyView : UserControl
{
    public CalligraphyView(MainWindowViewModel vm)
    {
        var root = new DockPanel { Margin = new Thickness(24) };
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar);
        var board = new Grid { Background = Brushes.White, ClipToBounds = true };
        var guide = new DirectText { GlyphTypeface = vm.CurrentFace, CodePoint = vm.SelectedGlyph?.CodePoint ?? 0x41, FitToBounds = true, Padding = new Thickness(70), Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 228)), IsHitTestVisible = false };
        board.Children.Add(guide);
        var ink = new InkCanvas { Background = Brushes.Transparent, DefaultDrawingAttributes = new DrawingAttributes { Color = Colors.Black, Width = 4, Height = 4, FitToCurve = true } };
        System.Windows.Automation.AutomationProperties.SetName(ink, "书法画布"); board.Children.Add(ink);
        toolbar.Children.Add(ViewElements.Button("画笔", () => ink.EditingMode = InkCanvasEditingMode.Ink));
        toolbar.Children.Add(ViewElements.Button("橡皮", () => ink.EditingMode = InkCanvasEditingMode.EraseByStroke));
        toolbar.Children.Add(ViewElements.Button("撤销笔画", () => { if (ink.Strokes.Count > 0) ink.Strokes.RemoveAt(ink.Strokes.Count - 1); }));
        toolbar.Children.Add(ViewElements.Button("清空", () => ink.Strokes.Clear()));
        toolbar.Children.Add(ViewElements.Button("保存 PNG", () =>
        {
            var dialog = new SaveFileDialog { FileName = "Calligraphy.png", Filter = "PNG 图片|*.png" };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
            try { GlyphExportService.SavePng(dialog.FileName, board, (int)board.ActualWidth, (int)board.ActualHeight); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), ex.Message, "保存失败"); }
        }));
        var thickness = new Slider { Minimum = 1, Maximum = 24, Value = 4, Width = 100, VerticalAlignment = VerticalAlignment.Center };
        thickness.ValueChanged += (_, _) => { ink.DefaultDrawingAttributes.Width = thickness.Value; ink.DefaultDrawingAttributes.Height = thickness.Value; };
        thickness.ToolTip = "笔画粗细"; toolbar.Children.Add(thickness);
        root.Children.Add(board); Content = root;
    }
}

public sealed class SettingsView : UserControl
{
    public SettingsView(MainWindowViewModel vm)
    {
        DataContext = vm;
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(ViewElements.Label("设置", 24)); panel.Children.Add(ViewElements.Label("字符单元格大小"));
        var slider = new Slider { Minimum = 56, Maximum = 160, TickFrequency = 8, Margin = new Thickness(0, 4, 0, 16) };
        slider.SetBinding(Slider.ValueProperty, new Binding(nameof(vm.ItemSize)) { Mode = BindingMode.TwoWay }); panel.Children.Add(slider);
        var annotations = new CheckBox { Content = "显示 Unicode 码点标注", Margin = new Thickness(0, 8, 0, 16) };
        annotations.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(vm.ShowAnnotations))); panel.Children.Add(annotations);
        panel.Children.Add(ViewElements.Label("主题"));
        var theme = new ComboBox { ItemsSource = new[] { "跟随系统", "浅色", "深色" }, SelectedIndex = ThemeService.Mode };
        theme.SelectionChanged += (_, _) => ThemeService.Apply(theme.SelectedIndex); panel.Children.Add(theme);
        panel.Children.Add(ViewElements.Label("字体、主题、单元格大小和码点标注在关闭窗口时自动保存。", 12));
        Content = panel;
    }
}

public sealed class FontInfoView : UserControl
{
    public FontInfoView(MainWindowViewModel vm)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(ViewElements.Label(vm.SelectedFont?.DisplayName ?? "字体信息", 24));
        panel.Children.Add(ViewElements.Label($"字形变体：{vm.SelectedVariant?.Name}\n{vm.GlyphCountLabel}\n\n字体来源：{vm.SelectedFont?.SourceLabel}\n\n字重：{vm.CurrentFace?.Weight}\n字宽：{vm.CurrentFace?.Stretch}\n版本：{vm.CurrentFace?.Version}"));
        var face = vm.CurrentFace;
        foreach (var (label, value) in new[] { ("设计者", face?.DesignerNames.Values.FirstOrDefault()), ("版权", face?.Copyrights.Values.FirstOrDefault()), ("描述", face?.Descriptions.Values.FirstOrDefault()) })
            if (!string.IsNullOrWhiteSpace(value)) panel.Children.Add(ViewElements.Label($"{label}\n{value}"));
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
