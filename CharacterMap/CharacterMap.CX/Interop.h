#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "CanvasTextLayoutAnalysis.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;

namespace CharacterMapCX
{
    public ref class Interop sealed
    {
    public:
        Interop(CanvasDevice^ device);
		CanvasTextLayoutAnalysis^ AnalyzeFontLayout(CanvasTextLayout^ layout);
		CanvasTextLayoutAnalysis^ AnalyzeCharacterLayout(CanvasTextLayout^ layout);
		bool HasValidFonts(Uri^ uri);
		CanvasFontSet^ GetSystemFonts(bool includeDownloadableFonts);

	private:
		Microsoft::WRL::ComPtr<IDWriteFactory4> m_dwriteFactory;
		Microsoft::WRL::ComPtr<ID2D1Factory5> m_d2dFactory;
		Microsoft::WRL::ComPtr<ID2D1DeviceContext1> m_d2dContext;
    };
}
