using CharacterMap.Core;
using Microsoft.Graphics.Canvas.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CharacterMap.Models;

public record LigatureComponent(uint GlyphIndex, string Character, uint? UnicodeIndex = null, string TooltipText = null)
{
    public bool HasCharacter => !string.IsNullOrEmpty(Character);
    public bool IsZwj => UnicodeIndex == 0x200D || Character == "\u200D";
    public bool IsZwnj => UnicodeIndex == 0x200C || Character == "\u200C";
    public bool IsSpecial => IsZwj || IsZwnj || UnicodeIndex is 0xFE0F or 0xFE0E or 0x200B or 0x00A0 or 0x0020;

    public string DisplayText => UnicodeIndex switch
    {
        0x200D => "ZWJ",
        0x200C => "ZWNJ",
        0x200B => "ZWSP",
        0xFE0F => "VS16",
        0xFE0E => "VS15",
        0x00A0 => "NBSP",
        0x0020 => "Space",
        _ when Character == "\u200D" => "ZWJ",
        _ when Character == "\u200C" => "ZWNJ",
        _ when HasCharacter => Character,
        _ => $"#{GlyphIndex}"
    };

    public string Tooltip => TooltipText ?? (UnicodeIndex switch
    {
        0x200D => $"Zero Width Joiner (ZWJ)\r\nUnicode: U+200D\r\nGlyph #{GlyphIndex}",
        0x200C => $"Zero Width Non-Joiner (ZWNJ)\r\nUnicode: U+200C\r\nGlyph #{GlyphIndex}",
        0x200B => $"Zero Width Space (ZWSP)\r\nUnicode: U+200B\r\nGlyph #{GlyphIndex}",
        0xFE0F => $"Variation Selector-16 (VS16, Emoji)\r\nUnicode: U+FE0F\r\nGlyph #{GlyphIndex}",
        0xFE0E => $"Variation Selector-15 (VS15, Text)\r\nUnicode: U+FE0E\r\nGlyph #{GlyphIndex}",
        0x00A0 => $"No-Break Space (NBSP)\r\nUnicode: U+00A0\r\nGlyph #{GlyphIndex}",
        0x0020 => $"Space\r\nUnicode: U+0020\r\nGlyph #{GlyphIndex}",
        _ when HasCharacter => $"Unicode: U+{UnicodeIndex:X4}\r\nGlyph #{GlyphIndex}",
        _ => $"Glyph #{GlyphIndex}"
    });
}

[DebuggerDisplay("LigatureModel Glyph: {LigatureGlyph}, {Components.Count} components")]
public record LigatureModel(uint LigatureGlyph, IReadOnlyList<LigatureComponent> Components, CanvasTypographyFeatureName Feature, string Name = null)
{
    public bool ContainsZwj => Components.Any(c => c.IsZwj);
    public string GlyphName => !string.IsNullOrWhiteSpace(Name) ? Name : $"#{LigatureGlyph}";
    public string ComponentMakeupString => string.Join(" + ", Components.Select(c => c.DisplayText));
    public string CombinedString => field ??= string.Join(string.Empty, Components.Select(c => c.Character));
    public string ClipboardText => !string.IsNullOrEmpty(CombinedString) ? CombinedString : $"#{LigatureGlyph}";
}

[DebuggerDisplay("LigatureGroup {Title}")]
public class LigatureGroup : IGrouping<string, LigatureModel>
{
    public CanvasTypographyFeatureName Feature { get; init; }
    public IReadOnlyList<LigatureModel> Ligatures { get; init; }
    public int Count => Ligatures?.Count ?? 0;
    public string Tag { get; }
    public string FeatureName { get; }
    public string Key { get; }
    public string Title => Key;

    public LigatureGroup(string title, string tag, string featureName, CanvasTypographyFeatureName feature, IReadOnlyList<LigatureModel> ligatures)
    {
        Key = title;
        Tag = tag;
        FeatureName = featureName;
        Feature = feature;
        Ligatures = ligatures;
    }

    public IEnumerator<LigatureModel> GetEnumerator() => Ligatures?.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
