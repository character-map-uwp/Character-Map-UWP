namespace CharacterMap.Models;

public enum ExportNamingScheme
{
    /// <summary>
    /// Use the file names returned by the system
    /// </summary>
    System,
    /// <summary>
    /// Generate file names based on {Font Family} + {Font Name}
    /// </summary>
    Optimised
}
