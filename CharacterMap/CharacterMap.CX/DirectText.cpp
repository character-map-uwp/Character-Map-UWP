//
// DirectText.cpp
// Implementation of the DirectText class.
//

#pragma once
#include "pch.h"
#include "DWriteFallbackFont.h"
#include "NativeInterop.h"

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
using namespace Microsoft::WRL;
using namespace Microsoft::Graphics::Canvas::UI;
using namespace Microsoft::Graphics::Canvas::UI::Xaml;
using namespace Windows::Graphics;
using namespace Windows::Graphics::DirectX;
using namespace Windows::Graphics::DirectX::Direct3D11;
using namespace Microsoft::Graphics::Canvas::UI::Composition;
using namespace Windows::ApplicationModel;

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

    auto c = ref new DependencyPropertyChangedCallback(this, &DirectText::OnPropChanged);

    this->RegisterPropertyChangedCallback(DirectText::FontFamilyProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::FontSizeProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::ForegroundProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::FlowDirectionProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::RequestedThemeProperty, c);
}

void DirectText::OnPropChanged(DependencyObject^ d, DependencyProperty^ p)
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

    if (DesignMode::DesignModeEnabled)
        return;

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
    if (DesignMode::DesignModeEnabled)
        return size;

    bool hasText = UnicodeIndex > 0 || FontFace != nullptr;

    if (!hasText || Typography == nullptr || m_canvas == nullptr || FontFamily == nullptr || !m_canvas->ReadyToDraw)
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

        auto fontSize = 8 > FontSize ? 8 : FontSize;

        /* CREATE FORMAT */
        ComPtr<IDWriteTextFormat3> idFormat = 
            NativeInterop::_Current->CreateIDWriteTextFormat(
                fontFace,
                FontWeight,
                FontStyle,
                FontStretch,
                fontSize);

        /* Set flow direction */
        if (this->FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
            idFormat->SetReadingDirection(DWRITE_READING_DIRECTION_RIGHT_TO_LEFT);
        else
            idFormat->SetReadingDirection(DWRITE_READING_DIRECTION_LEFT_TO_RIGHT);


        /* Set blank fallback font */
        if (FallbackFont != nullptr)
            idFormat->SetFontFallback(FallbackFont->Fallback.Get());

        /* Set Variable Font Axis */
        if (Axis != nullptr && Axis->Size > 0)
        {
            DWRITE_FONT_AXIS_VALUE* values = new DWRITE_FONT_AXIS_VALUE[Axis->Size];
            for (int i = 0; i < Axis->Size; i++)
            {
                values[i] = Axis->GetAt(i)->GetDWriteValue();
            }
            idFormat->SetFontAxisValues(values, Axis->Size);
        }

        /* Set trimming. Much easier to let Win2D handle this */
        auto format = GetOrCreate<CanvasTextFormat>(idFormat.Get());
        if (IsTextWrappingEnabled)
        {
            idFormat->SetWordWrapping(DWRITE_WORD_WRAPPING::DWRITE_WORD_WRAPPING_CHARACTER);
            format->TrimmingGranularity = CanvasTextTrimmingGranularity::Character;
            format->TrimmingSign = CanvasTrimmingSign::Ellipsis;
        }

        /* Prepare typography */
        auto typography = ref new CanvasTypography();
        if (Typography->Feature != CanvasTypographyFeatureName::None)
            typography->AddFeature(Typography->Feature, 1);

        ComPtr<IDWriteTextLayout4> dlayout;
        /* CREATE LAYOUT */
        /* calculate dimensions */
        auto device = m_canvas->Device;
        float width = IsTextWrappingEnabled ? size.Width : m;
        float height = IsTextWrappingEnabled ? size.Height : m;
        width = min(width, m);
        height = min(height, m);

        /* Create and set properties */
        auto layout = ref new CanvasTextLayout(device, text, format, width, height);
        layout->SetTypography(0, text->Length(), typography);
        if (IsColorFontEnabled && !IsOverwriteCompensationEnabled)
            layout->Options = CanvasDrawTextOptions::EnableColorFont | CanvasDrawTextOptions::Clip;
        else if (IsColorFontEnabled)
            layout->Options = CanvasDrawTextOptions::EnableColorFont;
        else if (!IsCharacterFitEnabled && !IsOverwriteCompensationEnabled)
            layout->Options = CanvasDrawTextOptions::Clip;
        else
            layout->Options = CanvasDrawTextOptions::Default;

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

    if (IsCharacterFitEnabled)
    {
        if (targetsize.Width < size.Width || targetsize.Height < size.Height)
        {
            m_minWidth = FontSize / 2.2;
            auto dHeight = m_layout->DrawBounds.Height;
            auto dWidth = max(m_minWidth, m_layout->DrawBounds.Width);

            auto lHeight = m_layout->LayoutBounds.Height;
            auto lWidth = max(m_minWidth, m_layout->LayoutBounds.Width);

            targetsize = Size(dWidth, dHeight);

            auto scale = min(size.Width / targetsize.Width, size.Height / targetsize.Height);
            if (targetsize.Width == 0 || targetsize.Height == 0)
                scale = 1;

            m_targetScale = scale;
            targetsize = Size(targetsize.Width * scale, targetsize.Height * scale);
        }
    }
    else
    {
        m_targetScale = 1;
    }

    return targetsize;
}

void DirectText::OnDraw(CanvasControl^ sender, CanvasDrawEventArgs^ args)
{
    if (m_layout == nullptr)
        return;

    // Useful for debugging to see which textboxes are DX
    if (Windows::UI::Xaml::Application::Current->DebugSettings->IsTextPerformanceVisualizationEnabled)
        args->DrawingSession->Clear(Windows::UI::Colors::DarkRed);

    auto db = m_layout->DrawBounds;
    auto lb = m_layout->LayoutBounds;
    auto left = -min(db.Left, lb.Left);
    auto top = -min(db.Top, lb.Top);

    if (IsCharacterFitEnabled)
    {
        auto bounds = RectHelper::FromCoordinatesAndDimensions(
            min(db.Left, lb.Left),
            min(db.Top, lb.Top),
            max(db.Width, 0),
            max(db.Height, 0));

        auto ds = this->DesiredSize;
        auto rs = this->RenderSize;
        left = -db.Left;
        top = -db.Top;

        double scale = min(rs.Width / bounds.Width, rs.Height / bounds.Height);
        args->DrawingSession->Transform = Windows::Foundation::Numerics::make_float3x2_scale(scale / 1.0);

        // Horizontally centre glyphs
        if (db.Width < m_minWidth)
            left += (m_minWidth - db.Width) / 2.0; 
    }

    bool drawMetrics = false;
    if (drawMetrics)
    {
        args->DrawingSession->DrawRectangle(left + lb.Left, top + lb.Top, lb.Width, lb.Height, Windows::UI::Colors::DarkGreen);
        args->DrawingSession->DrawRectangle(left + db.Left, top + db.Top, db.Width, db.Height, Windows::UI::Colors::DarkBlue);
        
        auto metrics = this->FontFace->GetMetrics();
        double capRatio = (double)metrics.capHeight / (double)metrics.designUnitsPerEm;
        auto capHeight = FontSize * capRatio;

        auto base = m_layout->LineMetrics[0].Baseline + lb.Top + top;
        auto cap = base - capHeight;
        args->DrawingSession->DrawLine(left + db.Left, base, left + db.Right, base, Windows::UI::Colors::DarkGoldenrod);
        args->DrawingSession->DrawLine(left + db.Left, cap, left + db.Right, cap, Windows::UI::Colors::DarkMagenta);
    }

    if (IsOverwriteCompensationEnabled && !IsCharacterFitEnabled && (m_layout->DrawBounds.Left < 0 || m_layout->DrawBounds.Top < 0))
    {
        auto b = m_layout->DrawBounds.Left;
        auto t = m_layout->DrawBounds.Top;

        m_canvas->Margin = ThicknessHelper::FromLengths(b, t, 0, 0);
        left -= b;
        top -= t;
    }
    else
        m_canvas->Margin = ThicknessHelper::FromUniformLength(0);

    //if (IsOverwriteCompensationEnabled && (m_layout->DrawBounds.Left < 0 || m_layout->DrawBounds.Top < 0))
    //{
    //    auto b = db.Left;
    //    auto t = db.Top;

    //    m_canvas->Margin = ThicknessHelper::FromLengths(b, t, 0, 0);
    //    left -= b;
    //    top -= t;
    //}
    //else if (IsOverwriteCompensationEnabled && m_layout->DrawBounds.Left > 0)
    //{
    //    //m_canvas->Margin = ThicknessHelper::FromLengths(-db.Left, 0, 0, 0);
    //    left += db.Left;
    //}
    //else
        //m_canvas->Margin = ThicknessHelper::FromUniformLength(0);

    if (this->FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
    {
        // Note: something is wrong here causing the right hand side to clip slightly.
        //       currently we use 4 as a magic number to avoid this in 90% of cases.
        //       need to figure out what's up at some point.
        left += m_canvas->ActualWidth - m_layout->DrawBounds.Width - 4;
    }

    auto fam = m_layout->DefaultFontFamily;
    auto fam2 = m_layout->GetFontFamily(0);
    auto loc = m_layout->DefaultLocaleName;
    m_layout->SetLocaleName(0, 1, L"en-us");

    args->DrawingSession->DrawTextLayout(m_layout, float2(left, top), ((SolidColorBrush^)this->Foreground)->Color);

    m_render = false;
}

void DirectText::OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args)
{
    Update();
};
