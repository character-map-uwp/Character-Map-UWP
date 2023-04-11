#pragma once
#include "pch.h"
#include "DWriteFontFamily.h"

using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;
using namespace concurrency;


void DWriteFontFamily::Inflate()
{
	wchar_t localeName[LOCALE_NAME_MAX_LENGTH];
	int ls = GetUserDefaultLocaleName(localeName, LOCALE_NAME_MAX_LENGTH);

	String^ familyName = nullptr;
	ComPtr<IDWriteLocalizedStrings> names;
	if (SUCCEEDED(m_family->GetFamilyNames(&names)))
		familyName = DirectWrite::GetLocaleString(names, ls, localeName);

	auto fonts = ref new Vector<DWriteFontFace^>();
	auto fontCount = m_family->GetFontCount();
	for (uint32_t j = 0; j < fontCount; ++j)
	{
		ComPtr<IDWriteFont3> font;
		m_family->GetFont(j, &font);

		if (font != nullptr && font->GetLocality() == DWRITE_LOCALITY::DWRITE_LOCALITY_LOCAL)
		{
			String^ fontName = nullptr;
			if (SUCCEEDED(font->GetFaceNames(&names)))
				fontName = DirectWrite::GetLocaleString(names, ls, localeName);

			auto props = ref new DWriteProperties(
				DWriteFontSource::Unknown,
				nullptr,
				familyName,
				fontName,
				font);

			fonts->Append(ref new DWriteFontFace(font, props));
		}
	}

	m_fonts = fonts->GetView();
}


