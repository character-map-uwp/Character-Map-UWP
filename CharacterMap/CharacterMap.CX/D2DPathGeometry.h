#pragma once

#include <d2d1_3.h>
#include <wrl.h>
#include <WindowsNumerics.h>

namespace CharacterMapCX
{
    [Windows::Foundation::Metadata::WebHostHidden]
    public ref class D2DPathGeometry sealed
    {
    public:
        D2DPathGeometry();
        virtual ~D2DPathGeometry();

        void BeginFigure(Windows::Foundation::Numerics::float2 startPoint);
        void BeginFigure(Windows::Foundation::Numerics::float2 startPoint, bool filled);
        void AddLine(Windows::Foundation::Numerics::float2 endPoint);
        void AddArc(
            Windows::Foundation::Numerics::float2 endPoint,
            float radiusX,
            float radiusY,
            float rotationAngle,
            bool isClockwise,
            bool isLargeArc);
        void AddBezier(
            Windows::Foundation::Numerics::float2 point1,
            Windows::Foundation::Numerics::float2 point2,
            Windows::Foundation::Numerics::float2 point3);
        void EndFigure(bool closed);
        void Close();

        Windows::Graphics::IGeometrySource2D^ AsGeometrySource();
        Windows::UI::Composition::CompositionPath^ ToCompositionPath();
        Platform::String^ ToSVGPath();

        static D2DPathGeometry^ CreateRoundedRectangle(
            Windows::Foundation::Rect rect,
            Windows::UI::Xaml::CornerRadius cornerRadius);

    internal:
        ID2D1PathGeometry* GetGeometry() { return m_geometry.Get(); }

    private:
        void EnsureOpen();

        Microsoft::WRL::ComPtr<ID2D1PathGeometry> m_geometry;
        Microsoft::WRL::ComPtr<ID2D1GeometrySink> m_sink;
    };
}

