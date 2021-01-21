using CharacterMap.Helpers;
using Windows.UI.Xaml.Markup;

namespace CharacterMap.Core
{
    [MarkupExtensionReturnType(ReturnType = typeof(string))]
    public class Localizer : MarkupExtension
    {
        public string Key { get; set; }

        protected override object ProvideValue()
        {
            return Localization.Get(Key);
        }
    }
}
