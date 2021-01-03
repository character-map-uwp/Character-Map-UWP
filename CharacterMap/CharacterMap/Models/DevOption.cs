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
        public DevOption(string nameKey, string value, bool forceExtended = false)
        {
            Name = Localization.Get(nameKey);
            Value = value;

            UseExtendedCopy = forceExtended || (value != null && value.Length > 2048);
        }
        public bool UseExtendedCopy { get; }
        public string Name { get; }
        public string Value { get; }
    }


}
