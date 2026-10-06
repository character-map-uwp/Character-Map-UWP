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
			Microsoft::Graphics::Canvas::Text::CanvasTypographyFeatureName feature);

		static Windows::Foundation::Collections::IVectorView<Microsoft::Graphics::Canvas::Text::CanvasTypographyFeatureName>^ GetSupportedTypographicFeatures(
			DWriteFontFace^ fontFace,
			Platform::String^ text,
			Windows::Foundation::Collections::IVectorView<Microsoft::Graphics::Canvas::Text::CanvasTypographyFeatureName>^ features);
	};
}
