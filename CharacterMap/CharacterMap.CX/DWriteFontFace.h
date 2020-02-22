#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include "ColorTextAnalyzer.h"
#include "DWriteProperties.h"
#include <vector>

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class DWriteFontFace sealed
	{
	public:

		property CanvasFontFace^ FontFace
		{
			CanvasFontFace^ get() { return m_fontFace; }
		}

		property DWriteProperties^ Properties
		{
			DWriteProperties^ get() { return m_dwProperties; }
		}


	internal:
		DWriteFontFace(CanvasFontFace^ fontFace, DWriteProperties^ properties)
		{
			m_fontFace = fontFace;
			m_dwProperties = properties;
		};

	private:
		inline DWriteFontFace() { }

		CanvasFontFace^ m_fontFace = nullptr;
		DWriteProperties^ m_dwProperties = nullptr;
	};
}