#pragma once

#include "DWriteFontFace.h"

using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class DWriteFontSet sealed
	{
	public:

		property IVectorView<DWriteFontFace^>^ Fonts
		{
			IVectorView<DWriteFontFace^>^ get() { return m_fonts; }
		}

		property int AppxFontCount
		{
			int get() { return m_appxCount; }
		}

		property int CloudFontCount
		{
			int get() { return m_cloudCount; }
		}

	internal:
		DWriteFontSet(IVectorView<DWriteFontFace^>^ fonts, int appxCount, int cloudCount)
		{
			m_fonts = fonts;
			m_appxCount = appxCount;
			m_cloudCount = cloudCount;
		}

	private:
		inline DWriteFontSet() { }

		IVectorView<DWriteFontFace^>^ m_fonts = nullptr;
		int m_appxCount = 0;
		int m_cloudCount = 0;
	};
}