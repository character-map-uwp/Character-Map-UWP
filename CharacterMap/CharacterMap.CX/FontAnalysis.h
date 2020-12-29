#pragma once

#include "SvgTableReader.h"
#include "SbixTableReader.h"
#include "ColrTableReader.h"
#include "CblcTableReader.h"
#include "PostTableReader.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class FontAnalysis sealed
	{
	public:

		property bool HasBitmapGlyphs { bool get() { return m_hasBitmap; } }

		property bool HasCOLRGlyphs { bool get() { return m_hasCOLR; } }

		property bool HasSVGGlyphs { bool get() { return m_hasSVG; } }

		property bool ContainsVectorColorGlyphs { bool get() { return m_hasSVG || m_hasCOLR; } }
		
		property bool HasGlyphNames { bool get() { return m_hasGlyphNames; } }

		/// <summary>
		/// Size of the underlying font file in bytes
		/// </summary>
		property int FileSize { int get() { return m_fileSize; } }

		/// <summary>
		/// Absolute path to the underlying font file
		/// </summary>
		property String^ FilePath { String^ get() { return m_filePath; } }

		property IVectorView<DWriteFontAxis^>^ Axis
		{
			IVectorView<DWriteFontAxis^>^ get() { return m_axis; }
		}

		property IVectorView<DWriteFontAxis^>^ VariableAxis
		{
			IVectorView<DWriteFontAxis^>^ get() { return m_variableAxis; }
		}

		property IVectorView<GlyphNameMap^>^ GlyphNames;

		FontAnalysis() { }

		FontAnalysis(CanvasFontFace^ fontFace)
		{
			auto ref = GetWrappedResource<IDWriteFontFaceReference>(fontFace);
			AnalyseTables(ref);
			GetFileProperties(ref);
		}

	private:

		bool m_hasBitmap = false;
		bool m_hasCOLR = false;
		bool m_hasSVG = false;
		bool m_hasGlyphNames = false;

		int m_fileSize = 0;
		Platform::String^ m_filePath = nullptr;
		IVectorView<DWriteFontAxis^>^ m_variableAxis;
		IVectorView<DWriteFontAxis^>^ m_axis;




		void GetFileProperties(ComPtr<IDWriteFontFaceReference> faceRef)
		{
			m_axis = DirectWrite::GetAxis(faceRef);

			Vector<DWriteFontAxis^>^ variable = ref new Vector<DWriteFontAxis^>();
			for each (auto a in m_axis)
			{
				if (a->Attribute == DWriteFontAxisAttribute::Variable)
					variable->Append(a);
			}
			m_variableAxis = variable->GetView();


			// Get File Size
			m_fileSize = faceRef->GetFileSize();

			// Attempt to get FilePath. 
			// This involves acquiring the FileLoader and querying it
			// for the file path;
			ComPtr<IDWriteFontFile> file;
			ComPtr<IDWriteFontFileLoader> loader;
			ComPtr<IDWriteLocalFontFileLoader> localLoader;

			if (faceRef->GetFontFile(&file) == S_OK
				&& file->GetLoader(&loader) == S_OK
				&& loader->QueryInterface<IDWriteLocalFontFileLoader>(&localLoader) == S_OK)
			{
				const void* refKey = nullptr;
				uint32 size = 0;
				if (file->GetReferenceKey(&refKey, &size) == S_OK)
				{
					UINT filePathSize = 0;
					UINT filePathLength = 0;
					if (localLoader->GetFilePathLengthFromKey(refKey, size, &filePathLength) == S_OK)
					{
						wchar_t* buffer = new (std::nothrow) wchar_t[filePathLength + 1];
						if (localLoader->GetFilePathFromKey(refKey, size, buffer, filePathLength + 1) == S_OK)
						{
							m_filePath = ref new Platform::String(buffer);
						}
					}
				}
			}
		}

		void AnalyseTables(ComPtr<IDWriteFontFaceReference> faceRef)
		{
			ComPtr<IDWriteFontFace3> f3;
			faceRef->CreateFontFace(&f3);

			ComPtr<IDWriteFontFace5> face;
			f3.As(&face);

			const void* tableData;
			UINT32 tableSize;
			BOOL exists;
			void* context;

			// SVG
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('S', 'V', 'G', ' '), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto svgreader = ref new SvgTableReader(tableData, tableSize);
				m_hasSVG = svgreader->EntryCount > 0;
				delete svgreader;
			}
			face->ReleaseFontTable(context);

			// COLR
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'O', 'L', 'R'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new ColrTableReader(tableData, tableSize);
				m_hasCOLR = reader->BaseGlyphRecordsCount > 0;
				delete reader;
			}
			face->ReleaseFontTable(context);

			// SBIX
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
				face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('C', 'B', 'L', 'C'), &tableData, &tableSize, &context, &exists);
				if (exists)
				{
					auto reader = ref new CblcTableReader(tableData, tableSize);
					m_hasBitmap = reader->SizeTableCount > 0;
					delete reader;
				}
				face->ReleaseFontTable(context);
			}

			// POST
			face->TryGetFontTable(DWRITE_MAKE_OPENTYPE_TAG('p', 'o', 's', 't'), &tableData, &tableSize, &context, &exists);
			if (exists)
			{
				auto reader = ref new PostTableReader(tableData, tableSize);
				GlyphNames = reader->Map;
				delete reader;
			}
			face->ReleaseFontTable(context);
		}
	};
}