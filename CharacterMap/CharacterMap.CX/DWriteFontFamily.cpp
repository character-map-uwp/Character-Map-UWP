#pragma once
#include "pch.h"
#include "DWriteFontFamily.h"

using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;
using namespace concurrency;


namespace
{
	struct CachedLocale
	{
		wchar_t name[LOCALE_NAME_MAX_LENGTH];
		int length;
	};

	const CachedLocale& GetCachedUserLocale()
	{
		static CachedLocale s_locale = []() {
			CachedLocale loc{};
			loc.length = GetUserDefaultLocaleName(loc.name, LOCALE_NAME_MAX_LENGTH);
			return loc;
		}();
		return s_locale;
	}
}

void DWriteFontFamily::Inflate()
{
	if (m_fonts != nullptr)
		return;

	const auto& locale = GetCachedUserLocale();

	String^ familyName = nullptr;
	ComPtr<IDWriteLocalizedStrings> names;
	if (SUCCEEDED(m_family->GetFamilyNames(&names)))
		familyName = DirectWrite::GetLocaleString(names, locale.length, const_cast<wchar_t*>(locale.name));

	m_name = familyName;

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
				fontName = DirectWrite::GetLocaleString(names, locale.length, const_cast<wchar_t*>(locale.name));

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


