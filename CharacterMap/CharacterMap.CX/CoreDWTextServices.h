#pragma once

#include <d2d1_3.h>
#include <dwrite_3.h>
#include <wrl.h>
#include <vector>

#include "DWriteFontFace.h"
#include "DWriteFallbackFont.h"
#include "DWriteFontAxis.h"
#include "DWriteFontSimulations.h"
#include "ITypographyInfo.h"
#include "DWriteTypographyCollection.h"
#include "GlyphImageFormat.h"

namespace CharacterMapCX
{
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
    };
}
