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

    this->Loaded += ref new Windows::UI::Xaml::RoutedEventHandler(this, &CharacterMapCX::Controls::DirectText::OnLoaded);
    this->Unloaded += ref new Windows::UI::Xaml::RoutedEventHandler(this, &CharacterMapCX::Controls::DirectText::OnUnloaded);
}

void DirectText::OnPropChanged(DependencyObject^ d, DependencyProperty^ p)
{
    DirectText^ c = (DirectText^)d;
    if (!c->BlockUpdates)
        c->Update();
}

void CharacterMapCX::Controls::DirectText::OnLoaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    EnsureCanvas();
}

void CharacterMapCX::Controls::DirectText::OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    DestroyCanvas(m_canvas);
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
   
    EnsureCanvas();
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

    if (m_textLayout == nullptr || m_isStale)
    {
        m_isStale = false;

        if (m_textLayout != nullptr)
            m_textLayout = nullptr;

        auto fontFace = FontFace;

        Platform::String^ text = Text;
        textLength = text->Length();

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
            std::vector<DWRITE_FONT_AXIS_VALUE> values;
            values.reserve(Axis->Size);
            for (unsigned int i = 0; i < Axis->Size; ++i)
            {
                values.push_back(Axis->GetAt(i)->GetDWriteValue());
            }

            ThrowIfFailed(idFormat->SetFontAxisValues(values.data(), static_cast<UINT32>(values.size())));
        }

        /* Set trimming. */
        if (IsTextWrappingEnabled)
        {
            // Define the trimming options
            DWRITE_TRIMMING trimmingOptions = {};
            trimmingOptions.granularity = DWRITE_TRIMMING_GRANULARITY_CHARACTER; // Trim at the character level
            trimmingOptions.delimiter = 0; // No specific delimiter
            trimmingOptions.delimiterCount = 0;

            // Create the ellipsis trimming sign
            ComPtr<IDWriteInlineObject> ellipsisSign;
            HRESULT hr = NativeInterop::_Current->m_dwriteFactory->CreateEllipsisTrimmingSign(idFormat.Get(), &ellipsisSign);
            if (SUCCEEDED(hr))
            {
                // Set the trimming options and ellipsis sign on the text format
                idFormat->SetTrimming(&trimmingOptions, ellipsisSign.Get());
            }
        }


        /* CREATE LAYOUT */
        /* calculate dimensions */
        auto device = m_canvas->Device;
        float lwidth = IsTextWrappingEnabled ? size.Width : m;
        float lheight = IsTextWrappingEnabled ? size.Height : m;
        lwidth = min(lwidth, m);
        lheight = min(lheight, m);

        ComPtr<IDWriteTextLayout> textLayout;
        ThrowIfFailed(
            NativeInterop::_Current->m_dwriteFactory->CreateTextLayout(
                text->Data(),
                textLength,
                idFormat.Get(),
                lwidth,
                lheight,
                &textLayout));

       
		// Assign OpenType features
        if (Typography->Feature != CanvasTypographyFeatureName::None)
        {
			// Create a typography object
            ComPtr<IDWriteTypography> typography;
            ThrowIfFailed(NativeInterop::_Current->m_dwriteFactory->CreateTypography(&typography));

            // Add the feature to the typography object
            DWRITE_FONT_FEATURE f;
            f.nameTag = static_cast<DWRITE_FONT_FEATURE_TAG>(Typography->Feature);
            f.parameter = 1;
            typography->AddFontFeature(f);

			// Set typography on the text layout
            textLayout->SetTypography(typography.Get(), DWRITE_TEXT_RANGE{ 0 , textLength });
		}

        if (IsCharacterFitEnabled)
        {
		    textLayout->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);
			textLayout->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        }

        ComPtr<IDWriteTextLayout4> idl;
        ThrowIfFailed(textLayout.As(&idl));
        if (Axis != nullptr && Axis->Size > 0)
        {
            std::vector<DWRITE_FONT_AXIS_VALUE> values;
            values.reserve(Axis->Size);
            for (unsigned int i = 0; i < Axis->Size; ++i)
            {
                values.push_back(Axis->GetAt(i)->GetDWriteValue());
            }

            ThrowIfFailed(idl->SetFontAxisValues(
                values.data(), 
                static_cast<UINT32>(values.size()),
                DWRITE_TEXT_RANGE{ 0 , textLength }));
        }

        // Calculate LayoutBounds
        DWRITE_TEXT_METRICS1 dwriteMetrics;
        ThrowIfFailed(textLayout->GetMetrics(&dwriteMetrics));
        Rect rect { dwriteMetrics.left, dwriteMetrics.top, dwriteMetrics.width, dwriteMetrics.height };

		// Correct for alternate reading directions
        auto readingDirection = textLayout->GetReadingDirection();
        if (readingDirection == DWRITE_READING_DIRECTION_RIGHT_TO_LEFT)
        {
            const float whitespace = dwriteMetrics.widthIncludingTrailingWhitespace - dwriteMetrics.width;
            rect.X += whitespace;
        }
        else if (readingDirection == DWRITE_READING_DIRECTION_BOTTOM_TO_TOP)
        {
            const float whitespace = dwriteMetrics.heightIncludingTrailingWhitespace - dwriteMetrics.height;
            rect.Y += whitespace;
        }

        layoutBounds = rect;

		// Calculate DrawBounds
        DWRITE_OVERHANG_METRICS overhang;
        ThrowIfFailed(textLayout->GetOverhangMetrics(&overhang));

        const float left = -overhang.left;
        const float right = overhang.right + textLayout->GetMaxWidth();
        const float width = right - left;

        const float top = -overhang.top;
        const float bottom = overhang.bottom + textLayout->GetMaxHeight();
        const float height = bottom - top;

        Rect draw = { left, top, width, height };
        drawBounds = draw;

        /* Create and set properties */
        // TODO: See if we can do this without CanvasTextLayout?
        
        m_textLayout = textLayout;
        m_render = true;

        m_canvas->Invalidate();
    }


    auto minh = min(drawBounds.Top, layoutBounds.Top);
    auto maxh = max(drawBounds.Bottom, layoutBounds.Bottom);

    auto minw = min(drawBounds.Left, layoutBounds.Left);
    auto maxw = max(drawBounds.Right, layoutBounds.Right);

    auto targetsize = Size(min(m, ceil(maxw - minw)), min(m, ceil(maxh - minh)));

    if (IsOverwriteCompensationEnabled && drawBounds.Left < 0)
    {
        targetsize = Size(targetsize.Width - drawBounds.Left, targetsize.Height);
    }

    if (IsCharacterFitEnabled)
    {
        if (targetsize.Width < size.Width || targetsize.Height < size.Height)
        {
            m_minWidth = FontSize / 2.2;
            auto dHeight = drawBounds.Height;
            auto dWidth = max(m_minWidth, drawBounds.Width);

            auto lHeight = layoutBounds.Height;
            auto lWidth = max(m_minWidth, layoutBounds.Width);

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

void CharacterMapCX::Controls::DirectText::EnsureCanvas()
{
    if (m_canvas == nullptr && GetTemplateChild("Root") != nullptr)
    {
        auto root = (Border^)GetTemplateChild("Root");

        if (root->Child != nullptr && static_cast<CanvasControl^>(root->Child) != nullptr)
        {
            // This shouldn't ever get called, but just in case...
            DestroyCanvas(static_cast<CanvasControl^>(root->Child));
        }

        m_canvas = ref new CanvasControl();
        m_canvas->HorizontalAlignment = Windows::UI::Xaml::HorizontalAlignment::Stretch;
        m_canvas->VerticalAlignment = Windows::UI::Xaml::VerticalAlignment::Stretch;
        m_canvas->UseSharedDevice = false;
        root->Child = m_canvas;

        m_drawToken = m_canvas->Draw +=
            ref new TypedEventHandler<CanvasControl^, CanvasDrawEventArgs^>(this, &DirectText::OnDraw);
        m_createToken = m_canvas->CreateResources +=
            ref new TypedEventHandler<CanvasControl^, CanvasCreateResourcesEventArgs^>(this, &DirectText::OnCreateResources);
    }
}

void CharacterMapCX::Controls::DirectText::DestroyCanvas(CanvasControl^ control)
{
    if (control != nullptr)
    {
        auto parent = VisualTreeHelper::GetParent(control);

        control->Draw -= m_drawToken;
        control->CreateResources -= m_createToken;

        control->RemoveFromVisualTree();
        control = nullptr;

        if (parent != nullptr)
        {
            auto b = static_cast<Border^>(parent);
            VisualTreeHelper::DisconnectChildrenRecursive(b);
            b->Child = nullptr;
        }
    }

    m_canvas = nullptr;
}



void DirectText::OnDraw(CanvasControl^ sender, CanvasDrawEventArgs^ args)
{
    if (m_textLayout == nullptr)
        return;

    // Useful for debugging to see which textboxes are DX
  /*  if (Windows::UI::Xaml::Application::Current->DebugSettings->IsTextPerformanceVisualizationEnabled)
        args->DrawingSession->Clear(Windows::UI::Colors::DarkRed);*/

    auto db = drawBounds;
    auto lb = layoutBounds;
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
        
        // Fix later - removed Win2D TextLayout
        // 
        //auto metrics = this->FontFace->GetMetrics();
        //double capRatio = (double)metrics.capHeight / (double)metrics.designUnitsPerEm;
        //auto capHeight = FontSize * capRatio;

        //auto base = m_layout->LineMetrics[0].Baseline + lb.Top + top;
        //auto cap = base - capHeight;
        //args->DrawingSession->DrawLine(left + db.Left, base, left + db.Right, base, Windows::UI::Colors::DarkGoldenrod);
        //args->DrawingSession->DrawLine(left + db.Left, cap, left + db.Right, cap, Windows::UI::Colors::DarkMagenta);

        //// --- Draw a green line along the baseline ---
        //if (m_layout->LineMetrics != nullptr && m_layout->LineMetrics->Length > 0)
        //{
        //    // Baseline is relative to the layout bounds
        //    float baseline = m_layout->LineMetrics[0].Baseline + lb.Top + top;
        //    args->DrawingSession->DrawLine(
        //        left + db.Left, baseline,
        //        left + db.Right, baseline,
        //        Windows::UI::Colors::HotPink
        //    );
        //}
    }

   

    if (IsOverwriteCompensationEnabled && !IsCharacterFitEnabled && (db.Left < 0 || db.Top < 0))
    {
        auto b = db.Left;
        auto t = db.Top;

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

        // NB: Win2D Sample gallery actually has a note on this, remember to look
        // at it sometime
        left += m_canvas->ActualWidth - db.Width - 4; 
    }

   /* auto fam = m_layout->DefaultFontFamily;
    auto fam2 = m_layout->GetFontFamily(0);
    auto loc = m_layout->DefaultLocaleName;*/
    m_textLayout->SetLocaleName(L"en-us", { 0,  textLength });

    // Make sure we have a colour brush
    ComPtr<ID2D1DeviceContext1> ctx = GetWrappedResource<ID2D1DeviceContext1>(args->DrawingSession);
    if (m_brush == nullptr)
    {
		ctx->CreateSolidColorBrush(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color), &m_brush);
    }
    else
    {
        m_brush->SetColor(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color));
	}

    D2D1_DRAW_TEXT_OPTIONS ops = D2D1_DRAW_TEXT_OPTIONS::D2D1_DRAW_TEXT_OPTIONS_NONE;
    if (IsColorFontEnabled)
        ops = D2D1_DRAW_TEXT_OPTIONS::D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT;

    // Draw it
    ctx->DrawTextLayout(
        { left, top },
        m_textLayout.Get(),
        m_brush.Get(),
        ops
    );

    m_render = false;
}

void DirectText::OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args)
{
    Update();
};
