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

	public enum class DWriteColorRenderOption : int
	{
		Default = 0, // Renders with default color rendering behavior. COLRv0 fonts will render with COLRv0, COLRv1 fonts will render with COLRv1.
		ColrV0 = 1, // Forces COLRv0 rendering for fonts that support COLRv0, even if they also support COLRv1. Fonts that do not support COLRv0 will render with their default rendering behavior.
		ColrV1 = 2,// Forces COLRv1 rendering for fonts that support COLRv1, even if they also support COLRv0. Fonts that do not support COLRv1 will render with their default rendering behavior.
		Monochrome = 3, // Forces monochrome rendering. 
		Svg = 4
	};
}