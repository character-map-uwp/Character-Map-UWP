#include "pch.h"
#include "D2DPathGeometry.h"
#include "CompositionDeviceManager.h"
#include "SVGGeometrySink.h"
#include <windows.graphics.h>
#include <windows.graphics.interop.h>
#include <algorithm>

using namespace CharacterMapCX;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Numerics;
using namespace Windows::Graphics;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml;

namespace
{
    class D2DGeometrySource : public RuntimeClass<
        RuntimeClassFlags<WinRtClassicComMix>,
        ABI::Windows::Graphics::IGeometrySource2D,
        ABI::Windows::Graphics::IGeometrySource2DInterop>
    {
        InspectableClass(L"CharacterMapCX.D2DGeometrySource", BaseTrust);

    public:
        HRESULT RuntimeClassInitialize(ID2D1Geometry* geometry)
        {
            if (geometry == nullptr)
                return E_INVALIDARG;

            m_geometry = geometry;
            return S_OK;
        }

        IFACEMETHODIMP GetGeometry(ID2D1Geometry** value) override
        {
            if (value == nullptr)
                return E_POINTER;

            *value = m_geometry.Get();
            if (*value != nullptr)
                (*value)->AddRef();
            return S_OK;
        }

        IFACEMETHODIMP TryGetGeometryUsingFactory(ID2D1Factory* factory, ID2D1Geometry** value) override
        {
            if (value == nullptr)
                return E_POINTER;

            *value = nullptr;
            if (m_geometry == nullptr)
                return S_OK;

            if (factory != nullptr)
            {
                ComPtr<ID2D1Factory> geomFactory;
                m_geometry->GetFactory(&geomFactory);
                if (geomFactory.Get() != factory)
                    return S_OK;
            }

            *value = m_geometry.Get();
            (*value)->AddRef();
            return S_OK;
        }

    private:
        ComPtr<ID2D1Geometry> m_geometry;
    };
}

D2DPathGeometry::D2DPathGeometry()
{
    ComPtr<ID2D1Factory5> factory = CompositionDeviceManager::GetD2DFactory();
    if (factory == nullptr)
        throw ref new Platform::COMException(E_FAIL);

    HRESULT hr = factory->CreatePathGeometry(&m_geometry);
    if (FAILED(hr) || m_geometry == nullptr)
        throw ref new Platform::COMException(hr);

    hr = m_geometry->Open(&m_sink);
    if (FAILED(hr) || m_sink == nullptr)
        throw ref new Platform::COMException(hr);
}

D2DPathGeometry::~D2DPathGeometry()
{
    if (m_sink != nullptr)
    {
        m_sink->Close();
        m_sink.Reset();
    }
    m_geometry.Reset();
}

void D2DPathGeometry::EnsureOpen()
{
    if (m_sink == nullptr)
        throw ref new Platform::COMException(E_UNEXPECTED);
}

void D2DPathGeometry::BeginFigure(float2 startPoint)
{
    BeginFigure(startPoint, true);
}

void D2DPathGeometry::BeginFigure(float2 startPoint, bool filled)
{
    EnsureOpen();
    m_sink->BeginFigure(
        D2D1::Point2F(startPoint.x, startPoint.y),
        filled ? D2D1_FIGURE_BEGIN_FILLED : D2D1_FIGURE_BEGIN_HOLLOW);
}

void D2DPathGeometry::AddLine(float2 endPoint)
{
    EnsureOpen();
    m_sink->AddLine(D2D1::Point2F(endPoint.x, endPoint.y));
}

void D2DPathGeometry::AddArc(
    float2 endPoint,
    float radiusX,
    float radiusY,
    float rotationAngle,
    bool isClockwise,
    bool isLargeArc)
{
    EnsureOpen();
    D2D1_ARC_SEGMENT seg{};
    seg.point = D2D1::Point2F(endPoint.x, endPoint.y);
    seg.size = D2D1::SizeF(radiusX, radiusY);
    seg.rotationAngle = rotationAngle;
    seg.sweepDirection = isClockwise ? D2D1_SWEEP_DIRECTION_CLOCKWISE : D2D1_SWEEP_DIRECTION_COUNTER_CLOCKWISE;
    seg.arcSize = isLargeArc ? D2D1_ARC_SIZE_LARGE : D2D1_ARC_SIZE_SMALL;
    m_sink->AddArc(&seg);
}

void D2DPathGeometry::AddBezier(float2 point1, float2 point2, float2 point3)
{
    EnsureOpen();
    D2D1_BEZIER_SEGMENT seg{};
    seg.point1 = D2D1::Point2F(point1.x, point1.y);
    seg.point2 = D2D1::Point2F(point2.x, point2.y);
    seg.point3 = D2D1::Point2F(point3.x, point3.y);
    m_sink->AddBezier(&seg);
}

void D2DPathGeometry::EndFigure(bool closed)
{
    EnsureOpen();
    m_sink->EndFigure(closed ? D2D1_FIGURE_END_CLOSED : D2D1_FIGURE_END_OPEN);
}

void D2DPathGeometry::Close()
{
    if (m_sink != nullptr)
    {
        HRESULT hr = m_sink->Close();
        m_sink.Reset();
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);
    }
}

IGeometrySource2D^ D2DPathGeometry::AsGeometrySource()
{
    Close();

    if (m_geometry == nullptr)
        throw ref new Platform::COMException(E_UNEXPECTED);

    ComPtr<D2DGeometrySource> source;
    HRESULT hr = MakeAndInitialize<D2DGeometrySource>(&source, m_geometry.Get());
    if (FAILED(hr) || source == nullptr)
        throw ref new Platform::COMException(hr);

    return reinterpret_cast<IGeometrySource2D^>(source.Get());
}

CompositionPath^ D2DPathGeometry::ToCompositionPath()
{
    return ref new CompositionPath(AsGeometrySource());
}

Platform::String^ D2DPathGeometry::ToSVGPath()
{
    Close();

    if (m_geometry == nullptr)
        throw ref new Platform::COMException(E_UNEXPECTED);

    ComPtr<SVGGeometrySink> sink = new (std::nothrow) SVGGeometrySink();
    if (sink == nullptr)
        throw ref new Platform::COMException(E_OUTOFMEMORY);

    HRESULT hr = m_geometry->Stream(sink.Get());
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    sink->Close();
    return sink->GetPathData();
}

D2DPathGeometry^ D2DPathGeometry::CreateRoundedRectangle(Rect rc, CornerRadius cr)
{
    float left = static_cast<float>(rc.Left);
    float top = static_cast<float>(rc.Top);
    float right = static_cast<float>(rc.Right);
    float bottom = static_cast<float>(rc.Bottom);
    float width = (std::max)(0.001f, static_cast<float>(rc.Width));
    float height = (std::max)(0.001f, static_cast<float>(rc.Height));

    float tl = (std::max)(0.0f, static_cast<float>(cr.TopLeft));
    float tr = (std::max)(0.0f, static_cast<float>(cr.TopRight));
    float br = (std::max)(0.0f, static_cast<float>(cr.BottomRight));
    float bl = (std::max)(0.0f, static_cast<float>(cr.BottomLeft));

    float factor = 1.0f;
    if (tl + tr > width)
        factor = (std::min)(factor, width / (tl + tr));
    if (bl + br > width)
        factor = (std::min)(factor, width / (bl + br));
    if (tl + bl > height)
        factor = (std::min)(factor, height / (tl + bl));
    if (tr + br > height)
        factor = (std::min)(factor, height / (tr + br));

    if (factor < 1.0f)
    {
        tl *= factor;
        tr *= factor;
        br *= factor;
        bl *= factor;
    }

    D2DPathGeometry^ geom = ref new D2DPathGeometry();
    geom->BeginFigure(float2(right - tr, top));

    if (tr > 0.0f)
        geom->AddArc(float2(right, top + tr), tr, tr, 0.0f, true, false);
    else
        geom->AddLine(float2(right, top));

    geom->AddLine(float2(right, bottom - br));

    if (br > 0.0f)
        geom->AddArc(float2(right - br, bottom), br, br, 0.0f, true, false);
    else
        geom->AddLine(float2(right, bottom));

    geom->AddLine(float2(left + bl, bottom));

    if (bl > 0.0f)
        geom->AddArc(float2(left, bottom - bl), bl, bl, 0.0f, true, false);
    else
        geom->AddLine(float2(left, bottom));

    geom->AddLine(float2(left, top + tl));

    if (tl > 0.0f)
        geom->AddArc(float2(left + tl, top), tl, tl, 0.0f, true, false);
    else
        geom->AddLine(float2(left, top));

    geom->AddLine(float2(right - tr, top));
    geom->EndFigure(true);
    geom->Close();
    return geom;
}

