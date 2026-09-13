#pragma once
#include <pch.h>
#include <TableReader.h>
#include "FontPalette.h"
#include <vector>

namespace CharacterMapCX
{
	ref class CpalTableReader sealed : TableReader
	{
	public:
		property uint16 Version;
		property uint16 NumPaletteEntries;
		property uint16 NumPalettes;
		property uint16 NumColorRecords;

		virtual ~CpalTableReader()
		{
		}

	internal:
		CpalTableReader(const void* tableData, uint32 size)
			: TableReader(tableData, size), m_tableSize(size)
		{
			if (tableData != nullptr && size >= 12)
			{
				m_rawBytes = static_cast<const BYTE*>(tableData);
				Version = GetUInt16();
				NumPaletteEntries = GetUInt16();
				NumPalettes = GetUInt16();
				NumColorRecords = GetUInt16();
				uint32 colorRecordsArrayOffset = GetUInt32();

				// Skip colorRecordIndices[NumPalettes]
				for (uint16 i = 0; i < NumPalettes && !IsAtEnd(); ++i)
				{
					GetUInt16();
				}

				if (Version >= 1 && (m_tableSize >= 12u + (static_cast<uint32>(NumPalettes) * 2u) + 12u))
				{
					m_paletteTypesOffset = GetUInt32();
					m_paletteLabelsOffset = GetUInt32();
					m_paletteEntryLabelsOffset = GetUInt32();
				}
			}
		}

		FontPaletteType GetPaletteType(uint16 paletteIndex)
		{
			if (m_paletteTypesOffset != 0 && m_rawBytes != nullptr)
			{
				uint32 offset = m_paletteTypesOffset + (paletteIndex * 4);
				if (offset + 4 <= m_tableSize)
				{
					uint32 flags = (m_rawBytes[offset] << 24)
						| (m_rawBytes[offset + 1] << 16)
						| (m_rawBytes[offset + 2] << 8)
						| (m_rawBytes[offset + 3]);

					FontPaletteType type = FontPaletteType::None;
					if ((flags & 0x0001) != 0)
						type = static_cast<FontPaletteType>(static_cast<unsigned int>(type) | static_cast<unsigned int>(FontPaletteType::LightBackground));
					if ((flags & 0x0002) != 0)
						type = static_cast<FontPaletteType>(static_cast<unsigned int>(type) | static_cast<unsigned int>(FontPaletteType::DarkBackground));

					return type;
				}
			}

			return FontPaletteType::None;
		}

		uint16 GetPaletteLabelNameId(uint16 paletteIndex)
		{
			if (m_paletteLabelsOffset != 0 && m_rawBytes != nullptr)
			{
				uint32 offset = m_paletteLabelsOffset + (paletteIndex * 2);
				if (offset + 2 <= m_tableSize)
				{
					return (m_rawBytes[offset] << 8) | m_rawBytes[offset + 1];
				}
			}

			return 0xFFFF;
		}

		static Platform::String^ ResolveNameString(const void* nameData, uint32 nameSize, uint16 targetNameId)
		{
			if (nameData == nullptr || nameSize < 6 || targetNameId == 0xFFFF)
				return nullptr;

			const BYTE* data = static_cast<const BYTE*>(nameData);
			uint16 format = (data[0] << 8) | data[1];
			uint16 count = (data[2] << 8) | data[3];
			uint16 stringOffset = (data[4] << 8) | data[5];

			if (nameSize < 6u + (static_cast<uint32>(count) * 12u))
				return nullptr;

			const BYTE* bestRecord = nullptr;
			int bestScore = -1;

			for (uint16 i = 0; i < count; ++i)
			{
				const BYTE* rec = data + 6 + (i * 12);
				uint16 platformID = (rec[0] << 8) | rec[1];
				uint16 encodingID = (rec[2] << 8) | rec[3];
				uint16 languageID = (rec[4] << 8) | rec[5];
				uint16 nameID     = (rec[6] << 8) | rec[7];

				if (nameID == targetNameId)
				{
					int score = 0;
					if (platformID == 3 && (encodingID == 1 || encodingID == 10))
					{
						score = (languageID == 0x0409) ? 100 : 50;
					}
					else if (platformID == 0)
					{
						score = 40;
					}
					else if (platformID == 1 && encodingID == 0)
					{
						score = 20;
					}

					if (score > bestScore)
					{
						bestScore = score;
						bestRecord = rec;
					}
				}
			}

			if (bestRecord != nullptr)
			{
				uint16 platformID = (bestRecord[0] << 8) | bestRecord[1];
				uint16 length     = (bestRecord[8] << 8) | bestRecord[9];
				uint16 offset     = (bestRecord[10] << 8) | bestRecord[11];

				uint32 strStart = stringOffset + offset;
				if (strStart + length <= nameSize)
				{
					if (platformID == 3 || platformID == 0)
					{
						int charCount = length / 2;
						std::vector<wchar_t> wstr(charCount + 1);
						for (int c = 0; c < charCount; ++c)
						{
							wstr[c] = static_cast<wchar_t>((data[strStart + (c * 2)] << 8) | data[strStart + (c * 2) + 1]);
						}
						wstr[charCount] = L'\0';
						return ref new Platform::String(wstr.data(), charCount);
					}
					else if (platformID == 1)
					{
						std::vector<wchar_t> wstr(length + 1);
						for (int c = 0; c < length; ++c)
						{
							wstr[c] = static_cast<wchar_t>(data[strStart + c]);
						}
						wstr[length] = L'\0';
						return ref new Platform::String(wstr.data(), length);
					}
				}
			}

			return nullptr;
		}

	private:
		const BYTE* m_rawBytes = nullptr;
		uint32 m_tableSize = 0;
		uint32 m_paletteTypesOffset = 0;
		uint32 m_paletteLabelsOffset = 0;
		uint32 m_paletteEntryLabelsOffset = 0;
	};
}
