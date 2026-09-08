using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace CharacterMap.Wpf.Services;

public static class ThemeService
{
    public static int Mode { get; private set; }
    public static void Initialize()
    {
        Apply(SettingsService.Load().Theme);
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        Application.Current.Exit += (_, _) => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
    }
    private static void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => Application.Current.Dispatcher.BeginInvoke(() => Apply(Mode));
    public static void Apply(int mode)
    {
        Mode = mode = Math.Clamp(mode, 0, 2);
        bool dark = mode == 2 || mode == 0 && (int?)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) == 0;
        // WPF's built-in Fluent theme API is marked experimental in .NET 10.
#pragma warning disable WPF0001
        Application.Current.ThemeMode = mode switch { 1 => ThemeMode.Light, 2 => ThemeMode.Dark, _ => ThemeMode.System };
#pragma warning restore WPF0001
        var colors = new Dictionary<string, string>
        {
            ["PageBrush"] = dark ? "#202020" : "#FCFAFC", ["PaneBrush"] = dark ? "#282529" : "#F7F1F1",
            ["TabBarBrush"] = dark ? "#25242B" : "#F3F1F9", ["CardBrush"] = dark ? "#2C2C2C" : "#F9F8FA",
            ["TextBrush"] = dark ? "#F2F2F2" : "#252525", ["SecondaryBrush"] = dark ? "#B2ADB4" : "#777477",
            ["StrokeBrush"] = dark ? "#414041" : "#E4E0E4", ["HoverBrush"] = dark ? "#22FFFFFF" : "#0C808080"
        };
        foreach (var (key, color) in colors)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); Application.Current.Resources[key] = brush;
        }
        if (SystemParameters.HighContrast)
        {
            foreach (string key in new[] { "PageBrush", "PaneBrush", "TabBarBrush", "CardBrush" }) Application.Current.Resources[key] = SystemColors.WindowBrush;
            Application.Current.Resources["TextBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["SecondaryBrush"] = SystemColors.WindowTextBrush;
            Application.Current.Resources["StrokeBrush"] = SystemColors.WindowTextBrush;
        }
    }
}
