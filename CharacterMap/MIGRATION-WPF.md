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
- Local TTF/OTF/TTC/OTC/WOFF opening, folder import and file drop
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
maintained name table. Glyph previews support monochrome outlines and COLR/CPAL
base color layers, including Segoe UI Emoji.

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
3. Implement WOFF2/ZIP decoding, font installation and subsetting.
4. Match remaining UWP tool-view options, color glyphs, OpenType shaping controls,
   variable axes, drag-reordering of tabs, animation and touch behavior.
5. Import `.resw` resources, add UI Automation coverage, and produce MSIX/unpackaged releases.

## Conservative advanced-font compatibility

- `OpenTypeMetadata` reads color table presence and `fvar` axis tags, signed ranges
  and defaults on demand for the selected face, with bounded reads and validated
  offsets. Font information shows these values read-only. Collection fonts are
  explicitly reported as unknown because WPF does not expose a reliable face
  index for this parser; another collection face's metadata is never substituted.
- A persistent preview notice identifies color/variable fonts and unknown metadata.
  Character previews render COLR/CPAL base color layers in their original order
  with the default palette; unsupported formats, SVG/PNG export and printing still
  use available monochrome outlines. Arbitrary variable-axis values are not applied.
- `WebFontDecoder` reconstructs WOFF 1 TrueType/OpenType tables using .NET zlib.
  Input and decoded data are capped at 64 MiB; table ranges, alignment, overlap,
  checksums and exact decompressed lengths are checked. The sfnt checksum adjustment
  is recomputed. Optional metadata/private blocks are range-checked but not extracted.
  Original files are unchanged. Decoded fonts live in a unique process temporary
  directory, with best-effort deletion at normal process exit (crashes or font locks
  can leave temporary files). No system font installation or new package is needed.
- WOFF2 is recognized by signature and extension and rejected with an explicit
  TTF/OTF alternative. WOFF2 needs its own transformed-table decoder; it is not
  treated as ordinary Brotli data. Batch imports retain successful fonts and show
  up to three concrete errors, also available in the status tooltip.
- Tests cover compressed/uncompressed WOFF round trips using a real system font,
  unchanged table contents, imported character maps, checksums, malformed ranges,
  output limits, truncated data, signed axes, color detection and failure recovery.

Format references: [WOFF 1](https://www.w3.org/TR/WOFF/),
[WOFF 2](https://www.w3.org/TR/WOFF2/),
[OpenType fvar](https://learn.microsoft.com/en-us/typography/opentype/otspec181/fvar),
[COLR](https://learn.microsoft.com/en-us/typography/opentype/spec/colr),
[CPAL](https://learn.microsoft.com/en-us/typography/opentype/spec/cpal).

The font list provides delayed hover cards using the hovered face's glyphs, with
color emoji samples where available. The tab strip marks only its tab items and
buttons as interactive chrome, leaving empty space available for native dragging
and double-click maximize. Regression checks cover tooltip bindings, caption
properties, actual colored pixels, face/code-point changes and malformed color tables.
