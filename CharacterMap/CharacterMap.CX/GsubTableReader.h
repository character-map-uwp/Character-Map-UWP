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
#include "DWriteOpenType.h"

using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace Platform::Collections;
using namespace CharacterMapCX;

/*
	GSUB Table Reader for Opentype Typography Features, Ligatures & Substitutions
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

	struct RawSubstitution
	{
		uint16 InputGlyph = 0;
		uint16 TargetGlyph = 0;
		std::vector<uint16> InputSequence;
		std::vector<uint16> TargetSequence;

		bool operator<(const RawSubstitution& other) const
		{
			if (InputGlyph != other.InputGlyph)
				return InputGlyph < other.InputGlyph;
			if (TargetGlyph != other.TargetGlyph)
				return TargetGlyph < other.TargetGlyph;
			if (InputSequence != other.InputSequence)
				return InputSequence < other.InputSequence;
			return TargetSequence < other.TargetSequence;
		}
	};

	ref class GsubTableReader sealed : TableReader
	{
	public:
		virtual ~GsubTableReader()
		{
		}

		property IMapView<UINT32, UINT32>^ FeatureMap;

		property IVectorView<DWriteLigatureFeature^>^ LigatureFeatures
		{
			IVectorView<DWriteLigatureFeature^>^ get()
			{
				if (m_ligatureFeatures == nullptr)
					m_ligatureFeatures = ParseLigatures();
				return m_ligatureFeatures;
			}
		}

		property IVectorView<DWriteOpenTypeFeatureGroup^>^ OpenTypeFeatures
		{
			IVectorView<DWriteOpenTypeFeatureGroup^>^ get()
			{
				if (m_openTypeFeatures == nullptr)
					m_openTypeFeatures = ParseOpenTypeSubstitutions();
				return m_openTypeFeatures;
			}
		}

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

				ParseFeatures();
			}
			else
			{
				FeatureMap = (ref new Map<UINT32, UINT32>())->GetView();
				m_ligatureFeatures = (ref new Vector<DWriteLigatureFeature^>())->GetView();
				m_openTypeFeatures = (ref new Vector<DWriteOpenTypeFeatureGroup^>())->GetView();
			}
		}

	private:
		const BYTE* m_data = nullptr;
		uint32 m_size = 0;
		std::vector<std::pair<uint32, std::vector<uint16>>> m_featureList;
		IVectorView<DWriteLigatureFeature^>^ m_ligatureFeatures = nullptr;
		IVectorView<DWriteOpenTypeFeatureGroup^>^ m_openTypeFeatures = nullptr;

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

		void ParseFeatures()
		{
			Map<UINT32, UINT32>^ map = ref new Map<UINT32, UINT32>();
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
							featureTagIndices[dwriteTag] = m_featureList.size();
							m_featureList.push_back({ dwriteTag, std::move(lookups) });
						}
						else
						{
							auto& existing = m_featureList[it->second].second;
							for (auto l : lookups)
								existing.push_back(l);
						}
					}
				}
			}

			FeatureMap = map->GetView();
		}

		IVectorView<DWriteLigatureFeature^>^ ParseLigatures()
		{
			std::map<uint16, std::vector<RawLigature>> lookupLigaturesCache;
			Vector<DWriteLigatureFeature^>^ featuresVector = ref new Vector<DWriteLigatureFeature^>();

			for (const auto& feat : m_featureList)
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

			return featuresVector->GetView();
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

		IVectorView<DWriteOpenTypeFeatureGroup^>^ ParseOpenTypeSubstitutions()
		{
			std::map<uint16, std::vector<RawSubstitution>> lookupSubstitutionsCache;
			Vector<DWriteOpenTypeFeatureGroup^>^ featuresVector = ref new Vector<DWriteOpenTypeFeatureGroup^>();

			for (const auto& feat : m_featureList)
			{
				uint32 tag = feat.first;
				const auto& lookups = feat.second;

				std::set<RawSubstitution> uniqueSubs;

				for (uint16 lookupIdx : lookups)
				{
					auto it = lookupSubstitutionsCache.find(lookupIdx);
					if (it == lookupSubstitutionsCache.end())
					{
						std::vector<RawSubstitution> subs;
						std::set<uint16> visited;
						ParseLookupForSubstitutions(lookupIdx, subs, visited);
						it = lookupSubstitutionsCache.insert({ lookupIdx, std::move(subs) }).first;
					}

					for (const auto& s : it->second)
						uniqueSubs.insert(s);
				}

				if (!uniqueSubs.empty())
				{
					DWriteOpenTypeFeatureGroup^ group = ref new DWriteOpenTypeFeatureGroup();
					group->FeatureTag = tag;
					group->Tag = DirectWrite::GetFeatureTag(tag);
					group->FeatureName = DirectWrite::GetFeatureName(tag);
					group->Description = DirectWrite::GetFeatureDescription(group->Tag);

					Vector<DWriteOpenTypeSubstitution^>^ list = ref new Vector<DWriteOpenTypeSubstitution^>();
					for (const auto& raw : uniqueSubs)
					{
						DWriteOpenTypeSubstitution^ sub = ref new DWriteOpenTypeSubstitution();
						sub->InputGlyph = raw.InputGlyph;
						sub->TargetGlyph = raw.TargetGlyph;

						if (!raw.InputSequence.empty())
						{
							Vector<uint16>^ inSeq = ref new Vector<uint16>();
							for (uint16 g : raw.InputSequence) inSeq->Append(g);
							sub->InputSequence = inSeq->GetView();
						}

						if (!raw.TargetSequence.empty())
						{
							Vector<uint16>^ tgtSeq = ref new Vector<uint16>();
							for (uint16 g : raw.TargetSequence) tgtSeq->Append(g);
							sub->TargetSequence = tgtSeq->GetView();
						}

						list->Append(sub);
					}

					group->Substitutions = list->GetView();
					featuresVector->Append(group);
				}
			}

			return featuresVector->GetView();
		}

		void ParseLookupForSubstitutions(uint16 lookupIndex, std::vector<RawSubstitution>& outSubstitutions, std::set<uint16>& visitedLookups)
		{
			if (visitedLookups.find(lookupIndex) != visitedLookups.end())
				return;
			visitedLookups.insert(lookupIndex);

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
				ParseSubtableForSubstitutions(lookupType, subTableOffset, outSubstitutions, visitedLookups);
			}
		}

		void ParseSubtableForSubstitutions(uint16 lookupType, size_t subTableOffset, std::vector<RawSubstitution>& outSubstitutions, std::set<uint16>& visitedLookups)
		{
			if (lookupType == 7)
			{
				// Extension Substitution
				if (subTableOffset + 8 <= m_size)
				{
					uint16 substFormat = 0;
					uint16 extensionLookupType = 0;
					uint32 extensionOffset = 0;

					ReadUInt16(subTableOffset, substFormat);
					ReadUInt16(subTableOffset + 2, extensionLookupType);
					ReadUInt32(subTableOffset + 4, extensionOffset);

					if (substFormat == 1)
					{
						size_t actualSubtableOffset = subTableOffset + extensionOffset;
						ParseSubtableForSubstitutions(extensionLookupType, actualSubtableOffset, outSubstitutions, visitedLookups);
					}
				}
				return;
			}

			if (lookupType == 1)
			{
				// Single Substitution Format 1 & 2
				if (subTableOffset + 6 > m_size)
					return;

				uint16 substFormat = 0;
				uint16 coverageOffset = 0;
				ReadUInt16(subTableOffset, substFormat);
				ReadUInt16(subTableOffset + 2, coverageOffset);

				if (coverageOffset == 0)
					return;

				size_t coverageTableOffset = subTableOffset + coverageOffset;
				std::vector<uint16> coverageGlyphs;
				if (!ReadCoverageTable(coverageTableOffset, coverageGlyphs))
					return;

				if (substFormat == 1)
				{
					uint16 rawDelta = 0;
					ReadUInt16(subTableOffset + 4, rawDelta);
					int16 deltaGlyphID = static_cast<int16>(rawDelta);

					for (uint16 g : coverageGlyphs)
					{
						uint16 target = static_cast<uint16>((static_cast<int32>(g) + deltaGlyphID) & 0xFFFF);
						if (target != g)
							outSubstitutions.push_back({ g, target });
					}
				}
				else if (substFormat == 2)
				{
					uint16 glyphCount = 0;
					ReadUInt16(subTableOffset + 4, glyphCount);

					size_t count = std::min<size_t>(glyphCount, coverageGlyphs.size());
					size_t substGlyphsPos = subTableOffset + 6;

					for (size_t i = 0; i < count; i++)
					{
						uint16 target = 0;
						if (ReadUInt16(substGlyphsPos + i * 2, target))
						{
							if (target != coverageGlyphs[i])
								outSubstitutions.push_back({ coverageGlyphs[i], target });
						}
					}
				}
			}
			else if (lookupType == 2)
			{
				// Multiple Substitution Format 1
				if (subTableOffset + 6 > m_size)
					return;

				uint16 substFormat = 0;
				uint16 coverageOffset = 0;
				uint16 sequenceCount = 0;
				ReadUInt16(subTableOffset, substFormat);
				ReadUInt16(subTableOffset + 2, coverageOffset);
				ReadUInt16(subTableOffset + 4, sequenceCount);

				if (substFormat != 1 || coverageOffset == 0 || sequenceCount == 0)
					return;

				size_t coverageTableOffset = subTableOffset + coverageOffset;
				std::vector<uint16> coverageGlyphs;
				if (!ReadCoverageTable(coverageTableOffset, coverageGlyphs))
					return;

				size_t count = std::min<size_t>(sequenceCount, coverageGlyphs.size());
				size_t seqOffsetsPos = subTableOffset + 6;

				for (size_t i = 0; i < count; i++)
				{
					uint16 seqRelOffset = 0;
					if (!ReadUInt16(seqOffsetsPos + i * 2, seqRelOffset) || seqRelOffset == 0)
						continue;

					size_t seqOffset = subTableOffset + seqRelOffset;
					if (seqOffset + 2 > m_size)
						continue;

					uint16 glyphCount = 0;
					ReadUInt16(seqOffset, glyphCount);
					if (glyphCount == 0 || seqOffset + 2 + (size_t)glyphCount * 2 > m_size)
						continue;

					std::vector<uint16> targetSeq(glyphCount);
					bool valid = true;
					for (uint16 g = 0; g < glyphCount; g++)
					{
						if (!ReadUInt16(seqOffset + 2 + (size_t)g * 2, targetSeq[g]))
						{
							valid = false;
							break;
						}
					}

					if (valid)
					{
						RawSubstitution sub;
						sub.InputGlyph = coverageGlyphs[i];
						sub.TargetGlyph = targetSeq[0];
						sub.TargetSequence = targetSeq;
						outSubstitutions.push_back(std::move(sub));
					}
				}
			}
			else if (lookupType == 3)
			{
				// Alternate Substitution Format 1
				if (subTableOffset + 6 > m_size)
					return;

				uint16 substFormat = 0;
				uint16 coverageOffset = 0;
				uint16 alternateSetCount = 0;
				ReadUInt16(subTableOffset, substFormat);
				ReadUInt16(subTableOffset + 2, coverageOffset);
				ReadUInt16(subTableOffset + 4, alternateSetCount);

				if (substFormat != 1 || coverageOffset == 0 || alternateSetCount == 0)
					return;

				size_t coverageTableOffset = subTableOffset + coverageOffset;
				std::vector<uint16> coverageGlyphs;
				if (!ReadCoverageTable(coverageTableOffset, coverageGlyphs))
					return;

				size_t count = std::min<size_t>(alternateSetCount, coverageGlyphs.size());
				size_t altSetOffsetsPos = subTableOffset + 6;

				for (size_t i = 0; i < count; i++)
				{
					uint16 altSetRelOffset = 0;
					if (!ReadUInt16(altSetOffsetsPos + i * 2, altSetRelOffset) || altSetRelOffset == 0)
						continue;

					size_t altSetOffset = subTableOffset + altSetRelOffset;
					if (altSetOffset + 2 > m_size)
						continue;

					uint16 glyphCount = 0;
					ReadUInt16(altSetOffset, glyphCount);
					if (glyphCount == 0 || altSetOffset + 2 + (size_t)glyphCount * 2 > m_size)
						continue;

					uint16 inGlyph = coverageGlyphs[i];
					for (uint16 g = 0; g < glyphCount; g++)
					{
						uint16 altGlyph = 0;
						if (ReadUInt16(altSetOffset + 2 + (size_t)g * 2, altGlyph))
						{
							if (altGlyph != inGlyph)
								outSubstitutions.push_back({ inGlyph, altGlyph });
						}
					}
				}
			}
			else if (lookupType == 8)
			{
				// Reverse Chaining Contextual Single Substitution
				if (subTableOffset + 6 > m_size)
					return;

				uint16 substFormat = 0;
				uint16 coverageOffset = 0;
				ReadUInt16(subTableOffset, substFormat);
				ReadUInt16(subTableOffset + 2, coverageOffset);

				if (substFormat != 1 || coverageOffset == 0)
					return;

				size_t pos = subTableOffset + 4;
				uint16 backtrackGlyphCount = 0;
				if (!ReadUInt16(pos, backtrackGlyphCount))
					return;
				pos += 2 + (size_t)backtrackGlyphCount * 2;

				uint16 lookaheadGlyphCount = 0;
				if (!ReadUInt16(pos, lookaheadGlyphCount))
					return;
				pos += 2 + (size_t)lookaheadGlyphCount * 2;

				uint16 glyphCount = 0;
				if (!ReadUInt16(pos, glyphCount))
					return;
				pos += 2;

				size_t coverageTableOffset = subTableOffset + coverageOffset;
				std::vector<uint16> coverageGlyphs;
				if (!ReadCoverageTable(coverageTableOffset, coverageGlyphs))
					return;

				size_t count = std::min<size_t>(glyphCount, coverageGlyphs.size());
				for (size_t i = 0; i < count; i++)
				{
					uint16 target = 0;
					if (ReadUInt16(pos + i * 2, target))
					{
						if (target != coverageGlyphs[i])
							outSubstitutions.push_back({ coverageGlyphs[i], target });
					}
				}
			}
			else if (lookupType == 5 || lookupType == 6)
			{
				// Contextual / Chaining Contextual Substitution Format 3 (Coverage-based)
				if (subTableOffset + 4 > m_size)
					return;

				uint16 substFormat = 0;
				ReadUInt16(subTableOffset, substFormat);

				if (substFormat == 3)
				{
					size_t pos = subTableOffset + 2;
					uint16 inputGlyphCount = 0;

					if (lookupType == 6)
					{
						uint16 backtrackGlyphCount = 0;
						if (!ReadUInt16(pos, backtrackGlyphCount))
							return;
						pos += 2 + (size_t)backtrackGlyphCount * 2;
					}

					if (!ReadUInt16(pos, inputGlyphCount) || inputGlyphCount == 0 || inputGlyphCount > 16)
						return;
					pos += 2;

					std::vector<uint16> inputCovOffsets(inputGlyphCount);
					for (uint16 i = 0; i < inputGlyphCount; i++)
					{
						if (!ReadUInt16(pos, inputCovOffsets[i]))
							return;
						pos += 2;
					}

					if (lookupType == 6)
					{
						uint16 lookaheadGlyphCount = 0;
						if (!ReadUInt16(pos, lookaheadGlyphCount))
							return;
						pos += 2 + (size_t)lookaheadGlyphCount * 2;
					}

					uint16 substCount = 0;
					if (!ReadUInt16(pos, substCount) || substCount == 0)
						return;
					pos += 2;

					struct SubstRecord { uint16 seqIndex; uint16 lookupIndex; };
					std::vector<SubstRecord> substRecords(substCount);
					for (uint16 r = 0; r < substCount; r++)
					{
						if (!ReadUInt16(pos, substRecords[r].seqIndex) ||
						    !ReadUInt16(pos + 2, substRecords[r].lookupIndex))
							return;
						pos += 4;
					}

					// Read input coverages
					std::vector<std::vector<uint16>> inputCoverages(inputGlyphCount);
					for (uint16 i = 0; i < inputGlyphCount; i++)
					{
						if (inputCovOffsets[i] == 0)
							return;
						size_t covTableOffset = subTableOffset + inputCovOffsets[i];
						if (!ReadCoverageTable(covTableOffset, inputCoverages[i]) || inputCoverages[i].empty())
							return;
					}

					// Parse nested lookups to build substitution map per sequence index
					std::map<uint16, std::map<uint16, uint16>> seqSubstMaps;
					for (const auto& rec : substRecords)
					{
						std::vector<RawSubstitution> nestedSubs;
						ParseLookupForSubstitutions(rec.lookupIndex, nestedSubs, visitedLookups);
						for (const auto& ns : nestedSubs)
							seqSubstMaps[rec.seqIndex][ns.InputGlyph] = ns.TargetGlyph;
					}

					if (inputGlyphCount == 1)
					{
						const auto& cov = inputCoverages[0];
						const auto& subMap = seqSubstMaps[0];
						for (uint16 g : cov)
						{
							auto it = subMap.find(g);
							if (it != subMap.end() && it->second != g)
								outSubstitutions.push_back({ g, it->second });
						}
					}
					else
					{
						// Multi-glyph contextual sequence (e.g. ordn: [0-9] + [a/o] -> [0-9] + [ordn_a/ordn_o])
						size_t totalCombos = 1;
						for (const auto& cov : inputCoverages)
						{
							totalCombos *= cov.size();
							if (totalCombos > 128)
								break;
						}

						if (totalCombos <= 128)
						{
							// Generate Cartesian product of input sequences
							std::vector<std::vector<uint16>> combos = { {} };
							for (const auto& cov : inputCoverages)
							{
								std::vector<std::vector<uint16>> nextCombos;
								for (const auto& existing : combos)
								{
									for (uint16 g : cov)
									{
										auto copy = existing;
										copy.push_back(g);
										nextCombos.push_back(std::move(copy));
									}
								}
								combos = std::move(nextCombos);
							}

							for (const auto& inSeq : combos)
							{
								std::vector<uint16> tgtSeq = inSeq;
								bool changed = false;

								for (const auto& entry : seqSubstMaps)
								{
									uint16 seqIdx = entry.first;
									const auto& subMap = entry.second;
									if (seqIdx < tgtSeq.size())
									{
										auto it = subMap.find(tgtSeq[seqIdx]);
										if (it != subMap.end())
										{
											tgtSeq[seqIdx] = it->second;
											changed = true;
										}
									}
								}

								if (changed)
								{
									RawSubstitution sub;
									sub.InputGlyph = inSeq[0];
									sub.TargetGlyph = tgtSeq[0];
									sub.InputSequence = inSeq;
									sub.TargetSequence = tgtSeq;
									outSubstitutions.push_back(std::move(sub));
								}
							}
						}
					}
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
