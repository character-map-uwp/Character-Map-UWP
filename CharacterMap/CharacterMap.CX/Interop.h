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
		bool HasValidFonts(Uri^ uri);
		
		IAsyncOperation<bool>^ WriteToFileAsync(CanvasFontFace^ fontFace, Windows::Storage::StorageFile^ storageFile);
		IBuffer^ GetImageDataBuffer(CanvasFontFace^ fontFace, UINT32 pixelsPerEm, UINT unicodeIndex, UINT imageType);
		CanvasTextLayoutAnalysis^ AnalyzeFontLayout(CanvasTextLayout^ layout, CanvasFontFace^ fontFace);
		CanvasTextLayoutAnalysis^ AnalyzeCharacterLayout(CanvasTextLayout^ layout);
		IVectorView<PathData^>^ GetPathDatas(CanvasFontFace^ fontFace, const Platform::Array<UINT16>^ glyphIndicies);
		Platform::String^ GetPathData(CanvasFontFace^ fontFace, UINT16 glyphIndicie);
		PathData^ GetPathData(CanvasGeometry^ geometry);

		DWriteFontSet^ GetSystemFonts();
		DWriteFontSet^ GetFonts(Uri^ uri);

	private:
		__inline DWriteFontSet^ GetFonts(ComPtr<IDWriteFontSet3> fontSet);
		__inline DWriteProperties^ GetDWriteProperties(ComPtr<IDWriteFontSet3> fontSet, UINT index, ComPtr<IDWriteFontFaceReference1> faceRef, int ls, wchar_t* locale);
		__inline String^ GetLocaleString(ComPtr<IDWriteLocalizedStrings> strings, int ls, wchar_t* locale);
		__inline DWriteProperties^ GetDWriteProperties(CanvasFontSet^ fontSet, UINT index);

		IAsyncAction^ ListenForFontSetExpirationAsync();


		bool m_isFontSetStale = true;
		ComPtr<IDWriteFontSet3> m_systemFontSet;
		DWriteFontSet^ m_appFontSet;

		ComPtr<IDWriteFactory7> m_dwriteFactory;
		ComPtr<ID2D1Factory5> m_d2dFactory;
		ComPtr<ID2D1DeviceContext1> m_d2dContext;
    };
}
