//
// DirectText.h
// Declaration of the DirectText class.
//

#pragma once
#include "ITypographyInfo.h"
#include <DWriteFontAxis.h>
#include "DWriteFallbackFont.h"
#include "DWriteFontFace.h"


using namespace Platform;
using namespace Windows::Foundation;
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

			property bool BlockUpdates;

			virtual void OnApplyTemplate() override;

			virtual Windows::Foundation::Size MeasureOverride(Windows::Foundation::Size size) override;

			void Update()
			{
				BlockUpdates = false;
				m_isStale = true;
				this->InvalidateMeasure();
			}

			#pragma region Dependency Properties

			static void RegisterDependencyProperties();

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

			property CanvasControl^ InternalCanvas
			{
				CanvasControl^ get() { return m_canvas; }
			}

#pragma endregion

		private:
			static DependencyProperty^ _FallbackFontProperty;
			static DependencyProperty^ _IsColorFontEnabledProperty;
			static DependencyProperty^ _IsOverwriteCompensationEnabledProperty;
			static DependencyProperty^ _UnicodeIndexProperty;
			static DependencyProperty^ _TextProperty;
			static DependencyProperty^ _AxisProperty;
			static DependencyProperty^ _FontFaceProperty;
			static DependencyProperty^ _TypographyProperty;
			static DependencyProperty^ _IsTextWrappingEnabledProperty;
			static DependencyProperty^ _IsCharacterFitEnabledProperty;

			Windows::Foundation::EventRegistrationToken m_drawToken;
			Windows::Foundation::EventRegistrationToken m_createToken;

			ComPtr<ID2D1SolidColorBrush> m_brush;
			ComPtr<IDWriteTextLayout> m_textLayout;
			CanvasControl^ m_canvas;
			bool m_isStale;
			bool m_render;
			double m_minWidth = 1.0;
			double m_targetScale = 1.0;
			UINT32 textLength = 0;

			Rect drawBounds;
			Rect layoutBounds;


			void OnPropChanged(DependencyObject^ d, DependencyProperty^ p);

			static void OnRenderPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				DirectText^ c = (DirectText^)d;

				if (!c->BlockUpdates)
					c->Update();
			}

			void EnsureCanvas();
			void DestroyCanvas(CanvasControl^ control);

			void OnLoaded(Platform::Object^ sender, RoutedEventArgs^ e);
			void OnUnloaded(Platform::Object^ sender, RoutedEventArgs^ e);

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

			if (_IsCharacterFitEnabledProperty == nullptr)
			{
				_IsCharacterFitEnabledProperty = DependencyProperty::Register(
					"IsCharacterFitEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
			}

			if (_IsOverwriteCompensationEnabledProperty == nullptr)
			{
				_IsOverwriteCompensationEnabledProperty = DependencyProperty::Register(
					"IsOverwriteCompensationEnabled", bool::typeid, DirectText::typeid, ref new PropertyMetadata(false, callback));
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
		}
	}
}
