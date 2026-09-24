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

Platform::String^ DWriteFontFamily::Name::get()
{
	if (m_name == nullptr && m_family != nullptr)
	{
		AcquireSRWLockExclusive(&m_lock);
		if (m_name == nullptr)
		{
			const auto& locale = GetCachedUserLocale();
			ComPtr<IDWriteLocalizedStrings> names;
			if (SUCCEEDED(m_family->GetFamilyNames(&names)))
				m_name = DirectWrite::GetLocaleString(names, locale.length, const_cast<wchar_t*>(locale.name));
		}
		ReleaseSRWLockExclusive(&m_lock);
	}
	return m_name;
}

IVectorView<DWriteFontFace^>^ DWriteFontFamily::Fonts::get()
{
	if (m_fonts == nullptr)
		Inflate();
	return m_fonts;
}

int DWriteFontFamily::FontCount::get()
{
	if (m_fonts != nullptr)
		return (int)m_fonts->Size;
	return m_family ? (int)m_family->GetFontCount() : 0;
}

void DWriteFontFamily::Inflate()
{
	if (m_fonts != nullptr)
		return;

	AcquireSRWLockExclusive(&m_lock);
	if (m_fonts != nullptr)
	{
		ReleaseSRWLockExclusive(&m_lock);
		return;
	}

	const auto& locale = GetCachedUserLocale();

	if (m_name == nullptr && m_family != nullptr)
	{
		ComPtr<IDWriteLocalizedStrings> names;
		if (SUCCEEDED(m_family->GetFamilyNames(&names)))
			m_name = DirectWrite::GetLocaleString(names, locale.length, const_cast<wchar_t*>(locale.name));
	}

	auto fonts = ref new Vector<DWriteFontFace^>();
	if (m_family != nullptr)
	{
		auto fontCount = m_family->GetFontCount();
		for (uint32_t j = 0; j < fontCount; ++j)
		{
			ComPtr<IDWriteFont3> font;
			m_family->GetFont(j, &font);

			if (font != nullptr && font->GetLocality() == DWRITE_LOCALITY::DWRITE_LOCALITY_LOCAL)
			{
				String^ fontName = nullptr;
				ComPtr<IDWriteLocalizedStrings> names;
				if (SUCCEEDED(font->GetFaceNames(&names)))
					fontName = DirectWrite::GetLocaleString(names, locale.length, const_cast<wchar_t*>(locale.name));

				auto props = ref new DWriteProperties(
					DWriteFontSource::Unknown,
					nullptr,
					m_name,
					fontName,
					font);

				fonts->Append(ref new DWriteFontFace(font, props));
			}
		}
	}

	m_fonts = fonts->GetView();
	ReleaseSRWLockExclusive(&m_lock);
}


