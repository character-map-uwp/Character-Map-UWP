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
	};
}
