#pragma once
#include <pch.h>
#include <TableReader.h>
#include <dwrite_3.h>
#include <string>
#include <vector>
#include <map>
#include <set>
#include <algorithm>
#include "DWriteLigature.h"

using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace Platform::Collections;
using namespace CharacterMapCX;

/*
	GSUB Table Reader for Opentype Typography Features & Ligatures
	Spec: https://docs.microsoft.com/en-gb/typography/opentype/spec/gsub
*/

namespace CharacterMapCX
{
	struct RawLigature
	{
		uint16 LigatureGlyph;
		std::vector<uint16> Components;

		bool operator<(const RawLigature& other) const
		{
			if (LigatureGlyph != other.LigatureGlyph)
				return LigatureGlyph < other.LigatureGlyph;
			return Components < other.Components;
		}
	};

	ref class GsubTableReader sealed : TableReader
	{
	public:
		virtual ~GsubTableReader()
		{
		}

		property IMapView<UINT32, UINT32>^ FeatureMap;
		property IVectorView<DWriteLigatureFeature^>^ LigatureFeatures;

		property uint16 Major;
		property uint16 Minor;
		property uint16 FeatureOffset;
		property uint16 LookupOffset;

	internal:
		GsubTableReader(const void* tableData, uint32 size) : TableReader(tableData, size)
		{
			m_data = static_cast<const BYTE*>(tableData);
			m_size = size;

			if (m_data != nullptr && m_size >= 10)
			{
				uint16 major = 0, minor = 0, scriptOffset = 0, featOffset = 0, lookupOffset = 0;
				ReadUInt16(0, major);
				ReadUInt16(2, minor);
				ReadUInt16(4, scriptOffset);
				ReadUInt16(6, featOffset);
				ReadUInt16(8, lookupOffset);

				Major = major;
				Minor = minor;
				FeatureOffset = featOffset;
				LookupOffset = lookupOffset;

				ParseFeaturesAndLigatures();
			}
			else
			{
				FeatureMap = (ref new Map<UINT32, UINT32>())->GetView();
				LigatureFeatures = (ref new Vector<DWriteLigatureFeature^>())->GetView();
			}
		}

	private:
		const BYTE* m_data = nullptr;
		uint32 m_size = 0;

		inline bool ReadUInt16(size_t offset, uint16& val) const
		{
			if (offset + 2 > m_size) return false;
			val = (static_cast<uint16>(m_data[offset]) << 8) | static_cast<uint16>(m_data[offset + 1]);
			return true;
		}

		inline bool ReadUInt32(size_t offset, uint32& val) const
		{
			if (offset + 4 > m_size) return false;
			val = (static_cast<uint32>(m_data[offset]) << 24) |
			      (static_cast<uint32>(m_data[offset + 1]) << 16) |
			      (static_cast<uint32>(m_data[offset + 2]) << 8) |
			      static_cast<uint32>(m_data[offset + 3]);
			return true;
		}

		void ParseFeaturesAndLigatures()
		{
			Map<UINT32, UINT32>^ map = ref new Map<UINT32, UINT32>();
			std::vector<std::pair<uint32, std::vector<uint16>>> featureList;
			std::map<uint32, size_t> featureTagIndices;

			if (FeatureOffset > 0 && (size_t)FeatureOffset + 2 <= m_size)
			{
				uint16 featureCount = 0;
				ReadUInt16(FeatureOffset, featureCount);

				size_t recordsStart = (size_t)FeatureOffset + 2;
				for (uint16 i = 0; i < featureCount; i++)
				{
					size_t recOffset = recordsStart + (size_t)i * 6;
					if (recOffset + 6 > m_size)
						break;

					BYTE c0 = m_data[recOffset];
					BYTE c1 = m_data[recOffset + 1];
					BYTE c2 = m_data[recOffset + 2];
					BYTE c3 = m_data[recOffset + 3];

					// Check not a design-time feature
					if (c0 == 'z' && c1 == '0')
						continue;

					uint32 dwriteTag = DWRITE_MAKE_OPENTYPE_TAG(c0, c1, c2, c3);

					uint16 featOffset = 0;
					ReadUInt16(recOffset + 4, featOffset);

					if (!map->HasKey(dwriteTag))
						map->Insert(dwriteTag, dwriteTag);

					// Read Feature Table
					size_t featTableOffset = (size_t)FeatureOffset + featOffset;
					if (featTableOffset + 4 <= m_size)
					{
						uint16 lookupCount = 0;
						ReadUInt16(featTableOffset + 2, lookupCount);

						std::vector<uint16> lookups;
						lookups.reserve(lookupCount);
						size_t indicesStart = featTableOffset + 4;
						for (uint16 li = 0; li < lookupCount; li++)
						{
							uint16 lIdx = 0;
							if (ReadUInt16(indicesStart + (size_t)li * 2, lIdx))
								lookups.push_back(lIdx);
						}

						auto it = featureTagIndices.find(dwriteTag);
						if (it == featureTagIndices.end())
						{
							featureTagIndices[dwriteTag] = featureList.size();
							featureList.push_back({ dwriteTag, std::move(lookups) });
						}
						else
						{
							auto& existing = featureList[it->second].second;
							for (auto l : lookups)
								existing.push_back(l);
						}
					}
				}
			}

			FeatureMap = map->GetView();

			// Cache parsed ligatures per lookup index
			std::map<uint16, std::vector<RawLigature>> lookupLigaturesCache;
			Vector<DWriteLigatureFeature^>^ featuresVector = ref new Vector<DWriteLigatureFeature^>();

			for (const auto& feat : featureList)
			{
				uint32 tag = feat.first;
				const auto& lookups = feat.second;

				std::set<RawLigature> uniqueLigatures;

				for (uint16 lookupIdx : lookups)
				{
					auto it = lookupLigaturesCache.find(lookupIdx);
					if (it == lookupLigaturesCache.end())
					{
						std::vector<RawLigature> ligs;
						ParseLookupForLigatures(lookupIdx, ligs);
						it = lookupLigaturesCache.insert({ lookupIdx, std::move(ligs) }).first;
					}

					for (const auto& lig : it->second)
						uniqueLigatures.insert(lig);
				}

				if (!uniqueLigatures.empty())
				{
					DWriteLigatureFeature^ feature = ref new DWriteLigatureFeature();
					feature->FeatureTag = tag;
					feature->FeatureName = DirectWrite::GetFeatureName(tag);
					Vector<DWriteLigature^>^ ligList = ref new Vector<DWriteLigature^>();

					for (const auto& rawLig : uniqueLigatures)
					{
						DWriteLigature^ lig = ref new DWriteLigature();
						lig->LigatureGlyph = rawLig.LigatureGlyph;

						Vector<uint16>^ comps = ref new Vector<uint16>();
						for (uint16 c : rawLig.Components)
							comps->Append(c);
						lig->ComponentGlyphs = comps->GetView();

						ligList->Append(lig);
					}

					feature->Ligatures = ligList->GetView();
					featuresVector->Append(feature);
				}
			}

			LigatureFeatures = featuresVector->GetView();
		}

		void ParseLookupForLigatures(uint16 lookupIndex, std::vector<RawLigature>& outLigatures)
		{
			if (LookupOffset == 0 || (size_t)LookupOffset + 2 > m_size)
				return;

			uint16 totalLookups = 0;
			if (!ReadUInt16(LookupOffset, totalLookups) || lookupIndex >= totalLookups)
				return;

			size_t lookupOffsetPos = (size_t)LookupOffset + 2 + (size_t)lookupIndex * 2;
			uint16 lookupTableRelOffset = 0;
			if (!ReadUInt16(lookupOffsetPos, lookupTableRelOffset))
				return;

			size_t lookupTableOffset = (size_t)LookupOffset + lookupTableRelOffset;
			if (lookupTableOffset + 6 > m_size)
				return;

			uint16 lookupType = 0;
			uint16 lookupFlag = 0;
			uint16 subTableCount = 0;
			ReadUInt16(lookupTableOffset, lookupType);
			ReadUInt16(lookupTableOffset + 2, lookupFlag);
			ReadUInt16(lookupTableOffset + 4, subTableCount);

			size_t subTableOffsetsPos = lookupTableOffset + 6;
			for (uint16 s = 0; s < subTableCount; s++)
			{
				uint16 subTableRelOffset = 0;
				if (!ReadUInt16(subTableOffsetsPos + (size_t)s * 2, subTableRelOffset))
					break;

				size_t subTableOffset = lookupTableOffset + subTableRelOffset;

				if (lookupType == 4)
				{
					// Ligature Substitution Subtable Format 1
					ParseLigatureSubstFormat1(subTableOffset, outLigatures);
				}
				else if (lookupType == 7)
				{
					// Extension Substitution Subtable Format 1
					if (subTableOffset + 8 <= m_size)
					{
						uint16 substFormat = 0;
						uint16 extensionLookupType = 0;
						uint32 extensionOffset = 0;

						ReadUInt16(subTableOffset, substFormat);
						ReadUInt16(subTableOffset + 2, extensionLookupType);
						ReadUInt32(subTableOffset + 4, extensionOffset);

						if (substFormat == 1 && extensionLookupType == 4)
						{
							size_t actualSubtableOffset = subTableOffset + extensionOffset;
							ParseLigatureSubstFormat1(actualSubtableOffset, outLigatures);
						}
					}
				}
			}
		}

		void ParseLigatureSubstFormat1(size_t subtableOffset, std::vector<RawLigature>& outLigatures)
		{
			if (subtableOffset + 6 > m_size)
				return;

			uint16 substFormat = 0;
			uint16 coverageOffset = 0;
			uint16 ligSetCount = 0;

			ReadUInt16(subtableOffset, substFormat);
			ReadUInt16(subtableOffset + 2, coverageOffset);
			ReadUInt16(subtableOffset + 4, ligSetCount);

			if (substFormat != 1 || coverageOffset == 0 || ligSetCount == 0)
				return;

			size_t coverageTableOffset = subtableOffset + coverageOffset;
			std::vector<uint16> coverageGlyphs;
			if (!ReadCoverageTable(coverageTableOffset, coverageGlyphs))
				return;

			size_t ligSetOffsetsPos = subtableOffset + 6;
			size_t count = std::min<size_t>(ligSetCount, coverageGlyphs.size());

			for (size_t i = 0; i < count; i++)
			{
				uint16 ligSetRelOffset = 0;
				if (!ReadUInt16(ligSetOffsetsPos + i * 2, ligSetRelOffset))
					break;

				if (ligSetRelOffset == 0)
					continue;

				size_t ligSetOffset = subtableOffset + ligSetRelOffset;
				if (ligSetOffset + 2 > m_size)
					continue;

				uint16 firstGlyph = coverageGlyphs[i];
				uint16 ligatureCount = 0;
				ReadUInt16(ligSetOffset, ligatureCount);

				size_t ligOffsetsPos = ligSetOffset + 2;
				for (uint16 j = 0; j < ligatureCount; j++)
				{
					uint16 ligRelOffset = 0;
					if (!ReadUInt16(ligOffsetsPos + (size_t)j * 2, ligRelOffset))
						break;

					if (ligRelOffset == 0)
						continue;

					size_t ligOffset = ligSetOffset + ligRelOffset;
					if (ligOffset + 4 > m_size)
						continue;

					uint16 ligatureGlyph = 0;
					uint16 componentCount = 0;
					ReadUInt16(ligOffset, ligatureGlyph);
					ReadUInt16(ligOffset + 2, componentCount);

					if (componentCount < 1)
						continue;

					RawLigature rawLig;
					rawLig.LigatureGlyph = ligatureGlyph;
					rawLig.Components.reserve(componentCount);
					rawLig.Components.push_back(firstGlyph);

					bool validComponents = true;
					size_t compGlyphsPos = ligOffset + 4;
					for (uint16 c = 1; c < componentCount; c++)
					{
						uint16 compGlyph = 0;
						if (!ReadUInt16(compGlyphsPos + (size_t)(c - 1) * 2, compGlyph))
						{
							validComponents = false;
							break;
						}
						rawLig.Components.push_back(compGlyph);
					}

					if (validComponents)
						outLigatures.push_back(std::move(rawLig));
				}
			}
		}

		bool ReadCoverageTable(size_t covOffset, std::vector<uint16>& outGlyphs)
		{
			if (covOffset + 4 > m_size)
				return false;

			uint16 format = 0;
			ReadUInt16(covOffset, format);

			if (format == 1)
			{
				uint16 glyphCount = 0;
				ReadUInt16(covOffset + 2, glyphCount);

				if (covOffset + 4 + (size_t)glyphCount * 2 > m_size)
					return false;

				outGlyphs.resize(glyphCount);
				for (uint16 i = 0; i < glyphCount; i++)
					ReadUInt16(covOffset + 4 + (size_t)i * 2, outGlyphs[i]);

				return true;
			}
			else if (format == 2)
			{
				uint16 rangeCount = 0;
				ReadUInt16(covOffset + 2, rangeCount);

				if (covOffset + 4 + (size_t)rangeCount * 6 > m_size)
					return false;

				size_t rangesPos = covOffset + 4;
				uint32 totalGlyphs = 0;
				for (uint16 r = 0; r < rangeCount; r++)
				{
					size_t rPos = rangesPos + (size_t)r * 6;
					uint16 startGlyphID = 0, endGlyphID = 0, startCoverageIndex = 0;
					ReadUInt16(rPos, startGlyphID);
					ReadUInt16(rPos + 2, endGlyphID);
					ReadUInt16(rPos + 4, startCoverageIndex);

					if (endGlyphID >= startGlyphID)
					{
						uint32 count = (endGlyphID - startGlyphID) + 1;
						uint32 maxIndex = (uint32)startCoverageIndex + count;
						if (maxIndex > totalGlyphs)
							totalGlyphs = maxIndex;
					}
				}

				if (totalGlyphs == 0 || totalGlyphs > 65536)
					return false;

				outGlyphs.assign(totalGlyphs, 0);

				for (uint16 r = 0; r < rangeCount; r++)
				{
					size_t rPos = rangesPos + (size_t)r * 6;
					uint16 startGlyphID = 0, endGlyphID = 0, startCoverageIndex = 0;
					ReadUInt16(rPos, startGlyphID);
					ReadUInt16(rPos + 2, endGlyphID);
					ReadUInt16(rPos + 4, startCoverageIndex);

					if (endGlyphID >= startGlyphID)
					{
						uint16 count = (endGlyphID - startGlyphID) + 1;
						for (uint16 g = 0; g < count; g++)
						{
							size_t idx = (size_t)startCoverageIndex + g;
							if (idx < outGlyphs.size())
								outGlyphs[idx] = startGlyphID + g;
						}
					}
				}

				return true;
			}

			return false;
		}
	};
}