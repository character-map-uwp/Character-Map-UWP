using namespace Platform::Metadata;

namespace CharacterMapCX
{
	[Flags]
	public enum class DWriteFontSimulations : unsigned int
	{
		None = 0u,                // DWRITE_FONT_SIMULATIONS_NONE
		Bold = 1u,                // DWRITE_FONT_SIMULATIONS_BOLD
		Oblique = 2u              // DWRITE_FONT_SIMULATIONS_OBLIQUE
	};
}