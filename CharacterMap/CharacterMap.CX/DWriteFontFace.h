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
			CanvasFontFace^ get() { 

				if (m_fontFace == nullptr)
					Realize();
				return m_fontFace; 
			}
		}

		property DWriteProperties^ Properties
		{
			DWriteProperties^ get() { return m_dwProperties; }
		}

	internal:
		DWriteFontFace(ComPtr<IDWriteFont3> font, DWriteProperties^ properties)
		{
			m_font = font;
			m_dwProperties = properties;
		};

		void Realize()
		{
			ComPtr<IDWriteFontFaceReference> ref;
			ThrowIfFailed(m_font->GetFontFaceReference(&ref));
			m_fontFace = GetOrCreate<CanvasFontFace>(ref.Get());
		}

		/*DWriteFontFace(ComPtr<IDWriteFontFaceReference1> fontResource, DWriteProperties^ properties)
		{
			m_fontResource = fontResource;
			m_dwProperties = properties;
		};*/

		void SetProperties(DWriteProperties^ props)
		{
			m_dwProperties = props;
		}

		ComPtr<IDWriteFont3> m_font = nullptr;
		ComPtr<IDWriteFontFaceReference1> m_fontResource = nullptr;

	private:
		inline DWriteFontFace() { }

		CanvasFontFace^ m_fontFace = nullptr;
		DWriteProperties^ m_dwProperties = nullptr;
	};
}