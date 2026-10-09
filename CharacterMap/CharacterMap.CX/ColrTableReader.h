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
	ref class ColrTableReader sealed : TableReader
	{
	public:

		virtual ~ColrTableReader()
		{
		}

		property uint16 Version;
		property uint16 BaseGlyphRecordsCount;
		property uint32 BaseGlyphRecordsOffset;
		property uint32 LayersRecordsOffset;
		property uint16 LayerRecordsCount;
		property uint32 BaseGlyphPaintRecordsOffset;
		property uint32 LayerListOffset;
		property uint32 ClipListOffset;
		property uint32 VarIdxMapOffset;
		property uint32 ItemVariationStoreOffset;

		bool HasGlyphColrV0(uint16 glyphId)
		{
			if (BaseGlyphRecordsCount == 0 || BaseGlyphRecordsOffset == 0 || m_data == nullptr)
				return false;

			if ((uint64)BaseGlyphRecordsOffset + (uint64)BaseGlyphRecordsCount * 6 > (uint64)m_size)
				return false;

			int low = 0;
			int high = (int)BaseGlyphRecordsCount - 1;
			const BYTE* ptr = m_data + BaseGlyphRecordsOffset;

			while (low <= high)
			{
				int mid = low + (high - low) / 2;
				const BYTE* rec = ptr + mid * 6;
				uint16 gid = (static_cast<uint16>(rec[0]) << 8) | static_cast<uint16>(rec[1]);
				if (gid == glyphId)
					return true;
				if (gid < glyphId)
					low = mid + 1;
				else
					high = mid - 1;
			}

			return false;
		}

		bool HasGlyphColrV1(uint16 glyphId)
		{
			if (Version < 1 || BaseGlyphPaintRecordsOffset == 0 || m_data == nullptr)
				return false;

			if ((uint64)BaseGlyphPaintRecordsOffset + 4 > (uint64)m_size)
				return false;

			const BYTE* listPtr = m_data + BaseGlyphPaintRecordsOffset;
			uint32 count = (static_cast<uint32>(listPtr[0]) << 24) |
			               (static_cast<uint32>(listPtr[1]) << 16) |
			               (static_cast<uint32>(listPtr[2]) << 8)  |
			                static_cast<uint32>(listPtr[3]);

			if ((uint64)BaseGlyphPaintRecordsOffset + 4 + (uint64)count * 6 > (uint64)m_size)
				return false;

			int low = 0;
			int high = (int)count - 1;
			const BYTE* recs = listPtr + 4;

			while (low <= high)
			{
				int mid = low + (high - low) / 2;
				const BYTE* rec = recs + mid * 6;
				uint16 gid = (static_cast<uint16>(rec[0]) << 8) | static_cast<uint16>(rec[1]);
				if (gid == glyphId)
					return true;
				if (gid < glyphId)
					low = mid + 1;
				else
					high = mid - 1;
			}

			return false;
		}

	internal:
		ColrTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			m_data = (const BYTE*)tableData;
			m_size = size;

			if (size >= 14)
			{
				Version = GetUInt16();
				BaseGlyphRecordsCount = GetUInt16();
				BaseGlyphRecordsOffset = GetUInt32();
				LayersRecordsOffset = GetUInt32();
				LayerRecordsCount = GetUInt16();

				if (Version >= 1 && size >= 34)
				{
					BaseGlyphPaintRecordsOffset = GetUInt32();
					LayerListOffset = GetUInt32();
					ClipListOffset = GetUInt32();
					VarIdxMapOffset = GetUInt32();
					ItemVariationStoreOffset = GetUInt32();
				}
			}
		};

	private:
		const BYTE* m_data = nullptr;
		uint32 m_size = 0;


	};
}