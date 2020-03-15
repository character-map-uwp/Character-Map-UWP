#pragma once

#include <Microsoft.Graphics.Canvas.native.h>
#include <d2d1_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <WindowsNumerics.h>

using namespace Windows::Foundation;
using namespace Windows::Foundation::Numerics;
using namespace Platform;

namespace CharacterMapCX
{
	public ref class PathData sealed
	{
	public:

		property String^ Path
		{
			String^ get() { return m_path; }
		}

		property float3x2 Transform
		{
			float3x2 get() { return m_matrix; }
		}

		property Rect Bounds
		{
			Rect get() { return m_bounds; }
		}

	internal:
		PathData(String^ path, D2D1::Matrix3x2F* matrix)
		{
			m_path = path;
			m_matrix = { matrix->_11, matrix->_12, matrix->_21, matrix->_22, matrix->_31, matrix->_32 };
		}

		PathData(String^ path, Rect bounds)
		{
			m_path = path;
			m_bounds = bounds;
		}

	private:
		inline PathData() { }

		Rect m_bounds = Rect::Empty;
		float3x2 m_matrix;
		String^ m_path = nullptr;
	};
}