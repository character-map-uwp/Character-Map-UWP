using Windows.UI;

namespace CharacterMap.Core
{
    public sealed class ColorPathData
    {
        public string Path { get; set; }
        public Color Color { get; set; }
        public Windows.Foundation.Rect Bounds { get; set; }
        // New fields for COLRv1 paint handling
        public bool IsGradient { get; set; }
        public bool IsClip { get; set; }
        public string PaintReference { get; set; }
        public string PaintDefinition { get; set; }
    }
}
