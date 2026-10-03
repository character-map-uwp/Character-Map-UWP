#include "pch.h"
#include "GlyphAtlasManager.h"
#include <algorithm>

using namespace CharacterMapCX;
using namespace Microsoft::WRL;
using namespace Windows::UI::Composition;
using namespace Windows::Graphics::DirectX;

std::mutex GlyphAtlasManager::s_atlasMutex;
std::unordered_map<AtlasKey, AtlasSlot, AtlasKeyHasher> GlyphAtlasManager::s_slotCache;
std::vector<std::unique_ptr<GlyphAtlas>> GlyphAtlasManager::s_monochromeAtlases;
std::vector<std::unique_ptr<GlyphAtlas>> GlyphAtlasManager::s_colorAtlases;

GlyphAtlas::GlyphAtlas(CompositionGraphicsDevice^ device, DirectXPixelFormat format, LONG width, LONG height)
    : m_width(width)
    , m_height(height)
    , m_currentX(0)
    , m_currentY(0)
    , m_rowHeight(0)
{
    if (device != nullptr)
    {
        m_surface = device->CreateDrawingSurface(
            Windows::Foundation::Size(static_cast<float>(width), static_cast<float>(height)),
            format,
            DirectXAlphaMode::Premultiplied);
    }
}

bool GlyphAtlas::AllocateSlot(LONG slotWidth, LONG slotHeight, LONG& outX, LONG& outY)
{
    // Simple Shelf / Skyline packing
    // Add 1px padding between slots to prevent bilinear filtering bleeding
    LONG paddedW = slotWidth + 1;
    LONG paddedH = slotHeight + 1;

    if (paddedW > m_width || paddedH > m_height)
        return false;

    if (m_currentX + paddedW > m_width)
    {
        // Move down to next row
        m_currentX = 0;
        m_currentY += m_rowHeight;
        m_rowHeight = 0;
    }

    if (m_currentY + paddedH > m_height)
    {
        // Atlas full
        return false;
    }

    outX = m_currentX;
    outY = m_currentY;

    m_currentX += paddedW;
    m_rowHeight = (std::max)(m_rowHeight, paddedH);

    return true;
}

void GlyphAtlas::Reset()
{
    m_currentX = 0;
    m_currentY = 0;
    m_rowHeight = 0;
}

void GlyphAtlas::Clear()
{
    m_currentX = 0;
    m_currentY = 0;
    m_rowHeight = 0;
    m_surface = nullptr;
}

void GlyphAtlasManager::Clear()
{
    std::lock_guard<std::mutex> lock(s_atlasMutex);
    s_slotCache.clear();
    s_monochromeAtlases.clear();
    s_colorAtlases.clear();
}

AtlasSlot GlyphAtlasManager::GetOrCreateGlyphSlot(
    Compositor^ compositor,
    IDWriteFontFace* rawFace,
    UINT16 glyphIndex,
    FLOAT fontSize,
    Windows::UI::Xaml::Media::StyleSimulations simulations,
    bool isColor,
    float padLeft, float padTop, float baseline,
    FLOAT glyphAdvance,
    DWRITE_GLYPH_OFFSET glyphOffset,
    Windows::UI::Xaml::Media::Brush^ foreground,
    LONG requiredWidth, LONG requiredHeight)
{
    AtlasSlot result{};
    result.IsValid = false;

    if (compositor == nullptr || rawFace == nullptr || requiredWidth <= 0 || requiredHeight <= 0)
        return result;

    // Do not cache oversized glyphs (e.g. gigantic preview renders > 512px) in shared atlases
    if (requiredWidth > 512 || requiredHeight > 512)
        return result;

    AtlasKey key{};
    key.FontFace = rawFace;   // ComPtr::operator= AddRefs — face stays alive
    key.GlyphIndex = glyphIndex;
    key.FontSizeInt = static_cast<UINT32>(fontSize * 100.0f);
    key.Simulations = static_cast<UINT32>(simulations);
    key.IsColor = isColor;

    std::lock_guard<std::mutex> lock(s_atlasMutex);

    auto it = s_slotCache.find(key);
    if (it != s_slotCache.end() && it->second.IsValid && it->second.Surface != nullptr)
    {
        return it->second;
    }

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return result;

    auto& atlasList = isColor ? s_colorAtlases : s_monochromeAtlases;
    DirectXPixelFormat pixelFormat = isColor
        ? DirectXPixelFormat::B8G8R8A8UIntNormalized
        : DirectXPixelFormat::A8UIntNormalized;

    LONG slotX = 0, slotY = 0;
    GlyphAtlas* targetAtlas = nullptr;
    bool didEvict = false;

    // Try finding space in existing atlases
    for (auto& atlas : atlasList)
    {
        if (atlas->AllocateSlot(requiredWidth, requiredHeight, slotX, slotY))
        {
            targetAtlas = atlas.get();
            break;
        }
    }

    // Allocate up to MAX_PAGES (2 pages = 8MB monochrome or 32MB color).
    // Once cap is reached, reset the oldest page (FIFO) and evict its slot entries.
    const size_t MAX_PAGES = 2;
    if (targetAtlas == nullptr)
    {
        if (atlasList.size() < MAX_PAGES)
        {
            auto newAtlas = std::make_unique<GlyphAtlas>(graphicsDevice, pixelFormat, 2048, 2048);
            if (newAtlas->GetSurface() != nullptr && newAtlas->AllocateSlot(requiredWidth, requiredHeight, slotX, slotY))
            {
                targetAtlas = newAtlas.get();
                atlasList.push_back(std::move(newAtlas));
            }
            else
            {
                return result;
            }
        }
        else
        {
            // Evict the oldest atlas page (FIFO). Only purge slot cache entries that
            // reference the evicted surface — slots on the surviving page remain valid
            // and must not be re-rendered unnecessarily.
            auto oldestAtlas = std::move(atlasList.front());
            atlasList.erase(atlasList.begin());

            auto* evictedSurface = reinterpret_cast<IUnknown*>(oldestAtlas->GetSurface());
            for (auto evictIt = s_slotCache.begin(); evictIt != s_slotCache.end(); )
            {
                if (reinterpret_cast<IUnknown*>(evictIt->second.Surface) == evictedSurface)
                    evictIt = s_slotCache.erase(evictIt);
                else
                    ++evictIt;
            }

            oldestAtlas->Reset();
            if (oldestAtlas->AllocateSlot(requiredWidth, requiredHeight, slotX, slotY))
            {
                targetAtlas = oldestAtlas.get();
                atlasList.push_back(std::move(oldestAtlas));
                didEvict = true;  // signal to flush D2D cache after EndDraw
            }
            else
            {
                atlasList.push_back(std::move(oldestAtlas));
                return result;
            }
        }
    }

    auto surface = targetAtlas->GetSurface();
    if (surface == nullptr)
        return result;

    ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
    if (FAILED(reinterpret_cast<IUnknown*>(surface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop))))
        return result;

    RECT updateRect{};
    updateRect.left = slotX;
    updateRect.top = slotY;
    updateRect.right = slotX + requiredWidth;
    updateRect.bottom = slotY + requiredHeight;

    POINT updateOffset{};
    ComPtr<ID2D1DeviceContext> d2dContext;

    {
        std::lock_guard<std::mutex> renderLock(CompositionDeviceManager::GetRenderMutex());

        HRESULT hr = surfaceInterop->BeginDraw(&updateRect, IID_PPV_ARGS(&d2dContext), &updateOffset);
        if (FAILED(hr))
        {
            if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
                CompositionDeviceManager::HandleDeviceLost();
            return result;
        }

        if (d2dContext != nullptr)
        {
            // Clear only the allocated slot rectangle
            d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));
            d2dContext->SetTransform(D2D1::Matrix3x2F::Translation(static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y)));

            DWRITE_GLYPH_RUN glyphRun{};
            glyphRun.fontFace = rawFace;
            glyphRun.fontEmSize = fontSize;
            glyphRun.glyphCount = 1;
            glyphRun.glyphIndices = &glyphIndex;
            glyphRun.glyphAdvances = &glyphAdvance;
            glyphRun.glyphOffsets = &glyphOffset;
            glyphRun.isSideways = FALSE;
            glyphRun.bidiLevel = 0;

            D2D1_POINT_2F baselineOrigin = D2D1::Point2F(padLeft, baseline + padTop);

            if (!isColor)
            {
                // Render as pure alpha mask into A8 surface
                ComPtr<ID2D1SolidColorBrush> alphaBrush;
                d2dContext->CreateSolidColorBrush(D2D1::ColorF(1.0f, 1.0f, 1.0f, 1.0f), &alphaBrush);
                d2dContext->DrawGlyphRun(baselineOrigin, &glyphRun, alphaBrush.Get(), DWRITE_MEASURING_MODE_NATURAL);
            }
            else
            {
                D2D1_COLOR_F brushColor = D2D1::ColorF(D2D1::ColorF::White);
                if (auto scb = dynamic_cast<Windows::UI::Xaml::Media::SolidColorBrush^>(foreground))
                {
                    auto c = scb->Color;
                    brushColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(scb->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> d2dBrush;
                d2dContext->CreateSolidColorBrush(brushColor, &d2dBrush);

                CompositionDeviceManager::DrawGlyphRunWithColorSupport(
                    d2dContext.Get(),
                    CompositionDeviceManager::GetDWriteFactory().Get(),
                    baselineOrigin,
                    &glyphRun,
                    d2dBrush.Get(),
                    GlyphImageFormat::None);
            }

            surfaceInterop->EndDraw();
        }
    }

    // Flush D2D's internal glyph bitmap cache after an atlas page eviction.
    // D2D rasterizes each unique glyph into its own internal texture atlases and
    // never evicts them unprompted — for large CJK fonts this is the dominant
    // source of unbounded memory growth. ClearResources(0) must be called outside
    // of any BeginDraw/EndDraw scope.
    if (didEvict)
        CompositionDeviceManager::TrimD2DResources();

    result.Surface = surface;
    result.X = slotX;
    result.Y = slotY;
    result.Width = requiredWidth;
    result.Height = requiredHeight;
    result.IsValid = true;

    s_slotCache[key] = result;
    return result;
}
