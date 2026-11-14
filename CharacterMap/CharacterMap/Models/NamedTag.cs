namespace CharacterMap.Models;

public record class NamedTag(string DisplayName, object Tag);
public class NamedIcon
{
    public string DisplayName { get; set; }
    public string Id { get; set; }
    public ThemeIcon Icon { get; set; }
}
