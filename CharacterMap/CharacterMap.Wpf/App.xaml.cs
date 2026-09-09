using System.Windows;

namespace CharacterMap.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Services.ThemeService.Initialize();
    }
}
