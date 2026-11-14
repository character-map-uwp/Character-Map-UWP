//
// DirectText.cpp
// Implementation of the DirectText class.
//

#pragma once
#include "pch.h"
#include "DWriteFallbackFont.h"
#include "NativeInterop.h"
#include "CompositionDeviceManager.h"
#include "CoreDWTextServices.h"
#include <cfloat>
#include <cmath>
#include <algorithm>
#include <array>
#include <atomic>
#include <deque>
#include <thread>

using namespace CharacterMapCX;
using namespace CharacterMapCX::Controls;
using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Foundation::Numerics;
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
using namespace Windows::Graphics;
using namespace Windows::Graphics::DirectX;
using namespace Windows::Graphics::DirectX::Direct3D11;
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
DependencyProperty^ DirectText::_IsBackgroundRenderEnabledProperty = nullptr;
DependencyProperty^ DirectText::_SurfaceSizeProperty = nullptr;

// Caches the background-created IDWriteTextLayout across size-only updates (e.g., FontSize slider
// or container resize) so layout properties can be mutated in-place via SetFontSize/SetMaxWidth/etc.
// instead of recreating the layout from scratch. Guarded by Mutex against concurrent background draws.
struct CharacterMapCX::Controls::DeferredLayoutCache
{
    std::mutex Mutex;
    ComPtr<IDWriteTextLayout> TextLayout;
    ComPtr<IDWriteFontFace3> DrawFontFace;
    Platform::String^ Text = nullptr;
    DWriteFontFace^ FontFace = nullptr;
    ITypographyInfo^ TypographyInfo = nullptr;
    DWriteFallbackFont^ FallbackFont = nullptr;
    Windows::UI::Xaml::FlowDirection FlowDirection = Windows::UI::Xaml::FlowDirection::LeftToRight;
    bool WordWrapping = false;
    Windows::UI::Text::FontWeight FontWeight = Windows::UI::Text::FontWeights::Normal;
    Windows::UI::Text::FontStyle FontStyle = Windows::UI::Text::FontStyle::Normal;
    Windows::UI::Text::FontStretch FontStretch = Windows::UI::Text::FontStretch::Normal;
    Windows::UI::Xaml::Media::StyleSimulations Simulations = Windows::UI::Xaml::Media::StyleSimulations::None;
    std::vector<DWRITE_FONT_AXIS_VALUE> AxisValues;
    float FontSize = 0.0f;
    float MaxWidth = 0.0f;
    float MaxHeight = 0.0f;
};

DirectText::DirectText()
{
    RegisterDependencyProperties();

	DefaultStyleKey = "CharacterMapCX.Controls.DirectText";
    m_deferredLayoutCache = std::make_shared<DeferredLayoutCache>();
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

    m_fontSizeToken = this->RegisterPropertyChangedCallback(DirectText::FontSizeProperty, c);
    m_foregroundToken = this->RegisterPropertyChangedCallback(DirectText::ForegroundProperty, c);
    m_flowDirectionToken = this->RegisterPropertyChangedCallback(DirectText::FlowDirectionProperty, c);
    m_requestedThemeToken = this->RegisterPropertyChangedCallback(DirectText::RequestedThemeProperty, c);
    m_paddingToken = this->RegisterPropertyChangedCallback(DirectText::PaddingProperty, c);

    m_loadedToken = this->Loaded += ref new Windows::UI::Xaml::RoutedEventHandler([weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
        if (auto pThis = weakThis.Resolve<DirectText>())
            pThis->OnLoaded(sender, e);
    });

    m_unloadedToken = this->Unloaded += ref new Windows::UI::Xaml::RoutedEventHandler([weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
        if (auto pThis = weakThis.Resolve<DirectText>())
            pThis->OnUnloaded(sender, e);
    });
}

DirectText::~DirectText()
{
    try
    {
        this->UnregisterPropertyChangedCallback(DirectText::FontSizeProperty, m_fontSizeToken);
        this->UnregisterPropertyChangedCallback(DirectText::ForegroundProperty, m_foregroundToken);
        this->UnregisterPropertyChangedCallback(DirectText::FlowDirectionProperty, m_flowDirectionToken);
        this->UnregisterPropertyChangedCallback(DirectText::RequestedThemeProperty, m_requestedThemeToken);
        this->UnregisterPropertyChangedCallback(DirectText::PaddingProperty, m_paddingToken);
        this->Loaded -= m_loadedToken;
        this->Unloaded -= m_unloadedToken;
    }
    catch (...) {}

    ReleaseDrawingSurface();

    if (m_spriteVisual != nullptr)
    {
        try
        {
            if (Dispatcher != nullptr && Dispatcher->HasThreadAccess)
                ElementCompositionPreview::SetElementChildVisual(this, nullptr);
            m_spriteVisual->Brush = nullptr;
        }
        catch (...) {}
        m_spriteVisual = nullptr;
    }

    if (m_maskBrush != nullptr)
    {
        try
        {
            m_maskBrush->Mask = nullptr;
            m_maskBrush->Source = nullptr;
        }
        catch (...) {}
        m_maskBrush = nullptr;
    }

    m_colorBrush = nullptr;
    m_surfaceBrush = nullptr;
    ClearLayoutCache();
}

void CharacterMapCX::Controls::DirectText::ClearLayoutCache()
{
    m_textLayout = nullptr;
    m_drawFontFace = nullptr;
    m_lastFontFace = nullptr;
    m_lastTypography = nullptr;
    m_lastFallbackFont = nullptr;
    m_lastText = nullptr;
    m_lastFamilyName = nullptr;
    m_lastFaceName = nullptr;
    m_lastAxisValues.clear();
    m_lastShownFontFace = nullptr;
    m_deferredLayoutCache = std::make_shared<DeferredLayoutCache>();
}

void DirectText::OnPropChanged(DependencyObject^ d, DependencyProperty^ p)
{
    DirectText^ c = (DirectText^)d;
    if (p == DirectText::ForegroundProperty && !c->ShouldRenderColor() && c->m_drawingSurface != nullptr && !c->m_renderedColor)
    {
        if (c->m_maskBrush != nullptr && c->m_spriteVisual != nullptr)
        {
            c->m_colorBrush = CompositionDeviceManager::GetColorBrush(c->m_spriteVisual->Compositor, c->GetForegroundColor());
            c->m_maskBrush->Source = c->m_colorBrush;
            return;
        }
    }

    if (p != DirectText::ForegroundProperty)
        c->m_forceColorSurface = false;

    if (!c->BlockUpdates)
        c->Update();
}

void CharacterMapCX::Controls::DirectText::OnLoaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    if (m_isStale || m_needsRender || m_drawingSurface == nullptr)
    {
        this->InvalidateMeasure();
        this->InvalidateArrange();
    }
}

void CharacterMapCX::Controls::DirectText::OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    ReleaseDrawingSurface();

    if (m_spriteVisual != nullptr)
    {
        ElementCompositionPreview::SetElementChildVisual(this, nullptr);
        m_spriteVisual->Brush = nullptr;
        m_spriteVisual = nullptr;
    }

    if (m_maskBrush != nullptr)
    {
        m_maskBrush->Mask = nullptr;
        m_maskBrush->Source = nullptr;
        m_maskBrush = nullptr;
    }

    m_colorBrush = nullptr;
    m_surfaceBrush = nullptr;
    ClearLayoutCache();
    m_isStale = true;
    m_needsRender = true;
}

Windows::UI::Color DirectText::GetForegroundColor()
{
    return CompositionDeviceManager::ExtractSolidColor(this->Foreground, Windows::UI::Colors::White);
}

namespace
{
    // Lightweight IDWriteTextRenderer that probes whether any fallback run resolves to a color font.
    // Returns E_ABORT as soon as one color run is found so we don't walk the rest of the layout.
    class RunColorChecker final : public IDWriteTextRenderer
    {
    public:
        bool HasColorFont = false;

        IFACEMETHODIMP_(unsigned long) AddRef() override { return 1; }
        IFACEMETHODIMP_(unsigned long) Release() override { return 1; }
        IFACEMETHODIMP QueryInterface(IID const& riid, void** ppvObject) override
        {
            if (riid == __uuidof(IDWriteTextRenderer) || riid == __uuidof(IDWritePixelSnapping) || riid == __uuidof(IUnknown))
            {
                *ppvObject = this;
                return S_OK;
            }
            *ppvObject = nullptr;
            return E_NOINTERFACE;
        }
        IFACEMETHODIMP IsPixelSnappingDisabled(void*, BOOL* isDisabled) override { *isDisabled = TRUE; return S_OK; }
        IFACEMETHODIMP GetCurrentTransform(void*, DWRITE_MATRIX* transform) override { *transform = { 1, 0, 0, 1, 0, 0 }; return S_OK; }
        IFACEMETHODIMP GetPixelsPerDip(void*, FLOAT* pixelsPerDip) override { *pixelsPerDip = 1.0f; return S_OK; }
        IFACEMETHODIMP DrawUnderline(void*, FLOAT, FLOAT, DWRITE_UNDERLINE const*, IUnknown*) override { return S_OK; }
        IFACEMETHODIMP DrawStrikethrough(void*, FLOAT, FLOAT, DWRITE_STRIKETHROUGH const*, IUnknown*) override { return S_OK; }
        IFACEMETHODIMP DrawInlineObject(void*, FLOAT, FLOAT, IDWriteInlineObject*, BOOL, BOOL, IUnknown*) override { return S_OK; }
        IFACEMETHODIMP DrawGlyphRun(void*, FLOAT, FLOAT, DWRITE_MEASURING_MODE, DWRITE_GLYPH_RUN const* glyphRun, DWRITE_GLYPH_RUN_DESCRIPTION const*, IUnknown*) override
        {
            if (HasColorFont)
                return E_ABORT;

            if (glyphRun != nullptr && glyphRun->fontFace != nullptr)
            {
                ComPtr<IDWriteFontFace2> face2;
                if (SUCCEEDED(glyphRun->fontFace->QueryInterface(IID_PPV_ARGS(&face2))) && face2->IsColorFont())
                {
                    HasColorFont = true;
                    return E_ABORT;
                }
            }
            return S_OK;
        }
    };
}

bool DirectText::ShouldRenderColor()
{
    if (!IsColorFontEnabled || ColorRenderOption == DWriteColorRenderOption::Monochrome)
        return false;

    if (m_forceColorSurface)
        return true;

    if (FontFace != nullptr && FontFace->Properties != nullptr && FontFace->Properties->IsColorFont)
        return true;

    if (GlyphIndex < 0 && FallbackFont != nullptr && m_textLayout != nullptr)
    {
        RunColorChecker checker;
        m_textLayout->Draw(nullptr, &checker, 0.0f, 0.0f);
        return checker.HasColorFont;
    }

    return false;
}

void DirectText::OnApplyTemplate()
{
    if (DesignMode::DesignModeEnabled)
        return;

    Update();
}

Windows::Foundation::Size CharacterMapCX::Controls::DirectText::MeasureOverride(Windows::Foundation::Size size)
{
    if (DesignMode::DesignModeEnabled)
        return size;

    try
    {
        bool hasText = FontFace != nullptr && (GlyphIndex >= 0 || (Text != nullptr && Text->Length() > 0));

        if (!hasText || Typography == nullptr)
        {
            if (!hasText)
            {
                ClearLayoutCache();
                m_render = false;
            }
            return Size(this->MinWidth, this->MinHeight);
        }

    float dpiScale = CompositionDeviceManager::GetDpiScale(this->Dispatcher);

    double m = 8192.0 / dpiScale;

    Thickness padding = this->Padding;
    double padL = (std::max)(0.0, padding.Left);
    double padT = (std::max)(0.0, padding.Top);
    double padR = (std::max)(0.0, padding.Right);
    double padB = (std::max)(0.0, padding.Bottom);
    double padW = padL + padR;
    double padH = padT + padB;

    float availW = std::isinf(size.Width) ? static_cast<float>(m) : static_cast<float>((std::max)(1.0, static_cast<double>(size.Width) - padW));
    float availH = std::isinf(size.Height) ? static_cast<float>(m) : static_cast<float>((std::max)(1.0, static_cast<double>(size.Height) - padH));

    Windows::Foundation::Size fixedSurfaceSize = SurfaceSize;
    if (IsBackgroundRenderEnabled && !IsCharacterFitEnabled && GlyphIndex < 0 &&
        fixedSurfaceSize.Width > 0.0f && fixedSurfaceSize.Height > 0.0f)
    {
        m_deferredLayout = true;
        m_textLayout = nullptr;
        m_drawFontFace = nullptr;
        m_isStale = false;
        m_render = true;
        textLength = Text != nullptr ? Text->Length() : 0;

        float targetW = std::isinf(size.Width) ? fixedSurfaceSize.Width : (std::min)(size.Width, fixedSurfaceSize.Width);
        float targetH = std::isinf(size.Height) ? fixedSurfaceSize.Height : (std::min)(size.Height, fixedSurfaceSize.Height);
        m_deferredAvailW = IsTextWrappingEnabled ? min(availW, static_cast<float>(m)) : static_cast<float>(m);
        m_deferredAvailH = IsTextWrappingEnabled ? min(availH, static_cast<float>(m)) : static_cast<float>(m);
        return Size((std::max)(static_cast<float>(this->MinWidth), targetW), (std::max)(static_cast<float>(this->MinHeight), targetH));
    }
    m_deferredLayout = false;

    bool needsRebuild = m_isStale || (GlyphIndex >= 0 ? (m_drawFontFace == nullptr) : (m_textLayout == nullptr));
    bool sizeChangedForWrap = false;
    if (!needsRebuild && GlyphIndex < 0 && m_textLayout != nullptr && IsTextWrappingEnabled)
    {
        float lwidth = min(availW, static_cast<float>(m));
        float lheight = min(availH, static_cast<float>(m));
        if (lwidth != m_lastMaxWidth || lheight != m_lastMaxHeight)
            sizeChangedForWrap = true;
    }

    // Fast path: reuse existing IDWriteTextLayout when only FontSize, MaxWidth/MaxHeight, or variable font Axis changed.
    if ((needsRebuild || sizeChangedForWrap) && GlyphIndex < 0 && m_textLayout != nullptr && FontFace != nullptr)
    {
        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;
        auto simulations = CoreDWTextServices::ResolveSimulations(StyleSimulations, fontFace);

        float lwidth = IsTextWrappingEnabled ? availW : static_cast<float>(m);
        float lheight = IsTextWrappingEnabled ? availH : static_cast<float>(m);
        lwidth = min(lwidth, static_cast<float>(m));
        lheight = min(lheight, static_cast<float>(m));

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
        bool sameFallback = (m_lastFallbackFont == FallbackFont);
        bool sameFlow = (m_lastFlowDirection == this->FlowDirection);
        bool sameWrap = (m_lastWordWrapping == IsTextWrappingEnabled);
        bool sameFit = (m_lastIsCharacterFit == IsCharacterFitEnabled);
        bool sameText = (m_lastText == currentText) ||
            (m_lastText != nullptr && currentText != nullptr &&
             wcscmp(m_lastText->Data(), currentText->Data()) == 0);
        bool sameWeight = (m_lastFontWeight == FontWeight.Weight);
        bool sameStyle = (m_lastFontStyle == FontStyle);
        bool sameStretch = (m_lastFontStretch == FontStretch);
        bool sameSimulations = (m_lastSimulations == simulations);

        if (sameFontFace && sameTypography && sameFallback && sameFlow && sameWrap && sameFit &&
            sameFamily && sameFaceName && sameText && sameWeight && sameStyle && sameStretch && sameSimulations)
        {
            std::vector<DWRITE_FONT_AXIS_VALUE> newAxisValues;
            if (Axis != nullptr && Axis->Size > 0)
            {
                newAxisValues.reserve(Axis->Size);
                for (unsigned int i = 0; i < Axis->Size; ++i)
                    newAxisValues.push_back(Axis->GetAt(i)->GetDWriteValue());
            }
            else if (fontFace != nullptr && !fontFace->GetAxisValues().empty())
                newAxisValues = fontFace->GetAxisValues();

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

            // Can update axis values in-place only if newAxisValues is non-empty (clearing axes requires rebuild)
            if (!axisChanged || !newAxisValues.empty())
            {
                if (fontSize != m_lastFontSize)
                {
                    m_textLayout->SetFontSize(static_cast<FLOAT>(fontSize), DWRITE_TEXT_RANGE{ 0, textLength });
                    m_lastFontSize = fontSize;
                }

                if (lwidth != m_lastMaxWidth)
                {
                    m_textLayout->SetMaxWidth(lwidth);
                    m_lastMaxWidth = lwidth;
                }

                if (lheight != m_lastMaxHeight)
                {
                    m_textLayout->SetMaxHeight(lheight);
                    m_lastMaxHeight = lheight;
                }

                if (axisChanged)
                {
                    m_drawFontFace = fontFace->GetFontFace();
                    CoreDWTextServices::UpdateAxisValues(m_textLayout.Get(), newAxisValues, textLength);
                    m_lastAxisValues = newAxisValues;
                }

                if (SUCCEEDED(CoreDWTextServices::MeasureLayoutBounds(m_textLayout.Get(), &layoutBounds)))
                {
                    if (IsCharacterFitEnabled)
                    {
                        if (FAILED(CoreDWTextServices::MeasureInkBounds(m_textLayout.Get(), m_drawFontFace.Get(), &drawBounds)))
                            drawBounds = layoutBounds;
                    }
                    else
                    {
                        drawBounds = layoutBounds;
                    }

                    needsRebuild = false;
                    m_isStale = false;
                    m_render = true;
                }
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
            m_lastFamilyName = nullptr;  // invalidate fast path cache
            m_lastFaceName = nullptr;
            m_lastFontFace = nullptr;
            m_lastTypography = nullptr;
            m_lastFallbackFont = nullptr;
            m_lastText = nullptr;
            m_lastAxisValues.clear();
        }

        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;
        auto simulations = CoreDWTextServices::ResolveSimulations(StyleSimulations, fontFace);

        if (GlyphIndex >= 0)
        {
            m_drawFontFace = CoreDWTextServices::ResolveFontFace(fontFace, simulations, Axis);
            if (m_drawFontFace == nullptr || fontFace == nullptr)
                return Size(this->MinWidth, this->MinHeight);

            if (FAILED(CoreDWTextServices::MeasureGlyphBounds(
                m_drawFontFace.Get(),
                fontFace,
                static_cast<UINT16>(GlyphIndex),
                static_cast<float>(fontSize),
                &layoutBounds,
                &drawBounds)))
            {
                return Size(this->MinWidth, this->MinHeight);
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

            float lwidth = IsTextWrappingEnabled ? availW : static_cast<float>(m);
            float lheight = IsTextWrappingEnabled ? availH : static_cast<float>(m);
            lwidth = min(lwidth, static_cast<float>(m));
            lheight = min(lheight, static_cast<float>(m));

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

            // Cache layout key so future FontSize / MaxSize / Axis updates reuse m_textLayout
            m_lastFamilyName = fontFace->Properties != nullptr ? fontFace->Properties->FamilyName : nullptr;
            m_lastFaceName = fontFace->Properties != nullptr ? fontFace->Properties->FaceName : nullptr;
            m_lastFontFace = fontFace;
            m_lastTypography = Typography;
            m_lastFallbackFont = FallbackFont;
            m_lastFlowDirection = this->FlowDirection;
            m_lastWordWrapping = IsTextWrappingEnabled;
            m_lastIsCharacterFit = IsCharacterFitEnabled;
            m_lastText = text;
            m_lastFontSize = fontSize;
            m_lastMaxWidth = lwidth;
            m_lastMaxHeight = lheight;
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


    auto minh = min(drawBounds.Top, layoutBounds.Top);
    auto maxh = max(drawBounds.Bottom, layoutBounds.Bottom);

    auto minw = min(drawBounds.Left, layoutBounds.Left);
    auto maxw = max(drawBounds.Right, layoutBounds.Right);

    double h = maxh - minh;
    double w = maxw - minw;
    if (h <= 0) h = layoutBounds.Height > 0 ? layoutBounds.Height : 1.0;
    if (w <= 0) w = layoutBounds.Width > 0 ? layoutBounds.Width : 1.0;

    auto targetsize = Size(min(m, ceil(w + padW)), min(m, ceil(h + padH)));

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

Windows::Foundation::Size CharacterMapCX::Controls::DirectText::ArrangeOverride(Windows::Foundation::Size finalSize)
{
    Control::ArrangeOverride(finalSize);

    if (DesignMode::DesignModeEnabled)
        return finalSize;

    RenderToSurface(finalSize);
    return finalSize;
}

void CharacterMapCX::Controls::DirectText::ReleaseDrawingSurface()
{
    // Invalidate any in-flight background render before detaching the surface.
    ++m_renderGeneration;

    std::lock_guard<std::mutex> lock(m_surfaceMutex);
    try
    {
        if (m_surfaceBrush != nullptr)
            m_surfaceBrush->Surface = nullptr;

        if (m_drawingSurface != nullptr)
        {
            // Only delete the WinRT surface wrapper on the UI thread when no background task
            // is holding/drawing to it; otherwise let COM ref-counting release it safely.
            if (m_activeBackgroundDraws.load() == 0 && Dispatcher != nullptr && Dispatcher->HasThreadAccess)
                delete m_drawingSurface;
            m_drawingSurface = nullptr;
        }
    }
    catch (...) {}

    m_renderedWidth = 0;
    m_renderedHeight = 0;
}

namespace
{
    struct DirectTextRenderSnapshot
    {
        ComPtr<ICompositionDrawingSurfaceInterop> SurfaceInterop;
        ComPtr<IDWriteTextLayout> TextLayout;
        ComPtr<IDWriteFontFace3> DrawFontFace;
        Rect DrawBounds{};
        Rect LayoutBounds{};
        float ActualW = 0.0f;
        float ActualH = 0.0f;
        float DpiScale = 1.0f;
        UINT32 SurfaceWidth = 0;
        UINT32 SurfaceHeight = 0;
        double MinWidth = 1.0;
        double FontSize = 12.0;
        double PadLeft = 0.0;
        double PadTop = 0.0;
        double PadRight = 0.0;
        double PadBottom = 0.0;
        int GlyphIndex = -1;
        bool IsCharacterFitEnabled = false;
        bool RenderColor = true;
        bool DeferredLayout = false;
        bool IsColorFontEnabled = true;
        Windows::UI::Xaml::FlowDirection FlowDirection = Windows::UI::Xaml::FlowDirection::LeftToRight;
        DWriteColorRenderOption ColorRenderOption = DWriteColorRenderOption::Default;
        D2D1_COLOR_F ForegroundColor = D2D1::ColorF(D2D1::ColorF::White);
        D2D1_COLOR_F ActualForegroundColor = D2D1::ColorF(D2D1::ColorF::White);
        Windows::UI::Composition::CompositionGraphicsDevice^ GraphicsDevice = nullptr;
        TextLayoutParameters LayoutParams;
        std::shared_ptr<DeferredLayoutCache> LayoutCache;
    };

    void RenderSnapshotToContext(ID2D1DeviceContext* ctx, const DirectTextRenderSnapshot& snap, FLOAT offsetX, FLOAT offsetY)
    {
        auto db = snap.DrawBounds;
        auto lb = snap.LayoutBounds;
        double left = -min(db.Left, lb.Left) + snap.PadLeft;
        double top = -min(db.Top, lb.Top) + snap.PadTop;
        double scale = 1.0;

        if (snap.IsCharacterFitEnabled)
        {
            double padX = (std::max)(6.0, snap.ActualW * 0.04);
            double padY = (std::max)(6.0, snap.ActualH * 0.04);
            double availW = (std::max)(1.0, snap.ActualW - padX * 2.0);
            double availH = (std::max)(1.0, snap.ActualH - padY * 2.0);

            double totalW = (std::max)({ snap.MinWidth, static_cast<double>(lb.Width), static_cast<double>(db.Width), static_cast<double>(db.Right - (std::min)(0.0f, db.Left)) });
            double totalTop = (std::min)(static_cast<double>(lb.Top), static_cast<double>(db.Top));
            double totalBottom = (std::max)(static_cast<double>(lb.Bottom), static_cast<double>(db.Bottom));
            double totalH = (std::max)(1.0, totalBottom - totalTop);

            // Glyphs scale to fit available size (both scaling up and down) while maintaining baseline positioning
            scale = (std::min)(availW / totalW, availH / totalH);
            if (scale <= 0.0)
                scale = 1.0;

            double boxW = snap.ActualW / scale;
            double boxH = snap.ActualH / scale;

            // Horizontal positioning:
            // Center within advance width for regular characters.
            // Center ink bounds for zero-advance characters (combining marks / diacritics).
            if (lb.Width > 0)
            {
                left = (boxW - lb.Width) / 2.0 - lb.Left;
                if (left + db.Left < 0)
                    left = -db.Left;
                if (left + db.Right > boxW)
                    left = boxW - db.Right;
            }
            else
            {
                left = -db.Left + (boxW - db.Width) / 2.0;
            }

            // Vertical positioning:
            // Center the font line box so all characters share the exact same baseline.
            top = (boxH - lb.Height) / 2.0 - lb.Top;

            // Prevent clipping if extreme ascenders/descenders extend outside box
            if (top + db.Top < 0)
                top = -db.Top;
            if (top + db.Bottom > boxH)
                top = boxH - db.Bottom;
        }
        else if (snap.FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
        {
            left = -min(db.Left, lb.Left) + snap.ActualW - db.Width - 4 - snap.PadRight;
        }

        D2D1::Matrix3x2F transform =
            D2D1::Matrix3x2F::Scale(static_cast<float>(scale * snap.DpiScale), static_cast<float>(scale * snap.DpiScale)) *
            D2D1::Matrix3x2F::Translation(offsetX, offsetY);
        ctx->SetTransform(transform);

        bool color = snap.RenderColor && snap.ColorRenderOption != DWriteColorRenderOption::Monochrome;
        GlyphImageFormat preferredFormat = snap.RenderColor
            ? CoreDWTextServices::ToPreferredFormat(color, snap.ColorRenderOption)
            : GlyphImageFormat::TrueType;

        ComPtr<ID2D1SolidColorBrush> brush;
        ctx->CreateSolidColorBrush(snap.RenderColor ? snap.ForegroundColor : D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f), &brush);

        TextRenderParameters renderParams;
        renderParams.ForegroundBrush = brush.Get();
        renderParams.EnableColor = color;
        renderParams.PreferredFormat = preferredFormat;
        renderParams.OverrideFace = snap.DrawFontFace.Get();
        renderParams.IsMaskSurface = !snap.RenderColor;

        if (snap.GlyphIndex >= 0)
        {
            if (snap.DrawFontFace != nullptr)
            {
                UINT16 gIndex = static_cast<UINT16>(snap.GlyphIndex);
                FLOAT advance = static_cast<FLOAT>(lb.Width);

                DWRITE_GLYPH_RUN glyphRun{};
                glyphRun.fontFace = snap.DrawFontFace.Get();
                glyphRun.fontEmSize = static_cast<FLOAT>(snap.FontSize < 8 ? 8 : snap.FontSize);
                glyphRun.glyphCount = 1;
                glyphRun.glyphIndices = &gIndex;
                glyphRun.glyphAdvances = &advance;
                glyphRun.glyphOffsets = nullptr;
                glyphRun.isSideways = FALSE;
                glyphRun.bidiLevel = 0;

                CoreDWTextServices::DrawGlyphRun(ctx, { static_cast<float>(left), static_cast<float>(top) }, &glyphRun, renderParams);
            }
        }
        else
        {
            CoreDWTextServices::DrawTextLayout(ctx, snap.TextLayout.Get(), { static_cast<float>(left), static_cast<float>(top) }, renderParams);
        }
    }

    // Isolated D3D11/D2D1 device used off-thread for heavy color/SVG rasterization so slow
    // glyphs don't hold CompositionDeviceManager::GetRenderMutex() and block other UI draws.
    struct OffDeviceD2DRenderer
    {
        std::mutex Mutex;
        ComPtr<ID3D11Device> D3DDevice;
        ComPtr<ID2D1Factory5> D2DFactory;
        ComPtr<ID2D1Device> D2DDevice;
        ComPtr<ID2D1DeviceContext> D2DContext;

        bool RenderLocked(const DirectTextRenderSnapshot& snap, std::vector<uint8_t>& outPixels, UINT32& outPitch)
        {
            if (D3DDevice != nullptr && FAILED(D3DDevice->GetDeviceRemovedReason()))
            {
                D2DContext.Reset();
                D2DDevice.Reset();
                D3DDevice.Reset();
            }

            if (D2DContext == nullptr)
            {
                UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
                D3D_FEATURE_LEVEL levels[] = {
                    D3D_FEATURE_LEVEL_11_1,
                    D3D_FEATURE_LEVEL_11_0,
                    D3D_FEATURE_LEVEL_10_1,
                    D3D_FEATURE_LEVEL_10_0
                };
                D3D_FEATURE_LEVEL level{};
                HRESULT hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, flags, levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &D3DDevice, &level, nullptr);
                if (FAILED(hr))
                    hr = D3D11CreateDevice(nullptr, D3D_DRIVER_TYPE_WARP, nullptr, flags, levels, ARRAYSIZE(levels), D3D11_SDK_VERSION, &D3DDevice, &level, nullptr);
                if (FAILED(hr) || D3DDevice == nullptr)
                    return false;

                ComPtr<IDXGIDevice> dxgiDevice;
                if (FAILED(D3DDevice.As(&dxgiDevice)))
                    return false;

                if (D2DFactory == nullptr)
                {
                    D2D1_FACTORY_OPTIONS opts{};
                    if (FAILED(D2D1CreateFactory(D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof(ID2D1Factory5), &opts, &D2DFactory)))
                        return false;
                }

                if (FAILED(D2DFactory->CreateDevice(dxgiDevice.Get(), &D2DDevice)) || D2DDevice == nullptr)
                    return false;

                if (FAILED(D2DDevice->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &D2DContext)) || D2DContext == nullptr)
                    return false;
            }

            // Always use BGRA8 off-device even if the final surface were A8: D2D1DeviceContext
            // does not support A8_UNORM with D2D1_BITMAP_OPTIONS_TARGET on many drivers.
            D2D1_PIXEL_FORMAT pixelFormat = D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED);

            D2D1_BITMAP_PROPERTIES1 targetProps = D2D1::BitmapProperties1(
                D2D1_BITMAP_OPTIONS_TARGET,
                pixelFormat);

            ComPtr<ID2D1Bitmap1> targetBitmap;
            HRESULT hr = D2DContext->CreateBitmap(D2D1::SizeU(snap.SurfaceWidth, snap.SurfaceHeight), nullptr, 0, &targetProps, &targetBitmap);
            if (FAILED(hr) || targetBitmap == nullptr)
                return false;

            D2D1_BITMAP_PROPERTIES1 stagingProps = D2D1::BitmapProperties1(
                D2D1_BITMAP_OPTIONS_CPU_READ | D2D1_BITMAP_OPTIONS_CANNOT_DRAW,
                pixelFormat);

            ComPtr<ID2D1Bitmap1> stagingBitmap;
            hr = D2DContext->CreateBitmap(D2D1::SizeU(snap.SurfaceWidth, snap.SurfaceHeight), nullptr, 0, &stagingProps, &stagingBitmap);
            if (FAILED(hr) || stagingBitmap == nullptr)
                return false;

            D2DContext->SetTarget(targetBitmap.Get());
            D2DContext->BeginDraw();
            D2DContext->Clear(D2D1::ColorF(0, 0, 0, 0));
            RenderSnapshotToContext(D2DContext.Get(), snap, 0.0f, 0.0f);
            hr = D2DContext->EndDraw();
            D2DContext->SetTarget(nullptr);
            if (FAILED(hr))
            {
                if (hr == D2DERR_RECREATE_TARGET)
                {
                    D2DContext.Reset();
                    D2DDevice.Reset();
                    D3DDevice.Reset();
                }
                return false;
            }

            hr = stagingBitmap->CopyFromBitmap(nullptr, targetBitmap.Get(), nullptr);
            if (FAILED(hr))
                return false;

            D2D1_MAPPED_RECT mapped{};
            hr = stagingBitmap->Map(D2D1_MAP_OPTIONS_READ, &mapped);
            if (FAILED(hr) || mapped.bits == nullptr)
                return false;

            outPitch = mapped.pitch;
            size_t totalBytes = static_cast<size_t>(mapped.pitch) * snap.SurfaceHeight;
            outPixels.resize(totalBytes);
            memcpy(outPixels.data(), mapped.bits, totalBytes);
            stagingBitmap->Unmap();

            return true;
        }
    };

    template <size_t N>
    class OffDeviceRendererPool
    {
    public:
        bool Render(const DirectTextRenderSnapshot& snap, std::vector<uint8_t>& outPixels, UINT32& outPitch)
        {
            // Fast path: grab the first idle slot without blocking on a busy/stalled font.
            for (size_t i = 0; i < N; ++i)
            {
                std::unique_lock<std::mutex> lock(m_slots[i].Mutex, std::try_to_lock);
                if (lock.owns_lock())
                    return m_slots[i].RenderLocked(snap, outPixels, outPitch);
            }

            // Fallback if all N slots are currently busy (e.g. deferred mono->color upgrade).
            size_t idx = m_next.fetch_add(1, std::memory_order_relaxed) % N;
            std::lock_guard<std::mutex> lock(m_slots[idx].Mutex);
            return m_slots[idx].RenderLocked(snap, outPixels, outPitch);
        }

    private:
        std::array<OffDeviceD2DRenderer, N> m_slots;
        std::atomic<size_t> m_next{ 0 };
    };

    bool RenderSnapshotOffDevice(const DirectTextRenderSnapshot& snap, std::vector<uint8_t>& outPixels, UINT32& outPitch)
    {
        if (snap.SurfaceWidth == 0 || snap.SurfaceHeight == 0)
            return false;

        // Separate pools (4 monochrome, 2 color) so slow/difficult fonts never block the entire queue.
        static OffDeviceRendererPool<4> s_monoPool;
        static OffDeviceRendererPool<2> s_colorPool;

        return snap.RenderColor
            ? s_colorPool.Render(snap, outPixels, outPitch)
            : s_monoPool.Render(snap, outPixels, outPitch);
    }

    bool DrawSnapshotToSurface(DirectTextRenderSnapshot snap, bool offDevice = false, Platform::WeakReference weakThis = nullptr, uint64_t gen = 0)
    {
        if (snap.SurfaceInterop == nullptr)
            return false;

        std::unique_lock<std::mutex> layoutLock;
        if (snap.DeferredLayout)
        {
            if (snap.LayoutParams.Text == nullptr || snap.LayoutParams.Text->Length() == 0 || snap.LayoutParams.FontFace == nullptr)
                return false;

            if (snap.LayoutCache != nullptr)
                layoutLock = std::unique_lock<std::mutex>(snap.LayoutCache->Mutex);

            auto& p = snap.LayoutParams;
            bool reused = false;
            if (snap.LayoutCache != nullptr && snap.LayoutCache->TextLayout != nullptr)
            {
                auto& c = *snap.LayoutCache;
                bool sameText = (c.Text == p.Text) ||
                    (c.Text != nullptr && p.Text != nullptr && wcscmp(c.Text->Data(), p.Text->Data()) == 0);
                if (sameText &&
                    c.FontFace == p.FontFace &&
                    c.TypographyInfo == p.TypographyInfo &&
                    c.FallbackFont == p.FallbackFont &&
                    c.FlowDirection == p.FlowDirection &&
                    c.WordWrapping == p.WordWrapping &&
                    c.FontWeight.Weight == p.FontWeight.Weight &&
                    c.FontStyle == p.FontStyle &&
                    c.FontStretch == p.FontStretch &&
                    c.Simulations == p.Simulations)
                {
                    bool axisChanged = (c.AxisValues.size() != p.ExplicitAxisValues.size());
                    for (size_t i = 0; i < c.AxisValues.size() && !axisChanged; ++i)
                    {
                        if (c.AxisValues[i].axisTag != p.ExplicitAxisValues[i].axisTag ||
                            c.AxisValues[i].value != p.ExplicitAxisValues[i].value)
                            axisChanged = true;
                    }

                    if (!axisChanged || !p.ExplicitAxisValues.empty())
                    {
                        UINT32 len = p.Text->Length();
                        if (c.FontSize != p.FontSize)
                        {
                            c.TextLayout->SetFontSize(p.FontSize, DWRITE_TEXT_RANGE{ 0, len });
                            c.FontSize = p.FontSize;
                        }
                        if (c.MaxWidth != p.MaxWidth)
                        {
                            c.TextLayout->SetMaxWidth(p.MaxWidth);
                            c.MaxWidth = p.MaxWidth;
                        }
                        if (c.MaxHeight != p.MaxHeight)
                        {
                            c.TextLayout->SetMaxHeight(p.MaxHeight);
                            c.MaxHeight = p.MaxHeight;
                        }
                        if (axisChanged)
                        {
                            CoreDWTextServices::UpdateAxisValues(c.TextLayout.Get(), p.ExplicitAxisValues, len);
                            c.AxisValues = p.ExplicitAxisValues;
                        }

                        snap.TextLayout = c.TextLayout;
                        snap.DrawFontFace = c.DrawFontFace;
                        reused = true;
                    }
                }
            }

            if (!reused)
            {
                HRESULT layoutHr = CoreDWTextServices::CreateLayout(p, &snap.TextLayout, &snap.DrawFontFace);
                if (FAILED(layoutHr) || snap.TextLayout == nullptr)
                    return false;

                if (snap.LayoutCache != nullptr)
                {
                    auto& c = *snap.LayoutCache;
                    c.TextLayout = snap.TextLayout;
                    c.DrawFontFace = snap.DrawFontFace;
                    c.Text = p.Text;
                    c.FontFace = p.FontFace;
                    c.TypographyInfo = p.TypographyInfo;
                    c.FallbackFont = p.FallbackFont;
                    c.FlowDirection = p.FlowDirection;
                    c.WordWrapping = p.WordWrapping;
                    c.FontWeight = p.FontWeight;
                    c.FontStyle = p.FontStyle;
                    c.FontStretch = p.FontStretch;
                    c.Simulations = p.Simulations;
                    c.AxisValues = p.ExplicitAxisValues;
                    c.FontSize = p.FontSize;
                    c.MaxWidth = p.MaxWidth;
                    c.MaxHeight = p.MaxHeight;
                }
            }

            if (FAILED(CoreDWTextServices::MeasureLayoutBounds(snap.TextLayout.Get(), &snap.LayoutBounds)))
                return false;

            snap.DrawBounds = snap.LayoutBounds;

            // Deferred layouts bypass the UI-thread ShouldRenderColor() check, so probe fallback
            // runs here and upgrade the A8 mask surface to BGRA8 if a fallback glyph is colored.
            if (!snap.RenderColor && snap.IsColorFontEnabled &&
                snap.ColorRenderOption != DWriteColorRenderOption::Monochrome &&
                snap.LayoutParams.FallbackFont != nullptr)
            {
                RunColorChecker checker;
                snap.TextLayout->Draw(nullptr, &checker, 0.0f, 0.0f);
                if (checker.HasColorFont && snap.GraphicsDevice != nullptr && snap.SurfaceWidth > 0 && snap.SurfaceHeight > 0)
                {
                    auto pThis = weakThis.Resolve<DirectText>();
                    if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                        return false;

                    auto colorSurface = snap.GraphicsDevice->CreateDrawingSurface(
                        Size(static_cast<float>(snap.SurfaceWidth), static_cast<float>(snap.SurfaceHeight)),
                        Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                        Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
                    if (colorSurface == nullptr)
                        return false;

                    ComPtr<ICompositionDrawingSurfaceInterop> colorInterop;
                    if (FAILED(reinterpret_cast<IUnknown*>(colorSurface)->QueryInterface(IID_PPV_ARGS(&colorInterop))) || colorInterop == nullptr)
                        return false;

                    if (!pThis->UpgradeToColorSurface(gen, colorSurface))
                        return false;

                    snap.SurfaceInterop = colorInterop;
                    snap.RenderColor = true;
                    snap.ForegroundColor = snap.ActualForegroundColor;
                }
            }

            if (gen != 0)
            {
                auto pThis = weakThis.Resolve<DirectText>();
                if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                    return false;
            }
        }
        else if (gen != 0)
        {
            auto pThis = weakThis.Resolve<DirectText>();
            if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                return false;
        }

        // Rasterize off-device (using separate mono/color lanes) so background threads never hold
        // CompositionDeviceManager::GetRenderMutex() during DrawTextLayout.
        std::vector<uint8_t> offDevicePixels;
        UINT32 offDevicePitch = 0;
        bool hasOffDevicePixels = offDevice && RenderSnapshotOffDevice(snap, offDevicePixels, offDevicePitch);

        if (hasOffDevicePixels && gen != 0)
        {
            auto pThis = weakThis.Resolve<DirectText>();
            if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                return false;
        }

        POINT updateOffset{};
        ComPtr<ID2D1DeviceContext> ctx;

        std::lock_guard<std::mutex> renderLock(CompositionDeviceManager::GetRenderMutex());

        HRESULT hr = snap.SurfaceInterop->BeginDraw(nullptr, IID_PPV_ARGS(&ctx), &updateOffset);
        if (FAILED(hr))
        {
            if (CompositionDeviceManager::IsDeviceLostHResult(hr))
                CompositionDeviceManager::HandleDeviceLost();
            return false;
        }

        if (ctx != nullptr)
        {
            ctx->Clear(D2D1::ColorF(0, 0, 0, 0));

            if (hasOffDevicePixels)
            {
                D2D1_BITMAP_PROPERTIES1 uploadProps = D2D1::BitmapProperties1(
                    D2D1_BITMAP_OPTIONS_NONE,
                    D2D1::PixelFormat(DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE_PREMULTIPLIED));

                ComPtr<ID2D1Bitmap1> uploadBitmap;
                if (SUCCEEDED(ctx->CreateBitmap(D2D1::SizeU(snap.SurfaceWidth, snap.SurfaceHeight), offDevicePixels.data(), offDevicePitch, &uploadProps, &uploadBitmap)) && uploadBitmap != nullptr)
                {
                    ctx->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y)));
                    ctx->DrawBitmap(uploadBitmap.Get(), D2D1::RectF(0.0f, 0.0f, static_cast<FLOAT>(snap.SurfaceWidth), static_cast<FLOAT>(snap.SurfaceHeight)));
                }
                else
                {
                    RenderSnapshotToContext(ctx.Get(), snap, static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y));
                }
            }
            else
            {
                RenderSnapshotToContext(ctx.Get(), snap, static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y));
            }

            snap.SurfaceInterop->EndDraw();
        }

        return false;
    }

    struct BackgroundRenderJob
    {
        Platform::WeakReference WeakThis;
        uint64_t Generation = 0;
        DirectTextRenderSnapshot Snapshot;
        bool WasHidden = false;
    };

    // Bounded worker pool that pops from the middle of the queue so visible items in
    // ItemsWrapGrid's realized cache window (above + visible + below) render before
    // off-screen cache items, without flooding D2D/Composition during fast scrolls.
    class BackgroundRenderQueue
    {
    public:
        static BackgroundRenderQueue& MonoInstance()
        {
            static BackgroundRenderQueue s_instance(4);
            return s_instance;
        }

        static BackgroundRenderQueue& ColorInstance()
        {
            static BackgroundRenderQueue s_instance(2);
            return s_instance;
        }

        explicit BackgroundRenderQueue(size_t maxWorkers)
            : m_maxWorkers(maxWorkers)
        {
        }

        void Enqueue(BackgroundRenderJob&& job)
        {
            bool spawnWorker = false;
            {
                std::lock_guard<std::mutex> lock(m_mutex);
                m_queue.push_back(std::move(job));
                if (m_activeWorkers < m_maxWorkers)
                {
                    ++m_activeWorkers;
                    spawnWorker = true;
                }
            }

            if (spawnWorker)
                SpawnWorker();
        }

    private:

        void SpawnWorker()
        {
            concurrency::create_task([this]()
            {
                for (;;)
                {
                    BackgroundRenderJob job;
                    {
                        std::lock_guard<std::mutex> lock(m_mutex);
                        if (m_queue.empty())
                        {
                            --m_activeWorkers;
                            return;
                        }

                        // Consume from the middle of the queue (visible viewport sits between
                        // the leading and trailing ItemsWrapGrid cache buffers).
                        size_t mid = m_queue.size() / 2;
                        job = std::move(m_queue[mid]);
                        m_queue.erase(m_queue.begin() + mid);
                    }

                    struct DrawCounterGuard
                    {
                        Platform::WeakReference WeakThis;
                        ~DrawCounterGuard()
                        {
                            if (auto p = WeakThis.Resolve<DirectText>())
                                p->DecrementActiveBackgroundDraws();
                        }
                    } guard{ job.WeakThis };

                    auto pThis = job.WeakThis.Resolve<DirectText>();
                    if (pThis == nullptr || !pThis->IsGenerationCurrent(job.Generation))
                        continue;

                    DrawSnapshotToSurface(job.Snapshot, true, job.WeakThis, job.Generation);
                    if (!job.WasHidden)
                        continue;

                    // Composition properties (like IsVisible) are thread-safe and commit directly without Dispatcher.
                    if (auto pThis2 = job.WeakThis.Resolve<DirectText>())
                        pThis2->ShowVisualIfCurrent(job.Generation);
                }
            });
        }

        std::mutex m_mutex;
        std::deque<BackgroundRenderJob> m_queue;
        size_t m_activeWorkers = 0;
        size_t m_maxWorkers = 1;
    };
}

void DirectText::RenderToSurface(Windows::Foundation::Size finalSize)
{
    if (!m_render || (!m_deferredLayout && m_textLayout == nullptr && GlyphIndex < 0))
    {
        if (m_spriteVisual != nullptr)
            m_spriteVisual->Brush = nullptr;
        ReleaseDrawingSurface();
        m_needsRender = false;
        return;
    }

    float actualW = finalSize.Width > 0.0f ? finalSize.Width : static_cast<float>(ActualWidth);
    float actualH = finalSize.Height > 0.0f ? finalSize.Height : static_cast<float>(ActualHeight);
    if (actualW <= 0.0f || actualH <= 0.0f)
        return;

    auto visual = ElementCompositionPreview::GetElementVisual(this);
    auto compositor = visual != nullptr ? visual->Compositor : nullptr;
    if (compositor == nullptr)
        return;

    if (m_spriteVisual == nullptr)
    {
        m_spriteVisual = compositor->CreateSpriteVisual();
        ElementCompositionPreview::SetElementChildVisual(this, m_spriteVisual);
    }

    float dpiScale = CompositionDeviceManager::GetDpiScale(this->Dispatcher);

    Windows::Foundation::Size fixedSurfaceSize = SurfaceSize;
    float surfaceW = (fixedSurfaceSize.Width > 0.0f) ? fixedSurfaceSize.Width : actualW;
    float surfaceH = (fixedSurfaceSize.Height > 0.0f) ? fixedSurfaceSize.Height : actualH;

    LONG requiredWidth = (std::max)(1L, (std::min)(8192L, static_cast<LONG>(std::ceil(surfaceW * dpiScale))));
    LONG requiredHeight = (std::max)(1L, (std::min)(8192L, static_cast<LONG>(std::ceil(surfaceH * dpiScale))));

    bool renderColor = ShouldRenderColor();

    auto wireBrushes = [&]()
    {
        if (m_surfaceBrush != nullptr)
            m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
        m_spriteVisual->Size = float2(actualW, actualH);

        CompositionDeviceManager::ApplySurfaceOrMaskBrush(
            compositor,
            m_spriteVisual,
            m_surfaceBrush,
            m_maskBrush,
            m_colorBrush,
            !renderColor,
            GetForegroundColor());
    };

    if (!m_needsRender && m_drawingSurface != nullptr && m_renderedColor == renderColor &&
        m_renderedWidth >= requiredWidth && m_renderedHeight >= requiredHeight &&
        m_renderedWidth <= requiredWidth + 64 && m_renderedHeight <= requiredHeight + 64)
    {
        wireBrushes();
        return;
    }

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return;

    // Reuse the existing surface if it fits within a 64px slack window to avoid
    // reallocating CompositionDrawingSurface on every FontSize slider tick.
    bool needNewSurface = m_drawingSurface == nullptr ||
        m_renderedColor != renderColor ||
        m_renderedWidth < requiredWidth ||
        m_renderedHeight < requiredHeight ||
        m_renderedWidth > requiredWidth + 64 ||
        m_renderedHeight > requiredHeight + 64;

    if (needNewSurface)
    {
        ReleaseDrawingSurface();
        LONG allocW = requiredWidth;
        // Bucket height to 64px multiples (when not fixed) so growing text doesn't reallocate every step.
        LONG allocH = (fixedSurfaceSize.Height > 0.0f)
            ? requiredHeight
            : (std::min)(8192L, ((requiredHeight + 63L) / 64L) * 64L);
        // Monochrome text uses an A8 mask + CompositionMaskBrush (4x less VRAM, instant color changes);
        // color/SVG/COLR fonts require full BGRA8.
        auto pixelFormat = renderColor
            ? Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized
            : Windows::Graphics::DirectX::DirectXPixelFormat::A8UIntNormalized;
        m_drawingSurface = graphicsDevice->CreateDrawingSurface(
            Size(static_cast<float>(allocW), static_cast<float>(allocH)),
            pixelFormat,
            Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
        m_renderedWidth = allocW;
        m_renderedHeight = allocH;
        m_renderedColor = renderColor;
    }

    if (m_drawingSurface == nullptr)
        return;

    ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
    if (FAILED(reinterpret_cast<IUnknown*>(m_drawingSurface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop))))
        return;

    CompositionDeviceManager::EnsureSurfaceBrush(compositor, m_surfaceBrush, m_drawingSurface);

    wireBrushes();

    D2D1_COLOR_F actualFgColor = D2D1::ColorF(D2D1::ColorF::White);
    if (auto fgScb = dynamic_cast<SolidColorBrush^>(this->Foreground))
    {
        auto c = fgScb->Color;
        actualFgColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(fgScb->Opacity));
    }
    D2D1_COLOR_F fgColor = renderColor ? actualFgColor : D2D1::ColorF(D2D1::ColorF::White);

    Thickness padding = this->Padding;
    DirectTextRenderSnapshot snapshot;
    snapshot.SurfaceInterop = surfaceInterop;
    snapshot.TextLayout = m_textLayout;
    snapshot.DrawFontFace = m_drawFontFace;
    snapshot.DrawBounds = drawBounds;
    snapshot.LayoutBounds = layoutBounds;
    snapshot.ActualW = actualW;
    snapshot.ActualH = actualH;
    snapshot.DpiScale = dpiScale;
    snapshot.SurfaceWidth = static_cast<UINT32>(m_renderedWidth);
    snapshot.SurfaceHeight = static_cast<UINT32>(m_renderedHeight);
    snapshot.MinWidth = m_minWidth;
    snapshot.FontSize = FontSize;
    snapshot.PadLeft = (std::max)(0.0, padding.Left);
    snapshot.PadTop = (std::max)(0.0, padding.Top);
    snapshot.PadRight = (std::max)(0.0, padding.Right);
    snapshot.PadBottom = (std::max)(0.0, padding.Bottom);
    snapshot.GlyphIndex = GlyphIndex;
    snapshot.IsCharacterFitEnabled = IsCharacterFitEnabled;
    snapshot.RenderColor = renderColor;
    snapshot.DeferredLayout = m_deferredLayout;
    snapshot.IsColorFontEnabled = IsColorFontEnabled;
    snapshot.FlowDirection = this->FlowDirection;
    snapshot.ColorRenderOption = ColorRenderOption;
    snapshot.ForegroundColor = fgColor;
    snapshot.ActualForegroundColor = actualFgColor;

    if (m_deferredLayout)
    {
        snapshot.GraphicsDevice = graphicsDevice;
        snapshot.LayoutCache = m_deferredLayoutCache;
        auto fontFace = FontFace;
        auto fontSize = 8.0 > FontSize ? 8.0 : FontSize;
        snapshot.LayoutParams.Text = Text;
        snapshot.LayoutParams.FontFace = fontFace;
        snapshot.LayoutParams.FontSize = static_cast<float>(fontSize);
        snapshot.LayoutParams.FontWeight = FontWeight;
        snapshot.LayoutParams.FontStyle = FontStyle;
        snapshot.LayoutParams.FontStretch = FontStretch;
        snapshot.LayoutParams.Simulations = CoreDWTextServices::ResolveSimulations(StyleSimulations, fontFace);
        snapshot.LayoutParams.TypographyInfo = Typography;
        snapshot.LayoutParams.FallbackFont = FallbackFont;
        snapshot.LayoutParams.FlowDirection = this->FlowDirection;
        snapshot.LayoutParams.WordWrapping = IsTextWrappingEnabled;
        snapshot.LayoutParams.EnableTrimming = IsTextWrappingEnabled;
        snapshot.LayoutParams.IsCharacterFit = false;
        snapshot.LayoutParams.MaxWidth = m_deferredAvailW;
        snapshot.LayoutParams.MaxHeight = m_deferredAvailH;
        if (Axis != nullptr && Axis->Size > 0)
        {
            snapshot.LayoutParams.ExplicitAxisValues.reserve(Axis->Size);
            for (unsigned int i = 0; i < Axis->Size; ++i)
                snapshot.LayoutParams.ExplicitAxisValues.push_back(Axis->GetAt(i)->GetDWriteValue());
        }
        else if (fontFace != nullptr && !fontFace->GetAxisValues().empty())
        {
            snapshot.LayoutParams.ExplicitAxisValues = fontFace->GetAxisValues();
        }
    }

    m_needsRender = false;

    if (IsBackgroundRenderEnabled)
    {
        // Bump generation immediately so any in-flight background render for an older state
        // aborts before touching the surface or flipping IsVisible back on.
        uint64_t gen = ++m_renderGeneration;
        ++m_activeBackgroundDraws;
        // Only hide the visual when FontFace changes (prevents stale font flash on container recycle);
        // keep it visible during Text/size updates for smooth updates without flashing.
        bool hideVisual = HasContentChangedForVisibility();
        if (hideVisual)
            m_spriteVisual->IsVisible = false;
        bool wasHidden = !m_spriteVisual->IsVisible;
        m_lastShownFontFace = FontFace;
        BackgroundRenderJob job;
        job.WeakThis = Platform::WeakReference(this);
        job.Generation = gen;
        job.Snapshot = std::move(snapshot);
        job.WasHidden = wasHidden;
        auto& queue = job.Snapshot.RenderColor
            ? BackgroundRenderQueue::ColorInstance()
            : BackgroundRenderQueue::MonoInstance();
        queue.Enqueue(std::move(job));
    }
    else
    {
        m_lastShownFontFace = FontFace;
        DrawSnapshotToSurface(snapshot);
        if (m_spriteVisual != nullptr)
            m_spriteVisual->IsVisible = true;
    }
}





