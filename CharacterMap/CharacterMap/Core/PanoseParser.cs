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
        public SerifStyle Style { get; }
        public PanoseFamily Family { get; }

        public Panose(PanoseFamily family, SerifStyle style)
        {
            Family = family;
            Style = style;
        }
    }

    public static class PanoseParser
    {
        public static Panose Parse(CanvasFontFace fontFace)
        {
            byte[] panose = fontFace.Panose;

            PanoseFamily family = (PanoseFamily)panose[0];
            SerifStyle style = SerifStyle.Any;

            if (family == PanoseFamily.TextDisplay)
            {
                style = (SerifStyle)panose[1];
            }

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
