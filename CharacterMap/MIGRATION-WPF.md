# UWP to WPF migration

The migration is intentionally hosted beside the UWP application. This keeps the
shipping implementation available as a behavior reference while allowing the WPF
application to build without the retired UWP toolchain and C++/CX project.

## Architecture mapping

| UWP area | WPF/.NET 10 replacement |
| --- | --- |
| `Windows.UI.Xaml` | `System.Windows` / WPF XAML |
| `Windows.Storage` and pickers | `System.IO` and `Microsoft.Win32` dialogs |
| UWP clipboard | `System.Windows.Clipboard` |
| Win2D text/glyph lookup | WPF `GlyphTypeface` for the first milestone |
| `ApplicationData` settings | JSON under `%LocalAppData%/CharacterMap.Wpf` |
| UWP navigation/pages | WPF window plus view-model-driven user controls |
| C++/CX DirectWrite bridge | A future C++/WinRT or managed DirectWrite adapter |
| AppX activation | unpackaged command-line/file activation, with optional MSIX |

## Implemented WPF controls and behavior

- SDK-style `net10.0-windows` WPF application and standalone solution
- Built-in .NET Fluent theme with `ThemeMode="System"`, including live Windows
  light/dark mode and accent-color synchronization
- Installed-font discovery using WPF's DirectWrite-backed font objects
- Local TTF/OTF/TTC/OTC opening, folder import and file drop
- Font filtering and actual character-to-glyph map enumeration
- Continuous, adaptive glyph browsing with viewport virtualization
- Shared UWP Unicode names and blocks; literal, hex and name search
- Font variants selected from actual `GlyphTypeface` instances
- Glyph preview, text composition, clipboard and code/HTML/C# copy tools
- SVG/PNG/text export and paginated printing of the filtered character map
- Font compare, ink calligraphy, font information and settings views
- Theme, last font, grid size and annotation preferences stored as JSON

| UWP control / area | WPF implementation | Behavior |
| --- | --- | --- |
| `CharacterGridView`, `AdaptiveGridView` | `Controls/CharacterGridView.cs`, `VirtualizingWrapPanel.cs` | Adaptive columns, continuous scrolling, viewport-only containers, arrow/Home/End/Page navigation, selection outline, tooltips, annotations, double-click/Enter to add |
| `DirectText` (C++/Win2D) | `Controls/DirectText.cs` | Actual selected-face glyph outline; supplementary Unicode scalars; size-to-fit preview; no fallback substitution |
| `ExtendedListView` | `Controls/ShellControls.cs`, `Themes/Controls.xaml` | Grouped, virtualized font list, names rendered in their own fonts, variant counts, selection and scroll-into-view |
| `ExtendedTabView` | `Controls/ShellControls.cs`, `FontTab` | Add/close/switch; independent font, variant, selection, search, block and composer state; Ctrl+T/W/Tab |
| `ButtonGroup`, `LabelButton` | `Controls/ShellControls.cs`, `Themes/Controls.xaml` | Rounded grouped menu cards with icons, descriptions, shortcuts, hover, pressed, focus and disabled states |
| `SuggestionBox` | `Controls/ShellControls.cs` | Search icon, watermark, focus accent, live filtering, Enter query and Escape clear |
| `CategoryFlyout`, `FilterFlyout`, `PreviewTip` | WPF popup, block selector and cell tooltip | Available blocks, cell size, annotation toggle, Unicode name and code tooltip |
| `ExtendedSplitView`, grid splitter | Main window sidebar and WPF `GridSplitter` | Collapsible font pane, adjustable preview width |
| Main page / app menu | `MainWindow.xaml` | Custom window chrome, font tabs, grouped menu, keyboard shortcuts, full screen |
| Compare / calligraphy / settings / font information | `Views/ToolViews.cs` | Working WPF views; compare text, pen/eraser/undo/PNG, preferences and font metadata |
| Font map print page | `Services/GlyphExportService.cs` | On-demand `DocumentPaginator`, actual glyph outlines, code annotations and page count |

The view model uses stable font object identity: caching a family's variants must
not change its hash while WPF selectors have it in their selection tables. Unicode
data is embedded from the existing UWP resources; it is not a second manually
maintained name table. Glyph rendering currently supports monochrome outlines.

## Verification

```powershell
dotnet build CharacterMap/CharacterMap.Wpf.slnx -c Release
dotnet run --project CharacterMap/CharacterMap.Wpf.Tests -c Release
```

The STA regression runner checks Unicode scalar parsing and names, system-font
loading and import, actual face selection, font selector identity, independent tabs,
composition, filters, real template and selector bindings, empty/refilled results,
SVG syntax, transparent PNG pixels and print pagination. With a 650 × 480 viewport,
50,000 characters realize 49 containers; scrolling to the final character and
resizing remain bounded. The runner also writes `wpf-main-render.png` beside its
executable as an offscreen layout artifact.

The desktop application was launched and its main layout, installed font list,
search input and glyph preview inspected. Computer Use was stopped by the user;
later selection fixes were verified by the WPF regression runner. Printer output,
pen hardware and every secondary-window interaction have not been manually tested.

## Next milestones

1. Introduce a DirectWrite interop boundary and port variable-font/OpenType metadata.
2. Port user collections and migrate existing UWP data.
3. Implement WOFF/WOFF2/ZIP decoding, font installation and subsetting.
4. Match remaining UWP tool-view options, color glyphs, OpenType shaping controls,
   variable axes, drag-reordering of tabs, animation and touch behavior.
5. Import `.resw` resources, add UI Automation coverage, and produce MSIX/unpackaged releases.
