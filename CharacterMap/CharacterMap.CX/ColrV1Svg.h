#pragma once

#include "pch.h"
#include "DWriteFontFace.h"

namespace CharacterMapCX
{
	class ColrV1Svg
	{
	public:
		static Platform::String^ GetSvg(
			DWriteFontFace^ fontFace,
			UINT16 glyphIndex,
			Windows::UI::Color defaultColor);
	};
}
