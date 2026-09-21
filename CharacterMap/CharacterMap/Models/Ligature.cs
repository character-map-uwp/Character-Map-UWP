using CharacterMap.Core;
using Microsoft.Graphics.Canvas.Text;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CharacterMap.Models;

public record LigatureComponent(uint GlyphIndex, string Character)
{
    public bool HasCharacter => !string.IsNullOrEmpty(Character);
    public string DisplayText => HasCharacter ? Character : $"#{GlyphIndex}";
}

[DebuggerDisplay("LigatureModel {CombinedString} - Glyph: {LigatureGlyph}")]
public record LigatureModel(uint LigatureGlyph, IReadOnlyList<LigatureComponent> Components, CanvasTypographyFeatureName Feature)
{
    public string ComponentMakeupString => string.Join(" + ", Components.Select(c => c.DisplayText));
    public string CombinedString => field ??= string.Join(string.Empty, Components.Select(c => c.Character));
}

[DebuggerDisplay("LigatureGroup {Title}")]
public class LigatureGroup : IGrouping<string, LigatureModel>
{
    public CanvasTypographyFeatureName Feature { get; init; }
    public IReadOnlyList<LigatureModel> Ligatures { get; init; }
    public int Count => Ligatures?.Count ?? 0;
    public string Tag => DirectWrite.GetFeatureTag((uint)Feature);
    public string Key { get; }

    public LigatureGroup(string title, CanvasTypographyFeatureName feature, IReadOnlyList<LigatureModel> ligatures)
    {
        Key = title;
        Feature = feature;
        Ligatures = ligatures;
    }

    public IEnumerator<LigatureModel> GetEnumerator() => Ligatures?.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
