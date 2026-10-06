#include "pch.h"
#include "DWriteTypographyCollection.h"

using namespace CharacterMapCX;
using namespace Platform::Collections;
using namespace Windows::Foundation::Collections;

DWriteTypographyCollection::DWriteTypographyCollection()
{
	m_features = ref new Vector<DWriteTypographyFeature>();
}

DWriteTypographyCollection::DWriteTypographyCollection(DWriteTypographyFeatureName feature)
{
	m_features = ref new Vector<DWriteTypographyFeature>();
	AddFeature(feature, 1);
}

DWriteTypographyCollection::DWriteTypographyCollection(DWriteTypographyFeatureName feature, uint32 parameter)
{
	m_features = ref new Vector<DWriteTypographyFeature>();
	AddFeature(feature, parameter);
}

DWriteTypographyCollection::~DWriteTypographyCollection()
{
}

void DWriteTypographyCollection::AddFeature(DWriteTypographyFeature feature)
{
	if (feature.Name == DWriteTypographyFeatureName::None)
		return;

	m_features->Append(feature);
	DWRITE_FONT_FEATURE dwriteFeat{};
	dwriteFeat.nameTag = static_cast<DWRITE_FONT_FEATURE_TAG>(feature.Name);
	dwriteFeat.parameter = feature.Parameter;
	m_dwriteFeatures.push_back(dwriteFeat);
	if (m_typography != nullptr)
		m_typography->AddFontFeature(dwriteFeat);
}

void DWriteTypographyCollection::AddFeature(DWriteTypographyFeatureName feature, uint32 parameter)
{
	AddFeature(DWriteTypographyFeature{ feature, parameter });
}

void DWriteTypographyCollection::AddFeature(DWriteTypographyFeatureName feature)
{
	AddFeature(feature, 1);
}

DWriteTypographyFeature DWriteTypographyCollection::GetFeature(uint32 index)
{
	return m_features->GetAt(index);
}

Platform::Array<DWriteTypographyFeature>^ DWriteTypographyCollection::GetFeatures()
{
	auto arr = ref new Platform::Array<DWriteTypographyFeature>(m_features->Size);
	for (uint32 i = 0; i < m_features->Size; ++i)
		arr[i] = m_features->GetAt(i);
	return arr;
}

uint32 DWriteTypographyCollection::FeatureCount::get()
{
	return m_features->Size;
}

IVectorView<DWriteTypographyFeature>^ DWriteTypographyCollection::Features::get()
{
	return m_features->GetView();
}

void DWriteTypographyCollection::Clear()
{
	m_features->Clear();
	m_dwriteFeatures.clear();
	m_typography = nullptr;
}

uint32 DWriteTypographyCollection::GetKey()
{
	uint32 hash = 0;
	for (const auto& f : m_dwriteFeatures)
	{
		hash ^= static_cast<uint32>(f.nameTag) + 0x9e3779b9 + (hash << 6) + (hash >> 2);
		hash ^= f.parameter + 0x9e3779b9 + (hash << 6) + (hash >> 2);
	}
	return hash;
}

Microsoft::WRL::ComPtr<IDWriteTypography> DWriteTypographyCollection::GetDWriteTypography()
{
	if (m_typography == nullptr)
	{
		Microsoft::WRL::ComPtr<IDWriteFactory> factory;
		if (SUCCEEDED(DWriteCreateFactory(DWRITE_FACTORY_TYPE_SHARED, __uuidof(IDWriteFactory), &factory)))
		{
			if (SUCCEEDED(factory->CreateTypography(&m_typography)))
			{
				for (const auto& feat : m_dwriteFeatures)
				{
					m_typography->AddFontFeature(feat);
				}
			}
		}
	}
	return m_typography;
}
