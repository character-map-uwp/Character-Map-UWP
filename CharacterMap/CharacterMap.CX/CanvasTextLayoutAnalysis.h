#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "GlyphImageFormat.h"
#include <vector>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class CanvasTextLayoutAnalysis sealed
	{
	public:
		inline CanvasTextLayoutAnalysis()
		{

		}

		property bool HasColorGlyphs
		{
			bool get() { return m_hasColorGlyphs; }
		}

		property bool ContainsBitmapGlyphs
		{
			bool get() { return m_containsBitmapGlyphs; }
		}

		property bool IsFullVectorBased
		{
			bool get() { return !m_containsBitmapGlyphs; }
		}

		property IVectorView<GlyphImageFormat>^ GlyphFormats
		{
			IVectorView<GlyphImageFormat>^ get() { return m_glyphFormats; }
		}

	internal:
		CanvasTextLayoutAnalysis(bool hasColorGlyphs, std::vector<GlyphImageFormat> formats);

	private:
		bool m_hasColorGlyphs = false;
		bool m_containsBitmapGlyphs = false;
		IVectorView<GlyphImageFormat>^ m_glyphFormats;
	};
}