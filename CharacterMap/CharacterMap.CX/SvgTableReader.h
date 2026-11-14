#pragma once
#include <pch.h>
#include <TableReader.h>
#include "DWriteFontAxisAttribute.h"
#include "DWriteNamedFontAxisValue.h"

using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;
using namespace Platform;
using namespace CharacterMapCX;
using namespace std;

namespace CharacterMapCX
{
	ref class SvgTableReader sealed : TableReader
	{
	public:

		virtual ~SvgTableReader()
		{
		}

		property uint16 Version;
		property uint32 SvgListOffset;
		property uint16 EntryCount;

	internal:
		static void InspectTable(const void* tableData, uint32 size, bool& hasSvg, bool& hasBitmapSvg)
		{
			hasSvg = false;
			hasBitmapSvg = false;

			if (tableData == nullptr || size < 12)
				return;

			const uint8_t* bytes = static_cast<const uint8_t*>(tableData);
			auto readU16 = [bytes](uint32 offset) -> uint16
			{
				return (static_cast<uint16>(bytes[offset]) << 8) | bytes[offset + 1];
			};
			auto readU32 = [bytes](uint32 offset) -> uint32
			{
				return (static_cast<uint32>(bytes[offset]) << 24)
					| (static_cast<uint32>(bytes[offset + 1]) << 16)
					| (static_cast<uint32>(bytes[offset + 2]) << 8)
					| bytes[offset + 3];
			};

			uint32 svgListOffset = readU32(2);
			if (svgListOffset > size - 2)
				return;

			uint16 entryCount = readU16(svgListOffset);
			hasSvg = entryCount > 0;
			if (!hasSvg)
				return;

			uint16 sampleCount = entryCount < 3 ? entryCount : 3;
			uint32 maxScan = entryCount == 1 ? 8192u : 1024u;

			for (uint16 i = 0; i < sampleCount; ++i)
			{
				uint32 entryPos = svgListOffset + 2u + static_cast<uint32>(i) * 12u;
				if (entryPos + 12u > size)
					break;

				uint32 docOffset = readU32(entryPos + 4u);
				uint32 docLength = readU32(entryPos + 8u);
				if (docLength < 7 || docOffset > size - svgListOffset)
					continue;

				uint32 docStart = svgListOffset + docOffset;
				uint32 avail = size - docStart;
				uint32 scanLen = docLength < avail ? docLength : avail;
				if (scanLen > maxScan)
					scanLen = maxScan;

				// Skip gzip-compressed SVG documents (0x1F 0x8B)
				if (scanLen >= 2 && bytes[docStart] == 0x1Fu && bytes[docStart + 1] == 0x8Bu)
					continue;

				const uint8_t* p = bytes + docStart;
				for (uint32 j = 0; j + 6 < scanLen; ++j)
				{
					if (p[j] == '<'
						&& (p[j + 1] == 'i' || p[j + 1] == 'I')
						&& (p[j + 2] == 'm' || p[j + 2] == 'M')
						&& (p[j + 3] == 'a' || p[j + 3] == 'A')
						&& (p[j + 4] == 'g' || p[j + 4] == 'G')
						&& (p[j + 5] == 'e' || p[j + 5] == 'E'))
					{
						uint8_t next = p[j + 6];
						if (next == ' ' || next == '\t' || next == '\r' || next == '\n' || next == '>' || next == '/')
						{
							hasBitmapSvg = true;
							return;
						}
					}
				}
			}
		}

		SvgTableReader(
			const void* tableData,
			uint32 size) : TableReader(tableData, size)
		{
			Version = GetUInt16();
			SvgListOffset = GetUInt32();
			GoToPosition(SvgListOffset);

			EntryCount = GetUInt16();
		};

	private:


	};
}
