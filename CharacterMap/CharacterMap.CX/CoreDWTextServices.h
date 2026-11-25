#pragma once

#include <d2d1_3.h>
#include <dwrite_3.h>
#include <wrl.h>
#include <vector>
#include <deque>
#include <mutex>
#include <functional>
#include <cstdint>

struct ICompositionDrawingSurfaceInterop;

#include "DWriteFontFace.h"
#include "DWriteFallbackFont.h"
#include "DWriteFontAxis.h"
#include "DWriteFontSimulations.h"
#include "ITypographyInfo.h"
#include "DWriteTypographyCollection.h"
#include "GlyphImageFormat.h"

namespace CharacterMapCX
{
    namespace Controls
    {
        ref class GlyphBoundsOptions;
    }

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
    };

    struct GlyphBoundsDrawContext
    {
        float OffsetX = 0.0f;
        float OffsetY = 0.0f;
        float SurfaceWidthPx = 0.0f;
        float SurfaceHeightPx = 0.0f;
        float DpiScale = 1.0f;
        double OriginX = 0.0;
        double OriginY = 0.0;
        double ScaleX = 1.0;
        double ScaleY = 1.0;
        Windows::Foundation::Rect AdvanceBounds{};
        Windows::Foundation::Rect InkBounds{};
        bool HasInkBounds = true;
    };

    struct TextLayoutParameters
    {
        Platform::String^ Text = nullptr;
        DWriteFontFace^ FontFace = nullptr;
        float FontSize = 12.0f;
        Windows::UI::Text::FontWeight FontWeight = Windows::UI::Text::FontWeights::Normal;
        Windows::UI::Text::FontStyle FontStyle = Windows::UI::Text::FontStyle::Normal;
        Windows::UI::Text::FontStretch FontStretch = Windows::UI::Text::FontStretch::Normal;
        Windows::UI::Xaml::Media::StyleSimulations Simulations = Windows::UI::Xaml::Media::StyleSimulations::None;
        Windows::Foundation::Collections::IVectorView<DWriteFontAxis^>^ Axis = nullptr;
        std::vector<DWRITE_FONT_AXIS_VALUE> ExplicitAxisValues;
        ITypographyInfo^ TypographyInfo = nullptr;
        DWriteTypographyCollection^ TypographyCollection = nullptr;
        DWriteFallbackFont^ FallbackFont = nullptr;
        Windows::UI::Xaml::FlowDirection FlowDirection = Windows::UI::Xaml::FlowDirection::LeftToRight;
        Windows::UI::Xaml::TextAlignment TextAlignment = Windows::UI::Xaml::TextAlignment::Left;
        bool WordWrapping = false;
        bool EnableTrimming = false;
        float MaxWidth = 100000.0f;
        float MaxHeight = 100000.0f;
        bool IsCharacterFit = false;
    };

    struct TextRenderParameters
    {
        ID2D1Brush* ForegroundBrush = nullptr;
        bool EnableColor = true;
        GlyphImageFormat PreferredFormat = GlyphImageFormat::None;
        IDWriteFontFace* OverrideFace = nullptr;
        bool IsMaskSurface = false;
    };

    class CoreDWTextServices
    {
    public:
        // Converts ColorRenderOption & isColorEnabled into GlyphImageFormat
        static GlyphImageFormat ToPreferredFormat(bool isColorEnabled, DWriteColorRenderOption option);

        // Resolves StyleSimulations combining control property and font face properties
        static Windows::UI::Xaml::Media::StyleSimulations ResolveSimulations(
            Windows::UI::Xaml::Media::StyleSimulations controlSimulations,
            DWriteFontFace^ fontFace);

        // Obtains or clones an IDWriteFontFace3 with simulations and optional variable font axis values applied
        static Microsoft::WRL::ComPtr<IDWriteFontFace3> GetSimulatedFontFace(
            DWriteFontFace^ fontFace,
            Windows::UI::Xaml::Media::StyleSimulations simulations);

        static Microsoft::WRL::ComPtr<IDWriteFontFace3> ResolveFontFace(
            DWriteFontFace^ fontFace,
            Windows::UI::Xaml::Media::StyleSimulations simulations,
            Windows::Foundation::Collections::IVectorView<DWriteFontAxis^>^ axis);

        // Measures layout and ink bounds for a single glyph run
        static HRESULT MeasureGlyphBounds(
            IDWriteFontFace3* dwriteFontFace,
            DWriteFontFace^ fontFace,
            UINT16 glyphIndex,
            float fontSize,
            Windows::Foundation::Rect* pLayoutBounds,
            Windows::Foundation::Rect* pInkBounds);

        // Unified IDWriteTextLayout creation
        static HRESULT CreateLayout(
            const TextLayoutParameters& params,
            IDWriteTextLayout** ppLayout,
            IDWriteFontFace** ppSimulatedFace);

        static HRESULT CreateLayout(
            const TextLayoutParameters& params,
            IDWriteTextLayout** ppLayout,
            IDWriteFontFace3** ppSimulatedFace = nullptr);

        // Fast-path: updates existing IDWriteTextLayout with new axis values
        static bool UpdateAxisValues(
            IDWriteTextLayout* layout,
            const std::vector<DWRITE_FONT_AXIS_VALUE>& axisValues,
            UINT32 textLength);

        // Measures standard layout bounds (DWRITE_TEXT_METRICS1 with reading direction correction)
        static HRESULT MeasureLayoutBounds(
            IDWriteTextLayout* layout,
            Windows::Foundation::Rect* pLayoutBounds);

        // Measures tight ink/outline bounds using glyph outlines with overhang fallback
        static HRESULT MeasureInkBounds(
            IDWriteTextLayout* layout,
            IDWriteFontFace* overrideFace,
            Windows::Foundation::Rect* pInkBounds);

        // Applies shared grayscale + un-grid-fitted NATURAL_SYMMETRIC rendering params.
        // When isMaskSurface is true (white-on-A8 mask), uses gamma = 1.3 to compensate for alpha thinning.
        static void ApplyTextRenderingParams(
            ID2D1DeviceContext* context,
            bool isMaskSurface = false);

        // Renders an IDWriteTextLayout using Direct2D and color font support
        static void DrawTextLayout(
            ID2D1DeviceContext* context,
            IDWriteTextLayout* layout,
            D2D1_POINT_2F origin,
            const TextRenderParameters& params);

        // Renders a raw DWRITE_GLYPH_RUN using Direct2D and color font support
        static void DrawGlyphRun(
            ID2D1DeviceContext* context,
            D2D1_POINT_2F origin,
            const DWRITE_GLYPH_RUN* run,
            const TextRenderParameters& params);

        // Rasterizes onto a pooled off-device D2D target (4 mono / 2 color lanes) without holding GetRenderMutex()
        static bool RenderOffDevice(
            UINT32 surfaceWidth,
            UINT32 surfaceHeight,
            bool useColorPool,
            const std::function<void(ID2D1DeviceContext*, ID2D1SolidColorBrush*)>& drawCallback,
            std::vector<uint8_t>& outPixels,
            UINT32& outPitch);

        // Uploads off-device BGRA8 pixels to a CompositionDrawingSurface under GetRenderMutex()
        static bool UploadOffDevicePixelsToSurface(
            ICompositionDrawingSurfaceInterop* surfaceInterop,
            UINT32 surfaceWidth,
            UINT32 surfaceHeight,
            const std::vector<uint8_t>& pixels,
            UINT32 pitch);

        // Releases cached off-device target/staging bitmaps across all pooled renderers
        static void TrimOffDeviceRenderers();

        // Extracts FamilyName and FaceName from a DWriteFontFace^ (nullptr if unavailable).
        static void GetFontIdentity(
            DWriteFontFace^ fontFace,
            Platform::String^* outFamilyName,
            Platform::String^* outFaceName);

        // Compares a previously shown (FamilyName, FaceName) pair against a current DWriteFontFace^.
        // Returns true when FamilyName or FaceName differs, ignoring variable-axis variants of the same face.
        static bool HasFontIdentityChanged(
            Platform::String^ lastFamilyName,
            Platform::String^ lastFaceName,
            DWriteFontFace^ currentFontFace);

        // Populates visibility, thickness, draw order, and colors from a GlyphBoundsOptions^ object.
        static void PopulateGlyphBoundsOptions(
            Controls::GlyphBoundsOptions^ options,
            GlyphBoundsRenderData& outData);

        // Populates Ascent/CapHeight/x-Height/Baseline/Descent Y positions (in layout DIPs relative to ascent top)
        // from DWRITE_FONT_METRICS and fontSize.
        static void PopulateGlyphBoundsMetrics(
            const DWRITE_FONT_METRICS& fontMetrics,
            float fontSize,
            double baselineY,
            GlyphBoundsRenderData& outData);

        // Renders glyph metric guide lines, advance bounds, and ink bounds onto a D2D1 device context.
        static void DrawGlyphBounds(
            ID2D1DeviceContext* context,
            ID2D1SolidColorBrush* brush,
            const D2D1_COLOR_F& restoreColor,
            const GlyphBoundsRenderData& bounds,
            const GlyphBoundsDrawContext& drawCtx);
    };

    struct BackgroundRenderWorkItem
    {
        const void* ControlKey = nullptr;
        bool WasHidden = false;
        std::function<void(bool wasHidden)> Execute;
    };

    // Shared bounded worker pool that pops from the middle of the queue so visible items
    // in ItemsWrapGrid's realized cache window render before off-screen cache items.
    class BackgroundRenderQueue
    {
    public:
        static BackgroundRenderQueue& MonoInstance();
        static BackgroundRenderQueue& ColorInstance();

        explicit BackgroundRenderQueue(size_t maxWorkers);
        void Enqueue(BackgroundRenderWorkItem&& item);

    private:
        void SpawnWorker();

        std::mutex m_mutex;
        std::deque<BackgroundRenderWorkItem> m_queue;
        size_t m_activeWorkers = 0;
        size_t m_maxWorkers = 1;
    };
}
