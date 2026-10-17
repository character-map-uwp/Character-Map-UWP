#pragma once

#include <vector>
#include <WindowsNumerics.h>

using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Foundation::Numerics;

namespace CharacterMapCX
{
	public ref class DWriteGlyphOutline sealed
	{
	public:
		DWriteGlyphOutline()
			: m_contours(nullptr), m_pointOnCurve(nullptr), m_bounds(Rect::Empty)
		{
		}

		DWriteGlyphOutline(
			IVectorView<IVectorView<float2>^>^ contours,
			IVectorView<bool>^ pointOnCurve,
			Rect bounds)
			: m_contours(contours), m_pointOnCurve(pointOnCurve), m_bounds(bounds)
		{
		}

		property IVectorView<IVectorView<float2>^>^ Contours
		{
			IVectorView<IVectorView<float2>^>^ get() { return m_contours; }
			void set(IVectorView<IVectorView<float2>^>^ value) { m_contours = value; }
		}

		property IVectorView<bool>^ PointOnCurve
		{
			IVectorView<bool>^ get() { return m_pointOnCurve; }
			void set(IVectorView<bool>^ value) { m_pointOnCurve = value; }
		}

		property Rect Bounds
		{
			Rect get() { return m_bounds; }
			void set(Rect value) { m_bounds = value; }
		}

	private:
		IVectorView<IVectorView<float2>^>^ m_contours;
		IVectorView<bool>^ m_pointOnCurve;
		Rect m_bounds;
	};
}
