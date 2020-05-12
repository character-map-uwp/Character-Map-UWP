#pragma once
#include <pch.h>
#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
#include "DWriteFontAxisAttribute.h"
#include "DWriteNamedFontAxisValue.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;

namespace CharacterMapCX
{
	public ref class DWriteFallbackFont sealed
	{

	internal:
		DWriteFallbackFont(ComPtr<IDWriteFontFallback> fallback)
		{
			Fallback = fallback;
		}

		ComPtr<IDWriteFontFallback> Fallback;
	};
}