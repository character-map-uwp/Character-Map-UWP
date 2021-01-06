#pragma once
#include <pch.h>
#include <TableReader.h>
#include "DWriteFontAxisAttribute.h"
#include "DWriteNamedFontAxisValue.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Platform;
using namespace CharacterMapCX;
using namespace std;

namespace CharacterMapCX
{
	/* 
		This entire file is a Work-in-progress and not currently used.
		CMAP Spec: https://docs.microsoft.com/en-gb/typography/opentype/spec/cmap
	*/

	ref class SequentialMapGroup
	{
	public:
		/// <summary>
		/// First character code in this group
		/// </summary>
		property uint32 StartCharCode;

		/// <summary>
		/// Last character code in this group
		/// </summary>
		property uint32 EndCharCode;

		/// <summary>
		/// Glyph index corresponding to the starting character code
		/// </summary>
		property uint32 StartGlyphID;
	};

	ref class EncodingRecord
	{
	internal:
		property uint16	PlatformID;
		property uint16	EncodingID;
		property uint32	Offset;
	};

	ref class VariationRecord
	{
	internal:
		property int	VariationSelector;
		property uint16	DefaultUVSOffset;
		property uint32	NonDefaultUVSOffset;
	};

	ref class VariationSelector sealed
	{
	public:
		property IMapView<int, uint16>^ UVSMappings;
		property IVectorView<int>^ DefaultStartCodes;
		property IVectorView<int>^ DefaultEndCodes;

	internal:
		VariationSelector(
			Vector<int>^ starts,
			Vector<int>^ ends,
			IMapView<int, uint16>^ mappings)
		{
			DefaultStartCodes = starts->GetView();
			DefaultEndCodes = ends->GetView();
			UVSMappings = mappings;
		}
	};

	ref class CharMap
	{
	public:
		virtual uint16 Format() { return 0; }

		virtual uint32 GetGlyphIndex(uint32 unicode) { return 0; }
		virtual uint32 GetUnicode(uint32 glyphIndex) { return 0; }
	};

	/// <summary>
	/// Standard character-to-glyph-index mapping subtable for fonts that support only Unicode 
	/// Basic Multilingual Plane characters (U+0000 to U+FFFF).
	/// </summary>
	ref class Format4CharMap sealed : CharMap
	{
	public:
		uint16 Format() override { return 4; }

	internal:
		property uint16 Length;
		property uint16 Language;
		property uint16 SegCountX2;
		property uint16 SearchRange;
		property uint16 EntrySelector;
		property uint16 RangeShift;
		property IVectorView<uint16>^ EndCodes;
		property IVectorView<uint16>^ StartCodes;
	};

	/// <summary>
	/// Maps 16-bit characters to glyph indexes when the character codes for a font fall into 
	/// a single contiguous range.
	/// </summary>
	ref class Format6CharMap sealed : CharMap
	{
	public:
		uint16 Format() override { return 6; }

		uint32 GetGlyphIndex(uint32 unicode) override
		{
			uint32 offset = unicode - m_startCode;
			if (offset >= 0 && offset < sizeof(m_glyphs))
				return m_glyphs[offset];

			return 0;
		}

	internal:
		Format6CharMap(uint16 startCode, const Array<uint16>^ glyphs)
		{
			m_startCode = startCode;
			m_glyphs = glyphs;
		}

	private:
		uint16 m_startCode;
		const Array<uint16>^ m_glyphs;
	};

	/// <summary>
	/// Maps 32-bit characters to glyph indexes when the character codes for a font fall into 
	/// a single contiguous range.
	/// </summary>
	ref class Format10CharMap sealed : CharMap
	{
	public:
		uint16 Format() override { return 10; }

		uint32 GetGlyphIndex(uint32 unicode) override
		{
			uint32 offset = unicode - m_startCode;
			if (offset >= 0 && offset < sizeof(m_glyphs))
				return m_glyphs[offset];

			return 0;
		}

	internal:
		Format10CharMap(uint32 startCode, const Array<uint16>^ glyphs)
		{
			m_startCode = startCode;
			m_glyphs = glyphs;
		}

	private:
		uint32 m_startCode;
		const Array<uint16>^ m_glyphs;
	};

	/// <summary>
	/// Standard character-to-glyph-index mapping subtable for fonts supporting Unicode 
	/// character repertoires that include supplementary-plane characters (U+10000 to U+10FFFF).
	/// </summary>
	ref class Format12CharMap sealed : CharMap
	{
	public:
		uint16 Format() override { return 12; }

		uint32 GetGlyphIndex(uint32 unicode) override
		{
			for (auto group : m_map)
			{
				if (unicode >= group->StartCharCode && unicode <= group->EndCharCode)
				{
					uint32 offset = unicode - group->StartCharCode;
					return group->StartGlyphID + offset;
				}
			}

			return 0;
		}

	internal:
		Format12CharMap(Vector<SequentialMapGroup^>^ map)
		{
			m_map = map;
		}

	private:
		Vector<SequentialMapGroup^>^ m_map;
	};

	ref class CmapFormat14 sealed
	{
	public:
		property uint16 Length;
		property IMapView<int, VariationSelector^>^ VariationSelectors;


	internal:
		CmapFormat14(TableReader^ reader)
		{
			Length = reader->GetUInt16();
			auto recordCount = reader->GetUInt32();
			auto records = ref new Vector<VariationRecord^>();
			auto selectors = ref new Map<int, VariationSelector^>();

			for (int i = 0; i < recordCount; i++)
			{
				auto rec = ref new VariationRecord();
				rec->VariationSelector = reader->GetUInt24();
				rec->DefaultUVSOffset = reader->GetUInt32();
				rec->NonDefaultUVSOffset = reader->GetUInt32();
				records->Append(rec);
			}

			auto startCodes = ref new Vector<int>();
			auto endCodes = ref new Vector<int>();

			for (int i = 0; i < recordCount; i++)
			{
				auto record = records->GetAt(i);
				auto startCodes = ref new Vector<int>();
				auto endCodes = ref new Vector<int>();
				auto mappings = ref new Map<int, uint16>();

				if (record->DefaultUVSOffset != 0)
				{
					reader->GoToPosition(record->DefaultUVSOffset);
					auto ranges = reader->GetUInt32();
					for (int r = 0; r < ranges; r++)
					{
						int start = reader->GetUInt24();
						startCodes->Append(start);
						endCodes->Append(start + reader->GetUInt8());
					}
				}

				if (record->NonDefaultUVSOffset != 0)
				{
					reader->GoToPosition(record->DefaultUVSOffset);
					auto ranges = reader->GetUInt32();
					for (int r = 0; r < ranges; r++)
					{
						int unicode = reader->GetUInt24();
						uint16 glyph = reader->GetUInt16();
						mappings->Insert(unicode, glyph);
					}
				}

				auto selector = ref new VariationSelector(
					startCodes,
					endCodes,
					mappings->GetView());

				selectors->Insert(record->VariationSelector, selector);
			}

			VariationSelectors = selectors->GetView();
		}
	};

	public ref class CharacterMapping sealed
	{
	public:
		uint32 GetGlyphIndex(uint32 codepoint)
		{
			for (IKeyValuePair<uint16, CharMap^>^ m : m_maps)
			{
				auto index = m->Value->GetGlyphIndex(codepoint);
				if (index > 0)
					return index;
			}

			return 0;
		}

	internal:
		CharacterMapping(IMapView<uint16, CharMap^>^ maps)
		{
			m_maps = maps;
		}

	private:
		IMapView<uint16, CharMap^>^ m_maps = nullptr;
	};

	ref class CmapTableReader sealed : TableReader
	{
	public:

		virtual ~CmapTableReader()
		{
		}

		property uint16 Version;
		property uint16 NumTables;
		property uint32 BaseGlyphRecordsOffset;
		property uint32 LayersRecordsOffset;
		property uint16 LayerRecordsCount;

		property IVectorView<EncodingRecord^>^ Records;

		property IMapView<uint16, CharMap^>^ Maps;

	internal:
		CmapTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			NumTables = GetUInt16();

			// Read Record headers
			auto records = ref new Vector<EncodingRecord^>();
			for (int i = 0; i < NumTables; i++)
			{
				auto rec = ref new EncodingRecord();
				rec->PlatformID = GetUInt16();
				rec->EncodingID = GetUInt16();
				rec->Offset = GetUInt32();
				records->Append(rec);
			}
			Records = records->GetView();

			// Parse known character maps
			auto maps = ref new Map<uint16, CharMap^>();
			for(auto record : Records)
			{
				if (record->Offset > GetPosition())
				{
					GoToPosition(record->Offset);
					CharMap^ map = TryReadCharMap();

					if (map != nullptr)
						maps->Insert(map->Format(), map);
				}
			}
			Maps = maps->GetView();
		};

		CharacterMapping^ GetMapping()
		{
			if (Maps->Size > 0)
				return ref new CharacterMapping(Maps);

			return nullptr;
		}

	private:

		CharMap^ TryReadCharMap()
		{
			auto format = GetUInt16();

			if (format == 4)
				return ReadFormat4();
			if (format == 6)
				return ReadFormat6();
			if (format == 10)
				return ReadFormat10();
			if (format == 12)
				return ReadFormat12();

			return nullptr;
		}

		CharMap^ ReadFormat4()
		{
			auto map = ref new Format4CharMap();

			map->Length = GetUInt16();
			map->Language = GetUInt16();
			map->SegCountX2 = GetUInt16();
			map->SearchRange = GetUInt16();
			map->EntrySelector = GetUInt16();
			map->RangeShift = GetUInt16();

			auto segCount = map->SegCountX2 / 2;
			map->EndCodes = GetUInt16Vector(segCount);
			map->StartCodes = GetUInt16Vector(segCount);

			return map;
		}

		CharMap^ ReadFormat6()
		{
			auto length = GetUInt16();
			auto lang = GetUInt16();
			auto start = GetUInt16();
			auto count = GetUInt16();
			auto glyphs = GetUInt16Array(count);

			return ref new Format6CharMap(start, glyphs);
		}

		CharMap^ ReadFormat10()
		{
			auto reserved = GetUInt16();
			auto length = GetUInt32();
			auto lang = GetUInt32();
			auto start = GetUInt32();
			auto count = GetUInt32();
			auto glyphs = GetUInt16Array(count);

			return ref new Format10CharMap(start, glyphs);
		}

		CharMap^ ReadFormat12()
		{
			auto reserved = GetUInt16();
			auto length = GetUInt32();
			auto lang = GetUInt32();
			auto groupCount = GetUInt32();

			auto vec = ref new Vector<SequentialMapGroup^>();

			for (int i = 0; i < groupCount; i++)
			{
				auto group = ref new SequentialMapGroup();
				group->StartCharCode = GetUInt32();
				group->EndCharCode = GetUInt32();
				group->StartGlyphID = GetUInt32();
				vec->Append(group);
			}

			return ref new Format12CharMap(vec);
		}
	};
}