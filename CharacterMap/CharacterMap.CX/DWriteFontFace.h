#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "DWriteProperties.h"
#include "OS2TableReader.h"
#include <vector>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteFontTableSession sealed
	{
	internal:
		DWriteFontTableSession(ComPtr<IDWriteFontFace3> face, UINT32 tag)
		{
			m_face = face;
			m_tag = tag;
			BOOL exists;
			HRESULT hr = face->TryGetFontTable(tag, &m_tableData, &m_tableSize, &m_context, &exists);
			if (FAILED(hr) || !exists)
			{
				m_tableData = nullptr;
				m_tableSize = 0;
				m_context = nullptr;
			}
		}

	public:
		virtual ~DWriteFontTableSession()
		{
			if (m_context != nullptr)
			{
				m_face->ReleaseFontTable(m_context);
				m_context = nullptr;
			}
			m_tableData = nullptr;
			m_tableSize = 0;
		}

		property bool Exists
		{
			bool get() { return m_tableData != nullptr; }
		}

		property unsigned int Size
		{
			unsigned int get() { return m_tableSize; }
		}

		Array<uint8>^ GetPart(unsigned int offset, unsigned int length)
		{
			if (m_tableData == nullptr || offset >= m_tableSize)
				return nullptr;

			unsigned int copyLength = length;
			if (offset + copyLength > m_tableSize)
				copyLength = m_tableSize - offset;

			auto arr = ref new Array<uint8>(copyLength);
			if (copyLength > 0)
				memcpy(arr->Data, (const uint8*)m_tableData + offset, copyLength);

			return arr;
		}

	private:
		ComPtr<IDWriteFontFace3> m_face;
		UINT32 m_tag;
		const void* m_tableData = nullptr;
		UINT32 m_tableSize = 0;
		void* m_context = nullptr;
	};

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
			CanvasFontFileFormatType get() { return static_cast<CanvasFontFileFormatType>(GetFontFace()->GetType()); }
		}

		property DWriteProperties^ Properties
		{
			DWriteProperties^ get() { return m_dwProperties; }
		}

		bool HasCharacter(UINT32 character)
		{
			if (m_face != nullptr)
				return m_face->HasCharacter(character);
			else
				return m_font->HasCharacter(character);
		}

		IMapView<String^, String^>^ GetInformationalStrings(CanvasFontInformation fontInformation)
		{
			auto map = ref new Map<String^, String^>();
			ComPtr<IDWriteLocalizedStrings> localizedStrings;
			BOOL exists;

			if (m_face != nullptr)
				ThrowIfFailed(m_face->GetInformationalStrings(
					static_cast<DWRITE_INFORMATIONAL_STRING_ID>(fontInformation), &localizedStrings, &exists));
			else
				ThrowIfFailed(m_font->GetInformationalStrings(
					static_cast<DWRITE_INFORMATIONAL_STRING_ID>(fontInformation), &localizedStrings, &exists));

			if (exists)
			{
				const uint32_t stringCount = localizedStrings->GetCount();

				for (uint32_t i = 0; i < stringCount; ++i)
				{
					UINT32 length;
					localizedStrings->GetStringLength(i, &length);
					length++;
					std::vector<wchar_t> nameBuf(length + 1);
					localizedStrings->GetString(i, nameBuf.data(), length);

					std::vector<wchar_t> localeBuf;
					localizedStrings->GetLocaleNameLength(i, &length);
					if (length == 0) {
						localeBuf = nameBuf;
					} else {
						length++;
						localeBuf.resize(length + 1);
						localizedStrings->GetLocaleName(i, localeBuf.data(), length);
					}

					ThrowIfFailed(map->Insert(ref new String(localeBuf.data()), ref new String(nameBuf.data())));
				}
			}

			return map->GetView();
		}

		Array<CanvasUnicodeRange>^ GetUnicodeRanges()
		{
			uint32 rangeCount;
			uint32 actualRangeCount;
			if (m_face != nullptr)
				m_face->GetUnicodeRanges(0, nullptr, &rangeCount);
			else
				m_font->GetUnicodeRanges(0, nullptr, &rangeCount);
			DWRITE_UNICODE_RANGE* ranges = new DWRITE_UNICODE_RANGE[rangeCount];
			if (m_face != nullptr)
				m_face->GetUnicodeRanges(rangeCount, ranges, &actualRangeCount);
			else
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

		property UINT16 DesignUnitsPerEm
		{
			UINT16 get() { return GetMetrics().designUnitsPerEm; }
		}

		int64 GetGlyphMetricsPacked(UINT16 glyphIndex)
		{
			UINT16 indices[] = { glyphIndex };
			DWRITE_GLYPH_METRICS metrics;
			auto face = GetFontFace();
			ThrowIfFailed(face->GetDesignGlyphMetrics(indices, 1, &metrics, FALSE));

			uint64 aw = (uint32)metrics.advanceWidth;
			uint64 lsb = (uint32)metrics.leftSideBearing;
			return (int64)((lsb << 32) | aw);
		}

		Array<uint8>^ GetFontTable(String^ tagStr)
		{
			if (tagStr == nullptr || tagStr->Length() != 4)
				throw ref new InvalidArgumentException("Tag must be 4 characters.");

			char tagChars[4];
			for (int i = 0; i < 4; i++)
				tagChars[i] = (char)tagStr->Data()[i];

			UINT32 tag = DWRITE_MAKE_OPENTYPE_TAG(tagChars[0], tagChars[1], tagChars[2], tagChars[3]);

			const void* tableData;
			UINT32 tableSize;
			BOOL exists;
			void* context;
			auto face = GetFontFace();
			ThrowIfFailed(face->TryGetFontTable(tag, &tableData, &tableSize, &context, &exists));

			if (!exists)
			{
				return nullptr;
			}

			auto arr = ref new Array<uint8>(tableSize);
			if (tableSize > 0)
			{
				memcpy(arr->Data, tableData, tableSize);
			}

			face->ReleaseFontTable(context);
			return arr;
		}

		DWriteFontTableSession^ OpenTable(String^ tagStr)
		{
			if (tagStr == nullptr || tagStr->Length() != 4)
				throw ref new InvalidArgumentException("Tag must be 4 characters.");

			char tagChars[4];
			for (int i = 0; i < 4; i++)
				tagChars[i] = (char)tagStr->Data()[i];

			UINT32 tag = DWRITE_MAKE_OPENTYPE_TAG(tagChars[0], tagChars[1], tagChars[2], tagChars[3]);
			return ref new DWriteFontTableSession(GetFontFace(), tag);
		}

		FontEmbeddingType GetEmbeddingType()
		{
			if (!m_loadedEmbed)
			{
				const void* tableData;
				UINT32 tableSize;
				BOOL exists;
				void* context;
				auto face = GetFontFace();
				ThrowIfFailed(face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('O', 'S', '/', '2'), &tableData, &tableSize, &context, &exists));

				if (exists)
				{
					auto reader = ref new OS2TableReader(tableData, tableSize);
					m_embeddingType = reader->EmbeddingType;
					delete reader;
				}
				else
					m_embeddingType = FontEmbeddingType::Installable;

				face->ReleaseFontTable(&tableData);
				m_loadedEmbed = true;
			}

			return m_embeddingType;
		}


	internal:
		DWriteFontFace(ComPtr <IDWriteFontFace3> face)
		{
			m_face = face;
		}

		DWriteFontFace(ComPtr<IDWriteFont3> font, DWriteProperties^ properties)
		{
			m_font = font;
			m_dwProperties = properties;
		};

		ComPtr<IDWriteFontCollection3> GetFontCollection()
		{
			/* NOTE: Does not support IDWriteFontFace3 constructor */
			ComPtr<IDWriteFontFamily> family;
			m_font->GetFontFamily(&family);

			ComPtr<IDWriteFontCollection> col;
			family->GetFontCollection(&col);

			ComPtr<IDWriteFontCollection3> fontCollection;
			col.As<IDWriteFontCollection3>(&fontCollection);
			return fontCollection;
		}

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

				if (m_face != nullptr)
					ThrowIfFailed(m_face->GetFontFaceReference(&ref));
				else
					ThrowIfFailed(m_font->GetFontFaceReference(&ref));

				m_fontResource = ref;
			}

			return m_fontResource;
		}

		ComPtr<IDWriteFontFace3> GetFontFace()
		{
			if (m_face != nullptr)
				return m_face;

			ComPtr<IDWriteFontFaceReference> faceRef = GetReference();;
			ComPtr<IDWriteFontFace3> face;
			faceRef->CreateFontFace(&face);
			return face;
		}

		DWRITE_FONT_METRICS1 GetMetrics()
		{
			if (m_hasMetrics == false)
			{
				if (m_face != nullptr)
					m_face->GetMetrics(&m_metrics);
				else
					m_font->GetMetrics(&m_metrics);

				m_hasMetrics = true;
			}

			return m_metrics;
		}

		void SetProperties(DWriteProperties^ props)
		{
			m_dwProperties = props;
		}

		ComPtr<IDWriteFontFace3> m_face = nullptr;
		ComPtr<IDWriteFont3> m_font = nullptr;

		DWriteProperties^ m_dwProperties = nullptr;

	private:
		inline DWriteFontFace() { }

		bool m_loadedEmbed = false;
		bool m_hasMetrics = false;

		FontEmbeddingType m_embeddingType = FontEmbeddingType::Installable;
		DWRITE_FONT_METRICS1 m_metrics{};
		CanvasFontFace^ m_fontFace = nullptr;
		ComPtr<IDWriteFontFaceReference> m_fontResource = nullptr;
	};
}