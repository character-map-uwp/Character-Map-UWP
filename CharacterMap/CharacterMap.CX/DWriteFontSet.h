#pragma once

#include "DWriteFontFamily.h"
#include "DWriteFontFace.h"

using namespace Windows::Foundation::Collections;
using namespace Platform::Collections;

namespace CharacterMapCX
{
	public ref class DWriteFontSet sealed
	{
	public:

		property IVectorView<DWriteFontFace^>^ Fonts
		{
			IVectorView<DWriteFontFace^>^ get() { return m_fonts; }
		}

		property IVectorView<DWriteFontFamily^>^ Families
		{
			IVectorView<DWriteFontFamily^>^ get() { return m_families; }
		}

		property int AppxFontCount { int get() { return m_appxCount; } }

		property int CloudFontCount { int get() { return m_cloudCount; } }

		property int VariableFontCount { int get() { return m_varCount; } }

		DWriteFontSet^ Inflate()
		{
			for each (auto family in m_families)
				family->Inflate();

			this->Update();
			return this;
		}

		void Update()
		{
			int appxCount = 0;
			int cloudCount = 0;

			auto fonts = ref new Vector<DWriteFontFace^>();

			for each (auto family in m_families)
			{
				for each (auto font in family->m_fonts)
				{
					fonts->Append(font);
					if (font->Properties->Source == DWriteFontSource::AppxPackage)
						m_appxCount++;
				}
			}

			m_fonts = fonts->GetView();
		}

	internal:
		DWriteFontSet(IVectorView<DWriteFontFamily^>^ families)
		{
			m_families = families;
		}


		DWriteFontSet(IVectorView<DWriteFontFace^>^ fonts, int ls, wchar_t* locale, int appxCount, int cloudCount, int variableCount)
		{
			//m_fonts = fonts;
			m_appxCount = appxCount;
			m_cloudCount = cloudCount;
			m_varCount = variableCount;

			_ls = ls;
			_locale = locale;
		}

	private:
		inline DWriteFontSet() { }

		int _ls = 0;
		wchar_t* _locale = nullptr;

		IVectorView<DWriteFontFace^>^ m_fonts = nullptr;
		IVectorView<DWriteFontFamily^>^ m_families = nullptr;
		int m_appxCount = 0;
		int m_cloudCount = 0;
		int m_varCount = 1;
	};
}