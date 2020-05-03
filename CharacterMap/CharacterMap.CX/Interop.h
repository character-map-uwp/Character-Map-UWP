#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "CanvasTextLayoutAnalysis.h"
#include "DWriteFontSource.h"
#include "DWriteProperties.h"
#include "DWriteFontFace.h"
#include "DWriteFontSet.h"
#include "PathData.h"
#include "GlyphImageFormat.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::Graphics::Canvas::Geometry;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Storage::Streams;

namespace CharacterMapCX
{
	ref class Interop;
	public delegate void SystemFontSetInvalidated(Interop^ sender, Platform::Object^ args);

    public ref class Interop sealed
    {
    public:
		event SystemFontSetInvalidated^ FontSetInvalidated;

        Interop(CanvasDevice^ device);

		/// <summary>
		/// Verifies if a font file actually contains a font(s) usable by the system.
		/// </summary>
		bool HasValidFonts(Uri^ uri);
		
		/// <summary>
		/// Verifies if a font is actually completely on a users system. Some cloud fonts may only be partially downloaded.
		/// </summary>
		bool IsFontLocal(CanvasFontFace^ fontFace);

		/// <summary>
		/// Writes the underlying source file of a FontFace to a stream. 
		/// </summary>
		IAsyncOperation<bool>^ WriteToStreamAsync(CanvasFontFace^ fontFace, IOutputStream^ stream);

		/// <summary>
		/// Get a buffer representing an SVG or Bitmap image glyph. SVG glyphs may be compressed.
		/// </summary>
		IBuffer^ GetImageDataBuffer(CanvasFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, GlyphImageFormat format);

		CanvasTextLayoutAnalysis^ AnalyzeFontLayout(CanvasTextLayout^ layout, CanvasFontFace^ fontFace);
		CanvasTextLayoutAnalysis^ AnalyzeCharacterLayout(CanvasTextLayout^ layout);
		IVectorView<PathData^>^ GetPathDatas(CanvasFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies);
		Platform::String^ GetPathData(CanvasFontFace^ fontFace, UINT16 glyphIndicie);

		/// <summary>
		/// Attempts to get the source filename of a font. Will return NULL for cloud fonts.
		/// </summary>
		Platform::String^ GetFileName(CanvasFontFace^ fontFace);

		/// <summary>
		/// Returns an SVG-Path syntax compatible representation of the Canvas Text Geometry.
		/// </summary>
		PathData^ GetPathData(CanvasGeometry^ geometry);

		DWriteFontSet^ GetSystemFonts();
		__inline DWriteFontSet^ GetFonts(Uri^ uri);
		IVectorView<DWriteFontSet^>^ GetFonts(IVectorView<Uri^>^ uris);

	private:
		__inline DWriteFontSet^ GetFonts(ComPtr<IDWriteFontSet3> fontSet);
		__inline DWriteProperties^ GetDWriteProperties(ComPtr<IDWriteFontSet3> fontSet, UINT index, ComPtr<IDWriteFontFaceReference1> faceRef, int ls, wchar_t* locale);
		__inline DWriteProperties^ GetDWriteProperties(CanvasFontSet^ fontSet, UINT index);
		__inline String^ GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale);
		
		__inline bool IsLocalFont(ComPtr<IDWriteFontFileLoader> loader, const void* refKey, uint32 size);

		IAsyncAction^ ListenForFontSetExpirationAsync();


		bool m_isFontSetStale = true;
		ComPtr<IDWriteFontSet3> m_systemFontSet;
		DWriteFontSet^ m_appFontSet;

		ComPtr<IDWriteFactory7> m_dwriteFactory;
		ComPtr<ID2D1Factory5> m_d2dFactory;
		ComPtr<ID2D1DeviceContext1> m_d2dContext;
    };
}
