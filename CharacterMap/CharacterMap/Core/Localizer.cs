using CharacterMap.Helpers;
using Windows.UI.Xaml.Markup;

namespace CharacterMap.Core
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class Localizer : MarkupExtension
    {
        public string Key { get; set; }

        public bool Lowercase { get; set; }

        protected override object ProvideValue()
        {
            if (Lowercase)
                return Localization.Get(Key).ToLower();

            return Localization.Get(Key);
        }
    }
}
