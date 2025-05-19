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

		property String^ Label		{ String^ get() { return m_label; } }

		property UINT32 Tag			{ UINT32 get() { return m_tag_raw; } }

		property float Value;

		property float DefaultValue { float get() { return m_originalValue; } }

		property float AxisDefault	{ float get() { return m_defaultValue; } }

		property float Minimum		{ float get() { return m_minimumValue; } }

		property float Maximum		{ float get() { return m_maximumValue; } }

		property bool IsHidden		{ bool get() { return m_isHidden; } }

		
		DWriteFontAxis^ WithValue(float value)
		{
			auto a = ref new DWriteFontAxis(this);
			a->Value = value;
			return a;
		}

	internal:
		DWriteFontAxis(DWriteFontAxis^ axis)
		{
			m_attribute = axis->Attribute;

			m_minimumValue = axis->Minimum;
			m_maximumValue = axis->Maximum;
			m_defaultValue = axis->DefaultValue;

			Value = axis->Value;
			m_originalValue = axis->m_originalValue;
			m_label = axis->Label;
			m_tag_raw = axis->Tag;

			m_isHidden = (m_attribute & DWriteFontAxisAttribute::Hidden) == DWriteFontAxisAttribute::Hidden;
		};

		DWriteFontAxis(
			DWRITE_FONT_AXIS_ATTRIBUTES attribute,
			DWRITE_FONT_AXIS_RANGE range,
			DWRITE_FONT_AXIS_VALUE def,
			DWRITE_FONT_AXIS_VALUE value,
			UINT32 tag,
			String^ name)
		{
			m_attribute = static_cast<DWriteFontAxisAttribute>(attribute);

			m_minimumValue = range.minValue;
			m_maximumValue = range.maxValue;
			m_defaultValue = def.value;

			Value = value.value;
			m_originalValue = value.value;
			m_label = name == nullptr ? GetOpenTypeFeatureTag(tag) : name;
			m_tag_raw = tag;

			m_isHidden = (m_attribute & DWriteFontAxisAttribute::Hidden) == DWriteFontAxisAttribute::Hidden;
		};

		DWRITE_FONT_AXIS_VALUE GetDWriteValue()
		{
			return  { static_cast<DWRITE_FONT_AXIS_TAG>(m_tag_raw), this->Value };
		}

	private:
		inline DWriteFontAxis() { };

		DWriteFontAxisAttribute m_attribute;
		String^ m_label;

		UINT32 m_tag_raw = 0;
		float m_originalValue;
		float m_defaultValue;
		float m_minimumValue;
		float m_maximumValue;
		bool m_isHidden;
	};
}