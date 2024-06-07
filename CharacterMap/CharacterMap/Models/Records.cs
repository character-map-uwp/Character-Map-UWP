namespace CharacterMap.Models;

public record class FontQueryResults(IEnumerable<InstalledFont> FontList, string FilterTitle, bool ResultsFiltered);

public record FontImportResult(List<StorageFile> Imported, List<StorageFile> Existing, List<(IStorageItem, string)> Invalid);
