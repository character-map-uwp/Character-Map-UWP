#pragma once

#include <d3d11_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <wrl.h>
#include <vector>

#include "DWriteFontFace.h"
#include "DWriteTypographyCollection.h"

namespace CharacterMapCX
{
    namespace Controls
    {
        [Windows::Foundation::Metadata::WebHostHidden]
        public ref class FontGlyphs sealed : public Windows::UI::Xaml::FrameworkElement
        {
        public:
            FontGlyphs();
            virtual ~FontGlyphs();

            static void Trim();
            static void ReleaseGraphicsDevice(Windows::UI::Composition::Compositor^ compositor);
            static void ClearAtlases(Windows::UI::Composition::Compositor^ compositor);

            static int32 GetActiveSurfaceCount();
            static uint64 GetActiveSurfaceBytes();

            #pragma region Dependency Properties

            static void RegisterDependencyProperties();

            static property Windows::UI::Xaml::DependencyProperty^ FontFaceProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _FontFaceProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ FontSizeProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _FontSizeProperty; }
            }


            static property Windows::UI::Xaml::DependencyProperty^ IndicesProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _IndicesProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ UnicodeStringProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _UnicodeStringProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ ForegroundProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _ForegroundProperty; }
            }


            static property Windows::UI::Xaml::DependencyProperty^ IsColorFontEnabledProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _IsColorFontEnabledProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ StyleSimulationsProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _StyleSimulationsProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ StretchProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _StretchProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ StretchDirectionProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _StretchDirectionProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ TypographyProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _TypographyProperty; }
            }

            #pragma endregion

            #pragma region Properties

            property DWriteFontFace^ FontFace
            {
                DWriteFontFace^ get() { return (DWriteFontFace^)GetValue(FontFaceProperty); }
                void set(DWriteFontFace^ value) { SetValue(FontFaceProperty, value); }
            }

            property double FontSize
            {
                double get() { return (double)GetValue(FontSizeProperty); }
                void set(double value) { SetValue(FontSizeProperty, value); }
            }


            property Platform::String^ Indices
            {
                Platform::String^ get() { return (Platform::String^)GetValue(IndicesProperty); }
                void set(Platform::String^ value) { SetValue(IndicesProperty, value); }
            }

            property Platform::String^ UnicodeString
            {
                Platform::String^ get() { return (Platform::String^)GetValue(UnicodeStringProperty); }
                void set(Platform::String^ value) { SetValue(UnicodeStringProperty, value); }
            }

            property Windows::UI::Xaml::Media::Brush^ Foreground
            {
                Windows::UI::Xaml::Media::Brush^ get() { return (Windows::UI::Xaml::Media::Brush^)GetValue(ForegroundProperty); }
                void set(Windows::UI::Xaml::Media::Brush^ value) { SetValue(ForegroundProperty, value); }
            }


            property bool IsColorFontEnabled
            {
                bool get() { return (bool)GetValue(IsColorFontEnabledProperty); }
                void set(bool value) { SetValue(IsColorFontEnabledProperty, value); }
            }

            property Windows::UI::Xaml::Media::StyleSimulations StyleSimulations
            {
                Windows::UI::Xaml::Media::StyleSimulations get() { return (Windows::UI::Xaml::Media::StyleSimulations)GetValue(StyleSimulationsProperty); }
                void set(Windows::UI::Xaml::Media::StyleSimulations value) { SetValue(StyleSimulationsProperty, value); }
            }

            property Windows::UI::Xaml::Media::Stretch Stretch
            {
                Windows::UI::Xaml::Media::Stretch get() { return (Windows::UI::Xaml::Media::Stretch)GetValue(StretchProperty); }
                void set(Windows::UI::Xaml::Media::Stretch value) { SetValue(StretchProperty, value); }
            }

            property Windows::UI::Xaml::Controls::StretchDirection StretchDirection
            {
                Windows::UI::Xaml::Controls::StretchDirection get() { return (Windows::UI::Xaml::Controls::StretchDirection)GetValue(StretchDirectionProperty); }
                void set(Windows::UI::Xaml::Controls::StretchDirection value) { SetValue(StretchDirectionProperty, value); }
            }

            property DWriteTypographyCollection^ Typography
            {
                DWriteTypographyCollection^ get() { return (DWriteTypographyCollection^)GetValue(TypographyProperty); }
                void set(DWriteTypographyCollection^ value) { SetValue(TypographyProperty, value); }
            }

            #pragma endregion

            virtual Windows::Foundation::Size MeasureOverride(Windows::Foundation::Size availableSize) override;
            virtual Windows::Foundation::Size ArrangeOverride(Windows::Foundation::Size finalSize) override;

            void InvalidateLayoutAndRender();

        private:
            static void EnsureDependencyProperties();

            static Windows::UI::Xaml::DependencyProperty^ _FontFaceProperty;
            static Windows::UI::Xaml::DependencyProperty^ _FontSizeProperty;
            static Windows::UI::Xaml::DependencyProperty^ _IndicesProperty;
            static Windows::UI::Xaml::DependencyProperty^ _UnicodeStringProperty;
            static Windows::UI::Xaml::DependencyProperty^ _ForegroundProperty;
            static Windows::UI::Xaml::DependencyProperty^ _IsColorFontEnabledProperty;
            static Windows::UI::Xaml::DependencyProperty^ _StyleSimulationsProperty;
            static Windows::UI::Xaml::DependencyProperty^ _StretchProperty;
            static Windows::UI::Xaml::DependencyProperty^ _StretchDirectionProperty;
            static Windows::UI::Xaml::DependencyProperty^ _TypographyProperty;

            static void OnPropertyChanged(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            static void OnFontSizeChanged(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            static void OnForegroundChanged(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            void UpdateForground();

            void InvalidateRenderOnly();
            void ParseAndLayoutGlyphs();
            Windows::Foundation::Size ComputeScaleFactor(Windows::Foundation::Size availableSize, Windows::Foundation::Size contentSize);
            void RenderGlyphs();
            void ReleaseDrawingSurface();
            void OnLoaded(Platform::Object^ sender, Windows::UI::Xaml::RoutedEventArgs^ e);
            void OnUnloaded(Platform::Object^ sender, Windows::UI::Xaml::RoutedEventArgs^ e);
            Windows::UI::Color GetForegroundColor();
            UINT32 GetForegroundColorKey();
            bool ShouldRenderColor();

            Windows::UI::Composition::SpriteVisual^ m_spriteVisual;
            Windows::UI::Composition::CompositionSurfaceBrush^ m_surfaceBrush;
            Windows::UI::Composition::CompositionColorBrush^ m_colorBrush;
            Windows::UI::Composition::CompositionMaskBrush^ m_maskBrush;
            Windows::UI::Composition::CompositionDrawingSurface^ m_drawingSurface;
            bool m_isUsingSharedAtlas;
            LONG m_renderedWidth;
            LONG m_renderedHeight;
            bool m_renderedColor;
            float m_renderedScaleX;
            float m_renderedScaleY;
            Windows::Foundation::Size m_contentSize;
            Windows::Foundation::Size m_scale;
            float m_baseline;
            float m_padLeft;
            float m_padRight;
            float m_padTop;
            float m_padBottom;

            bool m_isLayoutDirty;
            bool m_isRenderDirty;
            Windows::Foundation::EventRegistrationToken m_loadedToken;
            Windows::Foundation::EventRegistrationToken m_unloadedToken;
            Windows::Foundation::EventRegistrationToken m_themeChangedToken;

            // Parsed glyph data with Small Buffer Optimization (SBO).
            // Up to 2 glyphs (covering single glyphs and surrogate pairs) are stored inline,
            // eliminating 3 heap allocations per control instance for character grid items.
            static const size_t SBO_GLYPH_CAPACITY = 2;
            size_t m_glyphCount;
            UINT16 m_inlineIndices[SBO_GLYPH_CAPACITY];
            FLOAT m_inlineAdvances[SBO_GLYPH_CAPACITY];
            DWRITE_GLYPH_OFFSET m_inlineOffsets[SBO_GLYPH_CAPACITY];

            std::vector<UINT16> m_heapIndices;
            std::vector<FLOAT> m_heapAdvances;
            std::vector<DWRITE_GLYPH_OFFSET> m_heapOffsets;

            const UINT16* GlyphIndicesData() const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineIndices : m_heapIndices.data(); }
            const FLOAT* GlyphAdvancesData() const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineAdvances : m_heapAdvances.data(); }
            const DWRITE_GLYPH_OFFSET* GlyphOffsetsData() const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineOffsets : m_heapOffsets.data(); }

            UINT16 GetGlyphIndex(size_t i) const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineIndices[i] : m_heapIndices[i]; }
            FLOAT GetGlyphAdvance(size_t i) const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineAdvances[i] : m_heapAdvances[i]; }
            DWRITE_GLYPH_OFFSET GetGlyphOffset(size_t i) const { return m_glyphCount <= SBO_GLYPH_CAPACITY ? m_inlineOffsets[i] : m_heapOffsets[i]; }

            // Zero-advance / combining mark bounds
            bool m_hasInkBounds;
            double m_designInkLeft;
            double m_designInkWidth;
            double m_designInkTop;
            double m_designInkHeight;
            double m_designUnitsPerEm;
        };
    }
}
