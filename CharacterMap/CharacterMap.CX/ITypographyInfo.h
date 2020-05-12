#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <WindowsNumerics.h>

using namespace Windows::Foundation;
using namespace Windows::Foundation::Numerics;
using namespace Platform;
using namespace Microsoft::Graphics::Canvas::Text;

namespace CharacterMapCX
{
	public interface class ITypographyInfo
	{
		property String^ DisplayName { String^ get(); };
		property CanvasTypographyFeatureName Feature { CanvasTypographyFeatureName get(); };
	};
}