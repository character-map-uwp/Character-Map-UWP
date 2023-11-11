namespace CharacterMap.Models;

public class DevOption
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="nameKey">Resource key for localized string</param>
    /// <param name="value">Value</param>
    public DevOption(string nameKey, string value, bool forceExtended = false, bool supportsTypography = false)
    {
        Name = Localization.Get(nameKey);
        Value = value;
        SupportsTypography = supportsTypography;

        UseExtendedCopy = forceExtended || (value != null && value.Length > 2048);
    }
    public string Name { get; }
    public string Value { get; }
    public bool UseExtendedCopy { get; }
    public bool SupportsTypography { get; }
}
