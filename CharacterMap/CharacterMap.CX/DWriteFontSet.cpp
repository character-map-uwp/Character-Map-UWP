#pragma once
#include "pch.h"
#include "DWriteFontSet.h"

using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;
using namespace concurrency;

//void DWriteFontSet::Inflate(IVectorView<DWriteFontFace^>^ fonts)
//{
//	for each (auto font in fonts)
//	{
//		ComPtr<IDWriteFontFace3> f3;
//
//		if (font->m_font->CreateFontFace(&f3) == S_OK)
//		{
//			ComPtr<IDWriteFontFace5> face;
//			f3.As(&face);
//
//			// 2. Attempt to get FAMILY locale index
//			String^ family = nullptr;
//			ComPtr<IDWriteLocalizedStrings> names;
//			if (SUCCEEDED(face->GetFamilyNames(&names)))
//				family = DirectWrite::GetLocaleString(names, _ls, _locale);
//
//			// 3. Attempt to get FACE locale index
//			String^ fname = nullptr;
//			names = nullptr;
//			if (SUCCEEDED(face->GetFaceNames(&names)))
//				fname = DirectWrite::GetLocaleString(names, _ls, _locale);
//
//			auto props = ref new DWriteProperties(DWriteFontSource::Unknown, nullptr, family, fname, font->m_font, face->HasVariations());
//			font->SetProperties(props);
//		};
//
//		font->Realize();
//	}
//}