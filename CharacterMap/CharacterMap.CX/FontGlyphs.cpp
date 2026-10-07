#include "pch.h"
#include "FontGlyphs.h"
#include "CompositionDeviceManager.h"
#include "GlyphAtlasManager.h"
#include "TypographyAnalyzer.h"
#include "DirectWrite.h"
#include <cwctype>
#include <algorithm>
#include <cmath>

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
    , m_designUnitsPerEm(0)
    , m_contentSize(0, 0)
    , m_scale(1, 1)
    , m_renderedWidth(0)
    , m_renderedHeight(0)
    , m_renderedColor(false)
    , m_isUsingSharedAtlas(false)
{
    m_unloadedToken.Value = 0;
    EnsureDependencyProperties();
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

    m_isUsingSharedAtlas = false;
    m_drawingSurface = nullptr;
    m_renderedWidth = 0;
    m_renderedHeight = 0;
}

FontGlyphs::~FontGlyphs()
{
    try
    {
        if (Dispatcher != nullptr && Dispatcher->HasThreadAccess)
        {
            if (m_unloadedToken.Value != 0)
            {
                this->Unloaded -= m_unloadedToken;
                m_unloadedToken.Value = 0;
            }

            ReleaseDrawingSurface();

            if (m_spriteVisual != nullptr)
            {
                m_spriteVisual->Brush = nullptr;
                ElementCompositionPreview::SetElementChildVisual(this, nullptr);
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
    }
    catch (...)
    {
    }

    m_unloadedToken.Value = 0;
    m_drawingSurface = nullptr;
    m_spriteVisual = nullptr;
    m_maskBrush = nullptr;
    m_surfaceBrush = nullptr;
    m_colorBrush = nullptr;
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
}

void FontGlyphs::OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    /*
     * When items are virtualized in a GridView/ListView, XAML recycles and pools
     * containers without destroying them. We preserve the composition visual and surface
     * structure here to avoid tearing down and reallocating visuals on every scroll frame.
     * Full teardown occurs in ~FontGlyphs() when the container is truly disposed.
     */
}

Windows::UI::Color FontGlyphs::GetForegroundColor()
{
    Brush^ fg = Foreground;
    if (auto scb = dynamic_cast<SolidColorBrush^>(fg))
    {
        auto c = scb->Color;
        byte a = static_cast<byte>(c.A * scb->Opacity);
        Windows::UI::Color clr;
        clr.A = a;
        clr.R = c.R;
        clr.G = c.G;
        clr.B = c.B;
        return clr;
    }

    Windows::UI::Color clr;
    clr.A = 255;
    clr.R = 255;
    clr.G = 255;
    clr.B = 255;
    return clr;
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
    glyphs->InvalidateLayoutAndRender();
}

void FontGlyphs::OnFontSizeChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto glyphs = (FontGlyphs^)d;
    glyphs->InvalidateLayoutAndRender();
}

void FontGlyphs::OnForegroundChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto glyphs = (FontGlyphs^)d;

    if (glyphs->m_maskBrush != nullptr && glyphs->m_colorBrush != nullptr)
    {
        auto visual = ElementCompositionPreview::GetElementVisual(glyphs);
        if (visual != nullptr)
        {
            glyphs->m_colorBrush = CompositionDeviceManager::GetColorBrush(visual->Compositor, glyphs->GetForegroundColor());
            glyphs->m_maskBrush->Source = glyphs->m_colorBrush;
            return;
        }
    }

    glyphs->InvalidateRenderOnly();
}

void FontGlyphs::InvalidateLayoutAndRender()
{
    m_isLayoutDirty = true;
    m_isRenderDirty = true;
    // Release unconditionally: shared-atlas references must be dropped here so that
    // the old m_surfaceBrush->Surface doesn't hold an atlas page alive across a
    // virtualised-container recycling cycle.
    ReleaseDrawingSurface();
    InvalidateMeasure();
    InvalidateArrange();
}

void FontGlyphs::InvalidateRenderOnly()
{
    m_isRenderDirty = true;
    InvalidateArrange();
}

void FontGlyphs::ParseAndLayoutGlyphs()
{
    m_glyphIndices.clear();
    m_glyphAdvances.clear();
    m_glyphOffsets.clear();
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
            m_glyphIndices.push_back(gid);
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
            m_glyphOffsets.push_back(off);

            if (adv < 0.0f)
            {
                INT32 designAdvance = 0;
                rawFace->GetDesignGlyphAdvances(1, &gid, &designAdvance, FALSE);
                adv = designAdvance * emScale;
            }
            m_glyphAdvances.push_back(adv);

            while (p < end && (*p == L';' || iswspace(*p))) p++;
        }
    }
    else if (UnicodeString != nullptr && UnicodeString->Length() > 0)
    {
        auto typography = Typography;
        bool shaped = false;

        if (typography != nullptr && typography->FeatureCount > 0)
        {
            shaped = TypographyAnalyzer::ShapeGlyphs(
                rawFace.Get(),
                UnicodeString->Data(),
                UnicodeString->Length(),
                static_cast<FLOAT>(emSize),
                typography->GetDWriteFontFeatures(),
                m_glyphIndices,
                m_glyphAdvances,
                m_glyphOffsets);
        }

        if (!shaped)
        {
            UINT32 len = UnicodeString->Length();
            std::vector<UINT32> codePoints;
            const wchar_t* strData = UnicodeString->Data();

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

            m_glyphIndices.resize(codePoints.size());
            m_glyphAdvances.resize(codePoints.size());
            m_glyphOffsets.resize(codePoints.size(), DWRITE_GLYPH_OFFSET{});

            rawFace->GetGlyphIndices(codePoints.data(), static_cast<UINT32>(codePoints.size()), m_glyphIndices.data());

            for (size_t i = 0; i < m_glyphIndices.size(); i++)
            {
                INT32 designAdvance = 0;
                rawFace->GetDesignGlyphAdvances(1, &m_glyphIndices[i], &designAdvance, FALSE);
                m_glyphAdvances[i] = designAdvance * emScale;
            }
        }
    }

    if (m_glyphIndices.empty())
        return;

    // Calculate logical layout bounds matching Glyphs.cpp CalculateBounds
    float layoutLeft = 0.0f;
    float layoutRight = 0.0f;
    float currentX = 0.0f;
    for (size_t i = 0; i < m_glyphIndices.size(); i++)
    {
        float uOff = (!m_glyphOffsets.empty()) ? m_glyphOffsets[i].advanceOffset : 0.0f;
        float glyphX = currentX + uOff;
        float adv = m_glyphAdvances[i];
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

    Rect inkBounds = FontFace->GetDesignGlyphBounds(m_glyphIndices[0]);
    if (inkBounds.Width > 0 && fontMetrics.designUnitsPerEm > 0)
    {
        m_hasInkBounds = true;
        m_designInkLeft = inkBounds.X;
        m_designInkWidth = inkBounds.Width;
        m_designUnitsPerEm = fontMetrics.designUnitsPerEm;

        if (totalAdvance <= 0.0f)
            totalAdvance = static_cast<float>(inkBounds.Width * emScale);
    }

    float totalWidth = (std::max)(totalAdvance, layoutRight - layoutLeft);
    m_contentSize = Size(totalWidth, totalHeight);

    // Query design glyph metrics to determine ink overhangs for padding
    DWRITE_GLYPH_METRICS stackMetrics[8];
    std::vector<DWRITE_GLYPH_METRICS> heapMetrics;
    DWRITE_GLYPH_METRICS* metrics = stackMetrics;
    if (m_glyphIndices.size() > 8)
    {
        heapMetrics.resize(m_glyphIndices.size());
        metrics = heapMetrics.data();
    }
    rawFace->GetDesignGlyphMetrics(m_glyphIndices.data(), static_cast<UINT32>(m_glyphIndices.size()), metrics, FALSE);

    float minInkLeft = 0.0f;
    float maxInkRight = totalWidth;
    float maxTopOverhang = 0.0f;
    float maxBottomOverhang = 0.0f;

    currentX = 0.0f;
    for (size_t i = 0; i < m_glyphIndices.size(); i++)
    {
        float uOff = (!m_glyphOffsets.empty()) ? m_glyphOffsets[i].advanceOffset : 0.0f;
        float vOff = (!m_glyphOffsets.empty()) ? m_glyphOffsets[i].ascenderOffset : 0.0f;
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

        currentX += m_glyphAdvances[i];
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

    if (m_hasInkBounds)
    {
        float inkLeft = static_cast<float>(m_designInkLeft * emScale);
        float inkRight = static_cast<float>((m_designInkLeft + m_designInkWidth) * emScale);
        if (inkLeft < minInkLeft)
            minInkLeft = inkLeft;
        if (inkRight > maxInkRight)
            maxInkRight = inkRight;
    }

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
    case Windows::UI::Xaml::Controls::StretchDirection::DownOnly:
        scaleX = (std::min)(1.0, scaleX);
        scaleY = (std::min)(1.0, scaleY);
        break;
    }

    return Size((float)scaleX, (float)scaleY);
}

Size FontGlyphs::MeasureOverride(Size availableSize)
{
    if (m_isLayoutDirty)
    {
        m_isLayoutDirty = false;
        ParseAndLayoutGlyphs();
    }

    m_scale = ComputeScaleFactor(availableSize, m_contentSize);

    return Size(m_scale.Width * m_contentSize.Width, m_scale.Height * m_contentSize.Height);
}

Size FontGlyphs::ArrangeOverride(Size finalSize)
{
    if (m_isLayoutDirty)
    {
        m_isLayoutDirty = false;
        ParseAndLayoutGlyphs();
    }

    if (m_isRenderDirty)
    {
        m_isRenderDirty = false;
        RenderGlyphs();
    }

    if (m_contentSize.Width <= 0 || m_contentSize.Height <= 0)
        return finalSize;

    m_scale = ComputeScaleFactor(finalSize, m_contentSize);

    double originX = (finalSize.Width - m_contentSize.Width) / 2.0;
    if (m_hasInkBounds)
    {
        double emScale = FontSize / m_designUnitsPerEm;
        double inkLeft = m_designInkLeft * emScale;
        double inkWidth = m_designInkWidth * emScale;
        originX = (finalSize.Width - inkWidth) / 2.0 - inkLeft;
    }

    double originY = (finalSize.Height - m_contentSize.Height) / 2.0;

    float posX = (float)(originX - m_padLeft);
    float posY = (float)(originY - m_padTop);

    if (m_spriteVisual != nullptr)
    {
        m_spriteVisual->Offset = float3(posX, posY, 0.0f);
        m_spriteVisual->CenterPoint = float3((float)(finalSize.Width / 2.0 - posX), (float)(finalSize.Height / 2.0 - posY), 0.0f);
        m_spriteVisual->Scale = float3((float)m_scale.Width, (float)m_scale.Height, 1.0f);
    }

    return finalSize;
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

    if (m_glyphIndices.empty() || m_contentSize.Width <= 0 || m_contentSize.Height <= 0 || FontFace == nullptr)
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
    LONG requiredWidth = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(m_contentSize.Width + m_padLeft + m_padRight))));
    LONG requiredHeight = static_cast<LONG>((std::max)(1.0f, static_cast<float>(std::ceil(m_contentSize.Height + m_padTop + m_padBottom))));

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return;

    // Path 1: Single glyph rendering via Shared Atlas (Grid & list view optimization)
    bool useSharedAtlas = (m_glyphIndices.size() == 1 && requiredWidth <= 512 && requiredHeight <= 512);
    if (useSharedAtlas)
    {
        FLOAT adv = m_glyphAdvances.empty() ? 0.0f : m_glyphAdvances[0];
        DWRITE_GLYPH_OFFSET off = m_glyphOffsets.empty() ? DWRITE_GLYPH_OFFSET{} : m_glyphOffsets[0];
        UINT32 typoKey = Typography != nullptr ? Typography->GetKey() : 0;

        AtlasSlot slot = GlyphAtlasManager::GetOrCreateGlyphSlot(
            compositor,
            rawFace.Get(),
            m_glyphIndices[0],
            static_cast<FLOAT>(FontSize),
            StyleSimulations,
            renderColor,
            m_padLeft, m_padTop, m_baseline,
            adv, off,
            Foreground,
            requiredWidth, requiredHeight,
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

            if (m_surfaceBrush == nullptr)
            {
                m_surfaceBrush = compositor->CreateSurfaceBrush(m_drawingSurface);
                m_surfaceBrush->Stretch = Windows::UI::Composition::CompositionStretch::None;
                m_surfaceBrush->HorizontalAlignmentRatio = 0.0f;
                m_surfaceBrush->VerticalAlignmentRatio = 0.0f;
            }
            else if (m_surfaceBrush->Surface != m_drawingSurface)
                m_surfaceBrush->Surface = m_drawingSurface;

            // Offset the brush so the slot aligns with (0,0) of the sprite visual
            m_surfaceBrush->Offset = float2(static_cast<float>(-slot.X), static_cast<float>(-slot.Y));
            m_spriteVisual->Size = float2(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight));

            if (!renderColor)
            {
                Windows::UI::Color textColor = GetForegroundColor();
                m_colorBrush = CompositionDeviceManager::GetColorBrush(compositor, textColor);

                if (m_maskBrush == nullptr)
                {
                    m_maskBrush = compositor->CreateMaskBrush();
                    m_maskBrush->Mask = m_surfaceBrush;
                    m_maskBrush->Source = m_colorBrush;
                }
                else
                {
                    if (m_maskBrush->Mask != m_surfaceBrush)
                        m_maskBrush->Mask = m_surfaceBrush;
                    if (m_maskBrush->Source != m_colorBrush)
                        m_maskBrush->Source = m_colorBrush;
                }

                if (m_spriteVisual->Brush != m_maskBrush)
                    m_spriteVisual->Brush = m_maskBrush;
            }
            else
            {
                if (m_spriteVisual->Brush != m_surfaceBrush)
                    m_spriteVisual->Brush = m_surfaceBrush;
            }

            return;
        }
    }

    // Path 2: Dedicated surface fallback (multi-glyph sequences, ligatures, or oversized preview)
    if (m_isUsingSharedAtlas)
        ReleaseDrawingSurface();

    auto pixelFormat = renderColor
        ? Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized
        : Windows::Graphics::DirectX::DirectXPixelFormat::A8UIntNormalized;

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
    }
    else if (m_renderedWidth != requiredWidth || m_renderedHeight != requiredHeight)
    {
        Microsoft::WRL::ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
        if (SUCCEEDED(reinterpret_cast<IUnknown*>(m_drawingSurface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop)))
            && SUCCEEDED(surfaceInterop->Resize(SIZE{ requiredWidth, requiredHeight })))
        {
            m_renderedWidth = requiredWidth;
            m_renderedHeight = requiredHeight;
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
        }
    }

    if (m_drawingSurface == nullptr)
        return;

    bool success = CompositionDeviceManager::RenderGlyphToSurface(
        m_drawingSurface,
        renderColor ? GlyphImageFormat::None : GlyphImageFormat::TrueType,
        m_padLeft, m_padTop, m_baseline,
        rawFace.Get(),
        static_cast<FLOAT>(FontSize),
        m_glyphIndices,
        m_glyphAdvances,
        m_glyphOffsets,
        Foreground,
        requiredWidth,
        requiredHeight);

    if (!success)
        return;

    if (m_surfaceBrush == nullptr)
    {
        m_surfaceBrush = compositor->CreateSurfaceBrush(m_drawingSurface);
        m_surfaceBrush->Stretch = Windows::UI::Composition::CompositionStretch::None;
        m_surfaceBrush->HorizontalAlignmentRatio = 0.0f;
        m_surfaceBrush->VerticalAlignmentRatio = 0.0f;
    }
    else if (m_surfaceBrush->Surface != m_drawingSurface)
        m_surfaceBrush->Surface = m_drawingSurface;

    m_surfaceBrush->Offset = float2(0.0f, 0.0f);
    m_spriteVisual->Size = float2(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight));

    if (!renderColor)
    {
        Windows::UI::Color textColor = GetForegroundColor();
        m_colorBrush = CompositionDeviceManager::GetColorBrush(compositor, textColor);

        if (m_maskBrush == nullptr)
        {
            m_maskBrush = compositor->CreateMaskBrush();
            m_maskBrush->Mask = m_surfaceBrush;
            m_maskBrush->Source = m_colorBrush;
        }
        else
        {
            if (m_maskBrush->Mask != m_surfaceBrush)
                m_maskBrush->Mask = m_surfaceBrush;
            if (m_maskBrush->Source != m_colorBrush)
                m_maskBrush->Source = m_colorBrush;
        }

        if (m_spriteVisual->Brush != m_maskBrush)
            m_spriteVisual->Brush = m_maskBrush;
    }
    else
    {
        if (m_spriteVisual->Brush != m_surfaceBrush)
            m_spriteVisual->Brush = m_surfaceBrush;
    }
}
