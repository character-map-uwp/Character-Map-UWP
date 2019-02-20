namespace CharacterMap.Core
{
    public class Character
    {
        public string Char { get; set; }

        public int UnicodeIndex { get; set; }

        public string UnicodeString => "U+" + UnicodeIndex.ToString("x").ToUpper();
    }
}