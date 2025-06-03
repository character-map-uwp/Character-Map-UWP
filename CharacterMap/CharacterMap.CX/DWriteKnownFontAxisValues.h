#pragma once

#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <string>
#include "DWriteNamedFontAxisValue.h"

using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace CharacterMapCX;

namespace CharacterMapCX
{
	public ref class DWriteKnownFontAxisValues sealed
	{
	public:
		property IVectorView<DWriteNamedFontAxisValue^>^ Values
		{
			IVectorView<DWriteNamedFontAxisValue^>^ get() { return m_values; }
		}

		property String^ Name { String^ get() { return m_name; } }

	internal:
		DWriteKnownFontAxisValues(
			String^ name,
			IVectorView<DWriteNamedFontAxisValue^>^ values)
		{
			m_name = name;
			m_values = values;
		};

	private:
		IVectorView<DWriteNamedFontAxisValue^>^ m_values;
		String^ m_name;
	};
}