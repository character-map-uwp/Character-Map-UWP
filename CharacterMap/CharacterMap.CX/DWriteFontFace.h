#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "DWriteProperties.h"
#include <vector>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteFontFace sealed
	{
	public:

		property CanvasFontFace^ FontFace
		{
			CanvasFontFace^ get() { 
				if (m_fontFace == nullptr)
					Realize();
				return m_fontFace; 
			}
		}

		property UINT32 GlyphCount
		{
			UINT32 get() { return GetFontFace()->GetGlyphCount(); }
		}

		property CanvasFontFileFormatType FileFormatType
		{
			CanvasFontFileFormatType get() {
				return static_cast<CanvasFontFileFormatType>(
					GetFontFace()->GetType());
			}
		}

		property DWriteProperties^ Properties
		{
			DWriteProperties^ get() { return m_dwProperties; }
		}

		bool HasCharacter(UINT32 character)
		{
			return m_font->HasCharacter(character);
		}

		IMapView<String^, String^>^ GetInformationalStrings(CanvasFontInformation fontInformation)
		{
			auto map = ref new Map<String^, String^>();
			ComPtr<IDWriteLocalizedStrings> localizedStrings;
			BOOL exists;
			ThrowIfFailed(m_font->GetInformationalStrings(
				static_cast<DWRITE_INFORMATIONAL_STRING_ID>(fontInformation), &localizedStrings, &exists));


			if (localizedStrings)
			{
				const uint32_t stringCount = localizedStrings->GetCount();

				for (uint32_t i = 0; i < stringCount; ++i)
				{
					UINT32 length;
					localizedStrings->GetStringLength(i, &length);
					length++;
					wchar_t* name = new wchar_t[length + 1];
					localizedStrings->GetString(i, name, length);

					localizedStrings->GetLocaleNameLength(i, &length);
					length++;
					wchar_t* locale = new wchar_t[length + 1];
					localizedStrings->GetLocaleName(i, locale, length);

					ThrowIfFailed(map->Insert(
						ref new String(locale), ref new String(name)));
				}
			}

			return map->GetView();
		}

		Array<CanvasUnicodeRange>^ GetUnicodeRanges()
		{
			uint32 rangeCount;
			uint32 actualRangeCount;
			m_font->GetUnicodeRanges(0, nullptr, &rangeCount);
			DWRITE_UNICODE_RANGE* ranges = new DWRITE_UNICODE_RANGE[rangeCount];
			m_font->GetUnicodeRanges(rangeCount, ranges, &actualRangeCount);
			auto mine = reinterpret_cast<CanvasUnicodeRange*>(ranges);
			return Platform::ArrayReference<CanvasUnicodeRange>(mine, actualRangeCount);
		}

		Array<INT32>^ GetGlyphIndices(const Array<UINT32>^ indicies)
		{
			std::vector<unsigned short> glyphIndices(indicies->Length);
			auto output = ref new Platform::Array<INT32>(indicies->Length);
			ThrowIfFailed(GetFontFace()->GetGlyphIndices(indicies->Data, indicies->Length, glyphIndices.data()));

			for (uint32_t i = 0; i < indicies->Length; ++i)
				output[i] = static_cast<INT32>(glyphIndices[i]);
			return output;
		}

		INT32 GetGlyphIndice(UINT32 indicie)
		{
			std::vector<unsigned int> in(1);
			in[0] = indicie;
			std::vector<unsigned short> out(1);
			ThrowIfFailed(GetFontFace()->GetGlyphIndices(in.data(), 1, out.data()));

			return static_cast<INT32>(out[0]);
		}

	internal:
		DWriteFontFace(ComPtr<IDWriteFont3> font, DWriteProperties^ properties)
		{
			m_font = font;
			m_dwProperties = properties;
		};

		void Realize()
		{
			GetReference();
			m_fontFace = GetOrCreate<CanvasFontFace>(m_fontResource.Get());
		}

		ComPtr<IDWriteFontFaceReference> GetReference()
		{
			if (m_fontResource == nullptr)
			{
				ComPtr<IDWriteFontFaceReference> ref;
				ThrowIfFailed(m_font->GetFontFaceReference(&ref));
				m_fontResource = ref;
			}

			return m_fontResource;
		}

		ComPtr<IDWriteFontFace3> GetFontFace()
		{
			ComPtr<IDWriteFontFaceReference> faceRef = GetReference();;
			ComPtr<IDWriteFontFace3> face;
			faceRef->CreateFontFace(&face);
			return face;
		}

		DWRITE_FONT_METRICS1 GetMetrics()
		{
			if (m_hasMetrics == false)
			{
				m_font->GetMetrics(&m_metrics);
				m_hasMetrics = true;
			}

			return m_metrics;
		}

		void SetProperties(DWriteProperties^ props)
		{
			m_dwProperties = props;
		}

		ComPtr<IDWriteFont3> m_font = nullptr;

	private:
		inline DWriteFontFace() { }

		bool m_hasMetrics = false;
		DWRITE_FONT_METRICS1 m_metrics{};
		CanvasFontFace^ m_fontFace = nullptr;
		DWriteProperties^ m_dwProperties = nullptr;
		ComPtr<IDWriteFontFaceReference> m_fontResource = nullptr;
	};
}