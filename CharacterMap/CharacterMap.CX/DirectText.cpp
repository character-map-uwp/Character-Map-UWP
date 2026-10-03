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

    bool hasText = GlyphIndex >= 0 || UnicodeIndex > 0 || FontFace != nullptr;

    if (!hasText || Typography == nullptr || m_canvas == nullptr || FontFamily == nullptr || !m_canvas->ReadyToDraw)
        return Size(this->MinWidth, this->MinHeight);

    auto dpi = m_canvas->Dpi / 96.0f;
    auto m = m_canvas->Device->MaximumBitmapSizeInPixels / dpi;

    m_canvas->Measure(size);

    if (m_textLayout == nullptr || m_isStale)
    {
        m_isStale = false;
        m_drawFontFace = nullptr;

        if (m_textLayout != nullptr)
            m_textLayout = nullptr;

        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;

        // Resolve font face with axis values (for variable fonts)
        ComPtr<IDWriteFontFaceReference> faceRef = fontFace->GetReference();
        ComPtr<IDWriteFontFace3> dwriteFontFace;

        if (Axis != nullptr && Axis->Size > 0)
        {
            ComPtr<IDWriteFontFaceReference1> faceRef1;
            if (SUCCEEDED(faceRef.As(&faceRef1)))
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
                        if (SUCCEEDED(fontResource->CreateFontFace(DWRITE_FONT_SIMULATIONS_NONE, values.data(), static_cast<UINT32>(values.size()), &face5_var)))
                        {
                            dwriteFontFace = face5_var;
                        }
                    }
                }
            }
        }

        if (dwriteFontFace == nullptr)
            dwriteFontFace = fontFace->GetFontFace();

        m_drawFontFace = dwriteFontFace;

        if (GlyphIndex >= 0)
        {
            UINT16 gIndex = static_cast<UINT16>(GlyphIndex);
            DWRITE_GLYPH_METRICS glyphMetrics;
            ThrowIfFailed(dwriteFontFace->GetDesignGlyphMetrics(&gIndex, 1, &glyphMetrics, FALSE));

            DWRITE_FONT_METRICS1 fontMetrics = fontFace->GetMetrics();
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
            ThrowIfFailed(NativeInterop::_Current->m_d2dFactory->CreatePathGeometry(&pathGeometry));

            ComPtr<ID2D1GeometrySink> sink;
            ThrowIfFailed(pathGeometry->Open(&sink));

            ThrowIfFailed(dwriteFontFace->GetGlyphRunOutline(
                static_cast<FLOAT>(fontSize),
                &gIndex,
                &advanceWidthF,
                nullptr,
                1,
                FALSE,
                FALSE,
                sink.Get()
            ));

            ThrowIfFailed(sink->Close());

            D2D1_RECT_F bounds;
            ThrowIfFailed(pathGeometry->GetBounds(nullptr, &bounds));

            if (!(bounds.left <= bounds.right) || !(bounds.top <= bounds.bottom) || (bounds.right - bounds.left <= 0) || (bounds.bottom - bounds.top <= 0))
            {
                drawBounds = layoutBounds;
            }
            else
            {
                drawBounds = Rect(
                    bounds.left,
                    bounds.top,
                    bounds.right - bounds.left,
                    bounds.bottom - bounds.top
                );
            }

            m_render = true;
            m_canvas->Invalidate();
        }
        else
        {
            // Standard TextLayout path
            Platform::String^ text = Text;
            textLength = text->Length();

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

            /*if (fontFace != nullptr)
            {
                if (fontFace->GetFontCollection() != nullptr)
                    textLayout->SetFontCollection(fontFace->GetFontCollection().Get(), DWRITE_TEXT_RANGE{ 0, textLength });
                if (fontFace->Properties != nullptr && fontFace->Properties->FamilyName != nullptr)
                    textLayout->SetFontFamilyName(fontFace->Properties->FamilyName->Data(), DWRITE_TEXT_RANGE{ 0, textLength });
            }*/

           
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

            if (width <= 0 || height <= 0)
            {
                drawBounds = layoutBounds;
            }
            else
            {
                Rect draw = { left, top, width, height };
                drawBounds = draw;
            }

            m_textLayout = textLayout;
            m_render = true;

            m_canvas->Invalidate();
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





namespace
{
    void DrawGlyphRunColrV0(
        ID2D1DeviceContext1* ctx,
        IDWriteFactory* dwriteFactory,
        D2D1_POINT_2F baselineOrigin,
        const DWRITE_GLYPH_RUN* glyphRun,
        const DWRITE_GLYPH_RUN_DESCRIPTION* glyphRunDescription,
        ID2D1Brush* defaultBrush,
        DWRITE_MEASURING_MODE measuringMode,
        bool isColorFontEnabled)
    {
        HRESULT hr_color = DWRITE_E_NOCOLOR;
        ComPtr<IDWriteColorGlyphRunEnumerator1> glyphRunEnumerator;

        if (isColorFontEnabled && dwriteFactory != nullptr)
        {
            ComPtr<IDWriteFactory4> factory4;
            if (SUCCEEDED(dwriteFactory->QueryInterface(__uuidof(IDWriteFactory4), &factory4)))
            {
                DWRITE_GLYPH_IMAGE_FORMATS supportedFormats =
                    DWRITE_GLYPH_IMAGE_FORMATS_TRUETYPE |
                    DWRITE_GLYPH_IMAGE_FORMATS_CFF |
                    DWRITE_GLYPH_IMAGE_FORMATS_COLR |
                    DWRITE_GLYPH_IMAGE_FORMATS_SVG |
                    DWRITE_GLYPH_IMAGE_FORMATS_PNG |
                    DWRITE_GLYPH_IMAGE_FORMATS_JPEG |
                    DWRITE_GLYPH_IMAGE_FORMATS_TIFF |
                    DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8;

                hr_color = factory4->TranslateColorGlyphRun(
                    baselineOrigin,
                    glyphRun,
                    glyphRunDescription,
                    supportedFormats,
                    measuringMode,
                    nullptr,
                    0,
                    &glyphRunEnumerator
                );
            }
        }

        if (SUCCEEDED(hr_color))
        {
            for (;;)
            {
                BOOL haveRun = FALSE;
                if (FAILED(glyphRunEnumerator->MoveNext(&haveRun)) || !haveRun)
                    break;

                DWRITE_COLOR_GLYPH_RUN1 const* colorRun = nullptr;
                if (FAILED(glyphRunEnumerator->GetCurrentRun(&colorRun)) || colorRun == nullptr)
                    break;

                ComPtr<ID2D1Brush> runBrush = defaultBrush;
                if (colorRun->paletteIndex != 0xFFFF)
                {
                    ComPtr<ID2D1SolidColorBrush> solidBrush;
                    if (SUCCEEDED(ctx->CreateSolidColorBrush(colorRun->runColor, &solidBrush)))
                        runBrush = solidBrush;
                }

                D2D1_POINT_2F runOrigin = { colorRun->baselineOriginX, colorRun->baselineOriginY };

                if (colorRun->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_SVG)
                {
                    ComPtr<ID2D1DeviceContext4> ctx4;
                    if (SUCCEEDED(ctx->QueryInterface(__uuidof(ID2D1DeviceContext4), &ctx4)))
                    {
                        ctx4->DrawSvgGlyphRun(
                            runOrigin,
                            &colorRun->glyphRun,
                            runBrush.Get(),
                            nullptr,
                            0,
                            measuringMode
                        );
                    }
                }
                else if (colorRun->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_PNG ||
                    colorRun->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_JPEG ||
                    colorRun->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_TIFF ||
                    colorRun->glyphImageFormat == DWRITE_GLYPH_IMAGE_FORMATS_PREMULTIPLIED_B8G8R8A8)
                {
                    ComPtr<ID2D1DeviceContext4> ctx4;
                    if (SUCCEEDED(ctx->QueryInterface(__uuidof(ID2D1DeviceContext4), &ctx4)))
                    {
                        ctx4->DrawColorBitmapGlyphRun(
                            colorRun->glyphImageFormat,
                            runOrigin,
                            &colorRun->glyphRun,
                            measuringMode,
                            D2D1_COLOR_BITMAP_GLYPH_SNAP_OPTION_DEFAULT
                        );
                    }
                }
                else
                {
                    ctx->DrawGlyphRun(
                        runOrigin,
                        &colorRun->glyphRun,
                        nullptr,
                        runBrush.Get(),
                        measuringMode
                    );
                }
            }
        }
        else
        {
            ctx->DrawGlyphRun(
                baselineOrigin,
                glyphRun,
                nullptr,
                defaultBrush,
                measuringMode
            );
        }
    }

    class ColrV0TextRenderer : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteTextRenderer, IDWritePixelSnapping>
    {
    private:
        ComPtr<ID2D1DeviceContext1> m_context;
        ComPtr<ID2D1Brush> m_defaultBrush;
        ComPtr<IDWriteFactory> m_factory;

    public:
        ColrV0TextRenderer(
            ComPtr<ID2D1DeviceContext1> context,
            ComPtr<ID2D1Brush> defaultBrush,
            ComPtr<IDWriteFactory> factory)
            : m_context(context), m_defaultBrush(defaultBrush), m_factory(factory)
        {}

        IFACEMETHOD(IsPixelSnappingDisabled)(_In_opt_ void*, _Out_ BOOL* isDisabled) override
        {
            *isDisabled = FALSE;
            return S_OK;
        }

        IFACEMETHOD(GetCurrentTransform)(_In_opt_ void*, _Out_ DWRITE_MATRIX* transform) override
        {
            D2D1_MATRIX_3X2_F m;
            m_context->GetTransform(&m);
            transform->m11 = m._11; transform->m12 = m._12;
            transform->m21 = m._21; transform->m22 = m._22;
            transform->dx = m._31;  transform->dy = m._32;
            return S_OK;
        }

        IFACEMETHOD(GetPixelsPerDip)(_In_opt_ void*, _Out_ FLOAT* pixelsPerDip) override
        {
            FLOAT dpiX, dpiY;
            m_context->GetDpi(&dpiX, &dpiY);
            *pixelsPerDip = dpiX / 96.0f;
            return S_OK;
        }

        IFACEMETHOD(DrawGlyphRun)(
            _In_opt_ void* clientDrawingContext,
            FLOAT baselineOriginX,
            FLOAT baselineOriginY,
            DWRITE_MEASURING_MODE measuringMode,
            _In_ DWRITE_GLYPH_RUN const* glyphRun,
            _In_ DWRITE_GLYPH_RUN_DESCRIPTION const* glyphRunDescription,
            IUnknown* clientDrawingEffect) override
        {
            ComPtr<ID2D1Brush> brush = m_defaultBrush;
            if (clientDrawingEffect != nullptr)
            {
                ComPtr<ID2D1Brush> effectBrush;
                if (SUCCEEDED(clientDrawingEffect->QueryInterface(__uuidof(ID2D1Brush), &effectBrush)))
                    brush = effectBrush;
            }

            DrawGlyphRunColrV0(
                m_context.Get(),
                m_factory.Get(),
                { baselineOriginX, baselineOriginY },
                glyphRun,
                glyphRunDescription,
                brush.Get(),
                measuringMode,
                true
            );
            return S_OK;
        }

        IFACEMETHOD(DrawUnderline)(
            _In_opt_ void* clientDrawingContext,
            FLOAT baselineOriginX,
            FLOAT baselineOriginY,
            _In_ DWRITE_UNDERLINE const* underline,
            IUnknown* clientDrawingEffect) override
        {
            ComPtr<ID2D1Brush> brush = m_defaultBrush;
            if (clientDrawingEffect != nullptr)
            {
                ComPtr<ID2D1Brush> effectBrush;
                if (SUCCEEDED(clientDrawingEffect->QueryInterface(__uuidof(ID2D1Brush), &effectBrush)))
                    brush = effectBrush;
            }

            D2D1_RECT_F rect = {
                baselineOriginX,
                baselineOriginY + underline->offset,
                baselineOriginX + underline->width,
                baselineOriginY + underline->offset + underline->thickness
            };
            m_context->FillRectangle(&rect, brush.Get());
            return S_OK;
        }

        IFACEMETHOD(DrawStrikethrough)(
            _In_opt_ void* clientDrawingContext,
            FLOAT baselineOriginX,
            FLOAT baselineOriginY,
            _In_ DWRITE_STRIKETHROUGH const* strikethrough,
            IUnknown* clientDrawingEffect) override
        {
            ComPtr<ID2D1Brush> brush = m_defaultBrush;
            if (clientDrawingEffect != nullptr)
            {
                ComPtr<ID2D1Brush> effectBrush;
                if (SUCCEEDED(clientDrawingEffect->QueryInterface(__uuidof(ID2D1Brush), &effectBrush)))
                    brush = effectBrush;
            }

            D2D1_RECT_F rect = {
                baselineOriginX,
                baselineOriginY + strikethrough->offset,
                baselineOriginX + strikethrough->width,
                baselineOriginY + strikethrough->offset + strikethrough->thickness
            };
            m_context->FillRectangle(&rect, brush.Get());
            return S_OK;
        }

        IFACEMETHOD(DrawInlineObject)(
            _In_opt_ void* clientDrawingContext,
            FLOAT originX,
            FLOAT originY,
            IDWriteInlineObject* inlineObject,
            BOOL isSideways,
            BOOL isRightToLeft,
            IUnknown* clientDrawingEffect) override
        {
            if (inlineObject != nullptr)
                return inlineObject->Draw(clientDrawingContext, this, originX, originY, isSideways, isRightToLeft, clientDrawingEffect);
            return S_OK;
        }
    };
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

	bool color = ColorRenderOption != DWriteColorRenderOption::Monochrome;

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

        ComPtr<ID2D1DeviceContext1> ctx = GetWrappedResource<ID2D1DeviceContext1>(args->DrawingSession);
        if (m_brush == nullptr)
        {
            ctx->CreateSolidColorBrush(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color), &m_brush);
        }
        else
        {
            m_brush->SetColor(ToD2DColor(((SolidColorBrush^)this->Foreground)->Color));
        }

        bool drawn = false;
        if (color && (ColorRenderOption == DWriteColorRenderOption::ColrV1 || ColorRenderOption == DWriteColorRenderOption::Default))
        {
            ComPtr<ID2D1DeviceContext7> ctx7;
            if (SUCCEEDED(ctx.As(&ctx7)))
            {
                ctx7->DrawGlyphRunWithColorSupport(
                    { static_cast<float>(left), static_cast<float>(top) },
                    &glyphRun,
                    nullptr,
                    m_brush.Get(),
                    nullptr,
                    0,
                    DWRITE_MEASURING_MODE_NATURAL
                );
                drawn = true;
            }
        }

        if (!drawn)
        {
            DrawGlyphRunColrV0(
                ctx.Get(),
                NativeInterop::_Current->m_dwriteFactory.Get(),
                { static_cast<float>(left), static_cast<float>(top) },
                &glyphRun,
                nullptr,
                m_brush.Get(),
                DWRITE_MEASURING_MODE_NATURAL,
                color
            );
        }
    }
    else
    {
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

        if (color && (ColorRenderOption == DWriteColorRenderOption::ColrV0))
        {
            auto renderer = Make<ColrV0TextRenderer>(ctx, m_brush, NativeInterop::_Current->m_dwriteFactory);
            m_textLayout->Draw(nullptr, renderer.Get(), static_cast<FLOAT>(left), static_cast<FLOAT>(top));
        }
        else
        {
            D2D1_DRAW_TEXT_OPTIONS ops = D2D1_DRAW_TEXT_OPTIONS::D2D1_DRAW_TEXT_OPTIONS_NONE;
            if (color)
                ops = D2D1_DRAW_TEXT_OPTIONS::D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT;

            // Draw it
            ctx->DrawTextLayout(
                { static_cast<float>(left), static_cast<float>(top) },
                m_textLayout.Get(),
                m_brush.Get(),
                ops
            );
        }
    }

    m_render = false;
}

void DirectText::OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args)
{
    Update();
};





