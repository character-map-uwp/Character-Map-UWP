#pragma once

#include "CompositionDeviceManager.h"
#include <tuple>
#include <unordered_map>
#include <vector>
#include <mutex>
#include <wrl.h>

namespace CharacterMapCX
{
    struct AtlasSlot
    {
        Windows::UI::Composition::CompositionDrawingSurface^ Surface;
        LONG X;
        LONG Y;
        LONG Width;
        LONG Height;
        bool IsValid;
    };

    struct AtlasKey
    {
        // ComPtr keeps the font face alive for the lifetime of this cache entry,
        // preventing address reuse from creating ghost entries when a face is freed
        // and a new allocation lands at the same address.
        Microsoft::WRL::ComPtr<IDWriteFontFace> FontFace;
        UINT16 GlyphIndex;
        UINT32 FontSizeInt;      // (UINT32)(FontSize * 100) to avoid float precision issues
        UINT32 Simulations;
        bool IsColor;
        UINT32 TypographyKey;

        bool operator==(const AtlasKey& other) const
        {
            return FontFace.Get() == other.FontFace.Get()
                && GlyphIndex == other.GlyphIndex
                && FontSizeInt == other.FontSizeInt
                && Simulations == other.Simulations
                && IsColor == other.IsColor
                && TypographyKey == other.TypographyKey;
        }
    };

    struct AtlasKeyHasher
    {
        std::size_t operator()(const AtlasKey& k) const
        {
            // Hash the underlying pointer value, not the ComPtr wrapper
            std::size_t h1 = std::hash<void*>{}(static_cast<void*>(k.FontFace.Get()));
            std::size_t h2 = std::hash<UINT16>{}(k.GlyphIndex);
            std::size_t h3 = std::hash<UINT32>{}(k.FontSizeInt);
            std::size_t h4 = std::hash<UINT32>{}(k.Simulations);
            std::size_t h5 = std::hash<bool>{}(k.IsColor);
            std::size_t h6 = std::hash<UINT32>{}(k.TypographyKey);
            return h1 ^ (h2 << 1) ^ (h3 << 2) ^ (h4 << 3) ^ (h5 << 4) ^ (h6 << 5);
        }
    };

    class GlyphAtlas
    {
    public:
        GlyphAtlas(Windows::UI::Composition::CompositionGraphicsDevice^ device,
                   Windows::Graphics::DirectX::DirectXPixelFormat format,
                   LONG width = 2048, LONG height = 2048);

        bool AllocateSlot(LONG slotWidth, LONG slotHeight, LONG& outX, LONG& outY);
        Windows::UI::Composition::CompositionDrawingSurface^ GetSurface() const { return m_surface; }
        void Reset();
        void Clear();

    private:
        Windows::UI::Composition::CompositionDrawingSurface^ m_surface;
        LONG m_width;
        LONG m_height;
        LONG m_currentX;
        LONG m_currentY;
        LONG m_rowHeight;
    };

    class GlyphAtlasManager
    {
    public:
        static AtlasSlot GetOrCreateGlyphSlot(
            Windows::UI::Composition::Compositor^ compositor,
            IDWriteFontFace* rawFace,
            UINT16 glyphIndex,
            FLOAT fontSize,
            Windows::UI::Xaml::Media::StyleSimulations simulations,
            bool isColor,
            float padLeft, float padTop, float baseline,
            FLOAT glyphAdvance,
            DWRITE_GLYPH_OFFSET glyphOffset,
            Windows::UI::Xaml::Media::Brush^ foreground,
            LONG requiredWidth, LONG requiredHeight,
            UINT32 typographyKey = 0);

        static void Clear();

    private:
        static std::mutex s_atlasMutex;
        static std::unordered_map<AtlasKey, AtlasSlot, AtlasKeyHasher> s_slotCache;
        static std::vector<std::unique_ptr<GlyphAtlas>> s_monochromeAtlases;
        static std::vector<std::unique_ptr<GlyphAtlas>> s_colorAtlases;
    };
}
