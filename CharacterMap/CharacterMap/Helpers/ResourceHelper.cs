using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers;

public enum DesignStyle : int
{
    Default = 0,
    Fluent11 = 1,
    ClassicWindows = 2,
    ZuneDesktop = 3,
}

public class ThemeChangedMessage { }

public static class ResourceHelper
{
    #region Generic

    public static bool TryGet<T>(string resourceKey, out T value)
    {
        if (TryGetInternal(Application.Current.Resources, resourceKey, out value))
            return true;

        value = default;
        return false;
    }

    public static T Get<T>(string resourceKey)
    {
        return TryGetInternal(Application.Current.Resources, resourceKey, out T value) ? value : default;
    }

    public static T Get<T>(this FrameworkElement root, string resourceKey)
    {
        if (TryGetInternal(root.Resources, resourceKey, out T value1))
            return value1;

        return TryGetInternal(Application.Current.Resources, resourceKey, out T value) ? value : default;
    }

    public static bool TryGetInternal<T>(ResourceDictionary dictionary, string key, out T v)
    {
        if (dictionary.TryGetValue(key, out object r) && r is T c)
        {
            v = c;
            return true;
        }

        foreach (var dic in dictionary.MergedDictionaries)
        {
            if (TryGetInternal(dic, key, out v))
                return true;
        }

        v = default;
        return false;
    }

    public static FrameworkElement InflateDataTemplate(string dataTemplateKey, object dataContext)
    {
        DataTemplate template = Get<DataTemplate>(dataTemplateKey);
        ElementFactoryGetArgs args = new ElementFactoryGetArgs { Data = dataContext };
        FrameworkElement content = (FrameworkElement)template.GetElement(args);
        content.DataContext = dataContext;
        return content;
    }

    #endregion




    /* Character Map Specific resources */

    private static List<FrameworkElement> _elements { get; } = new();

    private static AppSettings _settings;
    public static AppSettings AppSettings => _settings ??= new();

    public static ElementTheme GetEffectiveTheme()
    {
#if DEBUG
        if (DesignMode.DesignMode2Enabled)
            return ElementTheme.Default;
#endif

        // Certain themes only support Light theme
        if (Get<bool>("SupportsDarkTheme") is false)
            return ElementTheme.Light;

        return AppSettings.UserRequestedTheme switch
        {
            ElementTheme.Default => App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light,
            _ => AppSettings.UserRequestedTheme
        };
    }

    public static Task SetTransparencyAsync(bool enable)
    {
        // Disable transparency on relevant brushes
        return WindowService.RunOnViewsAsync(() =>
        {
            if (Get<AcrylicBrush>("DefaultHostBrush") is AcrylicBrush def)
                def.AlwaysUseFallback = !enable;

            if (Get<AcrylicBrush>("DefaultAcrylicBrush") is AcrylicBrush ac)
                ac.AlwaysUseFallback = !enable;

            if (Get<AcrylicBrush>("AltHostBrush") is AcrylicBrush alt)
                alt.AlwaysUseFallback = !enable;
        });
    }

    private static bool? _supportsTabs;
    public static bool SupportsTabs => _supportsTabs ??= Get<bool>("SupportsTabs");
    public static bool SupportsShadows() => Get<bool>("SupportsShadows");
    public static bool AllowAnimation => AppSettings.UseSelectionAnimations && CompositionFactory.UISettings.AnimationsEnabled;
    public static bool AllowExpensiveAnimation => AllowAnimation && AppSettings.AllowExpensiveAnimations;
    public static bool AllowFluentAnimation => AllowAnimation && SupportFluentAnimation && AppSettings.UseFluentPointerOverAnimations1;

    private static bool? _supportsFluentAnimation;
    public static bool SupportFluentAnimation => _supportsFluentAnimation ??= Get<bool>("SupportsFluentAnimation");

    public static bool UsePointerOverAnimations => AppSettings.UseFluentPointerOverAnimations1;




    /* Dynamic theme-ability */

    #region Dynamic Theming

    public static void GoToThemeState(Control control)
    {
        string state = AppSettings.ApplicationDesignTheme switch
        {
            0 => "DefaultThemeState",
            1 => "FUIThemeState",
            2 => "ClassicThemeState",
            3 => "ZuneThemeState",
            _ => "DefaultThemeState"
        };

        VisualStateManager.GoToState(control, state, false);
    }

    public static void UpdateResolvedThemes()
    {

        var items = _elements.ToList();

        //foreach (var element in items)
        //{
        //    if (element.Dispatcher.HasThreadAccess)
        //        element.Style = null;
        //    else
        //    {
        //        _ = element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
        //            () => { element.Style = null; });
        //    }
        //}

        //await Task.Delay(64);

        foreach (var element in items)
        {
            if (element.Dispatcher.HasThreadAccess)
                TryResolveThemeStyle2(element);
            else
            {
                _ = element.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.High,
                    () => TryResolveThemeStyle2(element));
            }
        }
    }

    internal static string GetAppName()
    {
        return "Character Map UWP";
    }

    public static void RegisterForThemeChanges<T>(T element) where T : FrameworkElement
    {
        UnregisterForThemeChanges(element);
        _elements.Add(element);
    }

    public static void UnregisterForThemeChanges<T>(T element) where T : FrameworkElement
    {
        _elements.Remove(element);
    }

    public static bool TryResolveThemeStyle(FrameworkElement element, FrameworkElement styleAnchor = null)
    {
        var ver = (Microsoft.UI.Xaml.Controls.ControlsResourcesVersion)(AppSettings.ApplicationDesignTheme + 1);


        string key = AppSettings.ApplicationDesignTheme == 0 ? "Default" : "FUI";
        if (Properties.GetStyleKey(element) is string styleKey)
        {
            string target = $"{key}{styleKey}";
            Style resolved = null;
            if (styleAnchor != null)
                resolved = ResourceHelper.Get<Style>(styleAnchor, target);
            else
                resolved = ResourceHelper.Get<Style>(target);

            if (resolved is not null)
            {
                element.Style = resolved;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveThemeStyle2(FrameworkElement element)
    {
        if (Properties.GetStyleKey(element) is string styleKey)
        {
            // Find source dictionary for current theme
            var dic = (ResourceDictionary)App.Current.Resources[$"StyleSource{AppSettings.ApplicationDesignTheme + 1}"];

            // Try resolve style from source dictionary
            TryGetInternal<Style>(dic, styleKey, out Style resolved);
            if (resolved is not null && resolved != element.Style)
            {
                // Assign style to element
                element.Style = resolved;
                return true;
            }
        }

        return false;
    }

    public static bool TryResolveThemeStyle3(FrameworkElement element)
    {
        if (element.ReadLocalValue(FrameworkElement.StyleProperty) != DependencyProperty.UnsetValue)
            return false;

        if (Properties.GetStyleKey(element) is string styleKey)
        {
            // Find source dictionary for current theme
            // Try resolve style from source dictionary
            Style resolved = Get<Style>(styleKey);
            if (resolved is not null && resolved != element.Style)
            {
                // Assign style to element
                element.Style = resolved;
                return true;
            }
        }

        return false;
    }

    internal static void SendThemeChanged()
    {
        UpdateResolvedThemes();

        //var content = Window.Current.Content;
        //Window.Current.Content = null;
        //Window.Current.Content = content;
    }

    #endregion

}

public class ThemeHelper
{
    private FrameworkElement _element;

    private Debouncer _debouncer { get; }

    public ThemeHelper(FrameworkElement element)
    {
        if (DesignMode.DesignModeEnabled)
            return;

        _element = element;
        ResourceHelper.TryResolveThemeStyle3(element);
        return;

        /* 
         * For the current version of the application we are not using real-time
         * style changing, so this code path is temporarily ignored.
         */

#pragma warning disable CS0162 // Unreachable code detected
        _debouncer = new Debouncer();

        element.Loaded += Element_Loaded;
        element.Unloaded += Element_Unloaded;

        Update();
#pragma warning restore CS0162 // Unreachable code detected
    }

    private void Element_Loaded(object sender, RoutedEventArgs e)
    {
        //if (_debouncer.IsActive)
        //{
        //    Debug.WriteLine("CANCEL UNLOAD");
        //    _debouncer.Cancel();
        //}

        Update();
    }

    private void Element_Unloaded(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine($"want unload {_element}");

        _debouncer.Debounce(5000, () =>
        {
            if (VisualTreeHelper.GetParent(_element) is null)
            {
                Debug.WriteLine($"UNREGISTERING {_element}");
                ResourceHelper.UnregisterForThemeChanges(_element);
            }

        });
    }

    public void Update()
    {
        if (DesignMode.DesignModeEnabled)
            return;

        ResourceHelper.TryResolveThemeStyle3(_element);
        return;

        //if (DesignMode.DesignModeEnabled)
        //    return;

        //if (_element is IThemeableControl themeable)
        //    ResourceHelper.TryResolveThemeStyle2(_element);

        //ResourceHelper.RegisterForThemeChanges(_element);
    }
}
