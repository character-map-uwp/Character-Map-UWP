#pragma once

// Underlying type is unsigned int for Flags. Must be explicitly specified
using namespace Platform::Metadata;

namespace CharacterMapCX
{
	public enum class DWriteFontSource : int
	{
		/// <summary>
		/// The font source is unknown or is not any of the other defined font source types.
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// The font source is a font file, which is installed for all users on the device.
		/// </summary>
		PerMachine = 1,

		/// <summary>
		/// The font source is a font file, which is installed for the current user.
		/// </summary>
		PerUser = 2,

		/// <summary>
		/// The font source is an APPX package, which includes one or more font files.
		/// The font source name is the full name of the package.
		/// </summary>
		AppxPackage = 3,

		/// <summary>
		/// The font source is a font provider for downloadable fonts.
		/// </summary>
		RemoteFontProvider = 4
	};
}
