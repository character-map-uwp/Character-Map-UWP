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
	public ref class DWriteFontAxis sealed
	{
	public:

		property DWriteFontAxisAttribute Attribute
		{
			DWriteFontAxisAttribute get() { return m_attribute; }
		}

		/*property IVectorView<DWriteNamedFontAxisValue^>^ NamedInstances
		{
			IVectorView<DWriteNamedFontAxisValue^>^ get() { return m_instances;  }
		}*/

		property String^ Tag		{ String^ get() { return m_tag; } }

		property float Value;

		property float Default		{ float get() { return m_defaultValue; } }

		property float Minimum		{ float get() { return m_minimumValue; } }

		property float Maximum		{ float get() { return m_maximumValue; } }

		



	internal:
		DWriteFontAxis(
			DWRITE_FONT_AXIS_ATTRIBUTES attribute,
			DWRITE_FONT_AXIS_RANGE range,
			DWRITE_FONT_AXIS_VALUE def,
			DWRITE_FONT_AXIS_VALUE value,
			//IVectorView<DWriteNamedFontAxisValue^>^ instances,
			UINT32 tag)
		{
			m_attribute = static_cast<DWriteFontAxisAttribute>(attribute);
			//m_instances = instances;

			m_minimumValue = range.minValue;
			m_maximumValue = range.maxValue;
			m_defaultValue = def.value;

			Value = value.value;
			m_tag = GetOpenTypeFeatureTag(tag);
			m_tag_raw = tag;
		};

		DWRITE_FONT_AXIS_VALUE GetDWriteValue()
		{
			auto value = DWRITE_FONT_AXIS_VALUE();
			value.axisTag = static_cast<DWRITE_FONT_AXIS_TAG>(m_tag_raw);
			value.value = this->Value;
			return value;
		}

	private:
		inline DWriteFontAxis() { };

		

		DWriteFontAxisAttribute m_attribute;
		//IVectorView<DWriteNamedFontAxisValue^>^ m_instances;
		String^ m_tag;

		UINT32 m_tag_raw = 0;
		float m_defaultValue;
		float m_minimumValue;
		float m_maximumValue;
	};
}