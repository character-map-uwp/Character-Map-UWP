#pragma once

// Underlying type is unsigned int for Flags. Must be explicitly specified
using namespace Platform::Metadata;

namespace CharacterMapCX
{
	[FlagsAttribute]
	public enum class GlyphImageFormat : unsigned int
	{
		/// <summary>
		/// Indicates no data is available for this glyph.
		/// </summary>
		None = 0x00000000,

		/// <summary>
		/// The glyph has TrueType outlines.
		/// </summary>
		TrueType = 0x00000001,

		/// <summary>
		/// The glyph has CFF outlines.
		/// </summary>
		Cff = 0x00000002,

		/// <summary>
		/// The glyph has multilayered COLR data.
		/// </summary>
		Colr = 0x00000004,

		/// <summary>
		/// The glyph has SVG outlines as standard XML.
		/// </summary>
		/// <remarks>
		/// Fonts may store the content gzip'd rather than plain text,
		/// indicated by the first two bytes as gzip header {0x1F 0x8B}.
		/// </remarks>
		Svg = 0x00000008,

		/// <summary>
		/// The glyph has PNG image data, with standard PNG IHDR.
		/// </summary>
		Png = 0x00000010,

		/// <summary>
		/// The glyph has JPEG image data, with standard JIFF SOI header.
		/// </summary>
		Jpeg = 0x00000020,

		/// <summary>
		/// The glyph has TIFF image data.
		/// </summary>
		Tiff = 0x00000040,

		/// <summary>
		/// The glyph has raw 32-bit premultiplied BGRA data.
		/// </summary>
		PremultipliedB8G8R8A8 = 0x00000080,
	};
}
