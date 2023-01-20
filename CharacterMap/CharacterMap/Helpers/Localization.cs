using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace CharacterMap.Helpers
{
    /// <summary>
    /// A helper for retrieving localized resources.
    /// </summary>
    public static class Localization
    {
        private static ResourceLoader _resources { get; }

        static Localization()
        {
            _resources = ResourceLoader.GetForViewIndependentUse();
        }

        /// <summary>
        /// Returns the resource string for a given resource key.
        /// </summary>
        /// <param name="key">Name of resource</param>
        /// <returns>Resource value</returns>
        public static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            return _resources.GetString(key);
        }

        public static string Get(string key, params object[] args)
        {
            return string.Format(_resources.GetString(key), args);
        }
    }
}
