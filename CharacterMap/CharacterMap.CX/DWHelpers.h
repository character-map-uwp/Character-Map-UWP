#pragma once
#include <basetsd.h>
#include <d2d1.h>
#include <dwrite_3.h>

template <class T> void SafeRelease(T** ppT)
{
    if (*ppT)
    {
        (*ppT)->Release();
        *ppT = NULL;
    }
}

namespace
{
	/// <summary>
	/// Opposite of DWRITE_MAKE_OPENTYPE_TAG, returns String
	/// representation of OpenType tag.
	/// </summary>
	Platform::String^ GetOpenTypeFeatureTag(UINT32 value)
	{
		wchar_t buffer[] = L"    ";
		buffer[3] = (wchar_t)((value >> 24) & 0xFF);
		buffer[2] = (wchar_t)((value >> 16) & 0xFF);
		buffer[1] = (wchar_t)((value >> 8) & 0xFF);
		buffer[0] = (wchar_t)((value >> 0) & 0xFF);

		return ref new Platform::String(buffer);
	}
	
	float ToNormalizedFloat(uint8_t v)
	{
		return static_cast<float>(v) / 255.0f;
	}
   

	D2D1_COLOR_F ToD2DColor(Windows::UI::Color const& color)
	{
		return D2D1::ColorF(
			ToNormalizedFloat(color.R),
			ToNormalizedFloat(color.G),
			ToNormalizedFloat(color.B),
			ToNormalizedFloat(color.A));
	}

	inline DWRITE_FONT_SIMULATIONS ToDWriteFontSimulations(Windows::UI::Xaml::Media::StyleSimulations simulations)
	{
		switch (simulations)
		{
			case Windows::UI::Xaml::Media::StyleSimulations::BoldSimulation:
				return DWRITE_FONT_SIMULATIONS_BOLD;
			case Windows::UI::Xaml::Media::StyleSimulations::ItalicSimulation:
				return DWRITE_FONT_SIMULATIONS_OBLIQUE;
			case Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation:
				return static_cast<DWRITE_FONT_SIMULATIONS>(DWRITE_FONT_SIMULATIONS_BOLD | DWRITE_FONT_SIMULATIONS_OBLIQUE);
			case Windows::UI::Xaml::Media::StyleSimulations::None:
			default:
				return DWRITE_FONT_SIMULATIONS_NONE;
		}
	}
	inline Windows::UI::Xaml::Media::StyleSimulations ToStyleSimulations(DWRITE_FONT_SIMULATIONS simulations)
	{
		bool isBold = (simulations & DWRITE_FONT_SIMULATIONS_BOLD) != 0;
		bool isOblique = (simulations & DWRITE_FONT_SIMULATIONS_OBLIQUE) != 0;

		if (isBold && isOblique)
			return Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation;
		if (isBold)
			return Windows::UI::Xaml::Media::StyleSimulations::BoldSimulation;
		if (isOblique)
			return Windows::UI::Xaml::Media::StyleSimulations::ItalicSimulation;

		return Windows::UI::Xaml::Media::StyleSimulations::None;
	}


}