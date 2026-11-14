#pragma once

#include <d2d1_3.h>
#include <d2d1svg.h>
#include <wrl.h>

namespace CharacterMapCX
{
    public ref class D2DSvgElement sealed
    {
    public:
        D2DSvgElement^ CreateAndAppendNamedChildElement(Platform::String^ name);

        void SetStringAttribute(Platform::String^ name, Platform::String^ value);
        void SetRectangleAttribute(Platform::String^ name, Windows::Foundation::Rect value);
        void SetColorAttribute(Platform::String^ name, Windows::UI::Color value);
        void SetFloatAttribute(Platform::String^ name, float value);

    internal:
        D2DSvgElement(ID2D1SvgElement* element);
        ID2D1SvgElement* GetElement() { return m_element.Get(); }

    private:
        Microsoft::WRL::ComPtr<ID2D1SvgElement> m_element;
    };

    public ref class D2DSvgDocument sealed
    {
    public:
        D2DSvgDocument();
        D2DSvgDocument(float viewportWidth, float viewportHeight);
        virtual ~D2DSvgDocument();

        property D2DSvgElement^ Root { D2DSvgElement^ get(); }

        Windows::Foundation::IAsyncAction^ SaveAsync(Windows::Storage::Streams::IRandomAccessStream^ stream);

        static Platform::String^ GetInkPathData(Windows::UI::Input::Inking::InkStroke^ stroke);

    internal:
        ID2D1SvgDocument* GetDocument() { return m_svgDocument.Get(); }

    private:
        void Initialize(float viewportWidth, float viewportHeight);

        Microsoft::WRL::ComPtr<ID2D1DeviceContext5> m_d2dContext;
        Microsoft::WRL::ComPtr<ID2D1SvgDocument> m_svgDocument;
    };
}

