using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CharacterMap.Core
{
    public enum PanoseFamily
    {
        /// <summary>
        /// Any typeface classification.
        /// </summary>
        Any = 0,
        /// <summary>
        /// No fit typeface classification.
        /// </summary>
        NoFit = 1,
        /// <summary>
        /// Text display typeface classification.
        /// </summary>
        TextDisplay = 2,
        /// <summary>
        /// Script (or hand written) typeface classification.
        /// </summary>
        Script = 3,
        /// <summary>
        /// Decorative typeface classification.
        /// </summary>
        Decorative = 4,
        /// <summary>
        /// Symbol typeface classification.
        /// </summary>
        Symbol = 5,
        /// <summary>
        /// Pictorial (or symbol) typeface classification.
        /// </summary>
        Pictorial = 6
    }
}
