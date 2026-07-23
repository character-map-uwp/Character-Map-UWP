#pragma once

#include <d2d1_2.h>
#include <dwrite_3.h>

using namespace Windows::Foundation;
using namespace Windows::UI;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class ColorPathData sealed
	{
	public:
		property String^ Path;
		property Color Color;
		property Rect Bounds;
		property bool IsGradient;
		property bool IsClip;
		property bool FillRuleEvenOdd;
		property String^ PaintReference;
		property String^ PaintDefinition;

		ColorPathData()
		{
			Path = nullptr;
			Color = Colors::Black;
			Bounds = Rect::Empty;
			IsGradient = false;
			IsClip = false;
			FillRuleEvenOdd = false;
			PaintReference = nullptr;
			PaintDefinition = nullptr;
		}
	};
}
