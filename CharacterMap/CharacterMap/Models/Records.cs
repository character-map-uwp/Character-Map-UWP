namespace CharacterMap.Models;

public record class FontQueryResults(IEnumerable<InstalledFont> FontList, string FilterTitle, bool ResultsFiltered);
