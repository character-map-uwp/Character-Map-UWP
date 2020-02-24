namespace CharacterMap.Core
{
    public class Character
    {
        public string Char { get; set; }

        public uint Index { get; set; }

        public int UnicodeIndex { get; set; }

        public string UnicodeString => "U+" + UnicodeIndex.ToString("x4").ToUpper();
    }
}