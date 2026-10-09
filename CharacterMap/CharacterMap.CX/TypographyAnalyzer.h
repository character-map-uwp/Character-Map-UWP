#pragma once

#include "pch.h"
#include "DWriteFontFace.h"

namespace CharacterMapCX
{
	class TypographyAnalyzer
	{
	public:
		static bool CheckTypographicFeature(
			DWriteFontFace^ fontFace,
			Platform::String^ text,
			DWriteTypographyFeatureName feature);

		static Windows::Foundation::Collections::IVectorView<DWriteTypographyFeatureName>^ GetSupportedTypographicFeatures(
			DWriteFontFace^ fontFace,
			Platform::String^ text,
			Windows::Foundation::Collections::IVectorView<DWriteTypographyFeatureName>^ features);

		static bool ShapeGlyphs(
			IDWriteFontFace* face,
			const wchar_t* text,
			UINT32 textLength,
			FLOAT fontSize,
			const std::vector<DWRITE_FONT_FEATURE>& features,
			std::vector<UINT16>& glyphIndices,
			std::vector<FLOAT>& glyphAdvances,
			std::vector<DWRITE_GLYPH_OFFSET>& glyphOffsets);
	};
}
