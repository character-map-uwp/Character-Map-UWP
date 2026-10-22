//
// DirectText.cpp
// Implementation of the DirectText class.
//

#pragma once
#include "pch.h"
#include "DWriteFallbackFont.h"
#include "NativeInterop.h"
#include "CoreDWTextServices.h"
#include <cfloat>

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
DependencyProperty^ DirectText::_ColorRenderOptionProperty = nullptr;
DependencyProperty^ DirectText::_IsColorFontEnabledProperty = nullptr;
DependencyProperty^ DirectText::_IsOverwriteCompensationEnabledProperty = nullptr;
DependencyProperty^ DirectText::_AxisProperty = nullptr;
DependencyProperty^ DirectText::_UnicodeIndexProperty = nullptr;
DependencyProperty^ DirectText::_GlyphIndexProperty = nullptr;
DependencyProperty^ DirectText::_TextProperty = nullptr;
DependencyProperty^ DirectText::_FontFaceProperty = nullptr;
DependencyProperty^ DirectText::_TypographyProperty = nullptr;
DependencyProperty^ DirectText::_IsTextWrappingEnabledProperty = nullptr;
DependencyProperty^ DirectText::_IsCharacterFitEnabledProperty = nullptr;
DependencyProperty^ DirectText::_StyleSimulationsProperty = nullptr;

DirectText::DirectText()
{
    RegisterDependencyProperties();

	DefaultStyleKey = "CharacterMapCX.Controls.DirectText";
    m_isStale = true;
    m_isAxisOnlyStale = false;
    m_lastFamilyName = nullptr;
    m_lastFaceName = nullptr;
    m_lastText = nullptr;
    m_lastFontSize = 0.0;

    auto weakThis = Platform::WeakReference(this);

    auto c = ref new DependencyPropertyChangedCallback([weakThis](DependencyObject^ d, DependencyProperty^ p) {
        if (auto pThis = weakThis.Resolve<DirectText>())
            pThis->OnPropChanged(d, p);
    });

    this->RegisterPropertyChangedCallback(DirectText::FontSizeProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::ForegroundProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::FlowDirectionProperty, c);
    this->RegisterPropertyChangedCallback(DirectText::RequestedThemeProperty, c);

    this->Loaded += ref new Windows::UI::Xaml::RoutedEventHandler([weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
        if (auto pThis = weakThis.Resolve<DirectText>())
            pThis->OnLoaded(sender, e);
    });

    this->Unloaded += ref new Windows::UI::Xaml::RoutedEventHandler([weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
        if (auto pThis = weakThis.Resolve<DirectText>())
            pThis->OnUnloaded(sender, e);
    });
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

    try
    {
        bool hasText = FontFace != nullptr && (GlyphIndex >= 0 || (Text != nullptr && Text->Length() > 0));

        if (!hasText || Typography == nullptr || m_canvas == nullptr || !m_canvas->ReadyToDraw)
        {
            if (!hasText)
            {
                m_textLayout = nullptr;
                m_drawFontFace = nullptr;
                m_render = false;
            }
            return Size(this->MinWidth, this->MinHeight);
        }

    auto dpi = m_canvas->Dpi / 96.0f;
    auto m = m_canvas->Device->MaximumBitmapSizeInPixels / dpi;

    m_canvas->Measure(size);

    bool needsRebuild = m_isStale || (GlyphIndex >= 0 ? (m_drawFontFace == nullptr) : (m_textLayout == nullptr));

    // Fast path: in CharacterFit mode, if only the axis values changed (same family/face/text/size/weights),
    // reuse the existing IDWriteTextLayout rather than rebuilding from scratch.
    // Just update axis values and recompute ink bounds.
    if (needsRebuild && IsCharacterFitEnabled && GlyphIndex < 0 && m_textLayout != nullptr && FontFace != nullptr)
    {
        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;
        auto simulations = StyleSimulations;
        if (simulations == Windows::UI::Xaml::Media::StyleSimulations::None && fontFace != nullptr && fontFace->Properties != nullptr)
        {
            simulations = fontFace->Properties->Simulations;
        }

        Platform::String^ currentFamily = fontFace->Properties != nullptr ? fontFace->Properties->FamilyName : nullptr;
        Platform::String^ currentFaceName = fontFace->Properties != nullptr ? fontFace->Properties->FaceName : nullptr;
        Platform::String^ currentText = Text;

        bool sameFamily = (m_lastFamilyName == currentFamily) ||
            (m_lastFamilyName != nullptr && currentFamily != nullptr &&
             wcscmp(m_lastFamilyName->Data(), currentFamily->Data()) == 0);
        bool sameFaceName = (m_lastFaceName == currentFaceName) ||
            (m_lastFaceName != nullptr && currentFaceName != nullptr &&
             wcscmp(m_lastFaceName->Data(), currentFaceName->Data()) == 0);
        bool sameFontFace = (m_lastFontFace == fontFace);
        bool sameTypography = (m_lastTypography == Typography);
        bool sameText = (m_lastText == currentText) ||
            (m_lastText != nullptr && currentText != nullptr &&
             wcscmp(m_lastText->Data(), currentText->Data()) == 0);
        bool sameSize = (m_lastFontSize == fontSize);
        bool sameWeight = (m_lastFontWeight == FontWeight.Weight);
        bool sameStyle = (m_lastFontStyle == FontStyle);
        bool sameStretch = (m_lastFontStretch == FontStretch);
        bool sameSimulations = (m_lastSimulations == simulations);

        if (sameFontFace && sameTypography && sameFamily && sameFaceName && sameText && sameSize && sameWeight && sameStyle && sameStretch && sameSimulations)
        {
            // Build current axis values
            std::vector<DWRITE_FONT_AXIS_VALUE> newAxisValues;
            if (Axis != nullptr && Axis->Size > 0)
            {
                newAxisValues.reserve(Axis->Size);
                for (unsigned int i = 0; i < Axis->Size; ++i)
                    newAxisValues.push_back(Axis->GetAt(i)->GetDWriteValue());
            }
            else if (fontFace != nullptr && !fontFace->GetAxisValues().empty())
                newAxisValues = fontFace->GetAxisValues();

            // Fast path is only valid when there are variable font axis values to update
            if (!newAxisValues.empty())
            {
                // Check if axis values actually changed
                bool axisChanged = (newAxisValues.size() != m_lastAxisValues.size());
                if (!axisChanged)
                {
                    for (size_t i = 0; i < newAxisValues.size() && !axisChanged; ++i)
                    {
                        if (newAxisValues[i].axisTag != m_lastAxisValues[i].axisTag ||
                            newAxisValues[i].value != m_lastAxisValues[i].value)
                            axisChanged = true;
                    }
                }

                if (axisChanged)
                {
                    // The new fontFace already encapsulates the variant axis, so use its internal face for drawing
                    m_drawFontFace = fontFace->GetFontFace();

                // Update axis values on the existing layout
                CoreDWTextServices::UpdateAxisValues(m_textLayout.Get(), newAxisValues, textLength);
                m_lastAxisValues = newAxisValues;
            }

            // Recompute drawBounds from outline (axis may have changed ink extent)
            CoreDWTextServices::MeasureInkBounds(m_textLayout.Get(), m_drawFontFace.Get(), &drawBounds);

            // Skip full rebuild — just return the updated target size
            auto db = drawBounds;
            bool hasConstrainedW = !std::isinf(size.Width);
            bool hasConstrainedH = !std::isinf(size.Height);
            
            needsRebuild = false; // Fast path successful
            m_isStale = false; // Prevent full rebuild on next pass
            
            if (hasConstrainedW || hasConstrainedH)
            {
                m_minWidth = FontSize / 2.2;
                auto dHeight = max(1.0, (double)db.Height);
                auto dWidth  = max(m_minWidth, (double)db.Width);
                double scaleW = hasConstrainedW ? (size.Width / dWidth) : 1.0;
                double scaleH = hasConstrainedH ? (size.Height / dHeight) : 1.0;
                auto scale = (hasConstrainedW && hasConstrainedH) ? min(scaleW, scaleH) : (hasConstrainedW ? scaleW : scaleH);
                if (scale <= 0) scale = 1;
                m_targetScale = scale;
                m_render = true;
                return Size(static_cast<float>(dWidth * scale), static_cast<float>(dHeight * scale));
            }
            m_render = true;
            return Size(static_cast<float>(ceil(db.Width)), static_cast<float>(ceil(db.Height)));
        }
    }
}

    if (needsRebuild)
    {
        m_isStale = false;
        m_drawFontFace = nullptr;

        if (m_textLayout != nullptr)
        {
            m_textLayout = nullptr;
            m_lastFamilyName = nullptr;  // invalidate axis-only fast path cache
            m_lastFaceName = nullptr;
            m_lastFontFace = nullptr;
            m_lastTypography = nullptr;
            m_lastText = nullptr;
            m_lastAxisValues.clear();
        }

        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;

        auto simulations = StyleSimulations;
        if (simulations == Windows::UI::Xaml::Media::StyleSimulations::None && fontFace != nullptr && fontFace->Properties != nullptr)
        {
            simulations = fontFace->Properties->Simulations;
        }

        // Resolve font face with axis values (for variable fonts)
        ComPtr<IDWriteFontFaceReference> faceRef = fontFace->GetReference();
        ComPtr<IDWriteFontFace3> dwriteFontFace;

        DWRITE_FONT_SIMULATIONS dwriteSimulations = ToDWriteFontSimulations(simulations);

        if (Axis != nullptr && Axis->Size > 0)
        {
            ComPtr<IDWriteFontFaceReference1> faceRef1;
            if (faceRef != nullptr && SUCCEEDED(faceRef.As(&faceRef1)))
            {
                ComPtr<IDWriteFontFace5> face5;
                if (SUCCEEDED(faceRef1->CreateFontFace(&face5)))
                {
                    ComPtr<IDWriteFontResource> fontResource;
                    if (SUCCEEDED(face5->GetFontResource(&fontResource)))
                    {
                        std::vector<DWRITE_FONT_AXIS_VALUE> values;
                        values.reserve(Axis->Size);
                        for (unsigned int i = 0; i < Axis->Size; ++i)
                            values.push_back(Axis->GetAt(i)->GetDWriteValue());

                        ComPtr<IDWriteFontFace5> face5_var;
                        if (SUCCEEDED(fontResource->CreateFontFace(dwriteSimulations, values.data(), static_cast<UINT32>(values.size()), &face5_var)))
                        {
                            dwriteFontFace = face5_var;
                        }
                    }
                }
            }
        }

        if (dwriteFontFace == nullptr && fontFace != nullptr)
        {
            if (dwriteSimulations != DWRITE_FONT_SIMULATIONS_NONE && faceRef != nullptr)
            {
                ComPtr<IDWriteFontFace3> simFace;
                if (SUCCEEDED(faceRef->CreateFontFaceWithSimulations(dwriteSimulations, &simFace)))
                    dwriteFontFace = simFace;
            }

            if (dwriteFontFace == nullptr)
                dwriteFontFace = fontFace->GetFontFace();
        }

        m_drawFontFace = dwriteFontFace;

        if (GlyphIndex >= 0)
        {
            if (dwriteFontFace == nullptr || fontFace == nullptr)
                return Size(this->MinWidth, this->MinHeight);

            UINT16 gIndex = static_cast<UINT16>(GlyphIndex);
            DWRITE_GLYPH_METRICS glyphMetrics{};
            if (FAILED(dwriteFontFace->GetDesignGlyphMetrics(&gIndex, 1, &glyphMetrics, FALSE)))
                return Size(this->MinWidth, this->MinHeight);

            DWRITE_FONT_METRICS1 fontMetrics = fontFace->GetMetrics();
            if (fontMetrics.designUnitsPerEm == 0)
                return Size(this->MinWidth, this->MinHeight);
            double scale = fontSize / fontMetrics.designUnitsPerEm;

            // Compute logical layout bounds
            double advanceWidth = glyphMetrics.advanceWidth * scale;
            double ascent = fontMetrics.ascent * scale;
            double descent = fontMetrics.descent * scale;
            layoutBounds = Rect(0, -ascent, advanceWidth, ascent + descent);

            // Compute visual bounds
            DWRITE_GLYPH_RUN glyphRun{};
            glyphRun.fontFace = dwriteFontFace.Get();
            glyphRun.fontEmSize = static_cast<FLOAT>(fontSize);
            glyphRun.glyphCount = 1;
            glyphRun.glyphIndices = &gIndex;
            FLOAT advanceWidthF = static_cast<FLOAT>(advanceWidth);
            glyphRun.glyphAdvances = &advanceWidthF;
            glyphRun.glyphOffsets = nullptr;
            glyphRun.isSideways = FALSE;
            glyphRun.bidiLevel = 0;

            ComPtr<ID2D1PathGeometry> pathGeometry;
            if (NativeInterop::_Current == nullptr || NativeInterop::_Current->m_d2dFactory == nullptr ||
                FAILED(NativeInterop::_Current->m_d2dFactory->CreatePathGeometry(&pathGeometry)))
            {
                drawBounds = layoutBounds;
            }
            else
            {
                ComPtr<ID2D1GeometrySink> sink;
                if (SUCCEEDED(pathGeometry->Open(&sink)))
                {
                    if (SUCCEEDED(dwriteFontFace->GetGlyphRunOutline(
                        static_cast<FLOAT>(fontSize),
                        &gIndex,
                        &advanceWidthF,
                        nullptr,
                        1,
                        FALSE,
                        FALSE,
                        sink.Get()
                    )) && SUCCEEDED(sink->Close()))
                    {
                        D2D1_RECT_F bounds;
                        if (SUCCEEDED(pathGeometry->GetBounds(nullptr, &bounds)) &&
                            bounds.left <= bounds.right && bounds.top <= bounds.bottom &&
                            (bounds.right - bounds.left > 0) && (bounds.bottom - bounds.top > 0))
                        {
                            drawBounds = Rect(
                                bounds.left,
                                bounds.top,
                                bounds.right - bounds.left,
                                bounds.bottom - bounds.top
                            );
                        }
                        else
                        {
                            drawBounds = layoutBounds;
                        }
                    }
                    else
                    {
                        sink->Close();
                        drawBounds = layoutBounds;
                    }
                }
                else
                {
                    drawBounds = layoutBounds;
                }
            }

            m_render = true;
        }
        else
        {
            // Standard TextLayout path
            Platform::String^ text = Text;
            textLength = text != nullptr ? text->Length() : 0;
            if (textLength == 0)
            {
                m_textLayout = nullptr;
                return Size(this->MinWidth, this->MinHeight);
            }

            float lwidth = IsTextWrappingEnabled ? size.Width : m;
            float lheight = IsTextWrappingEnabled ? size.Height : m;
            lwidth = min(lwidth, m);
            lheight = min(lheight, m);

            TextLayoutParameters params;
            params.Text = text;
            params.FontFace = fontFace;
            params.FontSize = static_cast<float>(fontSize);
            params.FontWeight = FontWeight;
            params.FontStyle = FontStyle;
            params.FontStretch = FontStretch;
            params.Simulations = simulations;
            params.Axis = Axis;
            params.TypographyInfo = Typography;
            params.FallbackFont = FallbackFont;
            params.FlowDirection = this->FlowDirection;
            params.WordWrapping = IsTextWrappingEnabled;
            params.EnableTrimming = IsTextWrappingEnabled;
            params.IsCharacterFit = IsCharacterFitEnabled;
            params.MaxWidth = lwidth;
            params.MaxHeight = lheight;

            ComPtr<IDWriteTextLayout> textLayout;
            HRESULT hr = CoreDWTextServices::CreateLayout(params, &textLayout, &m_drawFontFace);
            if (FAILED(hr) || textLayout == nullptr)
            {
                m_textLayout = nullptr;
                return Size(this->MinWidth, this->MinHeight);
            }

            // Calculate LayoutBounds
            hr = CoreDWTextServices::MeasureLayoutBounds(textLayout.Get(), &layoutBounds);
            if (FAILED(hr))
            {
                m_textLayout = nullptr;
                return Size(this->MinWidth, this->MinHeight);
            }

            // Calculate DrawBounds
            if (IsCharacterFitEnabled)
            {
                hr = CoreDWTextServices::MeasureInkBounds(textLayout.Get(), m_drawFontFace.Get(), &drawBounds);
                if (FAILED(hr))
                    drawBounds = layoutBounds;
            }
            else
            {
                drawBounds = layoutBounds;
            }

            m_textLayout = textLayout;
            m_render = true;

            // Cache layout key so the axis-only fast path can detect future axis-only updates
            if (IsCharacterFitEnabled)
            {
                m_lastFamilyName = fontFace->Properties != nullptr ? fontFace->Properties->FamilyName : nullptr;
                m_lastFaceName = fontFace->Properties != nullptr ? fontFace->Properties->FaceName : nullptr;
                m_lastFontFace = fontFace;
                m_lastTypography = Typography;
                m_lastText = text;
                m_lastFontSize = fontSize;
                m_lastFontWeight = FontWeight.Weight;
                m_lastFontStyle = FontStyle;
                m_lastFontStretch = FontStretch;
                m_lastSimulations = simulations;
                if (Axis != nullptr && Axis->Size > 0)
                {
                    m_lastAxisValues.clear();
                    m_lastAxisValues.reserve(Axis->Size);
                    for (unsigned int i = 0; i < Axis->Size; ++i)
                        m_lastAxisValues.push_back(Axis->GetAt(i)->GetDWriteValue());
                }
                else if (fontFace != nullptr && !fontFace->GetAxisValues().empty())
                    m_lastAxisValues = fontFace->GetAxisValues();
                else
                    m_lastAxisValues.clear();
            }
        }
    }


    auto minh = min(drawBounds.Top, layoutBounds.Top);
    auto maxh = max(drawBounds.Bottom, layoutBounds.Bottom);

    auto minw = min(drawBounds.Left, layoutBounds.Left);
    auto maxw = max(drawBounds.Right, layoutBounds.Right);

    double h = maxh - minh;
    double w = maxw - minw;
    if (h <= 0) h = layoutBounds.Height > 0 ? layoutBounds.Height : 1.0;
    if (w <= 0) w = layoutBounds.Width > 0 ? layoutBounds.Width : 1.0;

    auto targetsize = Size(min(m, ceil(w)), min(m, ceil(h)));

    if (IsCharacterFitEnabled)
    {
        bool hasConstrainedW = !std::isinf(size.Width);
        bool hasConstrainedH = !std::isinf(size.Height);

        if (hasConstrainedW || hasConstrainedH)
        {
            m_minWidth = FontSize / 2.2;
            auto totalW = (std::max)({ m_minWidth, static_cast<double>(layoutBounds.Width), static_cast<double>(drawBounds.Width), static_cast<double>(drawBounds.Right - (std::min)(0.0f, drawBounds.Left)) });
            auto totalTop = (std::min)(static_cast<double>(layoutBounds.Top), static_cast<double>(drawBounds.Top));
            auto totalBottom = (std::max)(static_cast<double>(layoutBounds.Bottom), static_cast<double>(drawBounds.Bottom));
            auto totalH = (std::max)(1.0, totalBottom - totalTop);

            double scaleW = hasConstrainedW ? (size.Width / totalW) : 1.0;
            double scaleH = hasConstrainedH ? (size.Height / totalH) : 1.0;
            auto scale = (hasConstrainedW && hasConstrainedH) ? (std::min)(scaleW, scaleH) : (hasConstrainedW ? scaleW : scaleH);
            if (scale <= 0)
                scale = 1;

            m_targetScale = scale;
            targetsize = Size(static_cast<float>(totalW * scale), static_cast<float>(totalH * scale));
        }
    }
    else
    {
        m_targetScale = 1;
    }

        return targetsize;
    }
    catch (...)
    {
        m_textLayout = nullptr;
        m_drawFontFace = nullptr;
        m_render = false;
        return Size(this->MinWidth, this->MinHeight);
    }
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

        auto weakThis = Platform::WeakReference(this);

        m_drawToken = m_canvas->Draw +=
            ref new TypedEventHandler<CanvasControl^, CanvasDrawEventArgs^>([weakThis](CanvasControl^ sender, CanvasDrawEventArgs^ args) {
                if (auto pThis = weakThis.Resolve<DirectText>())
                    pThis->OnDraw(sender, args);
            });
            
        m_createToken = m_canvas->CreateResources +=
            ref new TypedEventHandler<CanvasControl^, CanvasCreateResourcesEventArgs^>([weakThis](CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args) {
                if (auto pThis = weakThis.Resolve<DirectText>())
                    pThis->OnCreateResources(sender, args);
            });
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
    if (m_textLayout == nullptr && GlyphIndex < 0)
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
        auto rs = this->RenderSize;
        if (rs.Width <= 0 || rs.Height <= 0)
            return;

        double padX = (std::max)(6.0, rs.Width * 0.04);
        double padY = (std::max)(6.0, rs.Height * 0.04);
        double availW = (std::max)(1.0, rs.Width - padX * 2.0);
        double availH = (std::max)(1.0, rs.Height - padY * 2.0);

        double totalW = (std::max)({ m_minWidth, static_cast<double>(lb.Width), static_cast<double>(db.Width), static_cast<double>(db.Right - (std::min)(0.0f, db.Left)) });
        double totalTop = (std::min)(static_cast<double>(lb.Top), static_cast<double>(db.Top));
        double totalBottom = (std::max)(static_cast<double>(lb.Bottom), static_cast<double>(db.Bottom));
        double totalH = (std::max)(1.0, totalBottom - totalTop);

        // Glyphs scale to fit available size (both scaling up and down) while maintaining baseline positioning
        double scale = (std::min)(availW / totalW, availH / totalH);
        if (scale <= 0.0)
            scale = 1.0;

        args->DrawingSession->Transform = Windows::Foundation::Numerics::make_float3x2_scale(static_cast<float>(scale));

        double boxW = rs.Width / scale;
        double boxH = rs.Height / scale;

        // Horizontal positioning:
        // Center within advance width for regular characters.
        // Center ink bounds for zero-advance characters (combining marks / diacritics).
        if (lb.Width > 0)
        {
            left = static_cast<float>((boxW - lb.Width) / 2.0 - lb.Left);
            if (left + db.Left < 0)
                left = -db.Left;
            if (left + db.Right > boxW)
                left = static_cast<float>(boxW - db.Right);
        }
        else
        {
            left = static_cast<float>(-db.Left + (boxW - db.Width) / 2.0);
        }

        // Vertical positioning:
        // Center the font line box so all characters share the exact same baseline.
        // For textLayout (lb.Top == 0), top is line box top: (boxH - lb.Height) / 2.0.
        // For GlyphRun (lb.Top == -ascent), top is baseline origin: (boxH - lb.Height) / 2.0 + ascent.
        // Both equal (boxH - lb.Height) / 2.0 - lb.Top.
        top = static_cast<float>((boxH - lb.Height) / 2.0 - lb.Top);

        // Prevent clipping if extreme ascenders/descenders extend outside box
        if (top + db.Top < 0)
            top = -db.Top;
        if (top + db.Bottom > boxH)
            top = static_cast<float>(boxH - db.Bottom);
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

    if (!IsCharacterFitEnabled && this->FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
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

	bool color = ColorRenderOption != DWriteColorRenderOption::Monochrome;
    GlyphImageFormat preferredFormat = CoreDWTextServices::ToPreferredFormat(color, ColorRenderOption);

    ComPtr<ID2D1DeviceContext> ctx = GetWrappedResource<ID2D1DeviceContext>(args->DrawingSession);
    if (m_brush == nullptr)
    {
        ctx->CreateSolidColorBrush(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color), &m_brush);
    }
    else
    {
        m_brush->SetColor(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color));
    }

    TextRenderParameters renderParams;
    renderParams.ForegroundBrush = m_brush.Get();
    renderParams.EnableColor = color;
    renderParams.PreferredFormat = preferredFormat;
    renderParams.OverrideFace = m_drawFontFace.Get();

    if (GlyphIndex >= 0)
    {
        if (m_drawFontFace == nullptr)
            return;

        UINT16 gIndex = static_cast<UINT16>(GlyphIndex);
        FLOAT advance = static_cast<FLOAT>(layoutBounds.Width);

        DWRITE_GLYPH_RUN glyphRun{};
        glyphRun.fontFace = m_drawFontFace.Get();
        glyphRun.fontEmSize = static_cast<FLOAT>(FontSize < 8 ? 8 : FontSize);
        glyphRun.glyphCount = 1;
        glyphRun.glyphIndices = &gIndex;
        glyphRun.glyphAdvances = &advance;
        glyphRun.glyphOffsets = nullptr;
        glyphRun.isSideways = FALSE;
        glyphRun.bidiLevel = 0;

        CoreDWTextServices::DrawGlyphRun(ctx.Get(), { static_cast<float>(left), static_cast<float>(top) }, &glyphRun, renderParams);
    }
    else
    {
        CoreDWTextServices::DrawTextLayout(ctx.Get(), m_textLayout.Get(), { static_cast<float>(left), static_cast<float>(top) }, renderParams);
    }

    m_render = false;
}

void DirectText::OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args)
{
    Update();
};





