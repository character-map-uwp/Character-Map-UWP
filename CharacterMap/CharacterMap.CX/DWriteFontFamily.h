#pragma once

#include "DWriteFontFace.h"
using namespace Windows::Foundation::Collections;

namespace CharacterMapCX
{
	public ref class DWriteFontFamily sealed
	{
	public:
		void Inflate();

	internal:
		DWriteFontFamily(ComPtr<IDWriteFontFamily2> family)
		{
			m_family = family;
		}

		IVectorView<DWriteFontFace^>^ m_fonts = nullptr;

	private:
		ComPtr<IDWriteFontFamily2> m_family = nullptr;
		ComPtr<IDWriteFontCollection3> m_collection = nullptr;
	};
}
