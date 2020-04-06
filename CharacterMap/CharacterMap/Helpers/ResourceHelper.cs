using CharacterMap.Core;
using CharacterMap.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers
{
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

        static bool TryGetInternal<T>(ResourceDictionary dictionary, string key, out T v)
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

        private static AppSettings _settings;
        public static AppSettings AppSettings
        {
            get => _settings ?? (_settings = Get<AppSettings>(nameof(AppSettings)));
        }

        public static ElementTheme GetEffectiveTheme()
        {
            return AppSettings.UserRequestedTheme switch
            {
                ElementTheme.Default => App.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light,
                _ => AppSettings.UserRequestedTheme
            };
        }

        public static Task SetTransparencyAsync(bool enable)
        {
            return WindowService.RunOnViewsAsync(() =>
            {
                Get<AcrylicBrush>("DefaultHostBrush").AlwaysUseFallback = !enable;
                Get<AcrylicBrush>("AltHostBrush").AlwaysUseFallback = !enable;
                Get<AcrylicBrush>("DefaultAcrylicBrush").AlwaysUseFallback = !enable;
            });
        }
    }

}
