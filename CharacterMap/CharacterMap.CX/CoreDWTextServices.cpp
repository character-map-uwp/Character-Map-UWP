#include "pch.h"
#include "CoreDWTextServices.h"
#include "CompositionDeviceManager.h"
#include "DWHelpers.h"
#include "Utils.h"
#include <algorithm>

using namespace Microsoft::WRL;
using namespace CharacterMapCX;

namespace
{
    class LayoutInkSink : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteGeometrySink>
    {
    public:
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        bool hasPoints = false;

        void Pt(float x, float y)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            hasPoints = true;
        }

        void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE) override {}
        void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT) override {}
        void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F p, D2D1_FIGURE_BEGIN) override { Pt(p.x, p.y); }
        void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* pts, UINT32 n) override
        {
            for (UINT32 i = 0; i < n; ++i)
                Pt(pts[i].x, pts[i].y);
        }
        void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* b, UINT32 n) override
        {
            for (UINT32 i = 0; i < n; ++i)
            {
                Pt(b[i].point1.x, b[i].point1.y);
                Pt(b[i].point2.x, b[i].point2.y);
                Pt(b[i].point3.x, b[i].point3.y);
            }
        }
        void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END) override {}
        HRESULT STDMETHODCALLTYPE Close() override { return S_OK; }
    };

    class LayoutBoundsRenderer : public RuntimeClass<RuntimeClassFlags<ClassicCom>, IDWriteTextRenderer, IDWritePixelSnapping>
    {
    public:
        float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
        bool hasPoints = false;
        ComPtr<IDWriteFontFace> overrideFontFace;

        LayoutBoundsRenderer(ComPtr<IDWriteFontFace> overrideFace) : overrideFontFace(overrideFace) {}

        IFACEMETHOD(IsPixelSnappingDisabled)(_In_opt_ void*, _Out_ BOOL* isDisabled) override
        {
            *isDisabled = TRUE;
            return S_OK;
        }
        IFACEMETHOD(GetCurrentTransform)(_In_opt_ void*, _Out_ DWRITE_MATRIX* transform) override
        {
            transform->m11 = 1; transform->m12 = 0;
            transform->m21 = 0; transform->m22 = 1;
            transform->dx = 0;  transform->dy = 0;
            return S_OK;
        }
        IFACEMETHOD(GetPixelsPerDip)(_In_opt_ void*, _Out_ FLOAT* pixelsPerDip) override
        {
            *pixelsPerDip = 1.0f;
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
            if (glyphRun == nullptr || glyphRun->glyphCount == 0)
                return S_OK;

            IDWriteFontFace* face = overrideFontFace != nullptr ? overrideFontFace.Get() : glyphRun->fontFace;
            if (face == nullptr)
                return S_OK;

            LayoutInkSink sink;
            if (SUCCEEDED(face->GetGlyphRunOutline(
                glyphRun->fontEmSize,
                glyphRun->glyphIndices,
                glyphRun->glyphAdvances,
                glyphRun->glyphOffsets,
                glyphRun->glyphCount,
                glyphRun->isSideways,
                glyphRun->bidiLevel % 2,
                &sink)) && sink.hasPoints)
            {
                float runLeft   = baselineOriginX + sink.minX;
                float runRight  = baselineOriginX + sink.maxX;
                float runTop    = baselineOriginY + sink.minY;
                float runBottom = baselineOriginY + sink.maxY;

                if (runLeft < minX) minX = runLeft;
                if (runRight > maxX) maxX = runRight;
                if (runTop < minY) minY = runTop;
                if (runBottom > maxY) maxY = runBottom;
                hasPoints = true;
            }
            return S_OK;
        }
        IFACEMETHOD(DrawUnderline)(_In_opt_ void*, FLOAT, FLOAT, _In_ DWRITE_UNDERLINE const*, IUnknown*) override { return S_OK; }
        IFACEMETHOD(DrawStrikethrough)(_In_opt_ void*, FLOAT, FLOAT, _In_ DWRITE_STRIKETHROUGH const*, IUnknown*) override { return S_OK; }
        IFACEMETHOD(DrawInlineObject)(_In_opt_ void*, FLOAT, FLOAT, IDWriteInlineObject*, BOOL, BOOL, IUnknown*) override { return S_OK; }
    };
}

GlyphImageFormat CoreDWTextServices::ToPreferredFormat(bool isColorEnabled, DWriteColorRenderOption option)
{
    if (!isColorEnabled || option == DWriteColorRenderOption::Monochrome)
        return GlyphImageFormat::TrueType;

    switch (option)
    {
    case DWriteColorRenderOption::ColrV0:
        return GlyphImageFormat::Colr;
    case DWriteColorRenderOption::ColrV1:
        return GlyphImageFormat::ColrPaintTree;
    case DWriteColorRenderOption::Svg:
        return GlyphImageFormat::Svg;
    default:
        return GlyphImageFormat::None;
    }
}

Windows::UI::Xaml::Media::StyleSimulations CoreDWTextServices::ResolveSimulations(
    Windows::UI::Xaml::Media::StyleSimulations controlSimulations,
    DWriteFontFace^ fontFace)
{
    if (controlSimulations != Windows::UI::Xaml::Media::StyleSimulations::None)
        return controlSimulations;

    if (fontFace != nullptr && fontFace->Properties != nullptr)
        return fontFace->Properties->Simulations;

    return Windows::UI::Xaml::Media::StyleSimulations::None;
}

ComPtr<IDWriteFontFace3> CoreDWTextServices::GetSimulatedFontFace(
    DWriteFontFace^ fontFace,
    Windows::UI::Xaml::Media::StyleSimulations simulations)
{
    if (fontFace == nullptr)
        return nullptr;

    if (simulations != Windows::UI::Xaml::Media::StyleSimulations::None)
    {
        auto faceRef = fontFace->GetReference();
        if (faceRef != nullptr)
        {
            ComPtr<IDWriteFontFace3> simFace;
            if (SUCCEEDED(faceRef->CreateFontFaceWithSimulations(ToDWriteFontSimulations(simulations), &simFace)))
                return simFace;
        }
    }

    return fontFace->GetFontFace();
}

HRESULT CoreDWTextServices::CreateLayout(
    const TextLayoutParameters& params,
    IDWriteTextLayout** ppLayout,
    IDWriteFontFace** ppSimulatedFace)
{
    ComPtr<IDWriteFontFace3> simFace3;
    HRESULT hr = CreateLayout(params, ppLayout, &simFace3);
    if (ppSimulatedFace != nullptr)
    {
        *ppSimulatedFace = simFace3.Detach();
    }
    return hr;
}

HRESULT CoreDWTextServices::CreateLayout(
    const TextLayoutParameters& params,
    IDWriteTextLayout** ppLayout,
    IDWriteFontFace3** ppSimulatedFace)
{
    if (ppLayout == nullptr)
        return E_POINTER;

    *ppLayout = nullptr;
    if (ppSimulatedFace != nullptr)
        *ppSimulatedFace = nullptr;

    auto factory = CompositionDeviceManager::GetDWriteFactory();
    if (factory == nullptr)
        return E_FAIL;

    if (params.Text == nullptr || params.Text->Length() == 0)
        return S_OK;

    UINT32 textLength = params.Text->Length();

    // 1. Resolve simulations and simulated face
    auto simulations = ResolveSimulations(params.Simulations, params.FontFace);
    ComPtr<IDWriteFontFace3> simulatedFace = GetSimulatedFontFace(params.FontFace, simulations);
    if (ppSimulatedFace != nullptr && simulatedFace != nullptr)
    {
        *ppSimulatedFace = simulatedFace.Detach();
    }

    // 2. Resolve variable font axis values
    std::vector<DWRITE_FONT_AXIS_VALUE> axisValues;
    if (!params.ExplicitAxisValues.empty())
    {
        axisValues = params.ExplicitAxisValues;
    }
    else if (params.Axis != nullptr && params.Axis->Size > 0)
    {
        axisValues.reserve(params.Axis->Size);
        for (unsigned int i = 0; i < params.Axis->Size; ++i)
            axisValues.push_back(params.Axis->GetAt(i)->GetDWriteValue());
    }
    else if (params.FontFace != nullptr && !params.FontFace->GetAxisValues().empty())
    {
        axisValues = params.FontFace->GetAxisValues();
    }
    else if (params.FontFace != nullptr && params.FontFace->GetFontFace() != nullptr)
    {
        ComPtr<IDWriteFontFace5> face5;
        if (SUCCEEDED(params.FontFace->GetFontFace().As(&face5)))
        {
            UINT32 count = face5->GetFontAxisValueCount();
            if (count > 0)
            {
                axisValues.resize(count);
                if (FAILED(face5->GetFontAxisValues(axisValues.data(), count)))
                    axisValues.clear();
            }
        }
    }

    // 3. Prepare format
    ComPtr<IDWriteTextFormat> tempFormat;
    HRESULT hr = S_OK;

    Platform::String^ familyName = (params.FontFace != nullptr && params.FontFace->Properties != nullptr && params.FontFace->Properties->FamilyName != nullptr)
        ? params.FontFace->Properties->FamilyName
        : ref new Platform::String(L"Segoe UI");

    ComPtr<IDWriteFontCollection> collection = params.FontFace != nullptr ? params.FontFace->GetFontCollection() : nullptr;

    float fontSize = params.FontSize > 0.0f ? params.FontSize : 12.0f;

    if (!axisValues.empty())
    {
        ComPtr<IDWriteTextFormat3> format3;
        hr = factory->CreateTextFormat(
            familyName->Data(),
            collection.Get(),
            axisValues.data(),
            static_cast<UINT32>(axisValues.size()),
            fontSize,
            L"en-us",
            &format3);
        if (SUCCEEDED(hr))
            tempFormat = format3;
    }

    if (tempFormat == nullptr)
    {
        auto weight = params.FontWeight;
        auto style = params.FontStyle;
        auto stretch = params.FontStretch;

        if (params.FontFace != nullptr && params.FontFace->Properties != nullptr)
        {
            if (weight.Weight == Windows::UI::Text::FontWeights::Normal.Weight &&
                style == Windows::UI::Text::FontStyle::Normal &&
                stretch == Windows::UI::Text::FontStretch::Normal)
            {
                weight = params.FontFace->Properties->Weight;
                style = params.FontFace->Properties->Style;
                stretch = params.FontFace->Properties->Stretch;
            }
        }

        if (simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldSimulation ||
            simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
        {
            weight.Weight = (std::max)(weight.Weight, static_cast<unsigned short>(700));
        }
        if (simulations == Windows::UI::Xaml::Media::StyleSimulations::ItalicSimulation ||
            simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
        {
            style = Windows::UI::Text::FontStyle::Italic;
        }

        hr = factory->CreateTextFormat(
            familyName->Data(),
            collection.Get(),
            static_cast<DWRITE_FONT_WEIGHT>(weight.Weight),
            static_cast<DWRITE_FONT_STYLE>(style),
            static_cast<DWRITE_FONT_STRETCH>(stretch),
            fontSize,
            L"en-us",
            &tempFormat);
        if (FAILED(hr))
            return hr;
    }

    // 4. Configure reading direction
    if (params.FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
        tempFormat->SetReadingDirection(DWRITE_READING_DIRECTION_RIGHT_TO_LEFT);
    else
        tempFormat->SetReadingDirection(DWRITE_READING_DIRECTION_LEFT_TO_RIGHT);

    // 5. Configure text alignment
    if (params.IsCharacterFit)
    {
        tempFormat->SetParagraphAlignment(DWRITE_PARAGRAPH_ALIGNMENT_NEAR);
        tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
    }
    else
    {
        switch (params.TextAlignment)
        {
        case Windows::UI::Xaml::TextAlignment::Center:
            tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
            break;
        case Windows::UI::Xaml::TextAlignment::Right:
            tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_TRAILING);
            break;
        case Windows::UI::Xaml::TextAlignment::Justify:
            tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_JUSTIFIED);
            break;
        default:
            tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
            break;
        }
    }

    // 6. Font fallback
    if (params.FallbackFont != nullptr && params.FallbackFont->Fallback != nullptr)
    {
        ComPtr<IDWriteTextFormat2> format2;
        if (SUCCEEDED(tempFormat.As(&format2)))
            format2->SetFontFallback(params.FallbackFont->Fallback.Get());
    }

    // 7. Trimming
    if (params.EnableTrimming)
    {
        DWRITE_TRIMMING trimmingOptions = {};
        trimmingOptions.granularity = DWRITE_TRIMMING_GRANULARITY_CHARACTER;
        trimmingOptions.delimiter = 0;
        trimmingOptions.delimiterCount = 0;

        ComPtr<IDWriteInlineObject> ellipsisSign;
        if (SUCCEEDED(factory->CreateEllipsisTrimmingSign(tempFormat.Get(), &ellipsisSign)))
        {
            tempFormat->SetTrimming(&trimmingOptions, ellipsisSign.Get());
        }
    }

    // 8. Create layout
    ComPtr<IDWriteTextLayout> textLayout;
    hr = factory->CreateTextLayout(
        params.Text->Data(),
        textLength,
        tempFormat.Get(),
        params.MaxWidth,
        params.MaxHeight,
        &textLayout);
    if (FAILED(hr))
        return hr;

    // 9. Apply simulations on layout
    if (simulations == Windows::UI::Xaml::Media::StyleSimulations::ItalicSimulation ||
        simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
    {
        textLayout->SetFontStyle(DWRITE_FONT_STYLE_OBLIQUE, DWRITE_TEXT_RANGE{ 0, textLength });
    }
    if (simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldSimulation ||
        simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
    {
        textLayout->SetFontWeight(DWRITE_FONT_WEIGHT_BOLD, DWRITE_TEXT_RANGE{ 0, textLength });
    }

    // 10. Apply axis values on layout
    if (!axisValues.empty())
    {
        ComPtr<IDWriteTextLayout4> layout4;
        if (SUCCEEDED(textLayout.As(&layout4)))
        {
            layout4->SetFontAxisValues(
                axisValues.data(),
                static_cast<UINT32>(axisValues.size()),
                DWRITE_TEXT_RANGE{ 0, textLength });
        }
    }

    // 11. Apply OpenType typography features
    if (params.TypographyInfo != nullptr && params.TypographyInfo->Feature != DWriteTypographyFeatureName::None)
    {
        ComPtr<IDWriteTypography> dwriteTypo;
        if (SUCCEEDED(factory->CreateTypography(&dwriteTypo)))
        {
            DWRITE_FONT_FEATURE f;
            f.nameTag = static_cast<DWRITE_FONT_FEATURE_TAG>(params.TypographyInfo->Feature);
            f.parameter = 1;
            dwriteTypo->AddFontFeature(f);
            textLayout->SetTypography(dwriteTypo.Get(), DWRITE_TEXT_RANGE{ 0, textLength });
        }
    }
    else if (params.TypographyCollection != nullptr && params.TypographyCollection->FeatureCount > 0)
    {
        ComPtr<IDWriteTypography> dwriteTypo;
        if (SUCCEEDED(factory->CreateTypography(&dwriteTypo)))
        {
            for (const auto& f : params.TypographyCollection->GetDWriteFontFeatures())
                dwriteTypo->AddFontFeature(f);

            textLayout->SetTypography(dwriteTypo.Get(), DWRITE_TEXT_RANGE{ 0, textLength });
        }
    }

    // 12. Apply Word Wrapping
    textLayout->SetWordWrapping(params.WordWrapping ? DWRITE_WORD_WRAPPING_WRAP : DWRITE_WORD_WRAPPING_NO_WRAP);

    *ppLayout = textLayout.Detach();
    return S_OK;
}

bool CoreDWTextServices::UpdateAxisValues(
    IDWriteTextLayout* layout,
    const std::vector<DWRITE_FONT_AXIS_VALUE>& axisValues,
    UINT32 textLength)
{
    if (!layout || axisValues.empty() || textLength == 0)
        return false;

    ComPtr<IDWriteTextLayout4> layout4;
    if (SUCCEEDED(layout->QueryInterface(IID_PPV_ARGS(&layout4))))
    {
        return SUCCEEDED(layout4->SetFontAxisValues(
            axisValues.data(),
            static_cast<UINT32>(axisValues.size()),
            DWRITE_TEXT_RANGE{ 0, textLength }));
    }
    return false;
}

HRESULT CoreDWTextServices::MeasureLayoutBounds(
    IDWriteTextLayout* layout,
    Windows::Foundation::Rect* pLayoutBounds)
{
    if (!layout || !pLayoutBounds)
        return E_INVALIDARG;

    DWRITE_TEXT_METRICS1 dwriteMetrics{};
    HRESULT hr = layout->GetMetrics(&dwriteMetrics);
    if (FAILED(hr))
        return hr;

    Windows::Foundation::Rect rect(dwriteMetrics.left, dwriteMetrics.top, dwriteMetrics.width, dwriteMetrics.height);

    auto readingDirection = layout->GetReadingDirection();
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

    *pLayoutBounds = rect;
    return S_OK;
}

HRESULT CoreDWTextServices::MeasureInkBounds(
    IDWriteTextLayout* layout,
    IDWriteFontFace* overrideFace,
    Windows::Foundation::Rect* pInkBounds)
{
    if (!layout || !pInkBounds)
        return E_INVALIDARG;

    auto boundsRenderer = Make<LayoutBoundsRenderer>(overrideFace);
    if (SUCCEEDED(layout->Draw(nullptr, boundsRenderer.Get(), 0, 0)) && boundsRenderer->hasPoints)
    {
        float w = boundsRenderer->maxX - boundsRenderer->minX;
        float h = boundsRenderer->maxY - boundsRenderer->minY;
        if (w > 0 && h > 0)
        {
            *pInkBounds = Windows::Foundation::Rect(boundsRenderer->minX, boundsRenderer->minY, w, h);
            return S_OK;
        }
    }

    // Fallback: use DWrite overhang metrics
    DWRITE_TEXT_METRICS1 metrics{};
    DWRITE_OVERHANG_METRICS overhang{};
    if (SUCCEEDED(layout->GetMetrics(&metrics)) && SUCCEEDED(layout->GetOverhangMetrics(&overhang)))
    {
        const float left = metrics.left - overhang.left;
        const float right = metrics.left + metrics.widthIncludingTrailingWhitespace + overhang.right;
        const float top = metrics.top - overhang.top;
        const float bottom = metrics.top + metrics.height + overhang.bottom;
        *pInkBounds = Windows::Foundation::Rect(left, top, (std::max)(1.0f, right - left), (std::max)(1.0f, bottom - top));
        return S_OK;
    }

    return E_FAIL;
}

void CoreDWTextServices::DrawTextLayout(
    ID2D1DeviceContext* context,
    IDWriteTextLayout* layout,
    D2D1_POINT_2F origin,
    const TextRenderParameters& params)
{
    if (!context || !layout)
        return;

    GlyphImageFormat format = params.PreferredFormat;
    if (!params.EnableColor && format == GlyphImageFormat::None)
        format = GlyphImageFormat::TrueType;

    CustomColorTextRenderer renderer(
        context,
        CompositionDeviceManager::GetDWriteFactory().Get(),
        params.ForegroundBrush,
        format,
        params.OverrideFace);

    layout->Draw(nullptr, &renderer, origin.x, origin.y);
}

void CoreDWTextServices::DrawGlyphRun(
    ID2D1DeviceContext* context,
    D2D1_POINT_2F origin,
    const DWRITE_GLYPH_RUN* run,
    const TextRenderParameters& params)
{
    if (!context || !run)
        return;

    GlyphImageFormat format = params.PreferredFormat;
    if (!params.EnableColor && format == GlyphImageFormat::None)
        format = GlyphImageFormat::TrueType;

    DWRITE_GLYPH_RUN customRun = *run;
    if (params.OverrideFace != nullptr)
        customRun.fontFace = params.OverrideFace;

    CompositionDeviceManager::DrawGlyphRunWithColorSupport(
        context,
        CompositionDeviceManager::GetDWriteFactory().Get(),
        origin,
        &customRun,
        params.ForegroundBrush,
        format,
        DWRITE_MEASURING_MODE_NATURAL);
}
