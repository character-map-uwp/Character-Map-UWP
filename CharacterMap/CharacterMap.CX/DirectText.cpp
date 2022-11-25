//
// DirectText.cpp
// Implementation of the DirectText class.
//

#include "pch.h"
#include "DWriteFallbackFont.h"

using namespace CharacterMapCX;
using namespace CharacterMapCX::Controls;

using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Documents;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Interop;
using namespace Windows::UI::Xaml::Media;
using namespace Microsoft::Graphics::Canvas::UI;
using namespace Microsoft::Graphics::Canvas::UI::Xaml;
using namespace Windows::Graphics;
using namespace Windows::Graphics::DirectX;
using namespace Windows::Graphics::DirectX::Direct3D11;
using namespace Microsoft::Graphics::Canvas::UI::Composition;

DependencyProperty^ DirectText::_FallbackFontProperty = nullptr;
DependencyProperty^ DirectText::_IsColorFontEnabledProperty = nullptr;
DependencyProperty^ DirectText::_IsOverwriteCompensationEnabledProperty = nullptr;
DependencyProperty^ DirectText::_AxisProperty = nullptr;
DependencyProperty^ DirectText::_UnicodeIndexProperty = nullptr;
DependencyProperty^ DirectText::_TextProperty = nullptr;
DependencyProperty^ DirectText::_FontFaceProperty = nullptr;
DependencyProperty^ DirectText::_TypographyProperty = nullptr;
DependencyProperty^ DirectText::_IsTextWrappingEnabledProperty = nullptr;
DependencyProperty^ DirectText::_IsCharacterFitEnabledProperty = nullptr;

DirectText::DirectText()
{
	DefaultStyleKey = "CharacterMapCX.Controls.DirectText";
    m_isStale = true;

    auto c = ref new DependencyPropertyChangedCallback(this, &DirectText::OnFontSizeChanged);
    this->RegisterPropertyChangedCallback(DirectText::FontSizeProperty, c);
}

void DirectText::OnFontSizeChanged(DependencyObject^ d, DependencyProperty^ p)
{
    DirectText^ c = (DirectText^)d;
    c->Update();
}

void DirectText::OnApplyTemplate()
{
   /* if (gd == nullptr)
    {
        dpi = Display::DisplayInformation::GetForCurrentView()->LogicalDpi;
        auto device = CanvasDevice::GetSharedDevice();
        auto v = Windows::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(this);
        gd = CanvasComposition::CreateCompositionGraphicsDevice(
            v->Compositor, device);
            
        auto size = SizeInt32();
        size.Width = 2;
        size.Height = 2;
        surface = gd->CreateDrawingSurface2(
            size,
            DirectXPixelFormat::B8G8R8A8UIntNormalized,
            DirectXAlphaMode::Premultiplied);
    }*/

    if (m_canvas == nullptr)
    {
        m_canvas = (CanvasControl^)GetTemplateChild("TextCanvas");
        if (m_canvas != nullptr)
        {
            m_drawToken = m_canvas->Draw +=
                ref new TypedEventHandler<CanvasControl^, CanvasDrawEventArgs^>(this, &DirectText::OnDraw);
            m_canvas->CreateResources +=
                ref new TypedEventHandler<CanvasControl^, CanvasCreateResourcesEventArgs^>(this, &DirectText::OnCreateResources);
        }
    }

    Update();
}

Windows::Foundation::Size CharacterMapCX::Controls::DirectText::MeasureOverride(Windows::Foundation::Size size)
{
    bool hasText = UnicodeIndex > 0 || FontFace != nullptr;

    if (!hasText || Typography == nullptr || m_canvas == nullptr || !m_canvas->ReadyToDraw)
        return Size(this->MinWidth, this->MinHeight);

    auto dpi = m_canvas->Dpi / 96.0f;
    auto m = m_canvas->Device->MaximumBitmapSizeInPixels / dpi;

    m_canvas->Measure(size);

    if (m_layout == nullptr || m_isStale)
    {
        m_isStale = false;

        if (m_layout != nullptr)
            delete m_layout;

        auto fontFace = FontFace;

        Platform::String^ text = Text;

        /* CREATE FORMAT */
        auto format = ref new CanvasTextFormat();
        format->FontFamily = FontFamily->Source;
        format->FontSize = FontSize;
        format->FontWeight = FontWeight;
        format->FontStyle = FontStyle;
        format->FontStretch = FontStretch;

        if (IsColorFontEnabled && !IsOverwriteCompensationEnabled)
            format->Options = CanvasDrawTextOptions::EnableColorFont | CanvasDrawTextOptions::Clip;
        else if (IsColorFontEnabled)
            format->Options = CanvasDrawTextOptions::EnableColorFont;
        else if (!IsCharacterFitEnabled)
            format->Options = CanvasDrawTextOptions::Clip;

        if (IsTextWrappingEnabled)
        {
            format->WordWrapping = CanvasWordWrapping::Character;
            format->TrimmingGranularity = CanvasTextTrimmingGranularity::Character;
            format->TrimmingSign = CanvasTrimmingSign::Ellipsis;
        }

        /* Set blank fallback font */
        ComPtr<IDWriteTextFormat3> dformat = GetWrappedResource<IDWriteTextFormat3>(format);
        if (FallbackFont != nullptr)
            dformat->SetFontFallback(FallbackFont->Fallback.Get());

        /* Set Variable Font Axis */
        if (Axis != nullptr && Axis->Size > 0)
        {
            DWRITE_FONT_AXIS_VALUE* values = new DWRITE_FONT_AXIS_VALUE[Axis->Size];
            for (int i = 0; i < Axis->Size; i++)
            {
                values[i] = Axis->GetAt(i)->GetDWriteValue();
            }
            dformat->SetFontAxisValues(values, Axis->Size);
        }

        dformat = nullptr;
        ComPtr<IDWriteTextLayout4> dlayout;

        /* CREATE LAYOUT */
        auto typography = ref new CanvasTypography();
        if (Typography->Feature != CanvasTypographyFeatureName::None)
            typography->AddFeature(Typography->Feature, 1);


        auto device = m_canvas->Device;

        float width = IsTextWrappingEnabled ? size.Width : m;
        float height = IsTextWrappingEnabled ? size.Height : m;
        width = min(width, m);
        height = min(height, m);

        auto layout = ref new CanvasTextLayout(device, text, format, width, height);
        layout->SetTypography(0, text->Length(), typography);
        if (IsColorFontEnabled && !IsOverwriteCompensationEnabled)
            layout->Options = CanvasDrawTextOptions::EnableColorFont | CanvasDrawTextOptions::Clip;
        else if (IsColorFontEnabled)
            layout->Options = CanvasDrawTextOptions::EnableColorFont;
        else if (!IsCharacterFitEnabled)
            layout->Options = CanvasDrawTextOptions::Clip;

        if (IsCharacterFitEnabled)
        {
            layout->VerticalAlignment = CanvasVerticalAlignment::Top;
            layout->HorizontalAlignment = CanvasHorizontalAlignment::Center;
        }

        m_layout = layout;
        m_render = true;
        
        m_canvas->Invalidate();
        delete format;
    }

    auto minh = min(m_layout->DrawBounds.Top, m_layout->LayoutBounds.Top);
    auto maxh = max(m_layout->DrawBounds.Bottom, m_layout->LayoutBounds.Bottom);

    auto minw = min(m_layout->DrawBounds.Left, m_layout->LayoutBounds.Left);
    auto maxw = max(m_layout->DrawBounds.Right, m_layout->LayoutBounds.Right);

    auto targetsize = Size(min(m, ceil(maxw - minw)), min(m, ceil(maxh - minh)));

    if (IsOverwriteCompensationEnabled && m_layout->DrawBounds.Left < 0)
    {
        targetsize = Size(targetsize.Width - m_layout->DrawBounds.Left, targetsize.Height);
    }

    return targetsize;
}

void DirectText::OnDraw(CanvasControl^ sender, CanvasDrawEventArgs^ args)
{
    if (m_layout == nullptr)
        return;

    // Useful for debugging to see which textboxes are DX
    //args->DrawingSession->Clear(Windows::UI::Colors::DarkRed);

    auto t = m_layout->DrawBounds.Top;
    auto dt = m_layout->LayoutBounds.Top;

    auto left = -min(m_layout->DrawBounds.Left, m_layout->LayoutBounds.Left);

    if (IsCharacterFitEnabled)
    {
        auto hw = m_layout->DrawBounds.Width / 2.;
        auto cent = this->RenderSize.Width / 2.;
        left = cent - hw + left;

        if (m_layout->DrawBounds.Bottom > this->RenderSize.Height || m_layout->DrawBounds.Width > this->RenderSize.Width)
        {
            double destWidth = this->RenderSize.Width;
            double destHeight = this->RenderSize.Height;

            double scale = min(destWidth / (double)m_layout->DrawBounds.Width, destHeight / (double)m_layout->DrawBounds.Bottom);

            args->DrawingSession->Transform = Windows::Foundation::Numerics::make_float3x2_scale(scale / 1.);
        }
    }

    if (IsOverwriteCompensationEnabled && m_layout->DrawBounds.Left < 0)
    {
        auto b = m_layout->DrawBounds.Left;
        auto c = m_layout->LayoutBounds.Left;
        m_canvas->Margin = ThicknessHelper::FromLengths(b, 0, 0, 0);
        left -= b;
    }
    else
        m_canvas->Margin = ThicknessHelper::FromUniformLength(0);

    //args->DrawingSession->Clear(Windows::UI::Colors::Red);
    args->DrawingSession->DrawTextLayout(m_layout, float2(left, 0), ((SolidColorBrush^)this->Foreground)->Color);

    m_render = false;
}

void DirectText::OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args)
{
    Update();
};
