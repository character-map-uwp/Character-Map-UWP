using CharacterMap.Services;
using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.UI.Xaml;

namespace CharacterMap;

static class Program
{
    static void Main(string[] args)
    {
        if (args is not null && args.Any(a => string.Equals(a, FullTrustHost.LaunchArgument, StringComparison.OrdinalIgnoreCase)))
        {
            FullTrustHost.Run(args);
            return;
        }

        // The UWP GUI process still runs inside the AppContainer, so AppInstance works perfectly here
        var instance = AppInstance.FindOrRegisterInstanceForKey("GUI");
        if (instance.IsCurrentInstance)
            Application.Start((p) => new App());
        else
            instance.RedirectActivationTo();
    }
}
