# Character Map WPF migration

This is the .NET 10/WPF migration target. The original UWP project remains in the
solution as a functional reference while features are moved incrementally.

## Run

```powershell
dotnet build CharacterMap/CharacterMap.Wpf.slnx
dotnet run --project CharacterMap/CharacterMap.Wpf/CharacterMap.Wpf.csproj
```

The WPF shell now uses dedicated controls and templates derived from the UWP
interaction model. It includes a grouped font preview list, font tabs with
independent state, a continuous virtualized character grid, actual font-face
variants, large glyph preview, Unicode name/code-point/block search, an editable
character composer, and the grouped application menu. It opens local TTF/OTF/TTC/OTC
files and folders and accepts dropped font files.

Compare, calligraphy, font information and settings are WPF views. Selected glyphs
can be exported as SVG, transparent PNG or text, and the current filtered character
map can be printed. Font, theme, cell size and annotation preferences are persisted.

```powershell
dotnet run --project CharacterMap/CharacterMap.Wpf.Tests -c Release
```

This dependency-free STA test runner exercises real WPF templates and selection
bindings, 50,000-character virtualization, Unicode and tab behavior, font import,
vector/raster export and print pagination. See `../MIGRATION-WPF.md` for the detailed
control mapping and remaining gaps.

## Remaining UWP feature areas

- DirectWrite color glyphs, variable-font axes and OpenType feature inspection
- User collections and migration of UWP application data
- WOFF/WOFF2/ZIP decoding, font installation and font subsetting
- Full UWP tool-view options, composition animations and touch/pen parity
- Packaging, file association, localization and accessibility parity
