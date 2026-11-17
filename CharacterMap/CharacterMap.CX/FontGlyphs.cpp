#include "pch.h"
#include "FontGlyphs.h"
#include "CompositionDeviceManager.h"
#include "CoreDWTextServices.h"
#include "GlyphAtlasManager.h"
#include "TypographyAnalyzer.h"
#include "DirectWrite.h"
#include <cwctype>
#include <algorithm>
#include <array>
#include <cmath>
#include <deque>
#include <mutex>
#include <ppltasks.h>

using namespace CharacterMapCX::Controls;
using namespace Microsoft::WRL;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Numerics;
using namespace Windows::UI;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::UI::Xaml::Media;

const double REAL_EPSILON = 1.192092896e-07;

DependencyProperty^ FontGlyphs::_FontFaceProperty = nullptr;
DependencyProperty^ FontGlyphs::_FontSizeProperty = nullptr;
DependencyProperty^ FontGlyphs::_IndicesProperty = nullptr;
DependencyProperty^ FontGlyphs::_UnicodeStringProperty = nullptr;
DependencyProperty^ FontGlyphs::_ForegroundProperty = nullptr;
DependencyProperty^ FontGlyphs::_IsColorFontEnabledProperty = nullptr;
DependencyProperty^ FontGlyphs::_StyleSimulationsProperty = nullptr;
DependencyProperty^ FontGlyphs::_StretchProperty = nullptr;
DependencyProperty^ FontGlyphs::_StretchDirectionProperty = nullptr;
DependencyProperty^ FontGlyphs::_TypographyProperty = nullptr;
DependencyProperty^ FontGlyphs::_IsTextShapingEnabledProperty = nullptr;
DependencyProperty^ FontGlyphs::_IsBackgroundRenderEnabledProperty = nullptr;

static std::atomic<int32> s_activeSurfaceCount{ 0 };
static std::atomic<uint64> s_activeSurfaceBytes{ 0 };

int32 FontGlyphs::GetActiveSurfaceCount()
{
    return s_activeSurfaceCount.load(std::memory_order_relaxed);
}

uint64 FontGlyphs::GetActiveSurfaceBytes()
{
    return s_activeSurfaceBytes.load(std::memory_order_relaxed);
}

FontGlyphs::FontGlyphs()
    : m_isLayoutDirty(true)
    , m_isRenderDirty(true)
    , m_baseline(0)
    , m_padLeft(0)
    , m_padRight(0)
    , m_padTop(0)
    , m_padBottom(0)
    , m_hasInkBounds(false)
    , m_designInkLeft(0)
    , m_designInkWidth(0)
    , m_designInkTop(0)
    , m_designInkHeight(0)
    , m_designUnitsPerEm(0)
    , m_contentSize(0, 0)
    , m_scale(1, 1)
    , m_renderedWidth(0)
    , m_renderedHeight(0)
    , m_renderedColor(false)
    , m_renderedScaleX(0.0f)
    , m_renderedScaleY(0.0f)
    , m_isUsingSharedAtlas(false)
    , m_glyphCount(0)
    , m_inlineIndices{}
    , m_inlineAdvances{}
    , m_inlineOffsets{}
{
    m_loadedToken.Value = 0;
    m_unloadedToken.Value = 0;
    BlockUpdates = false;
    EnsureDependencyProperties();

    WeakReference weakThis(this);
    m_loadedToken = this->Loaded += ref new RoutedEventHandler(
        [weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
            if (auto strongThis = weakThis.Resolve<FontGlyphs>())
                strongThis->OnLoaded(sender, e);
        });
    m_unloadedToken = this->Unloaded += ref new RoutedEventHandler(
        [weakThis](Platform::Object^ sender, RoutedEventArgs^ e) {
            if (auto strongThis = weakThis.Resolve<FontGlyphs>())
                strongThis->OnUnloaded(sender, e);
        });
    m_themeChangedToken = this->ActualThemeChanged += ref new TypedEventHandler<FrameworkElement^, Platform::Object^>(
        [weakThis](FrameworkElement^, Platform::Object^) {
            if (auto strongThis = weakThis.Resolve<FontGlyphs>())
            {
                strongThis->UpdateForground();
            }
        });
}

FontGlyphs::~FontGlyphs()
{
    ++m_renderGeneration;
    try
    {
        if (m_unloadedToken.Value != 0)
        {
            this->Unloaded -= m_unloadedToken;
            m_unloadedToken.Value = 0;
        }

        if (m_loadedToken.Value != 0)
        {
            this->Loaded -= m_loadedToken;
            m_loadedToken.Value = 0;
        }

        if (m_themeChangedToken.Value != 0)
        {
            this->ActualThemeChanged -= m_themeChangedToken;
            m_themeChangedToken.Value = 0;
        }

        if (Dispatcher != nullptr && Dispatcher->HasThreadAccess)
        {
            ReleaseDrawingSurface();

            if (m_spriteVisual != nullptr)
            {
                ElementCompositionPreview::SetElementChildVisual(this, nullptr);
                m_spriteVisual->Brush = nullptr;
                delete m_spriteVisual;
            }

            if (m_maskBrush != nullptr)
            {
                m_maskBrush->Mask = nullptr;
                m_maskBrush->Source = nullptr;
                delete m_maskBrush;
            }

            if (m_surfaceBrush != nullptr)
            {
                delete m_surfaceBrush;
            }
        }
        else if (Dispatcher != nullptr)
        {
            // Non-UI thread (e.g. .NET GC finalizer thread):
            // Dispatch teardown of composition objects and surfaces to UI thread so they are closed deterministically.
            auto sprite = m_spriteVisual;
            auto surfBrush = m_surfaceBrush;
            auto maskBrush = m_maskBrush;
            auto surface = m_drawingSurface;
            bool isShared = m_isUsingSharedAtlas;
            LONG rendW = m_renderedWidth;
            LONG rendH = m_renderedHeight;

            if (!isShared && surface != nullptr && rendW > 0 && rendH > 0)
            {
                uint64 bytes = static_cast<uint64>(rendW) * rendH * (m_renderedColor ? 4 : 1);
                s_activeSurfaceCount.fetch_sub(1, std::memory_order_relaxed);
                s_activeSurfaceBytes.fetch_sub(bytes, std::memory_order_relaxed);
            }

            if (sprite != nullptr || surfBrush != nullptr || maskBrush != nullptr || (surface != nullptr && !isShared))
            {
                Dispatcher->RunAsync(
                    Windows::UI::Core::CoreDispatcherPriority::Low,
                    ref new Windows::UI::Core::DispatchedHandler([sprite, surfBrush, maskBrush, surface, isShared]()
                    {
                        try
                        {
                            if (sprite != nullptr)
                            {
                                sprite->Brush = nullptr;
                                delete sprite;
                            }
                            if (maskBrush != nullptr)
                            {
                                maskBrush->Mask = nullptr;
                                maskBrush->Source = nullptr;
                                delete maskBrush;
                            }
                            if (surfBrush != nullptr)
                            {
                                surfBrush->Surface = nullptr;
                                delete surfBrush;
                            }
                            if (surface != nullptr && !isShared)
                            {
                                delete surface;
                            }
                        }
                        catch (...) {}
                    }));
            }
        }
    }
    catch (...)
    {
    }

    m_unloadedToken.Value = 0;
    m_loadedToken.Value = 0;
    m_drawingSurface = nullptr;
    m_spriteVisual = nullptr;
    m_maskBrush = nullptr;
    m_surfaceBrush = nullptr;
    m_colorBrush = nullptr;
}


void FontGlyphs::EnsureDependencyProperties()
{
    static bool registered = false;
    if (!registered)
    {
        registered = true;
        RegisterDependencyProperties();
    }
}

void FontGlyphs::RegisterDependencyProperties()
{
    auto layoutCallback = ref new PropertyChangedCallback(&FontGlyphs::OnPropertyChanged);

    if (_FontFaceProperty == nullptr)
        _FontFaceProperty = DependencyProperty::Register("FontFace", DWriteFontFace::typeid, FontGlyphs::typeid, ref new PropertyMetadata(nullptr, layoutCallback));

    if (_FontSizeProperty == nullptr)
        _FontSizeProperty = DependencyProperty::Register("FontSize", double::typeid, FontGlyphs::typeid, ref new PropertyMetadata((double)24.0, ref new PropertyChangedCallback(&FontGlyphs::OnFontSizeChanged)));

    if (_IndicesProperty == nullptr)
        _IndicesProperty = DependencyProperty::Register("Indices", Platform::String::typeid, FontGlyphs::typeid, ref new PropertyMetadata(nullptr, layoutCallback));

    if (_UnicodeStringProperty == nullptr)
        _UnicodeStringProperty = DependencyProperty::Register("UnicodeString", Platform::String::typeid, FontGlyphs::typeid, ref new PropertyMetadata(nullptr, layoutCallback));

    if (_ForegroundProperty == nullptr)
        _ForegroundProperty = DependencyProperty::Register("Foreground", Brush::typeid, FontGlyphs::typeid, ref new PropertyMetadata(nullptr, ref new PropertyChangedCallback(&FontGlyphs::OnForegroundChanged)));

    if (_IsColorFontEnabledProperty == nullptr)
        _IsColorFontEnabledProperty = DependencyProperty::Register("IsColorFontEnabled", bool::typeid, FontGlyphs::typeid, ref new PropertyMetadata((bool)true, layoutCallback));

    if (_StyleSimulationsProperty == nullptr)
        _StyleSimulationsProperty = DependencyProperty::Register("StyleSimulations", Windows::UI::Xaml::Media::StyleSimulations::typeid, FontGlyphs::typeid, ref new PropertyMetadata(Windows::UI::Xaml::Media::StyleSimulations::None, layoutCallback));

    if (_StretchProperty == nullptr)
        _StretchProperty = DependencyProperty::Register("Stretch", Windows::UI::Xaml::Media::Stretch::typeid, FontGlyphs::typeid, ref new PropertyMetadata(Windows::UI::Xaml::Media::Stretch::Uniform, layoutCallback));

    if (_StretchDirectionProperty == nullptr)
        _StretchDirectionProperty = DependencyProperty::Register("StretchDirection", Windows::UI::Xaml::Controls::StretchDirection::typeid, FontGlyphs::typeid, ref new PropertyMetadata(Windows::UI::Xaml::Controls::StretchDirection::DownOnly, layoutCallback));

    if (_TypographyProperty == nullptr)
        _TypographyProperty = DependencyProperty::Register("Typography", DWriteTypographyCollection::typeid, FontGlyphs::typeid, ref new PropertyMetadata(nullptr, layoutCallback));

    if (_IsTextShapingEnabledProperty == nullptr)
        _IsTextShapingEnabledProperty = DependencyProperty::Register("IsTextShapingEnabled", bool::typeid, FontGlyphs::typeid, ref new PropertyMetadata((bool)false, layoutCallback));

    if (_IsBackgroundRenderEnabledProperty == nullptr)
        _IsBackgroundRenderEnabledProperty = DependencyProperty::Register("IsBackgroundRenderEnabled", bool::typeid, FontGlyphs::typeid, ref new PropertyMetadata((bool)false, layoutCallback));
}

void FontGlyphs::OnLoaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    if (m_isRenderDirty || m_drawingSurface == nullptr)
    {
        m_isRenderDirty = true;
        InvalidateArrange();
    }
}

void FontGlyphs::OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    ++m_renderGeneration;
    // Proactively release drawing surface and composition objects so off-tree controls
    // do not pin atlas pages, dedicated surfaces, or visual/brush allocations.
    ReleaseDrawingSurface();

    if (m_spriteVisual != nullptr)
    {
        ElementCompositionPreview::SetElementChildVisual(this, nullptr);
        m_spriteVisual->Brush = nullptr;
        delete m_spriteVisual;
        m_spriteVisual = nullptr;
    }

    if (m_maskBrush != nullptr)
    {
        m_maskBrush->Mask = nullptr;
        m_maskBrush->Source = nullptr;
        delete m_maskBrush;
        m_maskBrush = nullptr;
    }

    if (m_surfaceBrush != nullptr)
    {
        delete m_surfaceBrush;
        m_surfaceBrush = nullptr;
    }

    m_colorBrush = nullptr;
    m_dpiScale = 0.0f;
    m_lastBgArrangeSize = { 0, 0 };
    m_isRenderDirty = true;
}


void FontGlyphs::Trim()
{
    CompositionDeviceManager::Trim();
}

void FontGlyphs::ReleaseGraphicsDevice(Compositor^ compositor)
{
    CompositionDeviceManager::ReleaseGraphicsDevice(compositor);
}

void FontGlyphs::ClearAtlases(Compositor^ compositor)
{
    CompositionDeviceManager::ClearAtlases(compositor);
}

/*
 * Releases the native CompositionDrawingSurface and disconnects it from
 * the surface brush before calling delete (IClosable::Close) so the DWM
 * compositor immediately reclaims the DirectX surface texture.
 */
void FontGlyphs::ReleaseDrawingSurface()
{
    try
    {
        if (m_drawingSurface != nullptr)
        {
            // Always clear the brush's surface reference — this is the compositor-side
            // handle that keeps the underlying DX texture alive. We can safely do this
            // from any thread; it is a WinRT property assignment, not a destructor.
            if (m_surfaceBrush != nullptr)
                m_surfaceBrush->Surface = nullptr;

            // Only call delete (IClosable::Close) on the UI thread, as the compositor
            // requires it. If we're not on the UI thread the texture ref is already
            // dropped above; the compositor will release it when the surface object
            // is eventually GC'd or the next Trim() call fires.
            if (!m_isUsingSharedAtlas
                && Dispatcher != nullptr
                && Dispatcher->HasThreadAccess)
            {
                delete m_drawingSurface;
            }
        }
    }
    catch (...)
    {
    }

    if (!m_isUsingSharedAtlas && m_renderedWidth > 0 && m_renderedHeight > 0)
    {
        uint64 bytes = static_cast<uint64>(m_renderedWidth) * m_renderedHeight * (m_renderedColor ? 4 : 1);
        s_activeSurfaceCount.fetch_sub(1, std::memory_order_relaxed);
        s_activeSurfaceBytes.fetch_sub(bytes, std::memory_order_relaxed);
    }

    m_isUsingSharedAtlas = false;
    m_drawingSurface = nullptr;
    m_renderedWidth = 0;
    m_renderedHeight = 0;
}

Windows::UI::Color FontGlyphs::GetForegroundColor()
{
    return CompositionDeviceManager::ExtractSolidColor(Foreground, Windows::UI::Colors::White);
}

UINT32 FontGlyphs::GetForegroundColorKey()
{
    Windows::UI::Color c = GetForegroundColor();
    return (static_cast<UINT32>(c.A) << 24)
        | (static_cast<UINT32>(c.R) << 16)
        | (static_cast<UINT32>(c.G) << 8)
        | static_cast<UINT32>(c.B);
}

bool FontGlyphs::ShouldRenderColor()
{
    if (!IsColorFontEnabled)
        return false;

    if (FontFace != nullptr && FontFace->Properties != nullptr)
        return FontFace->Properties->IsColorFont;

    return false;
}

void FontGlyphs::OnPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto glyphs = (FontGlyphs^)d;
    if (!glyphs->BlockUpdates)
        glyphs->InvalidateLayoutAndRender();
}

void FontGlyphs::OnFontSizeChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto glyphs = (FontGlyphs^)d;
    if (!glyphs->BlockUpdates)
        glyphs->InvalidateLayoutAndRender();
}

void FontGlyphs::OnForegroundChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto glyphs = (FontGlyphs^)d;
    if (!glyphs->BlockUpdates)
        glyphs->UpdateForground();
}

void FontGlyphs::Update()
{
    BlockUpdates = false;
    InvalidateLayoutAndRender();
}

void FontGlyphs::CancelRender()
{
    ++m_renderGeneration;
    if (m_spriteVisual != nullptr && IsBackgroundRenderEnabled)
        m_spriteVisual->IsVisible = false;
}

void FontGlyphs::ShowVisualIfCurrent(uint64_t gen)
{
    if (IsGenerationCurrent(gen) && m_spriteVisual != nullptr)
        m_spriteVisual->IsVisible = true;
}



void FontGlyphs::UpdateForground()
{
    if (m_maskBrush != nullptr && m_colorBrush != nullptr)
    {
        auto visual = ElementCompositionPreview::GetElementVisual(this);
        if (visual != nullptr)
        {
            m_colorBrush = CompositionDeviceManager::GetColorBrush(visual->Compositor, GetForegroundColor());
            m_maskBrush->Source = m_colorBrush;
            return;
        }
    }

    InvalidateRenderOnly();
}

void FontGlyphs::InvalidateLayoutAndRender()
{
    ++m_renderGeneration;
    if (m_spriteVisual != nullptr && IsBackgroundRenderEnabled)
        m_spriteVisual->IsVisible = false;
    m_isLayoutDirty = true;
    m_isRenderDirty = true;
    if (!IsBackgroundRenderEnabled)
        ReleaseDrawingSurface();
    InvalidateMeasure();
    InvalidateArrange();
}

void FontGlyphs::InvalidateRenderOnly()
{
    ++m_renderGeneration;
    m_isRenderDirty = true;
    InvalidateArrange();
}

void FontGlyphs::ParseAndLayoutGlyphs()
{
    m_glyphCount = 0;
    if (!m_heapIndices.empty()) { m_heapIndices.clear(); m_heapIndices.shrink_to_fit(); }
    if (!m_heapAdvances.empty()) { m_heapAdvances.clear(); m_heapAdvances.shrink_to_fit(); }
    if (!m_heapOffsets.empty()) { m_heapOffsets.clear(); m_heapOffsets.shrink_to_fit(); }
    m_contentSize = Size(0, 0);
    m_baseline = 0;
    m_padLeft = 0;
    m_padRight = 0;
    m_padTop = 0;
    m_padBottom = 0;
    m_hasInkBounds = false;

    if (FontFace == nullptr)
        return;

    auto rawFace = FontFace->GetFontFace();
    if (rawFace == nullptr)
        return;

    DWRITE_FONT_METRICS fontMetrics;
    rawFace->GetMetrics(&fontMetrics);

    float emSize = static_cast<float>(FontSize);
    if (emSize <= 0) emSize = 24.0f;
    float designUnitsPerEm = static_cast<float>(fontMetrics.designUnitsPerEm);
    if (designUnitsPerEm <= 0) designUnitsPerEm = 2048.0f;
    float emScale = emSize / designUnitsPerEm;

    float ascent = fontMetrics.ascent * emScale;
    float descent = fontMetrics.descent * emScale;
    m_baseline = ascent;
    float totalHeight = ascent + descent;

    Platform::String^ indicesStr = Indices;
    if (indicesStr != nullptr && indicesStr->Length() > 0)
    {
        const wchar_t* p = indicesStr->Data();
        const wchar_t* end = p + indicesStr->Length();

        size_t count = 0;

        while (p < end)
        {
            while (p < end && iswspace(*p)) p++;
            if (p >= end) break;

            if (*p == L'(')
            {
                while (p < end && *p != L')') p++;
                if (p < end) p++;
            }

            wchar_t* next = nullptr;
            long val = wcstol(p, &next, 10);
            if (next == p) break;

            UINT16 gid = static_cast<UINT16>(val);
            p = next;

            float adv = -1.0f;
            if (p < end && *p == L',')
            {
                p++;
                float advVal = wcstof(p, &next);
                if (next != p)
                {
                    adv = advVal * (emSize / 100.0f);
                    p = next;
                }
            }

            float uOff = 0.0f;
            float vOff = 0.0f;
            if (p < end && *p == L',')
            {
                p++;
                uOff = wcstof(p, &next) * (emSize / 100.0f);
                p = next;
                if (p < end && *p == L',')
                {
                    p++;
                    vOff = wcstof(p, &next) * (emSize / 100.0f);
                    p = next;
                }
            }

            DWRITE_GLYPH_OFFSET off{};
            off.advanceOffset = uOff;
            off.ascenderOffset = vOff;

            if (adv < 0.0f)
            {
                INT32 designAdvance = 0;
                rawFace->GetDesignGlyphAdvances(1, &gid, &designAdvance, FALSE);
                adv = designAdvance * emScale;
            }

            if (count < SBO_GLYPH_CAPACITY)
            {
                m_inlineIndices[count] = gid;
                m_inlineAdvances[count] = adv;
                m_inlineOffsets[count] = off;
            }
            else
            {
                if (count == SBO_GLYPH_CAPACITY)
                {
                    m_heapIndices.assign(m_inlineIndices, m_inlineIndices + SBO_GLYPH_CAPACITY);
                    m_heapAdvances.assign(m_inlineAdvances, m_inlineAdvances + SBO_GLYPH_CAPACITY);
                    m_heapOffsets.assign(m_inlineOffsets, m_inlineOffsets + SBO_GLYPH_CAPACITY);
                }
                m_heapIndices.push_back(gid);
                m_heapAdvances.push_back(adv);
                m_heapOffsets.push_back(off);
            }
            count++;

            while (p < end && (*p == L';' || iswspace(*p))) p++;
        }

        m_glyphCount = count;
        if (m_glyphCount <= SBO_GLYPH_CAPACITY)
        {
            if (!m_heapIndices.empty()) { m_heapIndices.clear(); m_heapIndices.shrink_to_fit(); }
            if (!m_heapAdvances.empty()) { m_heapAdvances.clear(); m_heapAdvances.shrink_to_fit(); }
            if (!m_heapOffsets.empty()) { m_heapOffsets.clear(); m_heapOffsets.shrink_to_fit(); }
        }
    }
    else if (UnicodeString != nullptr && UnicodeString->Length() > 0)
    {
        auto typography = Typography;
        bool shouldShape = IsTextShapingEnabled || (typography != nullptr && typography->FeatureCount > 0);
        bool shaped = false;

        if (shouldShape)
        {
            std::vector<DWRITE_FONT_FEATURE> features;
            if (typography != nullptr && typography->FeatureCount > 0)
                features = typography->GetDWriteFontFeatures();

            UINT32 len = UnicodeString->Length();
            const wchar_t* strData = UnicodeString->Data();

            // Zero-allocation stack fast path for single code point (single character or surrogate pair)
            if (len == 1 || (len == 2 && strData[0] >= 0xD800 && strData[0] <= 0xDBFF && strData[1] >= 0xDC00 && strData[1] <= 0xDFFF))
            {
                size_t actualCount = 0;
                if (TypographyAnalyzer::ShapeSingleCodePoint(
                    rawFace.Get(),
                    strData,
                    len,
                    static_cast<FLOAT>(emSize),
                    features,
                    m_inlineIndices,
                    m_inlineAdvances,
                    m_inlineOffsets,
                    SBO_GLYPH_CAPACITY,
                    actualCount))
                {
                    m_glyphCount = actualCount;
                    shaped = true;
                }
            }

            if (!shaped)
            {
                shaped = TypographyAnalyzer::ShapeGlyphs(
                    rawFace.Get(),
                    UnicodeString->Data(),
                    UnicodeString->Length(),
                    static_cast<FLOAT>(emSize),
                    features,
                    m_heapIndices,
                    m_heapAdvances,
                    m_heapOffsets);

                if (shaped)
                {
                    m_glyphCount = m_heapIndices.size();
                    if (m_glyphCount <= SBO_GLYPH_CAPACITY)
                    {
                        for (size_t i = 0; i < m_glyphCount; i++)
                        {
                            m_inlineIndices[i] = m_heapIndices[i];
                            m_inlineAdvances[i] = m_heapAdvances[i];
                            m_inlineOffsets[i] = m_heapOffsets[i];
                        }
                        m_heapIndices.clear();
                        m_heapAdvances.clear();
                        m_heapOffsets.clear();
                    }
                }
            }
        }

        if (!shaped)
        {
            UINT32 len = UnicodeString->Length();
            const wchar_t* strData = UnicodeString->Data();

            // Fast path for single code point (single character or surrogate pair) - eliminates 4 heap allocations
            if (len == 1 || (len == 2 && strData[0] >= 0xD800 && strData[0] <= 0xDBFF && strData[1] >= 0xDC00 && strData[1] <= 0xDFFF))
            {
                UINT32 cp = len == 1 ? strData[0] : (0x10000 + ((strData[0] - 0xD800) << 10) + (strData[1] - 0xDC00));
                m_glyphCount = 1;
                rawFace->GetGlyphIndices(&cp, 1, &m_inlineIndices[0]);
                INT32 designAdvance = 0;
                rawFace->GetDesignGlyphAdvances(1, &m_inlineIndices[0], &designAdvance, FALSE);
                m_inlineAdvances[0] = designAdvance * emScale;
                m_inlineOffsets[0] = DWRITE_GLYPH_OFFSET{};
            }
            else
            {
                std::vector<UINT32> codePoints;
                for (UINT32 i = 0; i < len; i++)
                {
                    wchar_t ch = strData[i];
                    if (ch >= 0xD800 && ch <= 0xDBFF && i + 1 < len && strData[i + 1] >= 0xDC00 && strData[i + 1] <= 0xDFFF)
                    {
                        UINT32 cp = 0x10000 + ((ch - 0xD800) << 10) + (strData[i + 1] - 0xDC00);
                        codePoints.push_back(cp);
                        i++;
                    }
                    else
                    {
                        codePoints.push_back(ch);
                    }
                }

                m_glyphCount = codePoints.size();
                if (m_glyphCount <= SBO_GLYPH_CAPACITY)
                {
                    rawFace->GetGlyphIndices(codePoints.data(), static_cast<UINT32>(codePoints.size()), m_inlineIndices);
                    for (size_t i = 0; i < m_glyphCount; i++)
                    {
                        INT32 designAdvance = 0;
                        rawFace->GetDesignGlyphAdvances(1, &m_inlineIndices[i], &designAdvance, FALSE);
                        m_inlineAdvances[i] = designAdvance * emScale;
                        m_inlineOffsets[i] = DWRITE_GLYPH_OFFSET{};
                    }
                }
                else
                {
                    m_heapIndices.resize(codePoints.size());
                    m_heapAdvances.resize(codePoints.size());
                    m_heapOffsets.resize(codePoints.size(), DWRITE_GLYPH_OFFSET{});

                    rawFace->GetGlyphIndices(codePoints.data(), static_cast<UINT32>(codePoints.size()), m_heapIndices.data());

                    for (size_t i = 0; i < m_glyphCount; i++)
                    {
                        INT32 designAdvance = 0;
                        rawFace->GetDesignGlyphAdvances(1, &m_heapIndices[i], &designAdvance, FALSE);
                        m_heapAdvances[i] = designAdvance * emScale;
                    }
                }
            }
        }
    }

    if (m_glyphCount == 0)
        return;

    // Calculate logical layout bounds matching Glyphs.cpp CalculateBounds
    float layoutLeft = 0.0f;
    float layoutRight = 0.0f;
    float currentX = 0.0f;
    for (size_t i = 0; i < m_glyphCount; i++)
    {
        float uOff = GetGlyphOffset(i).advanceOffset;
        float glyphX = currentX + uOff;
        float adv = GetGlyphAdvance(i);
        if (i == 0)
        {
            layoutLeft = glyphX;
            layoutRight = glyphX + adv;
        }
        else
        {
            if (glyphX < layoutLeft)
                layoutLeft = glyphX;
            if (glyphX + adv > layoutRight)
                layoutRight = glyphX + adv;
        }
        currentX += adv;
    }
    float totalAdvance = currentX;

    struct OutlineBoundsSink : public IDWriteGeometrySink
    {
        float minX = 1e9f;
        float minY = 1e9f;
        float maxX = -1e9f;
        float maxY = -1e9f;
        bool hasPoints = false;

        ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
        ULONG STDMETHODCALLTYPE Release() override { return 1; }
        HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
        {
            if (riid == __uuidof(IDWriteGeometrySink) || riid == __uuidof(ID2D1SimplifiedGeometrySink) || riid == __uuidof(IUnknown))
            {
                *ppv = this;
                return S_OK;
            }
            *ppv = nullptr;
            return E_NOINTERFACE;
        }

        void Update(float x, float y)
        {
            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
            hasPoints = true;
        }

        void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE) override {}
        void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT) override {}
        void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN) override { Update(startPoint.x, startPoint.y); }
        void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* points, UINT32 pointsCount) override
        {
            for (UINT32 i = 0; i < pointsCount; ++i) Update(points[i].x, points[i].y);
        }
        void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* beziers, UINT32 beziersCount) override
        {
            for (UINT32 i = 0; i < beziersCount; ++i)
            {
                Update(beziers[i].point1.x, beziers[i].point1.y);
                Update(beziers[i].point2.x, beziers[i].point2.y);
                Update(beziers[i].point3.x, beziers[i].point3.y);
            }
        }
        void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END) override {}
        HRESULT STDMETHODCALLTYPE Close() override { return S_OK; }
    };

    OutlineBoundsSink outlineSink;
    if (rawFace != nullptr && m_glyphCount > 0)
    {
        rawFace->GetGlyphRunOutline(
            emSize,
            GlyphIndicesData(),
            GlyphAdvancesData(),
            GlyphOffsetsData(),
            static_cast<UINT32>(m_glyphCount),
            FALSE,
            FALSE,
            &outlineSink
        );
    }

    float minInkLeft = 0.0f;
    float maxInkRight = totalAdvance;
    float maxTopOverhang = 0.0f;
    float maxBottomOverhang = 0.0f;

    if (outlineSink.hasPoints && fontMetrics.designUnitsPerEm > 0)
    {
        m_hasInkBounds = true;
        m_designInkLeft = outlineSink.minX / emScale;
        m_designInkWidth = (outlineSink.maxX - outlineSink.minX) / emScale;
        m_designInkTop = (ascent + outlineSink.minY) / emScale;
        m_designInkHeight = (outlineSink.maxY - outlineSink.minY) / emScale;
        m_designUnitsPerEm = fontMetrics.designUnitsPerEm;

        if (totalAdvance <= 0.0f)
            totalAdvance = static_cast<float>(outlineSink.maxX - outlineSink.minX);

        minInkLeft = (std::min)(0.0f, outlineSink.minX);
        maxInkRight = (std::max)(totalAdvance, outlineSink.maxX);
        if (ascent + outlineSink.minY < 0.0f)
            maxTopOverhang = -(ascent + outlineSink.minY);
        if (outlineSink.maxY > descent)
            maxBottomOverhang = outlineSink.maxY - descent;
    }
    else
    {
        Rect inkBounds = FontFace->GetDesignGlyphBounds(GetGlyphIndex(0));
        if (inkBounds.Width > 0 && fontMetrics.designUnitsPerEm > 0)
        {
            m_hasInkBounds = true;
            m_designInkLeft = inkBounds.X;
            m_designInkWidth = inkBounds.Width;
            m_designInkTop = inkBounds.Y;
            m_designInkHeight = inkBounds.Height;
            m_designUnitsPerEm = fontMetrics.designUnitsPerEm;

            if (totalAdvance <= 0.0f)
                totalAdvance = static_cast<float>(inkBounds.Width * emScale);

            float inkL = static_cast<float>(inkBounds.X * emScale);
            float inkR = static_cast<float>((inkBounds.X + inkBounds.Width) * emScale);
            if (inkL < minInkLeft) minInkLeft = inkL;
            if (inkR > maxInkRight) maxInkRight = inkR;
        }

        // Query design glyph metrics to determine ink overhangs for padding
        DWRITE_GLYPH_METRICS stackMetrics[8];
        std::vector<DWRITE_GLYPH_METRICS> heapMetrics;
        DWRITE_GLYPH_METRICS* metrics = stackMetrics;
        if (m_glyphCount > 8)
        {
            heapMetrics.resize(m_glyphCount);
            metrics = heapMetrics.data();
        }
        rawFace->GetDesignGlyphMetrics(GlyphIndicesData(), static_cast<UINT32>(m_glyphCount), metrics, FALSE);

        currentX = 0.0f;
        for (size_t i = 0; i < m_glyphCount; i++)
        {
            float uOff = GetGlyphOffset(i).advanceOffset;
            float vOff = GetGlyphOffset(i).ascenderOffset;
            float glyphX = currentX + uOff;

            float inkLeft = glyphX + (metrics[i].leftSideBearing * emScale);
            float inkRight = glyphX + (static_cast<INT32>(metrics[i].advanceWidth) - metrics[i].rightSideBearing) * emScale;

            if (inkLeft < minInkLeft)
                minInkLeft = inkLeft;
            if (inkRight > maxInkRight)
                maxInkRight = inkRight;

            if (metrics[i].advanceHeight > 0)
            {
                float topDesign = static_cast<float>(metrics[i].verticalOriginY - metrics[i].topSideBearing);
                float topPixels = (topDesign * emScale) + vOff;
                if (topPixels > ascent)
                    maxTopOverhang = (std::max)(maxTopOverhang, topPixels - ascent);

                float blackBoxHeight = static_cast<float>(static_cast<INT32>(metrics[i].advanceHeight) - metrics[i].topSideBearing - metrics[i].bottomSideBearing);
                float bottomDesign = topDesign - blackBoxHeight;
                float bottomPixels = (-bottomDesign * emScale) - vOff;
                if (bottomPixels > descent)
                    maxBottomOverhang = (std::max)(maxBottomOverhang, bottomPixels - descent);
            }
            else if (vOff != 0.0f)
            {
                if (vOff > 0.0f)
                    maxTopOverhang = (std::max)(maxTopOverhang, vOff);
                else
                    maxBottomOverhang = (std::max)(maxBottomOverhang, -vOff);
            }

            currentX += GetGlyphAdvance(i);
        }

        ComPtr<IDWriteFontFace1> rawFace1;
        if (SUCCEEDED(rawFace.As(&rawFace1)))
        {
            DWRITE_FONT_METRICS1 fontMetrics1;
            rawFace1->GetMetrics(&fontMetrics1);

            if (fontMetrics1.glyphBoxTop > fontMetrics.ascent)
            {
                float topOverhang = (fontMetrics1.glyphBoxTop - fontMetrics.ascent) * emScale;
                maxTopOverhang = (std::max)(maxTopOverhang, topOverhang);
            }
            if (-fontMetrics1.glyphBoxBottom > fontMetrics.descent)
            {
                float bottomOverhang = (-fontMetrics1.glyphBoxBottom - fontMetrics.descent) * emScale;
                maxBottomOverhang = (std::max)(maxBottomOverhang, bottomOverhang);
            }
        }
    }

    float totalWidth = (std::max)(totalAdvance, layoutRight - layoutLeft);
    m_contentSize = Size(totalWidth, totalHeight);

    float extraPad = (std::max)(6.0f, emSize * 0.15f);

    m_padLeft = std::ceil(extraPad + (std::max)(0.0f, -minInkLeft));
    m_padRight = std::ceil(extraPad + (std::max)(0.0f, maxInkRight - totalWidth));
    m_padTop = std::ceil(extraPad + (std::max)({ 0.0f, maxTopOverhang, ascent * 0.20f }));
    m_padBottom = std::ceil(extraPad + (std::max)({ 0.0f, maxBottomOverhang, descent * 0.20f }));
}

static inline bool IsCloseReal(double a, double b)
{
    return std::abs((a - b) / ((b == 0.0) ? 1.0 : b)) < 10.0 * REAL_EPSILON;
}

Size FontGlyphs::ComputeScaleFactor(Size availableSize, Size contentSize)
{
    if (Stretch == Windows::UI::Xaml::Media::Stretch::None)
        return Size(1, 1);

    bool isConstrainedWidth = !std::isinf(availableSize.Width);
    bool isConstrainedHeight = !std::isinf(availableSize.Height);

    if (!isConstrainedWidth && !isConstrainedHeight)
        return Size(1, 1);

    bool isZeroWidth = IsCloseReal(contentSize.Width, 0.0) || contentSize.Width <= 0.0;
    bool isZeroHeight = IsCloseReal(contentSize.Height, 0.0) || contentSize.Height <= 0.0;

    if (isZeroWidth && isZeroHeight)
        return Size(1, 1);

    if (StretchDirection == Windows::UI::Xaml::Controls::StretchDirection::DownOnly
        && (isZeroWidth || !isConstrainedWidth || contentSize.Width <= availableSize.Width)
        && (isZeroHeight || !isConstrainedHeight || contentSize.Height <= availableSize.Height))
    {
        return Size(1, 1);
    }

    double scaleX = isZeroWidth ? 1.0 : (availableSize.Width / contentSize.Width);
    double scaleY = isZeroHeight ? 1.0 : (availableSize.Height / contentSize.Height);

    if (isZeroWidth)
        scaleX = scaleY;
    else if (isZeroHeight)
        scaleY = scaleX;

    if (!isConstrainedWidth)
        scaleX = scaleY;
    else if (!isConstrainedHeight)
        scaleY = scaleX;
    else
    {
        switch (Stretch)
        {
        case Windows::UI::Xaml::Media::Stretch::Uniform:
            scaleX = scaleY = (std::min)(scaleX, scaleY);
            break;
        case Windows::UI::Xaml::Media::Stretch::UniformToFill:
            scaleX = scaleY = (std::max)(scaleX, scaleY);
            break;
        case Windows::UI::Xaml::Media::Stretch::Fill:
        default:
            break;
        }
    }

    switch (StretchDirection)
    {
    case Windows::UI::Xaml::Controls::StretchDirection::UpOnly:
        scaleX = (std::max)(1.0, scaleX);
        scaleY = (std::max)(1.0, scaleY);
        break;
    case Windows::UI::Xaml::Controls::StretchDirection::Both:
        break;
    case Windows::UI::Xaml::Controls::StretchDirection::DownOnly:
    default:
        scaleX = (std::min)(1.0, scaleX);
        scaleY = (std::min)(1.0, scaleY);
        break;
    }

    return Size((float)scaleX, (float)scaleY);
}

Size FontGlyphs::MeasureOverride(Size availableSize)
{
    if (IsBackgroundRenderEnabled)
        return Size(static_cast<float>((std::max)(0.0, this->MinWidth)), static_cast<float>((std::max)(0.0, this->MinHeight)));

    if (m_isLayoutDirty)
    {
        m_isLayoutDirty = false;
        ParseAndLayoutGlyphs();
    }

    Size fitContentSize;
    if (m_hasInkBounds && m_designInkHeight > 0)
    {
        double emScale = FontSize / m_designUnitsPerEm;
        double inkLeft = m_designInkLeft * emScale;
        double inkWidth = m_designInkWidth * emScale;
        double inkHeight = m_designInkHeight * emScale;

        double visualWidth;
        if (m_glyphCount == 0 || GetGlyphAdvance(0) <= 0.0f)
            visualWidth = inkWidth;
        else
            visualWidth = (std::max)(static_cast<double>(m_contentSize.Width), (inkLeft + inkWidth) - (std::min)(0.0, inkLeft));

        // Use the glyph's actual visual height (ink height), not the entire font's theoretical line box.
        // In script fonts like Samantha, ascent + descent can exceed 3x the EM size, which would otherwise
        // force every single normal glyph to downscale to a tiny fraction of its intended size.
        double visualHeight = inkHeight;

        fitContentSize = Size(static_cast<float>(visualWidth), static_cast<float>(visualHeight));
    }
    else
    {
        fitContentSize = m_contentSize;
    }

    m_scale = ComputeScaleFactor(availableSize, fitContentSize);
    if (StretchDirection == Windows::UI::Xaml::Controls::StretchDirection::DownOnly)
    {
        m_scale.Width = (std::min)(1.0f, m_scale.Width);
        m_scale.Height = (std::min)(1.0f, m_scale.Height);
    }

    return Size(m_scale.Width * fitContentSize.Width, m_scale.Height * fitContentSize.Height);
}

Size FontGlyphs::ArrangeOverride(Size finalSize)
{
    if (IsBackgroundRenderEnabled)
    {
        if (m_isLayoutDirty || m_isRenderDirty || m_lastBgArrangeSize.Width != finalSize.Width || m_lastBgArrangeSize.Height != finalSize.Height || m_drawingSurface == nullptr)
        {
            m_isLayoutDirty = false;
            m_isRenderDirty = false;
            m_lastBgArrangeSize = finalSize;
            RenderGlyphsBackground(finalSize);
        }
        return finalSize;
    }

    if (m_isLayoutDirty)
    {
        m_isLayoutDirty = false;
        ParseAndLayoutGlyphs();
    }

    if (m_contentSize.Width <= 0 || m_contentSize.Height <= 0)
        return finalSize;

    Size fitContentSize;
    if (m_hasInkBounds && m_designInkHeight > 0)
    {
        double emScale = FontSize / m_designUnitsPerEm;
        double inkLeft = m_designInkLeft * emScale;
        double inkWidth = m_designInkWidth * emScale;
        double inkHeight = m_designInkHeight * emScale;

        double visualWidth;
        if (m_glyphCount == 0 || GetGlyphAdvance(0) <= 0.0f)
            visualWidth = inkWidth;
        else
            visualWidth = (std::max)(static_cast<double>(m_contentSize.Width), (inkLeft + inkWidth) - (std::min)(0.0, inkLeft));

        double visualHeight = inkHeight;

        fitContentSize = Size(static_cast<float>(visualWidth), static_cast<float>(visualHeight));
    }
    else
    {
        fitContentSize = m_contentSize;
    }

    m_scale = ComputeScaleFactor(finalSize, fitContentSize);
    if (StretchDirection == Windows::UI::Xaml::Controls::StretchDirection::DownOnly)
    {
        m_scale.Width = (std::min)(1.0f, m_scale.Width);
        m_scale.Height = (std::min)(1.0f, m_scale.Height);
    }

    if (m_isRenderDirty || m_renderedScaleX != m_scale.Width || m_renderedScaleY != m_scale.Height)
    {
        m_isRenderDirty = false;
        RenderGlyphs();
    }

    float scaleX = m_scale.Width > 0.0f ? m_scale.Width : 1.0f;
    float scaleY = m_scale.Height > 0.0f ? m_scale.Height : 1.0f;

    double scaledContentWidth = m_contentSize.Width * scaleX;
    double scaledContentHeight = m_contentSize.Height * scaleY;
    double scaledPadLeft = m_padLeft * scaleX;
    double scaledPadTop = m_padTop * scaleY;

    double scaledLogicalWidth = (m_contentSize.Width + m_padLeft + m_padRight) * scaleX;
    double scaledLogicalHeight = (m_contentSize.Height + m_padTop + m_padBottom) * scaleY;

    // Horizontal positioning:
    // For normal glyphs with advance > 0, center within advance width to preserve natural bearings.
    // For zero-advance glyphs (combining marks / diacritics), center ink bounds so they remain visible.
    double originX = (finalSize.Width - scaledContentWidth) / 2.0;
    if (m_hasInkBounds)
    {
        double emScale = FontSize / m_designUnitsPerEm;
        double inkLeft = m_designInkLeft * emScale * scaleX;
        double inkWidth = m_designInkWidth * emScale * scaleX;

        if (m_glyphCount == 0 || GetGlyphAdvance(0) <= 0.0f)
        {
            originX = (finalSize.Width - inkWidth) / 2.0 - inkLeft;
        }
        else
        {
            if (originX + inkLeft < 0)
                originX = -inkLeft;
            if (originX + inkLeft + inkWidth > finalSize.Width)
                originX = finalSize.Width - (inkLeft + inkWidth);
        }
    }

    // Vertical positioning:
    // Center the font's logical line box (m_contentSize.Height = ascent + descent).
    // This ensures every glyph shares the exact same baseline (originY + ascent * scaleY),
    // preserving superscripts, subscripts, periods, and descenders in their true typographic positions.
    double originY = (finalSize.Height - scaledContentHeight) / 2.0;
    if (m_hasInkBounds && m_designInkHeight > 0)
    {
        double emScale = FontSize / m_designUnitsPerEm;
        double inkTop = m_designInkTop * emScale * scaleY;
        double inkHeight = m_designInkHeight * emScale * scaleY;

        // Only adjust if extreme ascenders/descenders would be clipped
        if (originY + inkTop < 0)
            originY = -inkTop;
        if (originY + inkTop + inkHeight > finalSize.Height)
            originY = finalSize.Height - (inkTop + inkHeight);
    }

    float posX = (float)(originX - scaledPadLeft);
    float posY = (float)(originY - scaledPadTop);

    if (m_spriteVisual != nullptr)
    {
        m_spriteVisual->Offset = float3(posX, posY, 0.0f);
        m_spriteVisual->Size = float2(static_cast<float>(scaledLogicalWidth), static_cast<float>(scaledLogicalHeight));
    }

    return finalSize;
}

namespace
{
    struct FontGlyphsRenderJob
    {
        const void* ControlKey = nullptr;
        Platform::WeakReference WeakThis;
        uint64_t Generation = 0;

        DWriteFontFace^ FontFace = nullptr;
        FLOAT FontSize = 24.0f;
        Platform::String^ Indices = nullptr;
        Platform::String^ UnicodeString = nullptr;
        DWriteTypographyCollection^ Typography = nullptr;
        bool IsTextShapingEnabled = false;
        Windows::UI::Xaml::Media::StyleSimulations Simulations = Windows::UI::Xaml::Media::StyleSimulations::None;
        Windows::UI::Xaml::Media::Stretch Stretch = Windows::UI::Xaml::Media::Stretch::Uniform;
        Windows::UI::Xaml::Controls::StretchDirection StretchDirection = Windows::UI::Xaml::Controls::StretchDirection::DownOnly;
        Windows::Foundation::Size FinalSize{};
        float DpiScale = 1.0f;
        bool RenderColor = false;
        Windows::UI::Color ForegroundColor{};
        LONG RequiredWidth = 0;
        LONG RequiredHeight = 0;
        CompositionDrawingSurface^ Surface = nullptr;
    };

    struct GlyphLayoutResult
    {
        std::vector<UINT16> Indices;
        std::vector<FLOAT> Advances;
        std::vector<DWRITE_GLYPH_OFFSET> Offsets;
        Size ContentSize{ 0, 0 };
        float Baseline = 0.0f;
        float PadLeft = 0.0f;
        float PadRight = 0.0f;
        float PadTop = 0.0f;
        float PadBottom = 0.0f;
        bool HasInkBounds = false;
        double DesignInkLeft = 0.0;
        double DesignInkWidth = 0.0;
        double DesignInkTop = 0.0;
        double DesignInkHeight = 0.0;
        double DesignUnitsPerEm = 0.0;
    };

    Size ComputeScaleFactorFor(
        Windows::UI::Xaml::Media::Stretch stretch,
        Windows::UI::Xaml::Controls::StretchDirection stretchDirection,
        Size availableSize,
        Size contentSize)
    {
        double scaleX = 1.0;
        double scaleY = 1.0;

        bool isConstrainedWidth = !std::isinf(availableSize.Width);
        bool isConstrainedHeight = !std::isinf(availableSize.Height);

        if ((stretch == Windows::UI::Xaml::Media::Stretch::Uniform ||
             stretch == Windows::UI::Xaml::Media::Stretch::UniformToFill ||
             stretch == Windows::UI::Xaml::Media::Stretch::Fill) &&
            (isConstrainedWidth || isConstrainedHeight))
        {
            scaleX = (contentSize.Width <= REAL_EPSILON) ? 0.0 : availableSize.Width / contentSize.Width;
            scaleY = (contentSize.Height <= REAL_EPSILON) ? 0.0 : availableSize.Height / contentSize.Height;

            if (!isConstrainedWidth)
                scaleX = scaleY;
            else if (!isConstrainedHeight)
                scaleY = scaleX;
            else
            {
                switch (stretch)
                {
                case Windows::UI::Xaml::Media::Stretch::Uniform:
                {
                    double minScale = (std::min)(scaleX, scaleY);
                    scaleX = scaleY = minScale;
                    break;
                }
                case Windows::UI::Xaml::Media::Stretch::UniformToFill:
                {
                    double maxScale = (std::max)(scaleX, scaleY);
                    scaleX = scaleY = maxScale;
                    break;
                }
                case Windows::UI::Xaml::Media::Stretch::Fill:
                default:
                    break;
                }
            }

            switch (stretchDirection)
            {
            case Windows::UI::Xaml::Controls::StretchDirection::UpOnly:
                if (scaleX < 1.0) scaleX = 1.0;
                if (scaleY < 1.0) scaleY = 1.0;
                break;
            case Windows::UI::Xaml::Controls::StretchDirection::DownOnly:
                if (scaleX > 1.0) scaleX = 1.0;
                if (scaleY > 1.0) scaleY = 1.0;
                break;
            case Windows::UI::Xaml::Controls::StretchDirection::Both:
            default:
                break;
            }
        }

        return Size(static_cast<float>(scaleX), static_cast<float>(scaleY));
    }

    bool ComputeGlyphLayoutForJob(const FontGlyphsRenderJob& job, GlyphLayoutResult& out)
    {
        out.Indices.clear();
        out.Advances.clear();
        out.Offsets.clear();
        out.ContentSize = Size(0, 0);
        out.Baseline = 0.0f;
        out.PadLeft = 0.0f;
        out.PadRight = 0.0f;
        out.PadTop = 0.0f;
        out.PadBottom = 0.0f;
        out.HasInkBounds = false;
        out.DesignInkLeft = 0.0;
        out.DesignInkWidth = 0.0;
        out.DesignInkTop = 0.0;
        out.DesignInkHeight = 0.0;
        out.DesignUnitsPerEm = 0.0;

        if (job.FontFace == nullptr)
            return false;

        auto rawFace = job.FontFace->GetFontFace();
        if (rawFace == nullptr)
            return false;

        DWRITE_FONT_METRICS fontMetrics{};
        rawFace->GetMetrics(&fontMetrics);

        float emSize = job.FontSize > 0.0f ? job.FontSize : 24.0f;
        float designUnitsPerEm = static_cast<float>(fontMetrics.designUnitsPerEm);
        if (designUnitsPerEm <= 0.0f) designUnitsPerEm = 2048.0f;
        float emScale = emSize / designUnitsPerEm;

        float ascent = fontMetrics.ascent * emScale;
        float descent = fontMetrics.descent * emScale;
        out.Baseline = ascent;
        float totalHeight = ascent + descent;

        if (job.Indices != nullptr && job.Indices->Length() > 0)
        {
            const wchar_t* p = job.Indices->Data();
            const wchar_t* end = p + job.Indices->Length();
            while (p < end)
            {
                while (p < end && iswspace(*p)) p++;
                if (p >= end) break;
                if (*p == L'(')
                {
                    while (p < end && *p != L')') p++;
                    if (p < end) p++;
                }

                wchar_t* next = nullptr;
                long val = wcstol(p, &next, 10);
                if (next == p) break;

                UINT16 gid = static_cast<UINT16>(val);
                p = next;

                float adv = -1.0f;
                if (p < end && *p == L',')
                {
                    p++;
                    float advVal = wcstof(p, &next);
                    if (next != p)
                    {
                        adv = advVal * (emSize / 100.0f);
                        p = next;
                    }
                }

                float uOff = 0.0f;
                float vOff = 0.0f;
                if (p < end && *p == L',')
                {
                    p++;
                    uOff = wcstof(p, &next) * (emSize / 100.0f);
                    p = next;
                    if (p < end && *p == L',')
                    {
                        p++;
                        vOff = wcstof(p, &next) * (emSize / 100.0f);
                        p = next;
                    }
                }

                if (adv < 0.0f)
                {
                    INT32 designAdvance = 0;
                    rawFace->GetDesignGlyphAdvances(1, &gid, &designAdvance, FALSE);
                    adv = designAdvance * emScale;
                }

                out.Indices.push_back(gid);
                out.Advances.push_back(adv);
                out.Offsets.push_back(DWRITE_GLYPH_OFFSET{ uOff, vOff });

                while (p < end && (*p == L';' || iswspace(*p))) p++;
            }
        }
        else if (job.UnicodeString != nullptr && job.UnicodeString->Length() > 0)
        {
            bool shouldShape = job.IsTextShapingEnabled || (job.Typography != nullptr && job.Typography->FeatureCount > 0);
            bool shaped = false;

            if (shouldShape)
            {
                std::vector<DWRITE_FONT_FEATURE> features;
                if (job.Typography != nullptr && job.Typography->FeatureCount > 0)
                    features = job.Typography->GetDWriteFontFeatures();

                shaped = TypographyAnalyzer::ShapeGlyphs(
                    rawFace.Get(),
                    job.UnicodeString->Data(),
                    job.UnicodeString->Length(),
                    emSize,
                    features,
                    out.Indices,
                    out.Advances,
                    out.Offsets);
            }

            if (!shaped)
            {
                UINT32 len = job.UnicodeString->Length();
                const wchar_t* strData = job.UnicodeString->Data();
                std::vector<UINT32> codePoints;
                codePoints.reserve(len);
                for (UINT32 i = 0; i < len; i++)
                {
                    wchar_t ch = strData[i];
                    if (ch >= 0xD800 && ch <= 0xDBFF && i + 1 < len && strData[i + 1] >= 0xDC00 && strData[i + 1] <= 0xDFFF)
                    {
                        codePoints.push_back(0x10000 + ((ch - 0xD800) << 10) + (strData[i + 1] - 0xDC00));
                        i++;
                    }
                    else
                        codePoints.push_back(ch);
                }

                size_t count = codePoints.size();
                out.Indices.resize(count);
                out.Advances.resize(count);
                out.Offsets.resize(count, DWRITE_GLYPH_OFFSET{});
                rawFace->GetGlyphIndices(codePoints.data(), static_cast<UINT32>(count), out.Indices.data());
                for (size_t i = 0; i < count; i++)
                {
                    INT32 designAdvance = 0;
                    rawFace->GetDesignGlyphAdvances(1, &out.Indices[i], &designAdvance, FALSE);
                    out.Advances[i] = designAdvance * emScale;
                }
            }
        }

        if (out.Indices.empty())
            return false;

        float layoutLeft = 0.0f;
        float layoutRight = 0.0f;
        float currentX = 0.0f;
        for (size_t i = 0; i < out.Indices.size(); i++)
        {
            float glyphX = currentX + out.Offsets[i].advanceOffset;
            float adv = out.Advances[i];
            if (i == 0)
            {
                layoutLeft = glyphX;
                layoutRight = glyphX + adv;
            }
            else
            {
                if (glyphX < layoutLeft) layoutLeft = glyphX;
                if (glyphX + adv > layoutRight) layoutRight = glyphX + adv;
            }
            currentX += adv;
        }
        float totalAdvance = currentX;

        struct OutlineBoundsSink : public IDWriteGeometrySink
        {
            float minX = 1e9f, minY = 1e9f, maxX = -1e9f, maxY = -1e9f;
            bool hasPoints = false;
            ULONG STDMETHODCALLTYPE AddRef() override { return 1; }
            ULONG STDMETHODCALLTYPE Release() override { return 1; }
            HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppv) override
            {
                if (riid == __uuidof(IDWriteGeometrySink) || riid == __uuidof(ID2D1SimplifiedGeometrySink) || riid == __uuidof(IUnknown))
                {
                    *ppv = this;
                    return S_OK;
                }
                *ppv = nullptr;
                return E_NOINTERFACE;
            }
            void Update(float x, float y)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                hasPoints = true;
            }
            void STDMETHODCALLTYPE SetFillMode(D2D1_FILL_MODE) override {}
            void STDMETHODCALLTYPE SetSegmentFlags(D2D1_PATH_SEGMENT) override {}
            void STDMETHODCALLTYPE BeginFigure(D2D1_POINT_2F startPoint, D2D1_FIGURE_BEGIN) override { Update(startPoint.x, startPoint.y); }
            void STDMETHODCALLTYPE AddLines(const D2D1_POINT_2F* points, UINT32 pointsCount) override
            {
                for (UINT32 i = 0; i < pointsCount; ++i) Update(points[i].x, points[i].y);
            }
            void STDMETHODCALLTYPE AddBeziers(const D2D1_BEZIER_SEGMENT* beziers, UINT32 beziersCount) override
            {
                for (UINT32 i = 0; i < beziersCount; ++i)
                {
                    Update(beziers[i].point1.x, beziers[i].point1.y);
                    Update(beziers[i].point2.x, beziers[i].point2.y);
                    Update(beziers[i].point3.x, beziers[i].point3.y);
                }
            }
            void STDMETHODCALLTYPE EndFigure(D2D1_FIGURE_END) override {}
            HRESULT STDMETHODCALLTYPE Close() override { return S_OK; }
        };

        OutlineBoundsSink outlineSink;
        rawFace->GetGlyphRunOutline(
            emSize,
            out.Indices.data(),
            out.Advances.data(),
            out.Offsets.data(),
            static_cast<UINT32>(out.Indices.size()),
            FALSE,
            FALSE,
            &outlineSink);

        float minInkLeft = 0.0f;
        float maxInkRight = totalAdvance;
        float maxTopOverhang = 0.0f;
        float maxBottomOverhang = 0.0f;

        if (outlineSink.hasPoints && fontMetrics.designUnitsPerEm > 0)
        {
            out.HasInkBounds = true;
            out.DesignInkLeft = outlineSink.minX / emScale;
            out.DesignInkWidth = (outlineSink.maxX - outlineSink.minX) / emScale;
            out.DesignInkTop = (ascent + outlineSink.minY) / emScale;
            out.DesignInkHeight = (outlineSink.maxY - outlineSink.minY) / emScale;
            out.DesignUnitsPerEm = fontMetrics.designUnitsPerEm;

            if (totalAdvance <= 0.0f)
                totalAdvance = static_cast<float>(outlineSink.maxX - outlineSink.minX);

            minInkLeft = (std::min)(0.0f, outlineSink.minX);
            maxInkRight = (std::max)(totalAdvance, outlineSink.maxX);
            if (ascent + outlineSink.minY < 0.0f)
                maxTopOverhang = -(ascent + outlineSink.minY);
            if (outlineSink.maxY > descent)
                maxBottomOverhang = outlineSink.maxY - descent;
        }
        else if (out.Indices.size() > 1)
        {
            Rect inkBounds = job.FontFace->GetDesignGlyphBounds(out.Indices[0]);
            if (inkBounds.Width > 0 && fontMetrics.designUnitsPerEm > 0)
            {
                out.HasInkBounds = true;
                out.DesignInkLeft = inkBounds.X;
                out.DesignInkWidth = inkBounds.Width;
                out.DesignInkTop = inkBounds.Y;
                out.DesignInkHeight = inkBounds.Height;
                out.DesignUnitsPerEm = fontMetrics.designUnitsPerEm;

                if (totalAdvance <= 0.0f)
                    totalAdvance = static_cast<float>(inkBounds.Width * emScale);

                float inkL = static_cast<float>(inkBounds.X * emScale);
                float inkR = static_cast<float>((inkBounds.X + inkBounds.Width) * emScale);
                if (inkL < minInkLeft) minInkLeft = inkL;
                if (inkR > maxInkRight) maxInkRight = inkR;
            }
        }

        float totalWidth = (std::max)(totalAdvance, layoutRight - layoutLeft);
        out.ContentSize = Size(totalWidth, totalHeight);

        float extraPad = (std::max)(6.0f, emSize * 0.15f);
        out.PadLeft = std::ceil(extraPad + (std::max)(0.0f, -minInkLeft));
        out.PadRight = std::ceil(extraPad + (std::max)(0.0f, maxInkRight - totalWidth));
        out.PadTop = std::ceil(extraPad + (std::max)({ 0.0f, maxTopOverhang, ascent * 0.20f }));
        out.PadBottom = std::ceil(extraPad + (std::max)({ 0.0f, maxBottomOverhang, descent * 0.20f }));
        return out.ContentSize.Width > 0 && out.ContentSize.Height > 0;
    }

    void ExecuteFontGlyphsJob(const FontGlyphsRenderJob& job)
    {
        auto pThis = job.WeakThis.Resolve<FontGlyphs>();
        if (pThis == nullptr || !pThis->IsGenerationCurrent(job.Generation))
            return;

        thread_local GlyphLayoutResult layout;
        if (!ComputeGlyphLayoutForJob(job, layout))
            return;

        auto pCheck = job.WeakThis.Resolve<FontGlyphs>();
        if (pCheck == nullptr || !pCheck->IsGenerationCurrent(job.Generation))
            return;

        Size fitContentSize = layout.ContentSize;
        if (layout.HasInkBounds && layout.DesignInkHeight > 0 && layout.DesignUnitsPerEm > 0)
        {
            double emScale = job.FontSize / layout.DesignUnitsPerEm;
            double inkLeft = layout.DesignInkLeft * emScale;
            double inkWidth = layout.DesignInkWidth * emScale;
            double inkHeight = layout.DesignInkHeight * emScale;
            double visualWidth = (layout.Advances.empty() || layout.Advances[0] <= 0.0f)
                ? inkWidth
                : (std::max)(static_cast<double>(layout.ContentSize.Width), (inkLeft + inkWidth) - (std::min)(0.0, inkLeft));
            fitContentSize = Size(static_cast<float>(visualWidth), static_cast<float>(inkHeight));
        }

        Size scale = ComputeScaleFactorFor(job.Stretch, job.StretchDirection, job.FinalSize, fitContentSize);
        if (job.StretchDirection == Windows::UI::Xaml::Controls::StretchDirection::DownOnly)
        {
            scale.Width = (std::min)(1.0f, scale.Width);
            scale.Height = (std::min)(1.0f, scale.Height);
        }

        float scaleX = scale.Width > 0.0f ? scale.Width : 1.0f;
        float scaleY = scale.Height > 0.0f ? scale.Height : 1.0f;

        double scaledContentWidth = layout.ContentSize.Width * scaleX;
        double scaledContentHeight = layout.ContentSize.Height * scaleY;
        double scaledPadLeft = layout.PadLeft * scaleX;
        double scaledPadTop = layout.PadTop * scaleY;

        double originX = (job.FinalSize.Width - scaledContentWidth) / 2.0;
        if (layout.HasInkBounds && layout.DesignUnitsPerEm > 0)
        {
            double emScale = job.FontSize / layout.DesignUnitsPerEm;
            double inkLeft = layout.DesignInkLeft * emScale * scaleX;
            double inkWidth = layout.DesignInkWidth * emScale * scaleX;

            if (layout.Advances.empty() || layout.Advances[0] <= 0.0f)
                originX = (job.FinalSize.Width - inkWidth) / 2.0 - inkLeft;
            else
            {
                if (originX + inkLeft < 0) originX = -inkLeft;
                if (originX + inkLeft + inkWidth > job.FinalSize.Width)
                    originX = job.FinalSize.Width - (inkLeft + inkWidth);
            }
        }

        double originY = (job.FinalSize.Height - scaledContentHeight) / 2.0;
        if (layout.HasInkBounds && layout.DesignInkHeight > 0 && layout.DesignUnitsPerEm > 0)
        {
            double emScale = job.FontSize / layout.DesignUnitsPerEm;
            double inkTop = layout.DesignInkTop * emScale * scaleY;
            double inkHeight = layout.DesignInkHeight * emScale * scaleY;
            if (originY + inkTop < 0) originY = -inkTop;
            if (originY + inkTop + inkHeight > job.FinalSize.Height)
                originY = job.FinalSize.Height - (inkTop + inkHeight);
        }

        float posX = static_cast<float>(originX - scaledPadLeft);
        float posY = static_cast<float>(originY - scaledPadTop);

        float dpiScale = job.DpiScale;
        if (dpiScale != 1.0f)
        {
            for (float& a : layout.Advances) a *= dpiScale;
            for (DWRITE_GLYPH_OFFSET& o : layout.Offsets)
            {
                o.advanceOffset *= dpiScale;
                o.ascenderOffset *= dpiScale;
            }
        }

        auto rawFace = job.FontFace->GetFontFace();
        if (rawFace == nullptr)
            return;

        thread_local std::vector<uint8_t> offDevicePixels;
        UINT32 offDevicePitch = 0;
        UINT32 surfW = static_cast<UINT32>(job.RequiredWidth);
        UINT32 surfH = static_cast<UINT32>(job.RequiredHeight);

        bool hasOffDevicePixels = CoreDWTextServices::RenderOffDevice(
            surfW,
            surfH,
            job.RenderColor,
            [&](ID2D1DeviceContext* ctx, ID2D1SolidColorBrush* d2dBrush)
            {
                ctx->SetTransform(
                    D2D1::Matrix3x2F::Scale(scaleX, scaleY) *
                    D2D1::Matrix3x2F::Translation(posX * dpiScale, posY * dpiScale));

                DWRITE_GLYPH_RUN glyphRun{};
                glyphRun.fontFace = rawFace.Get();
                glyphRun.fontEmSize = static_cast<FLOAT>(job.FontSize * dpiScale);
                glyphRun.glyphCount = static_cast<UINT32>(layout.Indices.size());
                glyphRun.glyphIndices = layout.Indices.data();
                glyphRun.glyphAdvances = layout.Advances.data();
                glyphRun.glyphOffsets = layout.Offsets.data();
                glyphRun.isSideways = FALSE;
                glyphRun.bidiLevel = 0;

                D2D1_POINT_2F baselineOrigin = D2D1::Point2F(layout.PadLeft * dpiScale, (layout.Baseline + layout.PadTop) * dpiScale);
                D2D1_COLOR_F brushColor = job.RenderColor
                    ? D2D1::ColorF(
                        job.ForegroundColor.R / 255.0f,
                        job.ForegroundColor.G / 255.0f,
                        job.ForegroundColor.B / 255.0f,
                        job.ForegroundColor.A / 255.0f)
                    : D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f);

                d2dBrush->SetColor(brushColor);

                TextRenderParameters renderParams;
                renderParams.ForegroundBrush = d2dBrush;
                renderParams.EnableColor = job.RenderColor;
                renderParams.PreferredFormat = job.RenderColor ? GlyphImageFormat::None : GlyphImageFormat::TrueType;
                renderParams.IsMaskSurface = !job.RenderColor;

                CoreDWTextServices::DrawGlyphRun(ctx, baselineOrigin, &glyphRun, renderParams);
            },
            offDevicePixels,
            offDevicePitch);

        if (hasOffDevicePixels)
        {
            auto pCheck2 = job.WeakThis.Resolve<FontGlyphs>();
            if (pCheck2 == nullptr || !pCheck2->IsGenerationCurrent(job.Generation))
                return;

            ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
            if (job.Surface == nullptr ||
                FAILED(reinterpret_cast<IUnknown*>(job.Surface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop))))
                return;

            if (CoreDWTextServices::UploadOffDevicePixelsToSurface(surfaceInterop.Get(), surfW, surfH, offDevicePixels, offDevicePitch))
            {
                if (auto pDone = job.WeakThis.Resolve<FontGlyphs>())
                    pDone->ShowVisualIfCurrent(job.Generation);
            }
        }
        else
        {
            bool success = CompositionDeviceManager::RenderGlyphToSurface(
                job.Surface,
                job.RenderColor ? GlyphImageFormat::None : GlyphImageFormat::TrueType,
                layout.PadLeft * dpiScale, layout.PadTop * dpiScale, layout.Baseline * dpiScale,
                rawFace.Get(),
                static_cast<FLOAT>(job.FontSize * dpiScale),
                layout.Indices.data(),
                layout.Advances.data(),
                layout.Offsets.data(),
                static_cast<UINT32>(layout.Indices.size()),
                job.ForegroundColor,
                job.Simulations,
                job.RequiredWidth,
                job.RequiredHeight,
                scaleX, scaleY,
                posX * dpiScale, posY * dpiScale);

            if (success)
            {
                if (auto pDone = job.WeakThis.Resolve<FontGlyphs>())
                    pDone->ShowVisualIfCurrent(job.Generation);
            }
        }
    }
}

void FontGlyphs::RenderGlyphsBackground(Size finalSize)
{
    auto visual = ElementCompositionPreview::GetElementVisual(this);
    if (visual == nullptr)
        return;

    auto compositor = visual->Compositor;
    if (m_spriteVisual == nullptr)
    {
        m_spriteVisual = compositor->CreateSpriteVisual();
        ElementCompositionPreview::SetElementChildVisual(this, m_spriteVisual);
    }

    if (finalSize.Width <= 0.0f || finalSize.Height <= 0.0f || FontFace == nullptr ||
        ((Indices == nullptr || Indices->Length() == 0) && (UnicodeString == nullptr || UnicodeString->Length() == 0)))
    {
        m_spriteVisual->IsVisible = false;
        return;
    }

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return;

    if (m_isUsingSharedAtlas)
        ReleaseDrawingSurface();

    bool renderColor = ShouldRenderColor();
    if (m_dpiScale <= 0.0f)
        m_dpiScale = CompositionDeviceManager::GetDpiScale(this->Dispatcher);
    float dpiScale = m_dpiScale;
    LONG requiredWidth = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(finalSize.Width * dpiScale))));
    LONG requiredHeight = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(finalSize.Height * dpiScale))));

    auto pixelFormat = renderColor
        ? Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized
        : Windows::Graphics::DirectX::DirectXPixelFormat::A8UIntNormalized;

    bool needsSurfaceSetup = (m_surfaceBrush == nullptr || m_spriteVisual->Brush == nullptr);
    bool needsCreateOrResize = (m_drawingSurface == nullptr || m_renderedColor != renderColor || m_renderedWidth != requiredWidth || m_renderedHeight != requiredHeight);

    uint64 bpp = renderColor ? 4 : 1;
    if (m_drawingSurface == nullptr || m_renderedColor != renderColor)
    {
        ReleaseDrawingSurface();
        m_drawingSurface = graphicsDevice->CreateDrawingSurface(
            Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
            pixelFormat,
            Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
        m_renderedWidth = requiredWidth;
        m_renderedHeight = requiredHeight;
        m_renderedColor = renderColor;
        needsSurfaceSetup = true;

        if (m_drawingSurface != nullptr)
        {
            s_activeSurfaceCount.fetch_add(1, std::memory_order_relaxed);
            s_activeSurfaceBytes.fetch_add(static_cast<uint64>(requiredWidth) * requiredHeight * bpp, std::memory_order_relaxed);
        }
    }
    else if (m_renderedWidth != requiredWidth || m_renderedHeight != requiredHeight)
    {
        uint64 oldBytes = static_cast<uint64>(m_renderedWidth) * m_renderedHeight * bpp;
        Microsoft::WRL::ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
        if (SUCCEEDED(reinterpret_cast<IUnknown*>(m_drawingSurface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop)))
            && SUCCEEDED(surfaceInterop->Resize(SIZE{ requiredWidth, requiredHeight })))
        {
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
            uint64 newBytes = static_cast<uint64>(requiredWidth) * requiredHeight * bpp;
            s_activeSurfaceBytes.fetch_sub(oldBytes, std::memory_order_relaxed);
            s_activeSurfaceBytes.fetch_add(newBytes, std::memory_order_relaxed);
        }
        else
        {
            ReleaseDrawingSurface();
            m_drawingSurface = graphicsDevice->CreateDrawingSurface(
                Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
                pixelFormat,
                Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
            m_renderedColor = renderColor;
            needsSurfaceSetup = true;

            if (m_drawingSurface != nullptr)
            {
                s_activeSurfaceCount.fetch_add(1, std::memory_order_relaxed);
                s_activeSurfaceBytes.fetch_add(static_cast<uint64>(requiredWidth) * requiredHeight * bpp, std::memory_order_relaxed);
            }
        }
    }

    if (m_drawingSurface == nullptr)
        return;

    Windows::UI::Color foregroundColor = GetForegroundColor();
    if (needsSurfaceSetup || needsCreateOrResize)
    {
        CompositionDeviceManager::EnsureSurfaceBrush(compositor, m_surfaceBrush, m_drawingSurface);
        m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
        m_surfaceBrush->Offset = float2(0.0f, 0.0f);
        m_spriteVisual->Offset = float3(0.0f, 0.0f, 0.0f);
        m_spriteVisual->Size = float2(finalSize.Width, finalSize.Height);

        CompositionDeviceManager::ApplySurfaceOrMaskBrush(
            compositor,
            m_spriteVisual,
            m_surfaceBrush,
            m_maskBrush,
            m_colorBrush,
            !renderColor,
            foregroundColor);
    }
    else if (!renderColor && m_maskBrush != nullptr)
    {
        auto colorBrush = CompositionDeviceManager::GetColorBrush(compositor, foregroundColor);
        if (m_colorBrush != colorBrush)
        {
            m_colorBrush = colorBrush;
            m_maskBrush->Source = m_colorBrush;
        }
    }

    uint64_t gen = ++m_renderGeneration;
    m_spriteVisual->IsVisible = false;

    FontGlyphsRenderJob job;
    job.ControlKey = reinterpret_cast<const void*>(this);
    job.WeakThis = Platform::WeakReference(this);
    job.Generation = gen;
    job.FontFace = FontFace;
    job.FontSize = static_cast<FLOAT>(FontSize);
    job.Indices = Indices;
    job.UnicodeString = UnicodeString;
    job.Typography = Typography;
    job.IsTextShapingEnabled = IsTextShapingEnabled;
    job.Simulations = StyleSimulations;
    job.Stretch = Stretch;
    job.StretchDirection = StretchDirection;
    job.FinalSize = finalSize;
    job.DpiScale = dpiScale;
    job.RenderColor = renderColor;
    job.ForegroundColor = foregroundColor;
    job.RequiredWidth = requiredWidth;
    job.RequiredHeight = requiredHeight;
    job.Surface = m_drawingSurface;

    BackgroundRenderWorkItem workItem;
    workItem.ControlKey = reinterpret_cast<const void*>(this);
    workItem.Execute = [job = std::move(job)](bool)
    {
        ExecuteFontGlyphsJob(job);
    };

    auto& queue = renderColor
        ? BackgroundRenderQueue::ColorInstance()
        : BackgroundRenderQueue::MonoInstance();
    queue.Enqueue(std::move(workItem));
}

void FontGlyphs::RenderGlyphs()
{
    auto visual = ElementCompositionPreview::GetElementVisual(this);
    if (visual == nullptr)
        return;

    auto compositor = visual->Compositor;
    if (m_spriteVisual == nullptr)
    {
        m_spriteVisual = compositor->CreateSpriteVisual();
        ElementCompositionPreview::SetElementChildVisual(this, m_spriteVisual);
    }

    if (m_glyphCount == 0 || m_contentSize.Width <= 0 || m_contentSize.Height <= 0 || FontFace == nullptr)
    {
        if (m_spriteVisual != nullptr)
            m_spriteVisual->Brush = nullptr;
        ReleaseDrawingSurface();
        return;
    }

    auto rawFace = FontFace->GetFontFace();
    if (rawFace == nullptr)
        return;

    bool renderColor = ShouldRenderColor();
    
    float scaleX = m_scale.Width > 0.0f ? m_scale.Width : 1.0f;
    float scaleY = m_scale.Height > 0.0f ? m_scale.Height : 1.0f;
    m_renderedScaleX = scaleX;
    m_renderedScaleY = scaleY;

    float dpiScale = CompositionDeviceManager::GetDpiScale(this->Dispatcher);

    LONG logicalWidth = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(m_contentSize.Width + m_padLeft + m_padRight))));
    LONG logicalHeight = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(m_contentSize.Height + m_padTop + m_padBottom))));
    
    LONG scaledLogicalWidth = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(logicalWidth * scaleX))));
    LONG scaledLogicalHeight = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(logicalHeight * scaleY))));

    LONG requiredWidth = static_cast<LONG>(std::ceil(scaledLogicalWidth * dpiScale));
    LONG requiredHeight = static_cast<LONG>(std::ceil(scaledLogicalHeight * dpiScale));

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return;

    Windows::UI::Color foregroundColor = GetForegroundColor();

    // Path 1: Single glyph rendering via Shared Atlas (Grid & list view optimization)
    bool useSharedAtlas = (m_glyphCount == 1 && requiredWidth <= 512 && requiredHeight <= 512);
    if (useSharedAtlas)
    {
        FLOAT adv = GetGlyphAdvance(0);
        DWRITE_GLYPH_OFFSET off = GetGlyphOffset(0);
        UINT32 typoKey = Typography != nullptr ? Typography->GetKey() : 0;

        AtlasSlot slot = GlyphAtlasManager::GetOrCreateGlyphSlot(
            compositor,
            rawFace.Get(),
            GetGlyphIndex(0),
            static_cast<FLOAT>(FontSize * dpiScale),
            StyleSimulations,
            renderColor,
            m_padLeft * dpiScale, m_padTop * dpiScale, m_baseline * dpiScale,
            adv * dpiScale, DWRITE_GLYPH_OFFSET{ off.advanceOffset * dpiScale, off.ascenderOffset * dpiScale },
            foregroundColor,
            requiredWidth, requiredHeight,
            scaleX, scaleY,
            typoKey);

        if (slot.IsValid && slot.Surface != nullptr)
        {
            if (!m_isUsingSharedAtlas && m_drawingSurface != nullptr)
                ReleaseDrawingSurface();

            m_isUsingSharedAtlas = true;
            m_drawingSurface = slot.Surface;
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
            m_renderedColor = renderColor;

            CompositionDeviceManager::EnsureSurfaceBrush(compositor, m_surfaceBrush, m_drawingSurface);

            m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
            // Offset the brush so the slot aligns with (0,0) of the sprite visual
            m_surfaceBrush->Offset = float2(static_cast<float>(-slot.X) / dpiScale, static_cast<float>(-slot.Y) / dpiScale);
            m_spriteVisual->Size = float2(static_cast<float>(scaledLogicalWidth), static_cast<float>(scaledLogicalHeight));

            CompositionDeviceManager::ApplySurfaceOrMaskBrush(
                compositor,
                m_spriteVisual,
                m_surfaceBrush,
                m_maskBrush,
                m_colorBrush,
                !renderColor,
                foregroundColor);

            m_spriteVisual->IsVisible = true;
            return;
        }
    }

    // Path 2: Dedicated surface fallback (multi-glyph sequences, ligatures, or oversized preview)
    if (m_isUsingSharedAtlas)
        ReleaseDrawingSurface();

    auto pixelFormat = renderColor
        ? Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized
        : Windows::Graphics::DirectX::DirectXPixelFormat::A8UIntNormalized;

    uint64 bpp = renderColor ? 4 : 1;
    if (m_drawingSurface == nullptr || m_renderedColor != renderColor)
    {
        ReleaseDrawingSurface();
        m_drawingSurface = graphicsDevice->CreateDrawingSurface(
            Windows::Foundation::Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
            pixelFormat,
            Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
        m_renderedWidth = requiredWidth;
        m_renderedHeight = requiredHeight;
        m_renderedColor = renderColor;

        if (m_drawingSurface != nullptr)
        {
            s_activeSurfaceCount.fetch_add(1, std::memory_order_relaxed);
            s_activeSurfaceBytes.fetch_add(static_cast<uint64>(requiredWidth) * requiredHeight * bpp, std::memory_order_relaxed);
        }
    }
    else if (m_renderedWidth != requiredWidth || m_renderedHeight != requiredHeight)
    {
        uint64 oldBytes = static_cast<uint64>(m_renderedWidth) * m_renderedHeight * bpp;
        Microsoft::WRL::ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
        if (SUCCEEDED(reinterpret_cast<IUnknown*>(m_drawingSurface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop)))
            && SUCCEEDED(surfaceInterop->Resize(SIZE{ requiredWidth, requiredHeight })))
        {
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
            uint64 newBytes = static_cast<uint64>(requiredWidth) * requiredHeight * bpp;
            s_activeSurfaceBytes.fetch_sub(oldBytes, std::memory_order_relaxed);
            s_activeSurfaceBytes.fetch_add(newBytes, std::memory_order_relaxed);
        }
        else
        {
            ReleaseDrawingSurface();
            m_drawingSurface = graphicsDevice->CreateDrawingSurface(
                Windows::Foundation::Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
                pixelFormat,
                Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
            m_renderedColor = renderColor;

            if (m_drawingSurface != nullptr)
            {
                s_activeSurfaceCount.fetch_add(1, std::memory_order_relaxed);
                s_activeSurfaceBytes.fetch_add(static_cast<uint64>(requiredWidth) * requiredHeight * bpp, std::memory_order_relaxed);
            }
        }
    }

    if (m_drawingSurface == nullptr)
        return;

    std::vector<FLOAT> scaledAdvances;
    std::vector<DWRITE_GLYPH_OFFSET> scaledOffsets;
    if (dpiScale != 1.0f)
    {
        scaledAdvances.reserve(m_glyphCount);
        for (size_t i = 0; i < m_glyphCount; i++) scaledAdvances.push_back(GetGlyphAdvance(i) * dpiScale);
        
        scaledOffsets.reserve(m_glyphCount);
        for (size_t i = 0; i < m_glyphCount; i++)
        {
            auto off = GetGlyphOffset(i);
            scaledOffsets.push_back(DWRITE_GLYPH_OFFSET{ off.advanceOffset * dpiScale, off.ascenderOffset * dpiScale });
        }
    }

    const FLOAT* pAdvances = (dpiScale != 1.0f) ? scaledAdvances.data() : GlyphAdvancesData();
    const DWRITE_GLYPH_OFFSET* pOffsets = (dpiScale != 1.0f) ? scaledOffsets.data() : GlyphOffsetsData();

    bool success = CompositionDeviceManager::RenderGlyphToSurface(
        m_drawingSurface,
        renderColor ? GlyphImageFormat::None : GlyphImageFormat::TrueType,
        m_padLeft * dpiScale, m_padTop * dpiScale, m_baseline * dpiScale,
        rawFace.Get(),
        static_cast<FLOAT>(FontSize * dpiScale),
        GlyphIndicesData(),
        pAdvances,
        pOffsets,
        static_cast<UINT32>(m_glyphCount),
        foregroundColor,
        StyleSimulations,
        requiredWidth,
        requiredHeight,
        scaleX, scaleY);

    if (!success)
    {
        ReleaseDrawingSurface();
        graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
        if (graphicsDevice != nullptr)
        {
            m_drawingSurface = graphicsDevice->CreateDrawingSurface(
                Windows::Foundation::Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
                pixelFormat,
                Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
            if (m_drawingSurface != nullptr)
            {
                s_activeSurfaceCount.fetch_add(1, std::memory_order_relaxed);
                s_activeSurfaceBytes.fetch_add(static_cast<uint64>(requiredWidth) * requiredHeight * bpp, std::memory_order_relaxed);
                m_renderedWidth = requiredWidth;
                m_renderedHeight = requiredHeight;
                m_renderedColor = renderColor;

                success = CompositionDeviceManager::RenderGlyphToSurface(
                    m_drawingSurface,
                    renderColor ? GlyphImageFormat::None : GlyphImageFormat::TrueType,
                    m_padLeft * dpiScale, m_padTop * dpiScale, m_baseline * dpiScale,
                    rawFace.Get(),
                    static_cast<FLOAT>(FontSize * dpiScale),
                    GlyphIndicesData(),
                    pAdvances,
                    pOffsets,
                    static_cast<UINT32>(m_glyphCount),
                    foregroundColor,
                    StyleSimulations,
                    requiredWidth,
                    requiredHeight,
                    scaleX, scaleY);
            }
        }

        if (!success)
            return;
    }

    CompositionDeviceManager::EnsureSurfaceBrush(compositor, m_surfaceBrush, m_drawingSurface);

    m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
    m_surfaceBrush->Offset = float2(0.0f, 0.0f);
    m_spriteVisual->Size = float2(static_cast<float>(scaledLogicalWidth), static_cast<float>(scaledLogicalHeight));

    CompositionDeviceManager::ApplySurfaceOrMaskBrush(
        compositor,
        m_spriteVisual,
        m_surfaceBrush,
        m_maskBrush,
        m_colorBrush,
        !renderColor,
        foregroundColor);

    m_spriteVisual->IsVisible = true;
}
