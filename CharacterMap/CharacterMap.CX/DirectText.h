//
// DirectText.h
// Declaration of the DirectText class.
//

#pragma once
#include "ITypographyInfo.h"


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

			static property DependencyProperty^ TextProperty
			{
				DependencyProperty^ get() { return _TextProperty; }
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
#pragma endregion

		private:
			static DependencyProperty^ _UnicodeIndexProperty;
			static DependencyProperty^ _TextProperty;
			static DependencyProperty^ _FontFaceProperty;
			static DependencyProperty^ _TypographyProperty;

			Windows::Foundation::EventRegistrationToken m_drawToken;
			CanvasControl^ m_canvas;
			CanvasTextLayout^ m_layout;
			bool m_isStale;
			bool m_render;

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

			void Render(CanvasDrawingSession^ ds);
			void OnDraw(CanvasControl^ sender, CanvasDrawEventArgs^ args);
			void OnCreateResources(CanvasControl^ sender, CanvasCreateResourcesEventArgs^ args);
			void OnLoaded(Platform::Object^ sender, Windows::UI::Xaml::RoutedEventArgs^ e);
		};


		// This function is called from the App constructor in App.xaml.cpp
		// to register the properties
		void DirectText::RegisterDependencyProperties()
		{
			auto callback = ref new PropertyMetadata(nullptr,
				ref new PropertyChangedCallback(&DirectText::OnRenderPropertyChanged));

			if (_UnicodeIndexProperty == nullptr)
			{
				_UnicodeIndexProperty = DependencyProperty::Register(
					"UnicodeIndex", UINT32::typeid, DirectText::typeid, ref new PropertyMetadata((UINT32)0,
						ref new PropertyChangedCallback(&DirectText::OnRenderPropertyChanged)));
			}

			if (_TextProperty == nullptr)
			{
				_TextProperty = DependencyProperty::Register(
					"Text", Platform::String::typeid, DirectText::typeid, callback);
			}

			if (_FontFaceProperty == nullptr)
			{
				_FontFaceProperty = DependencyProperty::Register(
					"FontFace", Microsoft::Graphics::Canvas::Text::CanvasFontFace::typeid, DirectText::typeid, callback);
			}

			if (_TypographyProperty == nullptr)
			{
				_TypographyProperty = DependencyProperty::Register(
					"Typography", ITypographyInfo::typeid, DirectText::typeid, callback);
			}
		}
	}
}
