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

	public ref class VariationSelector sealed
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

	ref class CmapFormat
	{
	public:
		property uint16 Format;
		property uint16 PlatformId;
		property uint16 EncodingId;

	internal:
		virtual uint16 GetGlyphIndex(int codepoint)
		{
			return 0;
		}
	};

	public ref class CmapFormat14 sealed
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

	ref class CmapSub10
	{
	internal:
		property uint16 Format;
		property uint16 Length;
		property uint16 Language;
		property uint16 SegCountX2;
		property uint16 SearchRange;
		property uint16 EntrySelector;
		property uint16 RangeShift;
		property IVectorView<uint16>^ EndCodes;
		property IVectorView<uint16>^ StartCodes;
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

		property CmapSub10^ FormatFour;

	internal:
		CmapTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			NumTables = GetUInt16();

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

			for(auto record : Records)
			{
				if (record->PlatformID == 3 && record->EncodingID == 1)
				{
					GoToPosition(record->Offset);

					FormatFour = ref new CmapSub10();
					FormatFour->Format = GetUInt16();
					FormatFour->Length = GetUInt16();
					FormatFour->Language = GetUInt16();
					FormatFour->SegCountX2 = GetUInt16();
					FormatFour->SearchRange = GetUInt16();
					FormatFour->EntrySelector = GetUInt16();
					FormatFour->RangeShift = GetUInt16();

					auto endcodes = ref new Vector<uint16>();
					auto segCount = FormatFour->SegCountX2 / 2;
					for (int i = 0; i < segCount; i++)
					{
						endcodes->Append(GetUInt16());
					}
					FormatFour->EndCodes = endcodes->GetView();

					auto startcodes = ref new Vector<uint16>();
					for (int i = 0; i < segCount; i++)
					{
						startcodes->Append(GetUInt16());
					}
					FormatFour->StartCodes = startcodes->GetView();

					break;
				}
			}
		};

	private:


	};

	

	/*ref class Sequent

	class CmapSub12
	{
	public:

		uint16 Format;
		uint32 Length;
		uint32 Language;
		uint32 NumGroups;

	internal:
	};*/
}