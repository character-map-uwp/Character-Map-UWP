using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CharacterMap.Helpers
{
    public static class ResourceHelper
    {
        public static bool TryGet<T>(string resourceKey, out T value)
        {
            if (TryGetInternal(Application.Current.Resources, resourceKey, out value))
                return true;

            value = default;
            return false;
        }

        public static T Get<T>(string resourceKey)
        {
            if (TryGetInternal(Application.Current.Resources, resourceKey, out T value))
                return value;

            return default;
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
    }

}
