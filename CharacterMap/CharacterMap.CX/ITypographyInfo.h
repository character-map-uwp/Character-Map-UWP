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
	public interface class ICharacter
	{
		property String^ Char { String^ get(); };
		property String^ UnicodeString { String^ get(); };
		property unsigned int UnicodeIndex {  unsigned int get(); };
	};

	public interface class IFontFace
	{
		String^ GetDescription(ICharacter^ c, bool allowUnihan);
	};

	public interface class ITypographyInfo
	{
		property String^ DisplayName { String^ get(); };
		property CanvasTypographyFeatureName Feature { CanvasTypographyFeatureName get(); };
	};
}