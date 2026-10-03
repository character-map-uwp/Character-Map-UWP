#pragma once

#include "SvgTableReader.h"
#include "SbixTableReader.h"
#include "ColrTableReader.h"
#include "CblcTableReader.h"
#include "PostTableReader.h"
#include "FontPalette.h"
#include "CpalTableReader.h"
//#include "CmapTableReader.h"

using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class FontAnalysis sealed
	{
	public:

		property bool HasBitmapGlyphs { bool get() { ReadTables(); return m_hasBitmap; } }

		property bool HasCOLRGlyphs { bool get() { ReadTables(); return m_hasCOLR; } }

		property bool HasSVGGlyphs { bool get() { ReadTables(); return m_hasSVG; } }

		property bool ContainsVectorColorGlyphs { bool get() { ReadTables(); return m_hasSVG || m_hasCOLR; } }
		
		property bool HasGlyphNames { bool get() { ReadTables(); return m_hasGlyphNames; } }

		property int COLRVersion { int get() { ReadTables(); return m_colrVersion; } }

		property bool SupportsCOLRv1 { bool get() { ReadTables(); return m_colrVersion >= 1; } }

		property bool HasPalettes { bool get() { ReadTables(); return m_palettes != nullptr && m_palettes->Size > 0; } }

		property uint32 PaletteCount { uint32 get() { ReadTables(); return m_palettes != nullptr ? m_palettes->Size : 0; } }

		property IVectorView<FontPalette^>^ Palettes { IVectorView<FontPalette^>^ get() { ReadTables(); return m_palettes; } }

		property bool HasVariationAxis { bool get() { GetVariableProperties(); return m_variableAxis != nullptr && m_variableAxis->Size > 0; } }

		property bool IsRemote { bool get() { return m_isRemote; } }

		/*property String^ DesignLanguages { String^ get() { return m_dlng; } }

		property String^ ScriptLanguages { String^ get() { return m_slng; } }*/

		/// <summary>
		/// Size of the underlying font file in bytes
		/// </summary>
		property int FileSize { int get() { return m_fileSize; } }

		/// <summary>
		/// Absolute path to the underlying font file. May be null for Remote fonts.
		/// </summary>
		property String^ FilePath { String^ get() { return m_filePath; } }

		property IVectorView<DWriteFontAxis^>^ Axis
		{
			IVectorView<DWriteFontAxis^>^ get() { GetVariableProperties(); return m_axis; }
		}

		property IVectorView<DWriteFontAxis^>^ VariableAxis
		{
			IVectorView<DWriteFontAxis^>^ get() { GetVariableProperties(); return m_variableAxis; }
		}

		void ResetVariableAxis()
		{
			if (m_variableAxis == nullptr)
				return;

			for each (auto a in m_variableAxis)
				a->Value = a->DefaultValue; 
		}

		/// <summary>
		/// Mappings of glyph index to font-provided glyph names
		/// </summary>
		property IMapView<int, String^>^ GlyphNameMappings { IMapView<int, String^>^ get() { ReadTables(); return m_mappings; } }

		FontAnalysis()
		{
		}

		FontAnalysis(DWriteFontFace^ fontFace)
		{
			m_ref = fontFace->GetReference();
			GetFileProperties(m_ref);
		}

	private:

		bool m_isRemote = false;
		bool m_hasBitmap = false;
		bool m_hasCOLR = false;
		bool m_hasSVG = false;
		bool m_hasGlyphNames = false;
		int m_colrVersion = -1;
		String^ m_dlng = nullptr;
		String^ m_slng = nullptr;

		int m_fileSize = 0;
		Platform::String^ m_filePath = nullptr;
		IVectorView<DWriteFontAxis^>^ m_variableAxis;
		IVectorView<DWriteFontAxis^>^ m_axis;

		IMapView<int, String^>^ m_mappings;
		ComPtr<IDWriteFontFaceReference> m_ref;
		IVectorView<FontPalette^>^ m_palettes;

		bool m_tables = false;
		bool m_var = false;

		void ReadTables()
		{
			if (m_tables)
				return;

			m_tables = true;
			AnalyseTables();
		}


		void GetVariableProperties()
		{
			if (m_var)
				return;

			m_var = true;

			m_axis = DirectWrite::GetAxis(m_ref);

			// Check for variable font axis
			Vector<DWriteFontAxis^>^ variable = ref new Vector<DWriteFontAxis^>();
			for each (auto a in m_axis)
			{
				if (a->Attribute != DWriteFontAxisAttribute::None)
					variable->Append(a);
			}
			m_variableAxis = variable->GetView();
		}

		void GetFileProperties(ComPtr<IDWriteFontFaceReference> faceRef)
		{
			// Get File Size
			m_fileSize = faceRef->GetFileSize();

			// Attempt to get FilePath. 
			// This involves acquiring the FileLoader and querying it for the file path;
			ComPtr<IDWriteFontFile> file;
			ComPtr<IDWriteFontFileLoader> loader;
			ComPtr<IDWriteLocalFontFileLoader> localLoader;
			ComPtr<IDWriteRemoteFontFileLoader> remoteLoader;

			if (faceRef->GetFontFile(&file) == S_OK
				&& file->GetLoader(&loader) == S_OK)
			{
				if (loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
				{
					const void* refKey = nullptr;
					uint32 size = 0;
					if (file->GetReferenceKey(&refKey, &size) == S_OK)
					{
						UINT filePathSize = 0;
						UINT filePathLength = 0;
						if (localLoader->GetFilePathLengthFromKey(refKey, size, &filePathLength) == S_OK)
						{
							std::vector<wchar_t> buffer(filePathLength + 1);
							if (localLoader->GetFilePathFromKey(refKey, size, buffer.data(), filePathLength + 1) == S_OK)
							{
								m_filePath = ref new Platform::String(buffer.data());
							}
						}
					}
				}
				else if (loader->QueryInterface<IDWriteRemoteFontFileLoader>(&remoteLoader) == S_OK)
				{
					m_isRemote = true;
				}
			}
		}

		void AnalyseTables()
		{
			ComPtr<IDWriteFontFace3> f3;
			m_ref->CreateFontFace(&f3);

			ComPtr<IDWriteFontFace5> face;
			f3.As(&face);

			const void* tableData;
			UINT32 tableSize;
			BOOL exists;
			void* context;

			// SVG
			// Determines if a font contains SVG glyphs
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('S', 'V', 'G', ' '), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto svgreader = ref new SvgTableReader(tableData, tableSize);
				m_hasSVG = svgreader->EntryCount > 0;
				delete svgreader;
			}
			face->ReleaseFontTable(context);

			// COLR
			// Determines if a font contains COLR glyphs
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'O', 'L', 'R'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new ColrTableReader(tableData, tableSize);
				m_colrVersion = reader->Version;
				m_hasCOLR = m_colrVersion >= 1 || reader->BaseGlyphRecordsCount > 0;
				delete reader;
			}
			face->ReleaseFontTable(context);



			// SBIX
			// Determines if a font contains SBIX bitmap image glyphs
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('s', 'b', 'i', 'x'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new SbixTableReader(tableData, tableSize);
				m_hasBitmap = reader->NumberOfStrikes > 0;
				delete reader;
			}
			face->ReleaseFontTable(context);

			if (!m_hasBitmap)
			{
				// CBLC
				// Determines if a font contains CBLC bitmap image glyphs
				face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'B', 'L', 'C'), &tableData, &tableSize, &context, &exists);
				if (exists)
				{
					auto reader = ref new CblcTableReader(tableData, tableSize);
					m_hasBitmap = reader->SizeTableCount > 0;
					delete reader;
				}
				face->ReleaseFontTable(context);
			}

			// META
			// These strings can also be read using face->GetInformationalStrings(...);
			// Not currently used, so commented out for now.
			/*face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('m', 'e', 't', 'a'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new MetaTableReader(tableData, tableSize);
				m_dlng = reader->DesignLanguages;
				m_slng = reader->ScriptLanguages;
				delete reader;
			}
			face->ReleaseFontTable(context);*/

			// POST
			// Attempts to get custom glyph names for a font
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('p', 'o', 's', 't'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new PostTableReader(tableData, tableSize);
				m_mappings = reader->Mapping;
				m_hasGlyphNames = GlyphNameMappings != nullptr && GlyphNameMappings->Size > 0;
				delete reader;
			}
			face->ReleaseFontTable(context);

			// CMAP
			// Attempts to get the data for mapping a Unicode codepoint to the glyph index of a character inside the font
			/*face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('c', 'm', 'a', 'p'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new CmapTableReader(tableData, tableSize);
				GlyphMap = reader->GetMapping();
				delete reader;
			}
			face->ReleaseFontTable(context);*/



			return;

			// NOT CURRENTLY USED , BUT MAY BE USEFUL IN THE FUTURE
			// CPAL / Color Palettes
			ComPtr<IDWriteFontFace2> face2;
			f3.As(&face2);
			if (face2 != nullptr)
			{
				UINT32 paletteCount = face2->GetColorPaletteCount();
				UINT32 entryCount = face2->GetPaletteEntryCount();

				if (paletteCount > 0 && entryCount > 0)
				{
					CpalTableReader^ cpalReader = nullptr;
					const void* cpalData = nullptr;
					UINT32 cpalSize = 0;
					void* cpalContext = nullptr;
					BOOL cpalExists = FALSE;
					face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'P', 'A', 'L'), &cpalData, &cpalSize, &cpalContext, &cpalExists);
					if (cpalExists)
					{
						cpalReader = ref new CpalTableReader(cpalData, cpalSize);
					}

					const void* nameData = nullptr;
					UINT32 nameSize = 0;
					void* nameContext = nullptr;
					BOOL nameExists = FALSE;
					face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('n', 'a', 'm', 'e'), &nameData, &nameSize, &nameContext, &nameExists);

					auto palettes = ref new Vector<FontPalette^>();
					std::vector<DWRITE_COLOR_F> colorBuffer(entryCount);

					for (UINT32 p = 0; p < paletteCount; ++p)
					{
						FontPaletteType paletteType = FontPaletteType::None;
						Platform::String^ paletteName = nullptr;

						if (cpalReader != nullptr)
						{
							paletteType = cpalReader->GetPaletteType(static_cast<uint16>(p));
							uint16 nameId = cpalReader->GetPaletteLabelNameId(static_cast<uint16>(p));
							if (nameExists && nameData != nullptr && nameId != 0xFFFF)
							{
								paletteName = CpalTableReader::ResolveNameString(nameData, nameSize, nameId);
							}
						}

						if (paletteName == nullptr || paletteName->IsEmpty())
						{
							if (p == 0)
							{
								paletteName = "Default";
							}
							else if ((static_cast<unsigned int>(paletteType) & static_cast<unsigned int>(FontPaletteType::LightBackground)) != 0)
							{
								paletteName = "Light";
							}
							else if ((static_cast<unsigned int>(paletteType) & static_cast<unsigned int>(FontPaletteType::DarkBackground)) != 0)
							{
								paletteName = "Dark";
							}
							else
							{
								paletteName = "Palette " + p;
							}
						}

						auto colors = ref new Vector<Windows::UI::Color>();
						if (SUCCEEDED(face2->GetPaletteEntries(p, 0, entryCount, colorBuffer.data())))
						{
							for (const auto& col : colorBuffer)
							{
								Windows::UI::Color c = Windows::UI::ColorHelper::FromArgb(
									static_cast<UINT>(col.a * 255.0f),
									static_cast<UINT>(col.r * 255.0f),
									static_cast<UINT>(col.g * 255.0f),
									static_cast<UINT>(col.b * 255.0f)
								);
								colors->Append(c);
							}
						}

						palettes->Append(ref new FontPalette(p, paletteName, paletteType, colors->GetView()));
					}

					m_palettes = palettes->GetView();

					if (cpalExists)
						face->ReleaseFontTable(cpalContext);
					if (nameExists)
						face->ReleaseFontTable(nameContext);
				}
			}
		}
	};
}