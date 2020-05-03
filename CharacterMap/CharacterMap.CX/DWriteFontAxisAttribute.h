#pragma once

// Underlying type is unsigned int for Flags. Must be explicitly specified
using namespace Platform::Metadata;

namespace CharacterMapCX
{
	/// <summary>
	/// WinRT projection of DWRITE_FONT_AXIS_ATTRIBUTE
	/// </summary>
	public enum class DWriteFontAxisAttribute : int
	{
		/// <summary>
		/// No attributes.
		/// </summary>
		None = 0x0000,

		/// <summary>
		/// This axis is implemented as a variation axis in a variable font, with a continuous range of
		/// values, such as a range of weights from 100..900. Otherwise it is either a static axis that
		/// holds a single point, or it has a range but doesn't vary, such as optical size in the Skia
		/// Heading font which covers a range of points but doesn't interpolate any new glyph outlines.
		/// </summary>
		Variable = 0x0001,

		/// <summary>
		/// This axis is recommended to be remain hidden in user interfaces. The font developer may
		/// recommend this if an axis is intended to be accessed only programmatically, or is meant for
		/// font-internal or font-developer use only. The axis may be exposed in lower-level font
		/// inspection utilities, but should not be exposed in common or even advanced-mode user
		/// interfaces in content-authoring apps.
		/// </summary>
		Hidden = 0x0002,
	};
}