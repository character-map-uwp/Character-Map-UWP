//
// DirectText.h
// Declaration of the DirectText class.
//

#pragma once
#include "ITypographyInfo.h"
#include <DWriteFontAxis.h>
#include "DWriteFallbackFont.h"


using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Documents;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;
using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::UI;
using namespace Microsoft::Graphics::Canvas::UI::Xaml;
using namespace Microsoft::Graphics::Canvas::Text;


namespace CharacterMapCX
{
	namespace Controls
	{
		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class DirectText sealed : public Windows::UI::Xaml::Controls::Control
		{
		public:

			DirectText();

			virtual void OnApplyTemplate() override;

			virtual Windows::Foundation::Size MeasureOverride(Windows::Foundation::Size size) override;

			#pragma region Dependency Properties

			static void RegisterDependencyProperties();

			static property DependencyProperty^ IsColorFontEnabledProperty
			{
				DependencyProperty^ get() { return _IsColorFontEnabledProperty; }
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

			property DWriteFallbackFont^ FallbackFont
			{
				DWriteFallbackFont^ get() { return (DWriteFallbackFont^)GetValue(FallbackFontProperty); }
				void set(DWriteFallbackFont^ value) { SetValue(FallbackFontProperty, value); }
			}

			property bool IsColorFontEnabled
			{
				bool get() { return (bool)GetValue(IsColorFontEnabledProperty); }
				void set(bool value) { SetValue(IsColorFontEnabledProperty, value); }
			}

			property UINT32 UnicodeIndex
			{
				UINT32 get() { return (UINT32)GetValue(UnicodeIndexProperty); }
				void set(UINT32 value) { SetValue(UnicodeIndexProperty, value); }
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

			property CanvasFontFace^ FontFace
			{
				CanvasFontFace^ get() { return (CanvasFontFace^)GetValue(FontFaceProperty); }
				void set(CanvasFontFace^ value) { SetValue(FontFaceProperty, value); }
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
			static DependencyProperty^ _IsColorFontEnabledProperty;
			static DependencyProperty^ _UnicodeIndexProperty;
			static DependencyProperty^ _TextProperty;
			static DependencyProperty^ _AxisProperty;
			static DependencyProperty^ _FontFaceProperty;
			static DependencyProperty^ _TypographyProperty;
			static DependencyProperty^ _IsTextWrappingEnabledProperty;

			Windows::Foundation::EventRegistrationToken m_drawToken;
			CanvasControl^ m_canvas;
			CanvasTextLayout^ m_layout;
			bool m_isStale;
			bool m_render;

			void OnFontSizeChanged(DependencyObject^ d, DependencyProperty^ p);

			static void OnRenderPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				DirectText^ c = (DirectText^)d;
				c->Update();
			}

			void Update()
			{
				m_isStale = true;
				this->InvalidateMeasure();
			}

			void OnDraw(CanvasControl^ sender, CanvasDrawEventArgs^ args);
			void OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args);
		};


		// This function is called from the App constructor in App.xaml.cpp
		// to register the properties
		void DirectText::RegisterDependencyProperties()
		{
			auto callback = ref new PropertyChangedCallback(&DirectText::OnRenderPropertyChanged);
			auto meta = ref new PropertyMetadata(nullptr, callback);

			if (_FallbackFontProperty == nullptr)
			{
				_FallbackFontProperty = DependencyProperty::Register(
					"FallbackFont", DWriteFallbackFont::typeid, DirectText::typeid, meta);
			}

			if (_IsColorFontEnabledProperty == nullptr)
			{
				_IsColorFontEnabledProperty = DependencyProperty::Register(
					"IsColorFontEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(true, callback));
			}

			if (_UnicodeIndexProperty == nullptr)
			{
				_UnicodeIndexProperty = DependencyProperty::Register(
					"UnicodeIndex", UINT32::typeid, DirectText::typeid, ref new PropertyMetadata((UINT32)0, callback));
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
					"FontFace", Microsoft::Graphics::Canvas::Text::CanvasFontFace::typeid, DirectText::typeid, meta);
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
		}
	}
}
