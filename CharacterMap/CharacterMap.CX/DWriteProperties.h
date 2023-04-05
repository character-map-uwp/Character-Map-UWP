#pragma once

#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include "DWriteFontSource.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Text;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteProperties sealed
	{
	public:
		static DWriteProperties^ CreateDefault()
		{
			return ref new DWriteProperties(DWriteFontSource::PerMachine, nullptr, "Segoe UI", "Regular", false);
		}

		property bool IsColorFont { bool get() { return m_isColorFont; } }

		property bool IsSymbolFont	{ bool get() { return m_isSymbolFont; } }

		property bool HasVariations { bool get() { return m_hasVariations; } }

		property String^ FamilyName { String^ get() { return m_familyName; } }

		property String^ FaceName	{ String^ get() { return m_faceName; } }

		property FontWeight Weight { FontWeight get() { return m_weight; } }

		property FontStyle Style { FontStyle get() { return m_style; } }

		property FontStretch Stretch { FontStretch get() { return m_stretch; } }

		/// <summary>
		/// Source of the file
		/// </summary>
		property DWriteFontSource Source { DWriteFontSource get() { return m_source; } }

		property Array<UINT8>^ Panose { Array<UINT8>^ get() { return m_panose; }}


		/// <summary>
		/// Friendly name of the remote provider, if applicable
		/// </summary>
		property String^ RemoteProviderName
		{
			String^ get() { return m_remoteSource; }
		}

	internal:

		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, ComPtr<IDWriteFont3> font)
		{
			m_weight = Windows::UI::Text::FontWeight{ static_cast<uint16_t>(font->GetWeight()) };
			m_style = static_cast<Windows::UI::Text::FontStyle>(font->GetStyle());
			m_stretch = static_cast<Windows::UI::Text::FontStretch>(font->GetStretch());

			m_isSymbolFont = font->IsSymbolFont();
			m_isColorFont = font->IsColorFont();

			ComPtr<IDWriteFontFace3> f3;
			ComPtr<IDWriteFontFace5> face;
			font->CreateFontFace(&f3);
			f3.As< IDWriteFontFace5>(&face);

			/*DWRITE_PANOSE panose;
			font->GetPanose(&panose);

			m_panose = ref new Array<UINT8>(10);
			for (unsigned int i = 0; i < m_panose->Length; i++)
			{
				m_panose[i] = panose.values[i];
			}*/

			m_font = font;

			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;
			m_hasVariations = face->HasVariations();
		}

		DWriteProperties(DWriteFontSource source, String^ remoteSource, String^ familyName, String^ faceName, bool isColor, bool hasVariations)
		{
			m_isColorFont = isColor;
			m_source = source;
			m_remoteSource = remoteSource;
			m_familyName = familyName;
			m_faceName = faceName;
			m_hasVariations = hasVariations;
		}

		ComPtr<IDWriteFont3> m_font = nullptr;

	private:
		inline DWriteProperties() { }

		Array<UINT8>^ m_panose = nullptr;

		FontWeight m_weight;
		FontStyle m_style = FontStyle::Normal;
		FontStretch m_stretch = FontStretch::Normal;

		bool m_hasVariations = false;
		bool m_isColorFont = false;
		bool m_isSymbolFont = false;
		String^ m_remoteSource = nullptr;
		String^ m_familyName = nullptr;
		String^ m_faceName = nullptr;
		DWriteFontSource m_source = DWriteFontSource::Unknown;
	};
}