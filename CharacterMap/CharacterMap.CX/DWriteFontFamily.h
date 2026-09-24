#pragma once

#include "DWriteFontFace.h"
using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class DWriteFontFamily sealed
	{
	public:
		void Inflate();

		property Platform::String^ Name
		{
			Platform::String^ get()
			{
				if (m_name == nullptr)
					Inflate();
				return m_name;
			}
		}

		property IVectorView<DWriteFontFace^>^ Fonts
		{
			IVectorView<DWriteFontFace^>^ get() { return m_fonts; }
		}

	internal:
		DWriteFontFamily(ComPtr<IDWriteFontFamily2> family)
		{
			m_family = family;
		}

		Platform::String^ m_name = nullptr;
		IVectorView<DWriteFontFace^>^ m_fonts = nullptr;

	private:
		ComPtr<IDWriteFontFamily2> m_family = nullptr;
		ComPtr<IDWriteFontCollection3> m_collection = nullptr;
	};
}
