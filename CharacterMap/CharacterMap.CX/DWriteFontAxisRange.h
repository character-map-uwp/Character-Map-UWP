#pragma once

#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
#include "DWriteFontAxisAttribute.h"

using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;

namespace CharacterMapCX
{
	public ref class DWriteFontAxisRange sealed
	{
	public:
		property String^	Tag			{ String^ get() { return m_tag; } }
		property String^	Name		{ String^ get() { return m_name; } }
		property float		Minimum		{ float get() { return m_minimumValue; } }
		property float		Maximum		{ float get() { return m_maximumValue; } }



	internal:
		DWriteFontAxisRange(
			DWRITE_FONT_AXIS_RANGE range,
			String^ tag,
			String^ name)
		{
			m_minimumValue = range.minValue;
			m_maximumValue = range.maxValue;

			m_tag = tag;
			m_name = name;
		};

	private:
		inline DWriteFontAxisRange() { };

		DWriteFontAxisAttribute m_attribute;

		String^ m_tag;
		String^ m_name;

		float m_minimumValue;
		float m_maximumValue;
	};
}