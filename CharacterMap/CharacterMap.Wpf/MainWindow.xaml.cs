using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CharacterMap.Wpf.Controls;
using CharacterMap.Wpf.Models;
using CharacterMap.Wpf.Services;
using CharacterMap.Wpf.ViewModels;
using CharacterMap.Wpf.Views;
using Microsoft.Win32;

namespace CharacterMap.Wpf;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private bool _fullscreen;
    private WindowState _previousState;
    public MainWindow()
    {
        InitializeComponent(); DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, _) => { try { _viewModel.SaveSettings(); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
    }
    private void Menu_Click(object sender, RoutedEventArgs e) => AppMenu.IsOpen = !AppMenu.IsOpen;
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Sidebar_Click(object sender, RoutedEventArgs e) { AppMenu.IsOpen = false; SidebarColumn.Width = new GridLength(SidebarColumn.Width.Value == 0 ? 250 : 0); }
    private void OpenFont_Click(object sender, RoutedEventArgs e) { AppMenu.IsOpen = false; _viewModel.OpenFontCommand.Execute(null); }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { AppMenu.IsOpen = false; _viewModel.OpenFolderCommand.Execute(null); }
    private void GridMode_Click(object sender, RoutedEventArgs e) { CharacterGrid.Visibility = Visibility.Visible; CharacterList.Visibility = Visibility.Collapsed; }
    private void ListMode_Click(object sender, RoutedEventArgs e) { CharacterGrid.Visibility = Visibility.Collapsed; CharacterList.Visibility = Visibility.Visible; if (_viewModel.SelectedGlyph != null) CharacterList.ScrollIntoView(_viewModel.SelectedGlyph); }
    private void Annotation_Click(object sender, RoutedEventArgs e) => _viewModel.ShowAnnotations = !_viewModel.ShowAnnotations;
    private void Filter_Click(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = !FilterPopup.IsOpen;
    private void More_Click(object sender, RoutedEventArgs e) => FilterPopup.IsOpen = true;
    private void CharacterList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(CharacterList, e.OriginalSource as DependencyObject) is ListBoxItem { Content: GlyphItem glyph }) _viewModel.AddCommand.Execute(glyph);
    }
    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        AppMenu.IsOpen = false;
        if (!_fullscreen) { _previousState = WindowState; WindowState = WindowState.Normal; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; }
        else { ResizeMode = ResizeMode.CanResize; WindowState = _previousState; }
        _fullscreen = !_fullscreen;
    }
    private void ShowPanel(string title, FrameworkElement view, double width = 780, double height = 580)
    {
        AppMenu.IsOpen = false;
        var window = new Window { Owner = this, Title = title, Content = view, Width = width, Height = height, MinWidth = 520, MinHeight = 360, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (Brush)FindResource("PageBrush"), Foreground = (Brush)FindResource("TextBrush") };
        window.ShowDialog();
    }
    private void Compare_Click(object sender, RoutedEventArgs e) => ShowPanel("对比字体", new QuickCompareView(_viewModel), 1000, 620);
    private void Calligraphy_Click(object sender, RoutedEventArgs e) => ShowPanel("书法", new CalligraphyView(_viewModel), 940, 660);
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPanel("设置", new SettingsView(_viewModel), 620, 440);
    private void Info_Click(object sender, RoutedEventArgs e)
    {
        var face = _viewModel.CurrentFace;
        if (face == null) return;
        ShowPanel("字体信息", new FontInfoView(_viewModel), 640, 530);
    }
    private void About_Click(object sender, RoutedEventArgs e)
    {
        AppMenu.IsOpen = false;
        MessageBox.Show(this, "Character Map\n\nWPF 桌面版 · .NET 10\n基于 Character Map UWP，保留原项目 MIT 许可证。\n\n字符网格、字形预览、字体列表、分组菜单和标签页使用 WPF 控件实现。", "关于 Character Map", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedGlyph == null || _viewModel.CurrentFace == null) return;
        var dialog = new SaveFileDialog { Title = "导出所选字形", FileName = _viewModel.SelectedGlyph.Code.Replace('+', '-'), Filter = "SVG 矢量图 (*.svg)|*.svg|PNG 透明图片 (*.png)|文本 (*.txt)|*.txt", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { GlyphExportService.Export(dialog.FileName, _viewModel.CurrentFace, _viewModel.SelectedGlyph); _viewModel.StatusText = $"已保存 {Path.GetFileName(dialog.FileName)}"; }
        catch (Exception ex) { _viewModel.StatusText = $"导出失败：{ex.Message}"; }
    }
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        AppMenu.IsOpen = false;
        if (_viewModel.CurrentFace == null || _viewModel.Glyphs.Count == 0) return;
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true) return;
        try
        {
            dialog.PrintDocument(new FontMapPaginator(_viewModel.CurrentFace, _viewModel.Glyphs, _viewModel.SelectedFont?.DisplayName ?? "Character Map", new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight)), "Character Map");
        }
        catch (Exception ex) { _viewModel.StatusText = $"打印失败：{ex.Message}"; }
    }
    private void Tools_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedGlyph is not { } glyph) return;
        var menu = new ContextMenu();
        void Add(string label, string text) { var item = new MenuItem { Header = label }; item.Click += (_, _) => { try { Clipboard.SetText(text); _viewModel.StatusText = "已复制"; } catch (Exception ex) { _viewModel.StatusText = ex.Message; } }; menu.Items.Add(item); }
        Add("复制 Unicode 码点", glyph.Code); Add("复制 Unicode 名称", glyph.Name); Add("复制 HTML 实体", $"&#x{glyph.CodePoint:X};");
        Add("复制 C# 转义", glyph.CodePoint <= 0xFFFF ? $"\\u{glyph.CodePoint:X4}" : $"\\U{glyph.CodePoint:X8}");
        menu.PlacementTarget = (UIElement)sender; menu.IsOpen = true;
    }
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) _viewModel.ImportFiles(paths.Where(MainWindowViewModel.IsFontFile));
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.F11) { Fullscreen_Click(this, e); e.Handled = true; return; }
        if (!ctrl) return;
        switch (e.Key)
        {
            case Key.O: if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) OpenFolder_Click(this, e); else OpenFont_Click(this, e); break;
            case Key.T: _viewModel.AddTab(); break;
            case Key.W: if (_viewModel.SelectedTab != null) _viewModel.CloseTab(_viewModel.SelectedTab); break;
            case Key.Tab:
                int delta = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1;
                _viewModel.SelectedTab = _viewModel.Tabs[(_viewModel.Tabs.IndexOf(_viewModel.SelectedTab!) + delta + _viewModel.Tabs.Count) % _viewModel.Tabs.Count]; break;
            case Key.F: GlyphSearchBox.Focus(); GlyphSearchBox.SelectAll(); break;
            case Key.K: Compare_Click(this, e); break;
            case Key.I: Calligraphy_Click(this, e); break;
            case Key.P: Print_Click(this, e); break;
            case Key.C when Keyboard.FocusedElement is not TextBox: _viewModel.CopyCommand.Execute(null); break;
            default: return;
        }
        e.Handled = true;
    }
}
