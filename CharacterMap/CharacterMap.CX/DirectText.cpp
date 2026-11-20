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

DependencyProperty^ GlyphBoundsOptions::_ShowAscentProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowCapHeightProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowXHeightProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowBaselineProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowDescentProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowAdvanceBoundsProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_ShowInkBoundsProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_AscentBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_CapHeightBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_XHeightBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_BaselineBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_DescentBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_AdvanceBoundsBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_InkBoundsBrushProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_LineThicknessProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_DrawLinesOverGlyphProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_AscentLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_CapHeightLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_XHeightLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_BaselineLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_DescentLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_AdvanceBoundsLabelProperty = nullptr;
DependencyProperty^ GlyphBoundsOptions::_InkBoundsLabelProperty = nullptr;

GlyphBoundsOptions::GlyphBoundsOptions()
{
    RegisterDependencyProperties();
}

void GlyphBoundsOptions::RegisterDependencyProperties()
{
    static bool registered = false;
    if (registered)
        return;

    registered = true;

    auto cb = ref new PropertyChangedCallback(&GlyphBoundsOptions::OnOptionChanged);

    _ShowAscentProperty = DependencyProperty::Register("ShowAscent", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(true, cb));
    _ShowCapHeightProperty = DependencyProperty::Register("ShowCapHeight", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(true, cb));
    _ShowXHeightProperty = DependencyProperty::Register("ShowXHeight", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(false, cb));
    _ShowBaselineProperty = DependencyProperty::Register("ShowBaseline", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(true, cb));
    _ShowDescentProperty = DependencyProperty::Register("ShowDescent", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(true, cb));
    _ShowAdvanceBoundsProperty = DependencyProperty::Register("ShowAdvanceBounds", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(false, cb));
    _ShowInkBoundsProperty = DependencyProperty::Register("ShowInkBounds", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(false, cb));

    _AscentBrushProperty = DependencyProperty::Register("AscentBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _CapHeightBrushProperty = DependencyProperty::Register("CapHeightBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _XHeightBrushProperty = DependencyProperty::Register("XHeightBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _BaselineBrushProperty = DependencyProperty::Register("BaselineBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _DescentBrushProperty = DependencyProperty::Register("DescentBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _AdvanceBoundsBrushProperty = DependencyProperty::Register("AdvanceBoundsBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));
    _InkBoundsBrushProperty = DependencyProperty::Register("InkBoundsBrush", SolidColorBrush::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(nullptr, cb));

    _LineThicknessProperty = DependencyProperty::Register("LineThickness", double::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(1.5, cb));
    _DrawLinesOverGlyphProperty = DependencyProperty::Register("DrawLinesOverGlyph", bool::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(true, cb));

    _AscentLabelProperty = DependencyProperty::Register("AscentLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Ascent"), cb));
    _CapHeightLabelProperty = DependencyProperty::Register("CapHeightLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Cap Height"), cb));
    _XHeightLabelProperty = DependencyProperty::Register("XHeightLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"x-Height"), cb));
    _BaselineLabelProperty = DependencyProperty::Register("BaselineLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Baseline"), cb));
    _DescentLabelProperty = DependencyProperty::Register("DescentLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Descent"), cb));
    _AdvanceBoundsLabelProperty = DependencyProperty::Register("AdvanceBoundsLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Advance Bounds"), cb));
    _InkBoundsLabelProperty = DependencyProperty::Register("InkBoundsLabel", Platform::String::typeid, GlyphBoundsOptions::typeid, ref new PropertyMetadata(ref new Platform::String(L"Ink Bounds"), cb));
}

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
DependencyProperty^ DirectText::_RenderGlyphBoundsProperty = nullptr;
DependencyProperty^ DirectText::_GlyphBoundsOptionsProperty = nullptr;
DependencyProperty^ DirectText::_TrimLeadingLineGapProperty = nullptr;

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
        UpdateGlyphBoundsOptionsSubscription(nullptr);
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

void DirectText::OnGlyphBoundsOptionsChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    if (auto c = dynamic_cast<DirectText^>(d))
    {
        c->UpdateGlyphBoundsOptionsSubscription(dynamic_cast<Controls::GlyphBoundsOptions^>(e->NewValue));
        if (!c->BlockUpdates && c->RenderGlyphBounds)
            c->Update();
    }
}

void DirectText::UpdateGlyphBoundsOptionsSubscription(Controls::GlyphBoundsOptions^ newOptions)
{
    if (m_subscribedBoundsOptions == newOptions)
        return;

    if (m_subscribedBoundsOptions != nullptr)
    {
        try { m_subscribedBoundsOptions->Changed -= m_glyphBoundsOptionsToken; }
        catch (...) {}
        m_subscribedBoundsOptions = nullptr;
    }

    if (newOptions != nullptr)
    {
        auto weakThis = Platform::WeakReference(this);
        m_glyphBoundsOptionsToken = newOptions->Changed += ref new Windows::Foundation::EventHandler<Platform::Object^>(
            [weakThis](Platform::Object^, Platform::Object^)
            {
                if (auto pThis = weakThis.Resolve<DirectText>())
                {
                    if (!pThis->BlockUpdates && pThis->RenderGlyphBounds)
                    {
                        pThis->m_needsRender = true;
                        pThis->InvalidateArrange();
                    }
                }
            });
        m_subscribedBoundsOptions = newOptions;
    }
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
    ++m_renderGeneration;
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
    HideBoundsToolTip();
    m_boundsHitTargets.clear();
    ClearLayoutCache();
    m_isStale = true;
    m_needsRender = true;
}

void DirectText::CancelHideBoundsToolTip()
{
    if (m_toolTipHideTimer != nullptr && m_toolTipHideTimer->IsEnabled)
        m_toolTipHideTimer->Stop();
}

void DirectText::HideBoundsToolTip()
{
    CancelHideBoundsToolTip();
    if (m_boundsToolTip != nullptr && m_boundsToolTip->IsOpen)
        m_boundsToolTip->IsOpen = false;
    m_activeBoundsLabel = nullptr;
}

void DirectText::ScheduleHideBoundsToolTip()
{
    if (m_boundsToolTip == nullptr || !m_boundsToolTip->IsOpen)
        return;

    if (m_toolTipHideTimer == nullptr)
    {
        m_toolTipHideTimer = ref new DispatcherTimer();
        TimeSpan interval{};
        interval.Duration = 100LL * 10000LL; // 100 ms
        m_toolTipHideTimer->Interval = interval;

        auto weakThis = Platform::WeakReference(this);
        m_toolTipHideTimer->Tick += ref new EventHandler<Platform::Object^>(
            [weakThis](Platform::Object^, Platform::Object^)
            {
                if (auto pThis = weakThis.Resolve<DirectText>())
                    pThis->HideBoundsToolTip();
            });
    }

    if (!m_toolTipHideTimer->IsEnabled)
        m_toolTipHideTimer->Start();
}

void DirectText::UpdateRootHitTestBackground()
{
    if (m_rootBorder == nullptr)
        return;

    if (RenderGlyphBounds)
    {
      /*  if (m_rootBorder->Background == nullptr)
            m_rootBorder->Background = ref new SolidColorBrush(Windows::UI::Colors::Transparent);*/
    }
    else
    {
       /* if (m_rootBorder->Background != nullptr)
            m_rootBorder->Background = nullptr;*/
        HideBoundsToolTip();
    }
}

void DirectText::OnPointerMoved(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e)
{
    Control::OnPointerMoved(e);

    if (!RenderGlyphBounds || m_boundsHitTargets.empty() || e == nullptr)
    {
        HideBoundsToolTip();
        return;
    }

    Point pt = e->GetCurrentPoint(this)->Position;
    double tol = m_boundsHitTolerance;
    double bestDist = tol + 1.0;
    const BoundsHitTarget* bestTarget = nullptr;

    for (const auto& t : m_boundsHitTargets)
    {
        double dist = tol + 1.0;
        if (t.Kind == 0)
        {
            dist = std::abs(static_cast<double>(pt.Y) - t.Pos);
        }
        else if (t.Kind == 1)
        {
            dist = std::abs(static_cast<double>(pt.X) - t.Pos);
        }
        else if (t.Kind == 2)
        {
            Rect box = t.Box;
            double l = box.Left, r = box.Right, top = box.Top, b = box.Bottom;
            if (pt.X >= l - tol && pt.X <= r + tol && pt.Y >= top - tol && pt.Y <= b + tol)
            {
                double dx = (std::min)(std::abs(pt.X - l), std::abs(pt.X - r));
                double dy = (std::min)(std::abs(pt.Y - top), std::abs(pt.Y - b));
                dist = (std::min)(dx, dy);
            }
        }

        if (dist <= tol && dist < bestDist)
        {
            bestDist = dist;
            bestTarget = &t;
        }
    }

    if (bestTarget != nullptr && bestTarget->Label != nullptr)
    {
        CancelHideBoundsToolTip();

        if (m_boundsToolTip == nullptr)
        {
            m_boundsToolTip = ref new ToolTip();
            m_boundsToolTip->Placement = Primitives::PlacementMode::Top;
            m_boundsToolTip->PlacementTarget = this;
            m_boundsToolTip->RenderTransform = ref new TranslateTransform();
            ToolTipService::SetToolTip(this, m_boundsToolTip);
        }

        auto ttTransform = dynamic_cast<TranslateTransform^>(m_boundsToolTip->RenderTransform);

        Platform::String^ bestLabel = bestTarget->Label;
        double lineAnchorY = static_cast<double>(pt.Y);
        if (bestTarget->Kind == 0)
            lineAnchorY = bestTarget->Pos;
        else if (bestTarget->Kind == 2)
        {
            Rect box = bestTarget->Box;
            lineAnchorY = box.Top;
        }

        bool labelChanged = (m_activeBoundsLabel != bestLabel);
        bool anchorYChanged = std::abs(m_toolTipAnchorY - lineAnchorY) > 0.5;
        bool wasOpen = m_boundsToolTip->IsOpen;

        if (!wasOpen || labelChanged || (bestTarget->Kind != 1 && anchorYChanged))
        {
            if (wasOpen)
                m_boundsToolTip->IsOpen = false;

            m_activeBoundsLabel = bestLabel;
            m_boundsToolTip->Content = bestLabel;
            m_toolTipAnchorX = pt.X;
            m_toolTipAnchorY = lineAnchorY;
            m_boundsToolTip->PlacementRect = Rect(pt.X - 1.0f, static_cast<float>(lineAnchorY) - 12.0f, 2.0f, 2.0f);
            m_boundsToolTip->HorizontalOffset = 0.0;
            m_boundsToolTip->VerticalOffset = 0.0;
            if (ttTransform != nullptr)
            {
                ttTransform->X = 0.0;
                ttTransform->Y = 0.0;
            }
            m_boundsToolTip->IsOpen = true;
        }
        else if (ttTransform != nullptr)
        {
            ttTransform->X = pt.X - m_toolTipAnchorX;
            ttTransform->Y = (bestTarget->Kind == 1) ? (pt.Y - m_toolTipAnchorY) : 0.0;
        }
    }
    else
    {
        ScheduleHideBoundsToolTip();
    }
}

void DirectText::OnPointerExited(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e)
{
    Control::OnPointerExited(e);
    ScheduleHideBoundsToolTip();
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
        IDWriteFontFace* IgnoreFontFace = nullptr;

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

            if (glyphRun != nullptr && glyphRun->fontFace != nullptr && glyphRun->fontFace != IgnoreFontFace)
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
    if (RenderGlyphBounds)
        return true;

    if (!IsColorFontEnabled || ColorRenderOption == DWriteColorRenderOption::Monochrome)
        return false;

    if (m_forceColorSurface)
        return true;

    if (FontFace != nullptr && FontFace->Properties != nullptr && FontFace->Properties->IsColorFont)
    {
        if (FontFace->Properties->HasBitmapSVGGlyphs && !FontFace->Properties->HasColorBitmapOutlines)
            return false;
        return true;
    }

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

    m_rootBorder = dynamic_cast<Border^>(GetTemplateChild("Root"));
    UpdateRootHitTestBackground();
    Update();
}

Windows::Foundation::Size CharacterMapCX::Controls::DirectText::MeasureOverride(Windows::Foundation::Size size)
{
    if (DesignMode::DesignModeEnabled)
        return size;

    try
    {
        bool hasText = FontFace != nullptr && (GlyphIndex >= 0 || (Text != nullptr && Text->Length() > 0));

        if (!hasText)
        {
            ClearLayoutCache();
            m_render = false;
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

    bool renderBounds = RenderGlyphBounds;
    bool trimLeading = TrimLeadingLineGap;
    bool fitToBounds = IsCharacterFitEnabled || renderBounds;

    Windows::Foundation::Size fixedSurfaceSize = SurfaceSize;
    if (IsBackgroundRenderEnabled && !fitToBounds && GlyphIndex < 0 &&
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

    if (sizeChangedForWrap)
        needsRebuild = true;

    // Fast path: reuse existing IDWriteTextLayout when only FontSize, MaxWidth/MaxHeight, or variable font Axis changed.
    // Never mutate m_textLayout in-place while a background draw is still reading it (IDWriteTextLayout is not thread-safe).
    if (needsRebuild && GlyphIndex < 0 && m_textLayout != nullptr && FontFace != nullptr && m_activeBackgroundDraws.load() == 0)
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
        bool sameFit = (m_lastIsCharacterFit == fitToBounds);
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
                    m_drawFontFace = fontFace->GetTemporaryFontFace();
                    CoreDWTextServices::UpdateAxisValues(m_textLayout.Get(), newAxisValues, textLength);
                    m_lastAxisValues = newAxisValues;
                }

                if (SUCCEEDED(CoreDWTextServices::MeasureLayoutBounds(m_textLayout.Get(), &layoutBounds)))
                {
                    bool skipOutlineOnUI = IsBackgroundRenderEnabled && !fitToBounds;
                    if (!skipOutlineOnUI && (fitToBounds || IsOverwriteCompensationEnabled))
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
            params.IsCharacterFit = fitToBounds;
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
            bool skipOutlineOnUI = IsBackgroundRenderEnabled && !fitToBounds;
            if (!skipOutlineOnUI && (fitToBounds || IsOverwriteCompensationEnabled))
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
            m_lastIsCharacterFit = fitToBounds;
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


    bool isSingleChar = (GlyphIndex >= 0);
    if (!isSingleChar && Text != nullptr)
    {
        UINT32 len = Text->Length();
        const wchar_t* data = Text->Data();
        isSingleChar = (len == 1) ||
            (len == 2 && data[0] >= 0xD800 && data[0] <= 0xDBFF && data[1] >= 0xDC00 && data[1] <= 0xDFFF);
    }
    m_isSingleChar = isSingleChar;

    if ((renderBounds || trimLeading) && FontFace != nullptr)
    {
        double effectiveFontSize = 8.0 > FontSize ? 8.0 : FontSize;
        DWRITE_FONT_METRICS1 fm = FontFace->GetMetrics();
        double emScale = (fm.designUnitsPerEm > 0)
            ? (effectiveFontSize / static_cast<double>(fm.designUnitsPerEm))
            : (effectiveFontSize / 1000.0);

        double ascentDip = fm.ascent * emScale;
        double descentDip = fm.descent * emScale;
        double capHeightDip = fm.capHeight * emScale;
        double xHeightDip = fm.xHeight * emScale;

        double baselineY = 0.0;
        if (GlyphIndex < 0 && m_textLayout != nullptr)
        {
            DWRITE_LINE_METRICS lineMetrics{};
            UINT32 lineCount = 0;
            if (SUCCEEDED(m_textLayout->GetLineMetrics(&lineMetrics, 1, &lineCount)) && lineCount > 0)
                baselineY = lineMetrics.baseline;
            else
                baselineY = ascentDip;
        }

        m_metricBaselineY = baselineY;
        m_metricAscentY = baselineY - ascentDip;
        m_metricCapHeightY = baselineY - capHeightDip;
        m_metricXHeightY = baselineY - xHeightDip;
        m_metricDescentY = baselineY + descentDip;
        m_hasCapHeight = (fm.capHeight > 0);
        m_hasXHeight = (fm.xHeight > 0);
    }

    double minh = layoutBounds.Top;
    double maxh = layoutBounds.Bottom;
    double minw = layoutBounds.Left;
    double maxw = layoutBounds.Right;
    if (fitToBounds || GlyphIndex >= 0)
    {
        minh = min(drawBounds.Top, layoutBounds.Top);
        maxh = max(drawBounds.Bottom, layoutBounds.Bottom);
        minw = min(drawBounds.Left, layoutBounds.Left);
        maxw = max(drawBounds.Right, layoutBounds.Right);
    }

    if (trimLeading)
    {
        minh = (std::min)(static_cast<double>(drawBounds.Top), m_metricAscentY);
        maxh = (std::max)(static_cast<double>(drawBounds.Bottom), m_metricDescentY);
    }
    else if (renderBounds)
    {
        minh = (std::min)(minh, m_metricAscentY);
        maxh = (std::max)(maxh, m_metricDescentY);
    }

    double h = maxh - minh;
    double w = maxw - minw;
    if (h <= 0) h = layoutBounds.Height > 0 ? layoutBounds.Height : 1.0;
    if (w <= 0) w = layoutBounds.Width > 0 ? layoutBounds.Width : 1.0;

    auto targetsize = Size(min(m, ceil(w + padW)), min(m, ceil(h + padH)));

    if (fitToBounds)
    {
        bool hasConstrainedW = !std::isinf(size.Width);
        bool hasConstrainedH = !std::isinf(size.Height);

        if (hasConstrainedW || hasConstrainedH)
        {
            double effectiveFontSize = 8.0 > FontSize ? 8.0 : FontSize;
            m_minWidth = (renderBounds && isSingleChar)
                ? (effectiveFontSize * 1.15)
                : (FontSize / 2.2);
            auto totalW = (std::max)({ m_minWidth, static_cast<double>(layoutBounds.Width), static_cast<double>(drawBounds.Width), static_cast<double>(drawBounds.Right - (std::min)(0.0f, drawBounds.Left)) });
            auto totalTop = trimLeading
                ? (std::min)(static_cast<double>(drawBounds.Top), m_metricAscentY)
                : (std::min)(static_cast<double>(layoutBounds.Top), static_cast<double>(drawBounds.Top));
            auto totalBottom = trimLeading
                ? (std::max)(static_cast<double>(drawBounds.Bottom), m_metricDescentY)
                : (std::max)(static_cast<double>(layoutBounds.Bottom), static_cast<double>(drawBounds.Bottom));
            if (renderBounds && !trimLeading)
            {
                totalTop = (std::min)(totalTop, m_metricAscentY);
                totalBottom = (std::max)(totalBottom, m_metricDescentY);
            }
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
        bool RenderGlyphBounds = false;
        bool TrimLeadingLineGap = false;
        bool IsSingleChar = false;
        bool RenderColor = true;
        bool UseColorQueue = false;
        bool DeferredLayout = false;
        bool IsColorFontEnabled = true;

        struct GlyphBoundsRenderData
        {
            bool ShowAscent = true;
            bool ShowCapHeight = true;
            bool ShowXHeight = false;
            bool ShowBaseline = true;
            bool ShowDescent = true;
            bool ShowAdvanceBounds = false;
            bool ShowInkBounds = false;
            bool DrawLinesOverGlyph = true;
            bool HasCapHeight = false;
            bool HasXHeight = false;
            double LineThickness = 1.5;
            double BaselineY = 0.0;
            double AscentY = 0.0;
            double CapHeightY = 0.0;
            double XHeightY = 0.0;
            double DescentY = 0.0;
            D2D1_COLOR_F AscentColor = D2D1::ColorF(16.0f / 255.0f, 124.0f / 255.0f, 16.0f / 255.0f, 1.0f);
            D2D1_COLOR_F CapHeightColor = D2D1::ColorF(59.0f / 255.0f, 130.0f / 255.0f, 246.0f / 255.0f, 1.0f);
            D2D1_COLOR_F XHeightColor = D2D1::ColorF(0.0f / 255.0f, 183.0f / 255.0f, 195.0f / 255.0f, 1.0f);
            D2D1_COLOR_F BaselineColor = D2D1::ColorF(224.0f / 255.0f, 82.0f / 255.0f, 134.0f / 255.0f, 1.0f);
            D2D1_COLOR_F DescentColor = D2D1::ColorF(242.0f / 255.0f, 140.0f / 255.0f, 56.0f / 255.0f, 1.0f);
            D2D1_COLOR_F AdvanceBoundsColor = D2D1::ColorF(0.5f, 0.5f, 0.5f, 0.5f);
            D2D1_COLOR_F InkBoundsColor = D2D1::ColorF(232.0f / 255.0f, 17.0f / 255.0f, 35.0f / 255.0f, 0.5f);
        } Bounds;
        Windows::UI::Xaml::FlowDirection FlowDirection = Windows::UI::Xaml::FlowDirection::LeftToRight;
        DWriteColorRenderOption ColorRenderOption = DWriteColorRenderOption::Default;
        D2D1_COLOR_F ForegroundColor = D2D1::ColorF(D2D1::ColorF::White);
        D2D1_COLOR_F ActualForegroundColor = D2D1::ColorF(D2D1::ColorF::White);
        Windows::UI::Composition::CompositionGraphicsDevice^ GraphicsDevice = nullptr;
        TextLayoutParameters LayoutParams;
        std::shared_ptr<DeferredLayoutCache> LayoutCache;
    };

    void RenderSnapshotToContext(ID2D1DeviceContext* ctx, const DirectTextRenderSnapshot& snap, FLOAT offsetX, FLOAT offsetY, ID2D1SolidColorBrush* sharedBrush = nullptr)
    {
        auto db = snap.DrawBounds;
        auto lb = snap.LayoutBounds;
        double left = -min(db.Left, lb.Left) + snap.PadLeft;
        double top = snap.TrimLeadingLineGap
            ? -(std::min)(static_cast<double>(db.Top), snap.Bounds.AscentY) + snap.PadTop
            : -min(db.Top, lb.Top) + snap.PadTop;
        double scale = 1.0;

        if (snap.IsCharacterFitEnabled)
        {
            double padX = (std::max)(6.0, snap.ActualW * 0.04);
            double padY = (std::max)(6.0, snap.ActualH * 0.04);
            double availW = (std::max)(1.0, snap.ActualW - padX * 2.0);
            double availH = (std::max)(1.0, snap.ActualH - padY * 2.0);

            double totalW = (std::max)({ snap.MinWidth, static_cast<double>(lb.Width), static_cast<double>(db.Width), static_cast<double>(db.Right - (std::min)(0.0f, db.Left)) });
            double totalTop = snap.TrimLeadingLineGap
                ? (std::min)(static_cast<double>(db.Top), snap.Bounds.AscentY)
                : (std::min)(static_cast<double>(lb.Top), static_cast<double>(db.Top));
            double totalBottom = snap.TrimLeadingLineGap
                ? (std::max)(static_cast<double>(db.Bottom), snap.Bounds.DescentY)
                : (std::max)(static_cast<double>(lb.Bottom), static_cast<double>(db.Bottom));
            if (snap.RenderGlyphBounds && !snap.TrimLeadingLineGap)
            {
                totalTop = (std::min)(totalTop, snap.Bounds.AscentY);
                totalBottom = (std::max)(totalBottom, snap.Bounds.DescentY);
            }
            double totalH = (std::max)(1.0, totalBottom - totalTop);

            // Glyphs scale to fit available size (both scaling up and down) while maintaining baseline positioning
            scale = (std::min)(availW / totalW, availH / totalH);
            if (scale <= 0.0)
                scale = 1.0;

            double boxW = snap.ActualW / scale;
            double boxH = snap.ActualH / scale;

            double padLeftUnits = padX / scale;
            double padTopUnits = padY / scale;
            double maxRightUnits = boxW - padLeftUnits;
            double maxBottomUnits = boxH - padTopUnits;

            // Horizontal positioning:
            // Center within advance width for regular characters.
            // Center ink bounds for zero-advance characters (combining marks / diacritics).
            if (lb.Width > 0)
            {
                left = (boxW - lb.Width) / 2.0 - lb.Left;
                if (left + db.Left < padLeftUnits)
                    left = padLeftUnits - db.Left;
                if (left + db.Right > maxRightUnits)
                    left = maxRightUnits - db.Right;
            }
            else
            {
                left = -db.Left + (boxW - db.Width) / 2.0;
            }

            // Vertical positioning:
            // Center the font line box (or ascent-to-descent metrics when rendering glyph bounds)
            // so all characters share the exact same baseline and guide-line positions.
            if (snap.TrimLeadingLineGap)
            {
                double refTop = snap.Bounds.AscentY;
                double refBottom = snap.Bounds.DescentY;
                top = (boxH - (refBottom - refTop)) / 2.0 - refTop;
            }
            else if (snap.RenderGlyphBounds && snap.IsSingleChar)
            {
                double refTop = (std::min)(static_cast<double>(lb.Top), snap.Bounds.AscentY);
                double refBottom = (std::max)(static_cast<double>(lb.Bottom), snap.Bounds.DescentY);
                top = (boxH - (refBottom - refTop)) / 2.0 - refTop;
            }
            else
            {
                top = (boxH - lb.Height) / 2.0 - lb.Top;
            }

            // Prevent clipping if extreme ascenders/descenders extend outside padded box
            if (top + db.Top < padTopUnits)
                top = padTopUnits - db.Top;
            if (top + db.Bottom > maxBottomUnits)
                top = maxBottomUnits - db.Bottom;
        }
        else if (snap.FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
        {
            left = -min(db.Left, lb.Left) + snap.ActualW - db.Width - 4 - snap.PadRight;
        }

        D2D1::Matrix3x2F transform =
            D2D1::Matrix3x2F::Scale(static_cast<float>(scale * snap.DpiScale), static_cast<float>(scale * snap.DpiScale)) *
            D2D1::Matrix3x2F::Translation(offsetX, offsetY);

        bool color = snap.RenderColor && snap.IsColorFontEnabled && snap.ColorRenderOption != DWriteColorRenderOption::Monochrome;
        GlyphImageFormat preferredFormat = snap.RenderColor
            ? CoreDWTextServices::ToPreferredFormat(color, snap.ColorRenderOption)
            : GlyphImageFormat::TrueType;

        D2D1_COLOR_F brushColor = snap.RenderColor ? snap.ForegroundColor : D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f);
        ComPtr<ID2D1SolidColorBrush> fallbackBrush;
        ID2D1SolidColorBrush* activeBrush = sharedBrush;
        if (activeBrush != nullptr)
            activeBrush->SetColor(brushColor);
        else
        {
            ctx->CreateSolidColorBrush(brushColor, &fallbackBrush);
            activeBrush = fallbackBrush.Get();
        }

        auto drawGlyphBounds = [&]()
        {
            if (!snap.RenderGlyphBounds || activeBrush == nullptr)
                return;

            ctx->SetTransform(D2D1::Matrix3x2F::Translation(offsetX, offsetY));
            float strokeW = (std::max)(1.0f, static_cast<float>(snap.Bounds.LineThickness * snap.DpiScale));
            float surfW = static_cast<float>(snap.ActualW * snap.DpiScale);
            float surfH = static_cast<float>(snap.ActualH * snap.DpiScale);
            double pxScale = scale * snap.DpiScale;

            auto drawHLine = [&](double layoutY, const D2D1_COLOR_F& lineColor)
            {
                if (lineColor.a <= 0.0f)
                    return;
                float py = static_cast<float>((top + layoutY) * pxScale);
                if (strokeW <= 2.0f)
                    py = std::floor(py) + 0.5f;
                activeBrush->SetColor(lineColor);
                ctx->DrawLine(D2D1::Point2F(0.0f, py), D2D1::Point2F(surfW, py), activeBrush, strokeW);
            };

            if (snap.Bounds.ShowAscent)
                drawHLine(snap.Bounds.AscentY, snap.Bounds.AscentColor);
            if (snap.Bounds.ShowCapHeight && snap.Bounds.HasCapHeight)
                drawHLine(snap.Bounds.CapHeightY, snap.Bounds.CapHeightColor);
            if (snap.Bounds.ShowXHeight && snap.Bounds.HasXHeight)
                drawHLine(snap.Bounds.XHeightY, snap.Bounds.XHeightColor);
            if (snap.Bounds.ShowBaseline)
                drawHLine(snap.Bounds.BaselineY, snap.Bounds.BaselineColor);
            if (snap.Bounds.ShowDescent)
                drawHLine(snap.Bounds.DescentY, snap.Bounds.DescentColor);

            if (snap.Bounds.ShowAdvanceBounds && snap.Bounds.AdvanceBoundsColor.a > 0.0f)
            {
                activeBrush->SetColor(snap.Bounds.AdvanceBoundsColor);
                float x0 = static_cast<float>((left + lb.Left) * pxScale);
                float x1 = static_cast<float>((left + lb.Right) * pxScale);
                if (strokeW <= 2.0f)
                {
                    x0 = std::floor(x0) + 0.5f;
                    x1 = std::floor(x1) + 0.5f;
                }
                ctx->DrawLine(D2D1::Point2F(x0, 0.0f), D2D1::Point2F(x0, surfH), activeBrush, strokeW);
                ctx->DrawLine(D2D1::Point2F(x1, 0.0f), D2D1::Point2F(x1, surfH), activeBrush, strokeW);
            }

            if (snap.Bounds.ShowInkBounds && snap.Bounds.InkBoundsColor.a > 0.0f)
            {
                activeBrush->SetColor(snap.Bounds.InkBoundsColor);
                float rx0 = static_cast<float>((left + db.Left) * pxScale);
                float ry0 = static_cast<float>((top + db.Top) * pxScale);
                float rx1 = static_cast<float>((left + db.Right) * pxScale);
                float ry1 = static_cast<float>((top + db.Bottom) * pxScale);
                if (strokeW <= 2.0f)
                {
                    rx0 = std::floor(rx0) + 0.5f;
                    ry0 = std::floor(ry0) + 0.5f;
                    rx1 = std::floor(rx1) + 0.5f;
                    ry1 = std::floor(ry1) + 0.5f;
                }
                ctx->DrawRectangle(D2D1::RectF(rx0, ry0, rx1, ry1), activeBrush, strokeW);
            }

            activeBrush->SetColor(brushColor);
        };

        if (snap.RenderGlyphBounds && !snap.Bounds.DrawLinesOverGlyph)
            drawGlyphBounds();

        ctx->SetTransform(transform);

        TextRenderParameters renderParams;
        renderParams.ForegroundBrush = activeBrush;
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

        if (snap.RenderGlyphBounds && snap.Bounds.DrawLinesOverGlyph)
            drawGlyphBounds();
    }

    bool RenderSnapshotOffDevice(const DirectTextRenderSnapshot& snap, std::vector<uint8_t>& outPixels, UINT32& outPitch)
    {
        return CoreDWTextServices::RenderOffDevice(
            snap.SurfaceWidth,
            snap.SurfaceHeight,
            snap.RenderColor || snap.UseColorQueue,
            [&snap](ID2D1DeviceContext* ctx, ID2D1SolidColorBrush* sharedBrush)
            {
                RenderSnapshotToContext(ctx, snap, 0.0f, 0.0f, sharedBrush);
            },
            outPixels,
            outPitch);
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
                ComPtr<IDWriteFontFace3> primaryFace;
                if (snap.LayoutParams.FontFace != nullptr &&
                    snap.LayoutParams.FontFace->Properties != nullptr &&
                    snap.LayoutParams.FontFace->Properties->HasBitmapSVGGlyphs &&
                    !snap.LayoutParams.FontFace->Properties->HasColorBitmapOutlines)
                {
                    primaryFace = snap.LayoutParams.FontFace->GetTemporaryFontFace();
                    checker.IgnoreFontFace = primaryFace.Get();
                }
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
        thread_local std::vector<uint8_t> offDevicePixels;
        UINT32 offDevicePitch = 0;
        bool hasOffDevicePixels = offDevice && RenderSnapshotOffDevice(snap, offDevicePixels, offDevicePitch);

        if (hasOffDevicePixels)
        {
            if (gen != 0)
            {
                auto pThis = weakThis.Resolve<DirectText>();
                if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                    return false;
            }

            if (CoreDWTextServices::UploadOffDevicePixelsToSurface(
                snap.SurfaceInterop.Get(),
                snap.SurfaceWidth,
                snap.SurfaceHeight,
                offDevicePixels,
                offDevicePitch))
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
            RenderSnapshotToContext(ctx.Get(), snap, static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y));
            snap.SurfaceInterop->EndDraw();
        }

        return false;
    }
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

    bool renderBounds = RenderGlyphBounds;
    bool trimLeading = TrimLeadingLineGap;
    bool fitToBounds = IsCharacterFitEnabled || renderBounds;
    UpdateRootHitTestBackground();

    float overL = 0.0f, overT = 0.0f, overR = 0.0f, overB = 0.0f;
    if (IsOverwriteCompensationEnabled && !fitToBounds && GlyphIndex < 0)
    {
        overL = (std::max)(0.0f, layoutBounds.Left - drawBounds.Left);
        overT = (std::max)(0.0f, layoutBounds.Top - drawBounds.Top);
        overR = (std::max)(0.0f, drawBounds.Right - layoutBounds.Right);
        overB = (std::max)(0.0f, drawBounds.Bottom - layoutBounds.Bottom);
    }

    Windows::Foundation::Size fixedSurfaceSize = SurfaceSize;
    float surfaceW = ((fixedSurfaceSize.Width > 0.0f) ? fixedSurfaceSize.Width : actualW) + overL + overR;
    float surfaceH = ((fixedSurfaceSize.Height > 0.0f) ? fixedSurfaceSize.Height : actualH) + overT + overB;

    LONG requiredWidth = (std::max)(1L, (std::min)(8192L, static_cast<LONG>(std::ceil(surfaceW * dpiScale))));
    LONG requiredHeight = (std::max)(1L, (std::min)(8192L, static_cast<LONG>(std::ceil(surfaceH * dpiScale))));

    bool renderColor = ShouldRenderColor();

    auto wireBrushes = [&]()
    {
        if (m_surfaceBrush != nullptr)
            m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
        m_spriteVisual->Size = float2(surfaceW, surfaceH);
        m_spriteVisual->Offset = float3(-overL, -overT, 0.0f);

        CompositionDeviceManager::ApplySurfaceOrMaskBrush(
            compositor,
            m_spriteVisual,
            m_surfaceBrush,
            m_maskBrush,
            m_colorBrush,
            !renderColor,
            GetForegroundColor());
    };

    bool sizeChangedForFit = fitToBounds &&
        (m_spriteVisual == nullptr ||
         std::abs(m_spriteVisual->Size.x - surfaceW) > 0.25f ||
         std::abs(m_spriteVisual->Size.y - surfaceH) > 0.25f);

    if (!m_needsRender && !sizeChangedForFit && m_drawingSurface != nullptr && m_renderedColor == renderColor &&
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

    bool needsBrushSetup = needNewSurface || m_surfaceBrush == nullptr || m_spriteVisual->Brush == nullptr;
    if (needsBrushSetup)
    {
        CompositionDeviceManager::EnsureSurfaceBrush(compositor, m_surfaceBrush, m_drawingSurface);
        wireBrushes();
    }
    else
    {
        m_spriteVisual->Size = float2(surfaceW, surfaceH);
        m_spriteVisual->Offset = float3(-overL, -overT, 0.0f);
        if (!renderColor && m_maskBrush != nullptr)
        {
            auto colorBrush = CompositionDeviceManager::GetColorBrush(compositor, GetForegroundColor());
            if (m_colorBrush != colorBrush)
            {
                m_colorBrush = colorBrush;
                m_maskBrush->Source = m_colorBrush;
            }
        }
    }

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
    snapshot.IsCharacterFitEnabled = fitToBounds;
    snapshot.RenderGlyphBounds = renderBounds;
    snapshot.TrimLeadingLineGap = trimLeading;
    snapshot.IsSingleChar = m_isSingleChar;
    snapshot.RenderColor = renderColor;

    if (renderBounds || trimLeading)
    {
        snapshot.Bounds.BaselineY = m_metricBaselineY;
        snapshot.Bounds.AscentY = m_metricAscentY;
        snapshot.Bounds.CapHeightY = m_metricCapHeightY;
        snapshot.Bounds.XHeightY = m_metricXHeightY;
        snapshot.Bounds.DescentY = m_metricDescentY;
        snapshot.Bounds.HasCapHeight = m_hasCapHeight;
        snapshot.Bounds.HasXHeight = m_hasXHeight;
    }

    if (renderBounds)
    {
        auto extractBrushColor = [](SolidColorBrush^ brush, const D2D1_COLOR_F& fallback) -> D2D1_COLOR_F
        {
            if (brush == nullptr)
                return fallback;
            auto c = brush->Color;
            return D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(brush->Opacity));
        };

        auto opts = GlyphBoundsOptions;

        if (opts != nullptr)
        {
            snapshot.Bounds.ShowAscent = opts->ShowAscent;
            snapshot.Bounds.ShowCapHeight = opts->ShowCapHeight;
            snapshot.Bounds.ShowXHeight = opts->ShowXHeight;
            snapshot.Bounds.ShowBaseline = opts->ShowBaseline;
            snapshot.Bounds.ShowDescent = opts->ShowDescent;
            snapshot.Bounds.ShowAdvanceBounds = opts->ShowAdvanceBounds;
            snapshot.Bounds.ShowInkBounds = opts->ShowInkBounds;
            snapshot.Bounds.DrawLinesOverGlyph = opts->DrawLinesOverGlyph;
            snapshot.Bounds.LineThickness = opts->LineThickness > 0.0 ? opts->LineThickness : 1.0;

            snapshot.Bounds.AscentColor = extractBrushColor(opts->AscentBrush, snapshot.Bounds.AscentColor);
            snapshot.Bounds.CapHeightColor = extractBrushColor(opts->CapHeightBrush, snapshot.Bounds.CapHeightColor);
            snapshot.Bounds.XHeightColor = extractBrushColor(opts->XHeightBrush, snapshot.Bounds.XHeightColor);
            snapshot.Bounds.BaselineColor = extractBrushColor(opts->BaselineBrush, snapshot.Bounds.BaselineColor);
            snapshot.Bounds.DescentColor = extractBrushColor(opts->DescentBrush, snapshot.Bounds.DescentColor);
            snapshot.Bounds.AdvanceBoundsColor = extractBrushColor(opts->AdvanceBoundsBrush, snapshot.Bounds.AdvanceBoundsColor);
            snapshot.Bounds.InkBoundsColor = extractBrushColor(opts->InkBoundsBrush, snapshot.Bounds.InkBoundsColor);
        }

        // Compute DIP-space hit targets matching RenderSnapshotToContext so pointer hover tooltips
        // accurately identify each visible measurement line.
        m_boundsHitTargets.clear();
        m_boundsHitTolerance = (std::max)(6.0, snapshot.Bounds.LineThickness * 2.0);

        double padX = (std::max)(6.0, static_cast<double>(actualW) * 0.04);
        double padY = (std::max)(6.0, static_cast<double>(actualH) * 0.04);
        double availW = (std::max)(1.0, static_cast<double>(actualW) - padX * 2.0);
        double availH = (std::max)(1.0, static_cast<double>(actualH) - padY * 2.0);

        double totalW = (std::max)({ m_minWidth, static_cast<double>(layoutBounds.Width), static_cast<double>(drawBounds.Width), static_cast<double>(drawBounds.Right - (std::min)(0.0f, drawBounds.Left)) });
        double totalTop = trimLeading
            ? (std::min)(static_cast<double>(drawBounds.Top), snapshot.Bounds.AscentY)
            : (std::min)({ static_cast<double>(layoutBounds.Top), static_cast<double>(drawBounds.Top), snapshot.Bounds.AscentY });
        double totalBottom = trimLeading
            ? (std::max)(static_cast<double>(drawBounds.Bottom), snapshot.Bounds.DescentY)
            : (std::max)({ static_cast<double>(layoutBounds.Bottom), static_cast<double>(drawBounds.Bottom), snapshot.Bounds.DescentY });
        double totalH = (std::max)(1.0, totalBottom - totalTop);

        double scale = (std::min)(availW / totalW, availH / totalH);
        if (scale <= 0.0)
            scale = 1.0;

        double boxW = actualW / scale;
        double boxH = actualH / scale;
        double padLeftUnits = padX / scale;
        double padTopUnits = padY / scale;
        double maxRightUnits = boxW - padLeftUnits;
        double maxBottomUnits = boxH - padTopUnits;

        double left = 0.0;
        if (layoutBounds.Width > 0)
        {
            left = (boxW - layoutBounds.Width) / 2.0 - layoutBounds.Left;
            if (left + drawBounds.Left < padLeftUnits)
                left = padLeftUnits - drawBounds.Left;
            if (left + drawBounds.Right > maxRightUnits)
                left = maxRightUnits - drawBounds.Right;
        }
        else
        {
            left = -drawBounds.Left + (boxW - drawBounds.Width) / 2.0;
        }

        double top = 0.0;
        if (trimLeading)
        {
            double refTop = snapshot.Bounds.AscentY;
            double refBottom = snapshot.Bounds.DescentY;
            top = (boxH - (refBottom - refTop)) / 2.0 - refTop;
        }
        else if (m_isSingleChar)
        {
            double refTop = (std::min)(static_cast<double>(layoutBounds.Top), snapshot.Bounds.AscentY);
            double refBottom = (std::max)(static_cast<double>(layoutBounds.Bottom), snapshot.Bounds.DescentY);
            top = (boxH - (refBottom - refTop)) / 2.0 - refTop;
        }
        else
        {
            top = (boxH - layoutBounds.Height) / 2.0 - layoutBounds.Top;
        }

        if (top + drawBounds.Top < padTopUnits)
            top = padTopUnits - drawBounds.Top;
        if (top + drawBounds.Bottom > maxBottomUnits)
            top = maxBottomUnits - drawBounds.Bottom;

        auto getLabel = [&](Platform::String^ custom, const wchar_t* fallback) -> Platform::String^
        {
            return (custom != nullptr && custom->Length() > 0) ? custom : ref new Platform::String(fallback);
        };

        if (snapshot.Bounds.ShowAscent && snapshot.Bounds.AscentColor.a > 0.0f)
            m_boundsHitTargets.push_back({ 0, (top + snapshot.Bounds.AscentY) * scale, {}, getLabel(opts != nullptr ? opts->AscentLabel : nullptr, L"Ascent") });
        if (snapshot.Bounds.ShowCapHeight && snapshot.Bounds.HasCapHeight && snapshot.Bounds.CapHeightColor.a > 0.0f)
            m_boundsHitTargets.push_back({ 0, (top + snapshot.Bounds.CapHeightY) * scale, {}, getLabel(opts != nullptr ? opts->CapHeightLabel : nullptr, L"Cap Height") });
        if (snapshot.Bounds.ShowXHeight && snapshot.Bounds.HasXHeight && snapshot.Bounds.XHeightColor.a > 0.0f)
            m_boundsHitTargets.push_back({ 0, (top + snapshot.Bounds.XHeightY) * scale, {}, getLabel(opts != nullptr ? opts->XHeightLabel : nullptr, L"x-Height") });
        if (snapshot.Bounds.ShowBaseline && snapshot.Bounds.BaselineColor.a > 0.0f)
            m_boundsHitTargets.push_back({ 0, (top + snapshot.Bounds.BaselineY) * scale, {}, getLabel(opts != nullptr ? opts->BaselineLabel : nullptr, L"Baseline") });
        if (snapshot.Bounds.ShowDescent && snapshot.Bounds.DescentColor.a > 0.0f)
            m_boundsHitTargets.push_back({ 0, (top + snapshot.Bounds.DescentY) * scale, {}, getLabel(opts != nullptr ? opts->DescentLabel : nullptr, L"Descent") });
        if (snapshot.Bounds.ShowAdvanceBounds && snapshot.Bounds.AdvanceBoundsColor.a > 0.0f)
        {
            auto advLabel = getLabel(opts != nullptr ? opts->AdvanceBoundsLabel : nullptr, L"Advance Bounds");
            m_boundsHitTargets.push_back({ 1, (left + layoutBounds.Left) * scale, {}, advLabel });
            m_boundsHitTargets.push_back({ 1, (left + layoutBounds.Right) * scale, {}, advLabel });
        }
        if (snapshot.Bounds.ShowInkBounds && snapshot.Bounds.InkBoundsColor.a > 0.0f)
        {
            float rx0 = static_cast<float>((left + drawBounds.Left) * scale);
            float ry0 = static_cast<float>((top + drawBounds.Top) * scale);
            float rw = static_cast<float>(drawBounds.Width * scale);
            float rh = static_cast<float>(drawBounds.Height * scale);
            m_boundsHitTargets.push_back({ 2, 0.0, Rect(rx0, ry0, (std::max)(0.0f, rw), (std::max)(0.0f, rh)), getLabel(opts != nullptr ? opts->InkBoundsLabel : nullptr, L"Ink Bounds") });
        }
    }
    else
    {
        m_boundsHitTargets.clear();
        HideBoundsToolTip();
    }
    snapshot.UseColorQueue = renderColor || (FontFace != nullptr && FontFace->Properties != nullptr && FontFace->Properties->IsExpensive);
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
        bool useColorQueue = snapshot.UseColorQueue;
        Platform::WeakReference weakThis(this);
        BackgroundRenderWorkItem workItem;
        workItem.ControlKey = reinterpret_cast<const void*>(this);
        workItem.WasHidden = wasHidden;
        workItem.Execute = [weakThis, gen, snap = std::move(snapshot)](bool effectiveWasHidden) mutable
        {
            struct DrawCounterGuard
            {
                Platform::WeakReference WeakThis;
                ~DrawCounterGuard()
                {
                    if (auto p = WeakThis.Resolve<DirectText>())
                        p->DecrementActiveBackgroundDraws();
                }
            } guard{ weakThis };

            auto pThis = weakThis.Resolve<DirectText>();
            if (pThis == nullptr || !pThis->IsGenerationCurrent(gen))
                return;

            DrawSnapshotToSurface(std::move(snap), true, weakThis, gen);
            if (!effectiveWasHidden)
                return;

            // Composition properties (like IsVisible) are thread-safe and commit directly without Dispatcher.
            if (auto pThis2 = weakThis.Resolve<DirectText>())
                pThis2->ShowVisualIfCurrent(gen);
        };

        auto& queue = useColorQueue
            ? BackgroundRenderQueue::ColorInstance()
            : BackgroundRenderQueue::MonoInstance();
        queue.Enqueue(std::move(workItem));
    }
    else
    {
        m_lastShownFontFace = FontFace;
        DrawSnapshotToSurface(snapshot);
        if (m_spriteVisual != nullptr)
            m_spriteVisual->IsVisible = true;
    }
}





