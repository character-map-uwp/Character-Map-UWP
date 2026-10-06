#pragma once

#include "DWriteTypographyFeature.h"
#include <vector>
#include <dwrite_3.h>
#include <wrl/client.h>
#include <collection.h>

namespace CharacterMapCX
{
	public ref class DWriteTypographyCollection sealed
	{
	public:
		DWriteTypographyCollection();
		DWriteTypographyCollection(DWriteTypographyFeatureName feature);
		DWriteTypographyCollection(DWriteTypographyFeatureName feature, uint32 parameter);
		virtual ~DWriteTypographyCollection();

		void AddFeature(DWriteTypographyFeature feature);
		void AddFeature(DWriteTypographyFeatureName feature, uint32 parameter);

		[Windows::Foundation::Metadata::DefaultOverload]
		void AddFeature(DWriteTypographyFeatureName feature);

		DWriteTypographyFeature GetFeature(uint32 index);
		Platform::Array<DWriteTypographyFeature>^ GetFeatures();

		property uint32 FeatureCount
		{
			uint32 get();
		}

		property Windows::Foundation::Collections::IVectorView<DWriteTypographyFeature>^ Features
		{
			Windows::Foundation::Collections::IVectorView<DWriteTypographyFeature>^ get();
		}

		void Clear();
		uint32 GetKey();

	internal:
		Microsoft::WRL::ComPtr<IDWriteTypography> GetDWriteTypography();
		const std::vector<DWRITE_FONT_FEATURE>& GetDWriteFontFeatures() const { return m_dwriteFeatures; }

	private:
		Platform::Collections::Vector<DWriteTypographyFeature>^ m_features;
		std::vector<DWRITE_FONT_FEATURE> m_dwriteFeatures;
		Microsoft::WRL::ComPtr<IDWriteTypography> m_typography;
	};
}
