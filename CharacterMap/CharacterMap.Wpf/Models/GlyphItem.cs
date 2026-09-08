using System.Globalization;
using System.Text;

namespace CharacterMap.Wpf.Models;

public sealed record GlyphItem(int CodePoint)
{
    public string Text => char.ConvertFromUtf32(CodePoint);
    public string Code => $"U+{CodePoint:X4}";
    public string DecimalCode => CodePoint.ToString(CultureInfo.InvariantCulture);
    public string Category => Rune.GetUnicodeCategory(new Rune(CodePoint)).ToString();
    public string Name => Services.UnicodeDataService.GetName(CodePoint);
    public string Block => Services.UnicodeDataService.GetBlock(CodePoint).Name;
    public string Description => $"{Name}\n{Code}, {Block}";
}
