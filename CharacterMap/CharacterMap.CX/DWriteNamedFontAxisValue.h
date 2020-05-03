#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
#include "DWriteFontAxisAttribute.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;

namespace CharacterMapCX
{
	public ref class DWriteNamedFontAxisValue sealed
	{
	public:
		property String^ Tag { String^ get() { return m_tag; } }
		property String^ Name { String^ get() { return m_name; } }
		property float Value { float get() { return m_value; } }



	internal:
		DWriteNamedFontAxisValue(
			DWRITE_FONT_AXIS_RANGE range,
			String^ tag,
			String^ name)
		{
			// for a named instance, min & max are the same;
			m_value = range.minValue;
			m_tag = tag;
			m_name = name;
		};

	private:
		inline DWriteNamedFontAxisValue() { };

		DWriteFontAxisAttribute m_attribute;

		String^ m_tag;
		String^ m_name;

		float m_value;
	};
}