using CharacterMap.Helpers;

namespace CharacterMap.Models
{
    public class DevOption
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="nameKey">Resource key for localized string</param>
        /// <param name="value">Value</param>
        public DevOption(string nameKey, string value)
        {
            Name = Localization.Get(nameKey);
            Value = value;
        }

        public string Name { get; }
        public string Value { get; }
    }


}
