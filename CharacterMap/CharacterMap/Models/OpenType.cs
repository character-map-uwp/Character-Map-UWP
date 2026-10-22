using CharacterMap.Core;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CharacterMap.Models;

[DebuggerDisplay("OpenTypeGlyphInfo Glyph: #{GlyphIndex}, Char: {Character}")]
public record OpenTypeGlyphInfo(
    uint GlyphIndex,
    string Character,
    uint? UnicodeIndex = null,
    string TooltipText = null)
{
    public bool HasCharacter => !string.IsNullOrEmpty(Character);

    public string DisplayText => UnicodeIndex switch
    {
        0x00A0 => "NBSP",
        0x0020 => "Space",
        _ when HasCharacter => Character,
        _ => $"#{GlyphIndex}"
    };

    public string Tooltip => TooltipText ?? (UnicodeIndex switch
    {
        0x00A0 => $"No-Break Space (NBSP)\r\nUnicode: U+00A0\r\nGlyph #{GlyphIndex}",
        0x0020 => $"Space\r\nUnicode: U+0020\r\nGlyph #{GlyphIndex}",
        _ when HasCharacter => $"Unicode: U+{UnicodeIndex:X4}\r\nGlyph #{GlyphIndex}",
        _ => $"Glyph #{GlyphIndex}"
    });
}

[DebuggerDisplay("OpenTypeSubstitutionModel Input: {InputDisplayText} -> {SubstituteDisplayText}")]
public record OpenTypeSubstitutionModel(
    IReadOnlyList<OpenTypeGlyphInfo> InputSequence,
    IReadOnlyList<OpenTypeGlyphInfo> SubstituteSequence,
    DWriteTypographyFeatureName Feature,
    string FeatureTag)
{
    public OpenTypeGlyphInfo Input => InputSequence?.FirstOrDefault();
    public OpenTypeGlyphInfo Substitute => SubstituteSequence?.FirstOrDefault();

    public bool IsSingle => (InputSequence?.Count ?? 0) <= 1 && (SubstituteSequence?.Count ?? 0) <= 1;

    public string InputDisplayText => InputSequence is not null
        ? string.Join(string.Empty, InputSequence.Select(c => c.DisplayText))
        : string.Empty;

    public string SubstituteDisplayText => SubstituteSequence is not null
        ? string.Join(string.Empty, SubstituteSequence.Select(c => c.DisplayText))
        : string.Empty;

    public OpenTypeSubstitutionModel(
        OpenTypeGlyphInfo input,
        OpenTypeGlyphInfo substitute,
        DWriteTypographyFeatureName feature,
        string featureTag)
        : this([input], [substitute], feature, featureTag)
    {
    }
}

[DebuggerDisplay("OpenTypeFeatureGroup {Key} ({Count} items)")]
public class OpenTypeFeatureGroup : IGrouping<string, OpenTypeSubstitutionModel>
{
    public string Key { get; }
    public string Tag { get; }
    public string FeatureName { get; }
    public string Description { get; }
    public DWriteTypographyFeatureName Feature { get; }
    public IReadOnlyList<OpenTypeSubstitutionModel> Substitutions { get; }
    public int Count => Substitutions?.Count ?? 0;

    public OpenTypeFeatureGroup(
        string title,
        string tag,
        string featureName,
        string description,
        DWriteTypographyFeatureName feature,
        IReadOnlyList<OpenTypeSubstitutionModel> substitutions)
    {
        Key = title;
        Tag = tag;
        FeatureName = featureName;
        Description = description;
        Feature = feature;
        Substitutions = substitutions;
    }

    public IEnumerator<OpenTypeSubstitutionModel> GetEnumerator() => Substitutions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
