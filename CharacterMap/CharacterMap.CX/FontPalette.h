#pragma once

using namespace Platform;
using namespace Platform::Metadata;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI;

namespace Windows
{
	namespace UI
	{
		inline bool operator==(const Windows::UI::Color& a, const Windows::UI::Color& b)
		{
			return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
		}

		inline bool operator!=(const Windows::UI::Color& a, const Windows::UI::Color& b)
		{
			return !(a == b);
		}
	}
}

namespace CharacterMapCX
{
	[FlagsAttribute]
	public enum class FontPaletteType : unsigned int
	{
		None = 0,
		LightBackground = 1,
		DarkBackground = 2
	};

	public ref class FontPalette sealed
	{
	public:
		property uint32 Index
		{
			uint32 get() { return m_index; }
		}

		property String^ Name
		{
			String^ get() { return m_name; }
		}

		property FontPaletteType PaletteType
		{
			FontPaletteType get() { return m_paletteType; }
		}

		property IVectorView<Color>^ Colors
		{
			IVectorView<Color>^ get() { return m_colors; }
		}

	internal:
		FontPalette(uint32 index, String^ name, FontPaletteType type, IVectorView<Color>^ colors)
			: m_index(index), m_name(name), m_paletteType(type), m_colors(colors)
		{
		}

	private:
		uint32 m_index;
		String^ m_name;
		FontPaletteType m_paletteType;
		IVectorView<Color>^ m_colors;
	};
}
