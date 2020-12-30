#pragma once
#include <pch.h>
#include <TableReader.h>
#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
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

/*
	CCMP parsing in this file is a Work-in-progress and not currently used.
	When complete, can be used to work out supported emoji variations.
	GSUB Spec: https://docs.microsoft.com/en-gb/typography/opentype/spec/gsub
*/

namespace CharacterMapCX
{
	ref class LigatureSet
	{
	public:
		property uint16 Glyph;
		property IVectorView<uint16>^ Components;
	};

	ref class CoverageTableFormat1
	{
	public:
		property uint16 Format;
		property uint16 Count;
		property IVectorView<uint16>^ GlyphIndices;
	};

	ref class FeatureTable
	{
	public:
		property uint16 ParamsOffset;
		property uint16 LookupIndexCount;
		property IVectorView<uint16>^ LookupListIndices;
	};

	ref class LookupTable
	{
	public:
		property uint16 Type;
		property uint16 Flag;
		property uint16 Count;
		property IVectorView<uint16>^ Offsets;
		property uint16	MarkFilteringSet;
	};

	ref class LookupTableBase
	{
	public:
		property uint16 Type;
	};

	ref class LookupTable7 : LookupTableBase
	{
	public:
		property uint16 Flag;
	};

	ref class LookupSubstitutionTable
	{
	public:
		/// <summary>
		/// Format identifier. Set to 1.
		/// </summary>
		property uint16 Format;

		/// <summary>
		/// Lookup type of subtable referenced by Offset (that is, the extension subtable).
		/// </summary>
		property uint16 Type;

		/// <summary>
		/// Offset to the extension subtable, of lookup type extensionLookupType, relative 
		/// to the start of the ExtensionSubstFormat1 subtable.
		/// </summary>
		property uint16 Offset;
	};

	ref class GsubTableReader sealed : TableReader
	{
	public:

		virtual ~GsubTableReader()
		{
		}

		property IMapView<UINT32, UINT32>^ FeatureMap;

		property uint16 Major;
		property uint16 Minor;
		property uint16 FeatureCount;
		property uint16 FeatureOffset;
		property uint16 LookupOffset;

	internal:
		GsubTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Major = GetUInt16();
			Minor = GetUInt16();
			auto scriptOffset = GetUInt16();
			FeatureOffset = GetUInt16();
			LookupOffset = GetUInt16();

			ParseFeatures();
		};

	private:
		void ParseLookups()
		{
			GoToPosition(LookupOffset);

			auto count = GetUInt16();
			auto offsets = GetUInt16Vector(count);
		}

		void ParseFeatures()
		{
			Map<UINT32, UINT32>^ map = ref new Map<UINT32, UINT32>();

			GoToPosition(FeatureOffset);
			auto count = GetUInt16();
			uint16 ccmpOffset = 0;

			wchar_t str[] = L"    ";
			for (int i = 0; i < count; i++)
			{
				auto tag = GetUInt32();

				if (!map->HasKey(tag))
				{
					str[0] = (wchar_t)((tag >> 24) & 0xFF);
					str[1] = (wchar_t)((tag >> 16) & 0xFF);
					str[2] = (wchar_t)((tag >> 8) & 0xFF);
					str[3] = (wchar_t)((tag >> 0) & 0xFF);

					// Check not a design-time feature
					if (str[0] != 'z' && str[1] != '0')
						map->Insert(tag, DWRITE_MAKE_OPENTYPE_TAG(str[0], str[1], str[2], str[3]));
				}

				auto offset = GetUInt16();

				/* WORK IN PROGRESS */
				/*if (ccmpOffset == 0 && str[0] == 'c' && str[1] == 'c' && str[3] == 'p')
				{
					ccmpOffset = offset;
				}*/
			}

			FeatureMap = map->GetView();

			// Currently will never be hit.
			// When CCMP work is complete this will construct our emoji mappings
			if (ccmpOffset != 0)
				ParseCCMP(ccmpOffset);
		}

		void ParseCCMP(uint16 offset)
		{
			GoToPosition(FeatureOffset + offset);
			auto table = ReadFeatureTable();
		}

		FeatureTable^ ReadFeatureTable()
		{
			auto table = ref new FeatureTable();
			table->ParamsOffset = GetUInt16();
			table->LookupIndexCount = GetUInt16();
			table->LookupListIndices = GetUInt16Vector(table->LookupIndexCount);

			return table;
		}

		LookupTable^ ReadLookupTable()
		{
			auto table = ref new LookupTable();
			table->Type = GetUInt16();
			table->Flag = GetUInt16();
			table->Count = GetUInt16();
			table->Offsets = GetUInt16Vector(table->Count);

			if (table->Flag == 0x0010)
				table->MarkFilteringSet = GetUInt16();

			return table;
		}

		CoverageTableFormat1^ ReadCoverageTable1()
		{
			auto table = ref new CoverageTableFormat1();
			table->Format = GetUInt16();
			table->Count = GetUInt16();
			table->GlyphIndices = GetUInt16Vector(table->Count);
			return table;
		}
	};
}