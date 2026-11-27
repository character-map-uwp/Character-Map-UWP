//
// DirectText.h
// Declaration of the DirectText class.
//

#pragma once
#include "ITypographyInfo.h"
#include <DWriteFontAxis.h>
#include "DWriteFallbackFont.h"
#include "DWriteFontFace.h"
#include <vector>
#include <atomic>
#include <memory>
#include <mutex>


using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Documents;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;


namespace CharacterMapCX
{
	namespace Controls
	{
		struct DeferredLayoutCache;

		[Windows::Foundation::Metadata::WebHostHidden]
		[Windows::UI::Xaml::Data::Bindable]
		public ref class GlyphBoundsOptions sealed : public Windows::UI::Xaml::DependencyObject
		{
		public:
			GlyphBoundsOptions();

			event Windows::Foundation::EventHandler<Platform::Object^>^ Changed;

			static void RegisterDependencyProperties();

			static property DependencyProperty^ IsEnabledProperty { DependencyProperty^ get() { return _IsEnabledProperty; } }
			static property DependencyProperty^ ShowAscentProperty { DependencyProperty^ get() { return _ShowAscentProperty; } }
			static property DependencyProperty^ ShowCapHeightProperty { DependencyProperty^ get() { return _ShowCapHeightProperty; } }
			static property DependencyProperty^ ShowXHeightProperty { DependencyProperty^ get() { return _ShowXHeightProperty; } }
			static property DependencyProperty^ ShowBaselineProperty { DependencyProperty^ get() { return _ShowBaselineProperty; } }
			static property DependencyProperty^ ShowDescentProperty { DependencyProperty^ get() { return _ShowDescentProperty; } }
			static property DependencyProperty^ ShowAdvanceBoundsProperty { DependencyProperty^ get() { return _ShowAdvanceBoundsProperty; } }
			static property DependencyProperty^ ShowInkBoundsProperty { DependencyProperty^ get() { return _ShowInkBoundsProperty; } }

			static property DependencyProperty^ AscentBrushProperty { DependencyProperty^ get() { return _AscentBrushProperty; } }
			static property DependencyProperty^ CapHeightBrushProperty { DependencyProperty^ get() { return _CapHeightBrushProperty; } }
			static property DependencyProperty^ XHeightBrushProperty { DependencyProperty^ get() { return _XHeightBrushProperty; } }
			static property DependencyProperty^ BaselineBrushProperty { DependencyProperty^ get() { return _BaselineBrushProperty; } }
			static property DependencyProperty^ DescentBrushProperty { DependencyProperty^ get() { return _DescentBrushProperty; } }
			static property DependencyProperty^ AdvanceBoundsBrushProperty { DependencyProperty^ get() { return _AdvanceBoundsBrushProperty; } }
			static property DependencyProperty^ InkBoundsBrushProperty { DependencyProperty^ get() { return _InkBoundsBrushProperty; } }

			static property DependencyProperty^ LineThicknessProperty { DependencyProperty^ get() { return _LineThicknessProperty; } }
			static property DependencyProperty^ DrawLinesOverGlyphProperty { DependencyProperty^ get() { return _DrawLinesOverGlyphProperty; } }

			static property DependencyProperty^ AscentLabelProperty { DependencyProperty^ get() { return _AscentLabelProperty; } }
			static property DependencyProperty^ CapHeightLabelProperty { DependencyProperty^ get() { return _CapHeightLabelProperty; } }
			static property DependencyProperty^ XHeightLabelProperty { DependencyProperty^ get() { return _XHeightLabelProperty; } }
			static property DependencyProperty^ BaselineLabelProperty { DependencyProperty^ get() { return _BaselineLabelProperty; } }
			static property DependencyProperty^ DescentLabelProperty { DependencyProperty^ get() { return _DescentLabelProperty; } }
			static property DependencyProperty^ AdvanceBoundsLabelProperty { DependencyProperty^ get() { return _AdvanceBoundsLabelProperty; } }
			static property DependencyProperty^ InkBoundsLabelProperty { DependencyProperty^ get() { return _InkBoundsLabelProperty; } }

			property bool IsEnabled
			{
				bool get() { return (bool)GetValue(IsEnabledProperty); }
				void set(bool value) { SetValue(IsEnabledProperty, value); }
			}

			property bool ShowAscent
			{
				bool get() { return (bool)GetValue(ShowAscentProperty); }
				void set(bool value) { SetValue(ShowAscentProperty, value); }
			}

			property bool ShowCapHeight
			{
				bool get() { return (bool)GetValue(ShowCapHeightProperty); }
				void set(bool value) { SetValue(ShowCapHeightProperty, value); }
			}

			property bool ShowXHeight
			{
				bool get() { return (bool)GetValue(ShowXHeightProperty); }
				void set(bool value) { SetValue(ShowXHeightProperty, value); }
			}

			property bool ShowBaseline
			{
				bool get() { return (bool)GetValue(ShowBaselineProperty); }
				void set(bool value) { SetValue(ShowBaselineProperty, value); }
			}

			property bool ShowDescent
			{
				bool get() { return (bool)GetValue(ShowDescentProperty); }
				void set(bool value) { SetValue(ShowDescentProperty, value); }
			}

			property bool ShowAdvanceBounds
			{
				bool get() { return (bool)GetValue(ShowAdvanceBoundsProperty); }
				void set(bool value) { SetValue(ShowAdvanceBoundsProperty, value); }
			}

			property bool ShowInkBounds
			{
				bool get() { return (bool)GetValue(ShowInkBoundsProperty); }
				void set(bool value) { SetValue(ShowInkBoundsProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ AscentBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(AscentBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(AscentBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ CapHeightBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(CapHeightBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(CapHeightBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ XHeightBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(XHeightBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(XHeightBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ BaselineBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(BaselineBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(BaselineBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ DescentBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(DescentBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(DescentBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ AdvanceBoundsBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(AdvanceBoundsBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(AdvanceBoundsBrushProperty, value); }
			}

			property Windows::UI::Xaml::Media::SolidColorBrush^ InkBoundsBrush
			{
				Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(InkBoundsBrushProperty); }
				void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(InkBoundsBrushProperty, value); }
			}

			property double LineThickness
			{
				double get() { return (double)GetValue(LineThicknessProperty); }
				void set(double value) { SetValue(LineThicknessProperty, value); }
			}

			property bool DrawLinesOverGlyph
			{
				bool get() { return (bool)GetValue(DrawLinesOverGlyphProperty); }
				void set(bool value) { SetValue(DrawLinesOverGlyphProperty, value); }
			}

			property Platform::String^ AscentLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(AscentLabelProperty); }
				void set(Platform::String^ value) { SetValue(AscentLabelProperty, value); }
			}

			property Platform::String^ CapHeightLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(CapHeightLabelProperty); }
				void set(Platform::String^ value) { SetValue(CapHeightLabelProperty, value); }
			}

			property Platform::String^ XHeightLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(XHeightLabelProperty); }
				void set(Platform::String^ value) { SetValue(XHeightLabelProperty, value); }
			}

			property Platform::String^ BaselineLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(BaselineLabelProperty); }
				void set(Platform::String^ value) { SetValue(BaselineLabelProperty, value); }
			}

			property Platform::String^ DescentLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(DescentLabelProperty); }
				void set(Platform::String^ value) { SetValue(DescentLabelProperty, value); }
			}

			property Platform::String^ AdvanceBoundsLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(AdvanceBoundsLabelProperty); }
				void set(Platform::String^ value) { SetValue(AdvanceBoundsLabelProperty, value); }
			}

			property Platform::String^ InkBoundsLabel
			{
				Platform::String^ get() { return (Platform::String^)GetValue(InkBoundsLabelProperty); }
				void set(Platform::String^ value) { SetValue(InkBoundsLabelProperty, value); }
			}

		private:
			static DependencyProperty^ _IsEnabledProperty;
			static DependencyProperty^ _ShowAscentProperty;
			static DependencyProperty^ _ShowCapHeightProperty;
			static DependencyProperty^ _ShowXHeightProperty;
			static DependencyProperty^ _ShowBaselineProperty;
			static DependencyProperty^ _ShowDescentProperty;
			static DependencyProperty^ _ShowAdvanceBoundsProperty;
			static DependencyProperty^ _ShowInkBoundsProperty;

			static DependencyProperty^ _AscentBrushProperty;
			static DependencyProperty^ _CapHeightBrushProperty;
			static DependencyProperty^ _XHeightBrushProperty;
			static DependencyProperty^ _BaselineBrushProperty;
			static DependencyProperty^ _DescentBrushProperty;
			static DependencyProperty^ _AdvanceBoundsBrushProperty;
			static DependencyProperty^ _InkBoundsBrushProperty;

			static DependencyProperty^ _LineThicknessProperty;
			static DependencyProperty^ _DrawLinesOverGlyphProperty;

			static DependencyProperty^ _AscentLabelProperty;
			static DependencyProperty^ _CapHeightLabelProperty;
			static DependencyProperty^ _XHeightLabelProperty;
			static DependencyProperty^ _BaselineLabelProperty;
			static DependencyProperty^ _DescentLabelProperty;
			static DependencyProperty^ _AdvanceBoundsLabelProperty;
			static DependencyProperty^ _InkBoundsLabelProperty;

			static void OnOptionChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				if (auto o = dynamic_cast<GlyphBoundsOptions^>(d))
					o->Changed(o, nullptr);
			}
		};

		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class DirectText sealed : public Windows::UI::Xaml::Controls::Control
		{
		public:

			DirectText();
			virtual ~DirectText();

			property bool BlockUpdates;

			virtual void OnApplyTemplate() override;

			virtual Windows::Foundation::Size MeasureOverride(Windows::Foundation::Size size) override;

			virtual Windows::Foundation::Size ArrangeOverride(Windows::Foundation::Size finalSize) override;

		protected:
			virtual void OnPointerMoved(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
			virtual void OnPointerExited(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;

		public:

			void Update()
			{
				BlockUpdates = false;
				m_isStale = true;
				m_needsRender = true;
				m_forceColorSurface = false;
				++m_renderGeneration;
				if (m_spriteVisual != nullptr && IsBackgroundRenderEnabled && HasContentChangedForVisibility())
					m_spriteVisual->IsVisible = false;
				this->InvalidateMeasure();
				this->InvalidateArrange();
			}

			void CancelRender()
			{
				++m_renderGeneration;
				if (m_spriteVisual != nullptr && IsBackgroundRenderEnabled)
					m_spriteVisual->IsVisible = false;
			}

		internal:
			bool IsGenerationCurrent(uint64_t gen) { return m_renderGeneration.load() == gen; }
			void DecrementActiveBackgroundDraws() { --m_activeBackgroundDraws; }
			void ShowVisualIfCurrent(uint64_t gen)
			{
				if (m_renderGeneration.load() == gen && m_spriteVisual != nullptr)
					m_spriteVisual->IsVisible = true;
			}
			bool UpgradeToColorSurface(uint64_t gen, Windows::UI::Composition::CompositionDrawingSurface^ newSurface)
			{
				std::lock_guard<std::mutex> lock(m_surfaceMutex);
				if (!IsGenerationCurrent(gen))
					return false;
				m_forceColorSurface = true;
				m_renderedColor = true;
				m_drawingSurface = newSurface;
				if (m_surfaceBrush != nullptr)
					m_surfaceBrush->Surface = newSurface;
				if (m_spriteVisual != nullptr && m_surfaceBrush != nullptr)
					m_spriteVisual->Brush = m_surfaceBrush;
				return true;
			}
			bool SwapDrawingSurface(uint64_t gen, Windows::UI::Composition::CompositionDrawingSurface^ newSurface)
			{
				std::lock_guard<std::mutex> lock(m_surfaceMutex);
				if (!IsGenerationCurrent(gen))
					return false;
				m_drawingSurface = newSurface;
				if (m_surfaceBrush != nullptr)
					m_surfaceBrush->Surface = newSurface;
				return true;
			}

		public:

			#pragma region Dependency Properties

			static void RegisterDependencyProperties();

			static property DependencyProperty^ ColorRenderOptionProperty
			{
				DependencyProperty^ get() { return _ColorRenderOptionProperty; }
			}

			static property DependencyProperty^ IsColorFontEnabledProperty
			{
				DependencyProperty^ get() { return _IsColorFontEnabledProperty; }
			}

			static property DependencyProperty^ IsOverwriteCompensationEnabledProperty
			{
				DependencyProperty^ get() { return _IsOverwriteCompensationEnabledProperty; }
			}

			static property DependencyProperty^ IsCharacterFitEnabledProperty
			{
				DependencyProperty^ get() { return _IsCharacterFitEnabledProperty; }
			}

			static property DependencyProperty^ FallbackFontProperty
			{
				DependencyProperty^ get() { return _FallbackFontProperty; }
			}

			static property DependencyProperty^ TextProperty
			{
				DependencyProperty^ get() { return _TextProperty; }
			}

			static property DependencyProperty^ AxisProperty
			{
				DependencyProperty^ get() { return _AxisProperty; }
			}

			static property DependencyProperty^ UnicodeIndexProperty
			{
				DependencyProperty^ get() { return _UnicodeIndexProperty; }
			}

			static property DependencyProperty^ GlyphIndexProperty
			{
				DependencyProperty^ get() { return _GlyphIndexProperty; }
			}

			static property DependencyProperty^ FontFaceProperty
			{
				DependencyProperty^ get() { return _FontFaceProperty; }
			}

			static property DependencyProperty^ TypographyProperty
			{
				DependencyProperty^ get() { return _TypographyProperty; }
			}

			static property DependencyProperty^ IsTextWrappingEnabledProperty
			{
				DependencyProperty^ get() { return _IsTextWrappingEnabledProperty; }
			}

			static property DependencyProperty^ StyleSimulationsProperty
			{
				DependencyProperty^ get() { return _StyleSimulationsProperty; }
			}

			static property DependencyProperty^ IsBackgroundRenderEnabledProperty
			{
				DependencyProperty^ get() { return _IsBackgroundRenderEnabledProperty; }
			}

			static property DependencyProperty^ SurfaceSizeProperty
			{
				DependencyProperty^ get() { return _SurfaceSizeProperty; }
			}

			static property DependencyProperty^ RenderGlyphBoundsProperty
			{
				DependencyProperty^ get() { return _RenderGlyphBoundsProperty; }
			}

			static property DependencyProperty^ GlyphBoundsOptionsProperty
			{
				DependencyProperty^ get() { return _GlyphBoundsOptionsProperty; }
			}

			static property DependencyProperty^ TrimLeadingLineGapProperty
			{
				DependencyProperty^ get() { return _TrimLeadingLineGapProperty; }
			}

			property bool TrimLeadingLineGap
			{
				bool get() { return (bool)GetValue(TrimLeadingLineGapProperty); }
				void set(bool value) { SetValue(TrimLeadingLineGapProperty, value); }
			}

			property bool RenderGlyphBounds
			{
				bool get() { return (bool)GetValue(RenderGlyphBoundsProperty); }
				void set(bool value) { SetValue(RenderGlyphBoundsProperty, value); }
			}

			property GlyphBoundsOptions^ GlyphBoundsOptions
			{
				Controls::GlyphBoundsOptions^ get() { return (Controls::GlyphBoundsOptions^)GetValue(GlyphBoundsOptionsProperty); }
				void set(Controls::GlyphBoundsOptions^ value) { SetValue(GlyphBoundsOptionsProperty, value); }
			}

			property bool IsBackgroundRenderEnabled
			{
				bool get() { return (bool)GetValue(IsBackgroundRenderEnabledProperty); }
				void set(bool value) { SetValue(IsBackgroundRenderEnabledProperty, value); }
			}

			property Windows::Foundation::Size SurfaceSize
			{
				Windows::Foundation::Size get() { return (Windows::Foundation::Size)GetValue(SurfaceSizeProperty); }
				void set(Windows::Foundation::Size value) { SetValue(SurfaceSizeProperty, value); }
			}

			property Windows::UI::Xaml::Media::StyleSimulations StyleSimulations
			{
				Windows::UI::Xaml::Media::StyleSimulations get() { return (Windows::UI::Xaml::Media::StyleSimulations)GetValue(StyleSimulationsProperty); }
				void set(Windows::UI::Xaml::Media::StyleSimulations value) { SetValue(StyleSimulationsProperty, value); }
			}

			property DWriteFallbackFont^ FallbackFont
			{
				DWriteFallbackFont^ get() { return (DWriteFallbackFont^)GetValue(FallbackFontProperty); }
				void set(DWriteFallbackFont^ value) { SetValue(FallbackFontProperty, value); }
			}

			property DWriteColorRenderOption ColorRenderOption
			{
				DWriteColorRenderOption get() { return (DWriteColorRenderOption)GetValue(ColorRenderOptionProperty); }
				void set(DWriteColorRenderOption value) { SetValue(ColorRenderOptionProperty, value); }
			}

			property bool IsColorFontEnabled
			{
				bool get() { return (bool)GetValue(IsColorFontEnabledProperty); }
				void set(bool value) { SetValue(IsColorFontEnabledProperty, value); }
			}

			property bool IsCharacterFitEnabled
			{
				bool get() { return (bool)GetValue(IsCharacterFitEnabledProperty); }
				void set(bool value) { SetValue(IsCharacterFitEnabledProperty, value); }
			}

			property bool IsOverwriteCompensationEnabled
			{
				bool get() { return (bool)GetValue(IsOverwriteCompensationEnabledProperty); }
				void set(bool value) { SetValue(IsOverwriteCompensationEnabledProperty, value); }
			}

			property UINT32 UnicodeIndex
			{
				UINT32 get() { return (UINT32)GetValue(UnicodeIndexProperty); }
				void set(UINT32 value) { SetValue(UnicodeIndexProperty, value); }
			}

			property int GlyphIndex
			{
				int get() { return (int)GetValue(GlyphIndexProperty); }
				void set(int value) { SetValue(GlyphIndexProperty, value); }
			}

			property String^ Text
			{
				String^ get() { return (String^)GetValue(TextProperty); }
				void set(String^ value) { SetValue(TextProperty, value); }
			}

			property IVectorView<DWriteFontAxis^>^ Axis
			{
				IVectorView<DWriteFontAxis^>^ get() { return (IVectorView<DWriteFontAxis^>^)GetValue(AxisProperty); }
				void set(IVectorView<DWriteFontAxis^>^ value) { SetValue(AxisProperty, value); }
			}

			property DWriteFontFace^ FontFace
			{
				DWriteFontFace^ get() { return (DWriteFontFace^)GetValue(FontFaceProperty); }
				void set(DWriteFontFace^ value) { SetValue(FontFaceProperty, value); }
			}

			property ITypographyInfo^ Typography
			{
				ITypographyInfo^ get() { return (ITypographyInfo^)GetValue(TypographyProperty); }
				void set(ITypographyInfo^ value) { SetValue(TypographyProperty, value); }
			}

			property bool IsTextWrappingEnabled
			{
				bool get() { return (bool)GetValue(IsTextWrappingEnabledProperty); }
				void set(bool value) { SetValue(IsTextWrappingEnabledProperty, value); }
			}

#pragma endregion

		private:
			static DependencyProperty^ _FallbackFontProperty;
			static DependencyProperty^ _ColorRenderOptionProperty;
			static DependencyProperty^ _IsColorFontEnabledProperty;
			static DependencyProperty^ _IsOverwriteCompensationEnabledProperty;
			static DependencyProperty^ _UnicodeIndexProperty;
			static DependencyProperty^ _GlyphIndexProperty;
			static DependencyProperty^ _TextProperty;
			static DependencyProperty^ _AxisProperty;
			static DependencyProperty^ _FontFaceProperty;
			static DependencyProperty^ _TypographyProperty;
			static DependencyProperty^ _IsTextWrappingEnabledProperty;
			static DependencyProperty^ _IsCharacterFitEnabledProperty;
			static DependencyProperty^ _StyleSimulationsProperty;
			static DependencyProperty^ _IsBackgroundRenderEnabledProperty;
			static DependencyProperty^ _SurfaceSizeProperty;
			static DependencyProperty^ _RenderGlyphBoundsProperty;
			static DependencyProperty^ _GlyphBoundsOptionsProperty;
			static DependencyProperty^ _TrimLeadingLineGapProperty;

			Microsoft::WRL::ComPtr<IDWriteFontFace3> m_drawFontFace;

			Windows::Foundation::EventRegistrationToken m_loadedToken{};
			Windows::Foundation::EventRegistrationToken m_unloadedToken{};
			Windows::Foundation::EventRegistrationToken m_glyphBoundsOptionsToken{};
			Controls::GlyphBoundsOptions^ m_subscribedBoundsOptions = nullptr;
			int64 m_fontSizeToken = 0;
			int64 m_foregroundToken = 0;
			int64 m_flowDirectionToken = 0;
			int64 m_requestedThemeToken = 0;
			int64 m_paddingToken = 0;

			ComPtr<IDWriteTextLayout> m_textLayout;
			std::shared_ptr<DeferredLayoutCache> m_deferredLayoutCache;
			Windows::UI::Composition::SpriteVisual^ m_spriteVisual = nullptr;
			Windows::UI::Composition::CompositionDrawingSurface^ m_drawingSurface = nullptr;
			Windows::UI::Composition::CompositionSurfaceBrush^ m_surfaceBrush = nullptr;
			Windows::UI::Composition::CompositionMaskBrush^ m_maskBrush = nullptr;
			Windows::UI::Composition::CompositionColorBrush^ m_colorBrush = nullptr;
			LONG m_renderedWidth = 0;
			LONG m_renderedHeight = 0;
			bool m_renderedColor = false;
			bool m_isStale;
			bool m_isAxisOnlyStale;  // only axis values changed — reuse existing IDWriteTextLayout
			bool m_render;
			bool m_needsRender = true;
			bool m_deferredLayout = false;
			bool m_forceColorSurface = false;
			float m_deferredAvailW = 1.0f;
			float m_deferredAvailH = 1.0f;
			std::atomic<uint64_t> m_renderGeneration{ 0 };
			std::atomic<int> m_activeBackgroundDraws{ 0 };
			std::mutex m_surfaceMutex;
			double m_minWidth = 1.0;
			double m_targetScale = 1.0;
			UINT32 textLength = 0;

			Rect drawBounds;
			Rect layoutBounds;
			double m_metricBaselineY = 0.0;
			double m_metricAscentY = 0.0;
			double m_metricCapHeightY = 0.0;
			double m_metricXHeightY = 0.0;
			double m_metricDescentY = 0.0;
			double m_metricBaselineEm = 0.0;
			double m_metricAscentEm = 0.0;
			double m_metricCapHeightEm = 0.0;
			double m_metricXHeightEm = 0.0;
			double m_metricDescentEm = 0.0;
			bool m_hasCapHeight = false;
			bool m_hasXHeight = false;
			bool m_isSingleChar = false;

			struct BoundsHitTarget
			{
				int Kind = 0; // 0 = Horizontal, 1 = Vertical, 2 = Rectangle
				double Pos = 0.0;
				Rect Box{};
				Platform::String^ Label = nullptr;
			};
			std::vector<BoundsHitTarget> m_boundsHitTargets;
			double m_boundsHitTolerance = 6.0;
			Windows::UI::Xaml::Controls::Border^ m_rootBorder = nullptr;
			Windows::UI::Xaml::Controls::ToolTip^ m_boundsToolTip = nullptr;
			Windows::UI::Xaml::DispatcherTimer^ m_toolTipHideTimer = nullptr;
			Platform::String^ m_activeBoundsLabel = nullptr;
			double m_toolTipAnchorX = 0.0;
			double m_toolTipAnchorY = 0.0;

			void HideBoundsToolTip();
			void ScheduleHideBoundsToolTip();
			void CancelHideBoundsToolTip();
			void UpdateRootHitTestBackground();

			// Cached state for layout reuse (FontSize / Axis / MaxSize fast path)
			std::vector<DWRITE_FONT_AXIS_VALUE> m_lastAxisValues;
			DWriteFontFace^ m_lastFontFace;
			ITypographyInfo^ m_lastTypography;
			DWriteFallbackFont^ m_lastFallbackFont;
			Platform::String^ m_lastFamilyName;  // nullptr = no cached layout
			Platform::String^ m_lastFaceName;
			Platform::String^ m_lastText;
			double m_lastFontSize = 0.0;
			float m_lastMaxWidth = 0.0f;
			float m_lastMaxHeight = 0.0f;
			unsigned short m_lastFontWeight = 400;
			Windows::UI::Text::FontStyle m_lastFontStyle = Windows::UI::Text::FontStyle::Normal;
			Windows::UI::Text::FontStretch m_lastFontStretch = Windows::UI::Text::FontStretch::Normal;
			Windows::UI::Xaml::Media::StyleSimulations m_lastSimulations = Windows::UI::Xaml::Media::StyleSimulations::None;
			Windows::UI::Xaml::FlowDirection m_lastFlowDirection = Windows::UI::Xaml::FlowDirection::LeftToRight;
			bool m_lastWordWrapping = false;
			bool m_lastIsCharacterFit = false;
			Platform::String^ m_lastShownFamilyName;
			Platform::String^ m_lastShownFaceName;

			bool HasContentChangedForVisibility();
			bool ShouldRenderGlyphBounds()
			{
				if (!RenderGlyphBounds)
					return false;
				auto opts = GlyphBoundsOptions;
				return opts == nullptr || opts->IsEnabled;
			}


			void OnPropChanged(DependencyObject^ d, DependencyProperty^ p);

			static void OnRenderPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				DirectText^ c = (DirectText^)d;

				if (!c->BlockUpdates)
					c->Update();
			}

			static void OnSurfaceSizeChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				DirectText^ c = (DirectText^)d;
				c->m_needsRender = true;
				if (!c->BlockUpdates)
					c->InvalidateArrange();
			}

			static void OnGlyphBoundsOptionsChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e);
			void UpdateGlyphBoundsOptionsSubscription(Controls::GlyphBoundsOptions^ newOptions);

			void RenderToSurface(Windows::Foundation::Size finalSize);
			void ReleaseDrawingSurface();
			void ClearLayoutCache();
			bool ShouldRenderColor();
			Windows::UI::Color GetForegroundColor();

			void OnLoaded(Platform::Object^ sender, RoutedEventArgs^ e);
			void OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e);
		};


		// This function is called from the App constructor in App.xaml.cpp
		// to register the properties
		inline void DirectText::RegisterDependencyProperties()
		{
			static bool registered = false;
			if (registered)
				return;
			
			registered = true;

			auto callback = ref new PropertyChangedCallback(&DirectText::OnRenderPropertyChanged);
			auto meta = ref new PropertyMetadata(nullptr, callback);

			if (_FallbackFontProperty == nullptr)
			{
				_FallbackFontProperty = DependencyProperty::Register(
					"FallbackFont", DWriteFallbackFont::typeid, DirectText::typeid, meta);
			}

			if (_ColorRenderOptionProperty == nullptr)
			{
				_ColorRenderOptionProperty = DependencyProperty::Register(
					"ColorRenderOption", DWriteColorRenderOption::typeid, DirectText::typeid, ref new PropertyMetadata(DWriteColorRenderOption::Default, callback));
			}

			if (_IsCharacterFitEnabledProperty == nullptr)
			{
				_IsCharacterFitEnabledProperty = DependencyProperty::Register(
					"IsCharacterFitEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			if (_IsOverwriteCompensationEnabledProperty == nullptr)
			{
				_IsOverwriteCompensationEnabledProperty = DependencyProperty::Register(
					"IsOverwriteCompensationEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(true, callback));
			}

			if (_IsColorFontEnabledProperty == nullptr)
			{
				_IsColorFontEnabledProperty = DependencyProperty::Register(
					"IsColorFontEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(true, callback));
			}

			if (_UnicodeIndexProperty == nullptr)
			{
				_UnicodeIndexProperty = DependencyProperty::Register(
					"UnicodeIndex", UINT32::typeid, DirectText::typeid, ref new PropertyMetadata(Windows::Foundation::PropertyValue::CreateUInt32(0), callback));
			}

			if (_TextProperty == nullptr)
			{
				_TextProperty = DependencyProperty::Register(
					"Text", Platform::String::typeid, DirectText::typeid, meta);
			}

			if (_AxisProperty == nullptr)
			{
				_AxisProperty = DependencyProperty::Register(
					"Axis", IVectorView<DWriteFontAxis^>::typeid, DirectText::typeid, meta);
			}

			if (_FontFaceProperty == nullptr)
			{
				_FontFaceProperty = DependencyProperty::Register(
					"FontFace", DWriteFontFace::typeid, DirectText::typeid, meta);
			}

			if (_TypographyProperty == nullptr)
			{
				_TypographyProperty = DependencyProperty::Register(
					"Typography", ITypographyInfo::typeid, DirectText::typeid, meta);
			}

			if (_IsTextWrappingEnabledProperty == nullptr)
			{
				_IsTextWrappingEnabledProperty = DependencyProperty::Register(
					"IsTextWrappingEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			if (_GlyphIndexProperty == nullptr)
			{
				_GlyphIndexProperty = DependencyProperty::Register(
					"GlyphIndex", int::typeid, DirectText::typeid, ref new PropertyMetadata(-1, callback));
			}

			if (_StyleSimulationsProperty == nullptr)
			{
				_StyleSimulationsProperty = DependencyProperty::Register(
					"StyleSimulations", Windows::UI::Xaml::Media::StyleSimulations::typeid, DirectText::typeid, ref new PropertyMetadata(Windows::UI::Xaml::Media::StyleSimulations::None, callback));
			}

			if (_IsBackgroundRenderEnabledProperty == nullptr)
			{
				_IsBackgroundRenderEnabledProperty = DependencyProperty::Register(
					"IsBackgroundRenderEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			if (_SurfaceSizeProperty == nullptr)
			{
				_SurfaceSizeProperty = DependencyProperty::Register(
					"SurfaceSize", Windows::Foundation::Size::typeid, DirectText::typeid,
					ref new PropertyMetadata(Windows::Foundation::Size(0, 0), ref new PropertyChangedCallback(&DirectText::OnSurfaceSizeChanged)));
			}

			if (_RenderGlyphBoundsProperty == nullptr)
			{
				_RenderGlyphBoundsProperty = DependencyProperty::Register(
					"RenderGlyphBounds", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			if (_GlyphBoundsOptionsProperty == nullptr)
			{
				_GlyphBoundsOptionsProperty = DependencyProperty::Register(
					"GlyphBoundsOptions", Controls::GlyphBoundsOptions::typeid, DirectText::typeid,
					ref new PropertyMetadata(nullptr, ref new PropertyChangedCallback(&DirectText::OnGlyphBoundsOptionsChanged)));
			}

			if (_TrimLeadingLineGapProperty == nullptr)
			{
				_TrimLeadingLineGapProperty = DependencyProperty::Register(
					"TrimLeadingLineGap", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			Controls::GlyphBoundsOptions::RegisterDependencyProperties();
		}
	}
}
