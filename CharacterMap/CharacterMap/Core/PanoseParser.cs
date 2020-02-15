using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Core
{
    public class Panose
    {
        public bool IsSansSerifStyle { get; }
        public SerifStyle Style { get; }
        public PanoseFamily Family { get; }

        public Panose(PanoseFamily family, SerifStyle style)
        {
            Family = family;
            Style = style;
            IsSansSerifStyle = PanoseParser.IsSansSerif(Style);
        }
    }

    public static class PanoseParser
    {
        public static Panose Parse(CanvasFontFace fontFace)
        {
            byte[] panose = fontFace.Panose;

            // The contents of the Panose byte array depends on the value of the first byte. 
            // See https://docs.microsoft.com/en-us/windows/win32/api/dwrite_1/ns-dwrite_1-dwrite_panose 
            // for how the Family value changes the meaning of the following 9 bytes.
            PanoseFamily family = (PanoseFamily)panose[0];

            // Only fonts in TextDisplay family will identify their serif style
            SerifStyle style = SerifStyle.Any;
            if (family == PanoseFamily.TextDisplay)
            {
                style = (SerifStyle)panose[1];
            }

            // Warning - not all fonts store correct values for Panose information. 
            // If expanding PanoseParser in the future to read all values, direct casting
            // enums may lead to errors - safer parsing may be needed to take into account
            // faulty panose classifications.

            return new Panose(family, style);
        }

        public static bool IsSansSerif(SerifStyle style)
        {
            return
                style == SerifStyle.NormalSans ||
                style == SerifStyle.ObtuseSans ||
                style == SerifStyle.PerpendicularSans ||
                style == SerifStyle.PerpSans;
        }
    }
}
