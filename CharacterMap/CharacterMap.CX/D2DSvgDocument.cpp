#include "pch.h"
#include "D2DSvgDocument.h"
#include "CompositionDeviceManager.h"
#include <shcore.h>
#include <ppltasks.h>
#include <algorithm>
#include <sstream>
#include <vector>
#include <d2d1_3.h>

#pragma comment(lib, "shcore.lib")

using namespace CharacterMapCX;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Storage::Streams;

D2DSvgElement::D2DSvgElement(ID2D1SvgElement* element)
    : m_element(element)
{
}

D2DSvgElement^ D2DSvgElement::CreateAndAppendNamedChildElement(Platform::String^ name)
{
    if (m_element == nullptr || name == nullptr)
        return nullptr;

    ComPtr<ID2D1SvgElement> child;
    HRESULT hr = m_element->CreateChild(name->Data(), &child);
    if (FAILED(hr) || child == nullptr)
        throw ref new Platform::COMException(hr);

    return ref new D2DSvgElement(child.Get());
}

void D2DSvgElement::SetStringAttribute(Platform::String^ name, Platform::String^ value)
{
    if (m_element == nullptr || name == nullptr || value == nullptr)
        return;

    HRESULT hr = m_element->SetAttributeValue(name->Data(), D2D1_SVG_ATTRIBUTE_STRING_TYPE_SVG, value->Data());
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

void D2DSvgElement::SetRectangleAttribute(Platform::String^ name, Rect value)
{
    if (m_element == nullptr || name == nullptr)
        return;

    D2D1_SVG_VIEWBOX viewBox{};
    viewBox.x = static_cast<FLOAT>(value.X);
    viewBox.y = static_cast<FLOAT>(value.Y);
    viewBox.width = static_cast<FLOAT>(value.Width);
    viewBox.height = static_cast<FLOAT>(value.Height);

    HRESULT hr = m_element->SetAttributeValue(name->Data(), D2D1_SVG_ATTRIBUTE_POD_TYPE_VIEWBOX, &viewBox, sizeof(viewBox));
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

void D2DSvgElement::SetColorAttribute(Platform::String^ name, Windows::UI::Color value)
{
    if (m_element == nullptr || name == nullptr)
        return;

    D2D1_COLOR_F color = D2D1::ColorF(value.R / 255.0f, value.G / 255.0f, value.B / 255.0f, value.A / 255.0f);
    HRESULT hr = m_element->SetAttributeValue(name->Data(), D2D1_SVG_ATTRIBUTE_POD_TYPE_COLOR, &color, sizeof(color));
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

void D2DSvgElement::SetFloatAttribute(Platform::String^ name, float value)
{
    if (m_element == nullptr || name == nullptr)
        return;

    HRESULT hr = m_element->SetAttributeValue(name->Data(), value);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

D2DSvgDocument::D2DSvgDocument()
{
    Initialize(100.0f, 100.0f);
}

D2DSvgDocument::D2DSvgDocument(float viewportWidth, float viewportHeight)
{
    Initialize(viewportWidth, viewportHeight);
}

D2DSvgDocument::~D2DSvgDocument()
{
    m_svgDocument.Reset();
    m_d2dContext.Reset();
}

void D2DSvgDocument::Initialize(float viewportWidth, float viewportHeight)
{
    ComPtr<ID2D1Device> d2dDevice = CompositionDeviceManager::GetD2DDevice();
    if (d2dDevice == nullptr)
        throw ref new Platform::COMException(E_FAIL);

    ComPtr<ID2D1DeviceContext> baseContext;
    HRESULT hr = d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &baseContext);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    hr = baseContext.As(&m_d2dContext);
    if (FAILED(hr) || m_d2dContext == nullptr)
        throw ref new Platform::COMException(hr);

    D2D1_SIZE_F viewport = D2D1::SizeF((std::max)(1.0f, viewportWidth), (std::max)(1.0f, viewportHeight));
    hr = m_d2dContext->CreateSvgDocument(nullptr, viewport, &m_svgDocument);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);
}

D2DSvgElement^ D2DSvgDocument::Root::get()
{
    if (m_svgDocument == nullptr)
        return nullptr;

    ComPtr<ID2D1SvgElement> root;
    m_svgDocument->GetRoot(&root);
    return root != nullptr ? ref new D2DSvgElement(root.Get()) : nullptr;
}

IAsyncAction^ D2DSvgDocument::SaveAsync(IRandomAccessStream^ stream)
{
    if (stream == nullptr)
        throw ref new Platform::InvalidArgumentException(L"stream");

    ComPtr<ID2D1SvgDocument> svgDoc = m_svgDocument;
    return concurrency::create_async([stream, svgDoc]()
    {
        if (svgDoc == nullptr)
            throw ref new Platform::COMException(E_UNEXPECTED);

        ComPtr<IStream> comStream;
        HRESULT hr = CreateStreamOverRandomAccessStream(reinterpret_cast<IUnknown*>(stream), IID_PPV_ARGS(&comStream));
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);

        hr = svgDoc->Serialize(comStream.Get(), nullptr);
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);
    });
}

namespace
{
    class SvgPathGeometrySink : public Microsoft::WRL::RuntimeClass<
        Microsoft::WRL::RuntimeClassFlags<Microsoft::WRL::ClassicCom>,
        ID2D1SimplifiedGeometrySink>
    {
        std::wostringstream m_ss;
    public:
        IFACEMETHOD_(void, SetFillMode)(D2D1_FILL_MODE) override {}
        IFACEMETHOD_(void, SetSegmentFlags)(D2D1_PATH_SEGMENT) override {}
        IFACEMETHOD_(void, BeginFigure)(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN) override
        {
            m_ss << L"M " << startPoint.x << L" " << startPoint.y << L" ";
        }
        IFACEMETHOD_(void, AddLines)(const D2D1_POINT_2F* points, UINT32 pointsCount) override
        {
            for (UINT32 i = 0; i < pointsCount; ++i)
                m_ss << L"L " << points[i].x << L" " << points[i].y << L" ";
        }
        IFACEMETHOD_(void, AddBeziers)(const D2D1_BEZIER_SEGMENT* beziers, UINT32 beziersCount) override
        {
            for (UINT32 i = 0; i < beziersCount; ++i)
                m_ss << L"C " << beziers[i].point1.x << L" " << beziers[i].point1.y << L" "
                     << beziers[i].point2.x << L" " << beziers[i].point2.y << L" "
                     << beziers[i].point3.x << L" " << beziers[i].point3.y << L" ";
        }
        IFACEMETHOD_(void, EndFigure)(D2D1_FIGURE_END figureEnd) override
        {
            if (figureEnd == D2D1_FIGURE_END_CLOSED)
                m_ss << L"Z ";
        }
        IFACEMETHOD(Close)() override { return S_OK; }

        Platform::String^ GetPathData() const
        {
            std::wstring str = m_ss.str();
            return ref new Platform::String(str.c_str());
        }
    };
}

Platform::String^ D2DSvgDocument::GetInkPathData(Windows::UI::Input::Inking::InkStroke^ stroke)
{
    if (stroke == nullptr)
        return L"";

    auto segments = stroke->GetRenderingSegments();
    if (segments == nullptr || segments->Size == 0)
        return L"";

    ComPtr<ID2D1Device> d2dDevice = CompositionDeviceManager::GetD2DDevice();
    if (d2dDevice == nullptr)
        throw ref new Platform::COMException(E_FAIL);

    ComPtr<ID2D1DeviceContext> baseContext;
    HRESULT hr = d2dDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &baseContext);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    ComPtr<ID2D1DeviceContext2> d2dContext;
    hr = baseContext.As(&d2dContext);
    if (FAILED(hr) || d2dContext == nullptr)
        throw ref new Platform::COMException(hr);

    auto attrs = stroke->DrawingAttributes;
    float width = static_cast<float>(attrs->Size.Width);
    float height = static_cast<float>(attrs->Size.Height);
    bool ignorePressure = attrs->IgnorePressure;

    auto calcRadius = [width, ignorePressure](float pressure) -> float
    {
        return ignorePressure ? (width * 0.5f) : (width * pressure);
    };

    auto seg0 = segments->GetAt(0);
    D2D1_INK_POINT startPoint{};
    startPoint.x = static_cast<float>(seg0->Position.X);
    startPoint.y = static_cast<float>(seg0->Position.Y);
    startPoint.radius = calcRadius(seg0->Pressure);

    ComPtr<ID2D1Ink> d2dInk;
    hr = d2dContext->CreateInk(&startPoint, &d2dInk);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    if (segments->Size > 1)
    {
        std::vector<D2D1_INK_BEZIER_SEGMENT> beziers;
        beziers.reserve(segments->Size - 1);
        float prevRadius = startPoint.radius;
        for (unsigned int i = 1; i < segments->Size; ++i)
        {
            auto seg = segments->GetAt(i);
            float nextRadius = calcRadius(seg->Pressure);
            D2D1_INK_BEZIER_SEGMENT b{};
            b.point1 = { static_cast<float>(seg->BezierControlPoint1.X), static_cast<float>(seg->BezierControlPoint1.Y), prevRadius + (nextRadius - prevRadius) * (1.0f / 3.0f) };
            b.point2 = { static_cast<float>(seg->BezierControlPoint2.X), static_cast<float>(seg->BezierControlPoint2.Y), prevRadius + (nextRadius - prevRadius) * (2.0f / 3.0f) };
            b.point3 = { static_cast<float>(seg->Position.X), static_cast<float>(seg->Position.Y), nextRadius };
            beziers.push_back(b);
            prevRadius = nextRadius;
        }
        hr = d2dInk->AddSegments(beziers.data(), static_cast<UINT32>(beziers.size()));
        if (FAILED(hr))
            throw ref new Platform::COMException(hr);
    }

    D2D1_INK_STYLE_PROPERTIES styleProps{};
    styleProps.nibShape = (attrs->PenTip == Windows::UI::Input::Inking::PenTipShape::Rectangle)
        ? D2D1_INK_NIB_SHAPE_SQUARE
        : D2D1_INK_NIB_SHAPE_ROUND;

    float yScale = (width > 0.0f) ? (height / width) : 1.0f;
    auto tipTx = attrs->PenTipTransform;
    styleProps.nibTransform = D2D1::Matrix3x2F(
        tipTx.m11, tipTx.m12 * yScale,
        tipTx.m21, tipTx.m22 * yScale,
        tipTx.m31, tipTx.m32);

    ComPtr<ID2D1InkStyle> inkStyle;
    hr = d2dContext->CreateInkStyle(&styleProps, &inkStyle);
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    auto ptTx = stroke->PointTransform;
    D2D1_MATRIX_3X2_F worldTransform = D2D1::Matrix3x2F(
        ptTx.m11, ptTx.m12,
        ptTx.m21, ptTx.m22,
        ptTx.m31, ptTx.m32);

    ComPtr<SvgPathGeometrySink> sink = Make<SvgPathGeometrySink>();
    hr = d2dInk->StreamAsGeometry(inkStyle.Get(), &worldTransform, 0.25f, sink.Get());
    if (FAILED(hr))
        throw ref new Platform::COMException(hr);

    sink->Close();
    return sink->GetPathData();
}

