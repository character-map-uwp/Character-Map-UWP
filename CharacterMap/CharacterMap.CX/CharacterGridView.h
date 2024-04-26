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
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Documents;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::UI::Xaml::Media;
using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::UI;
using namespace Microsoft::Graphics::Canvas::UI::Xaml;
using namespace Microsoft::Graphics::Canvas::Text;


namespace CharacterMapCX
{
	public ref class ItemTooltipData sealed
	{
	public:
		property ICharacter^ Char;
		property IFontFace^ Variant;
		property GridViewItem^ Container;
	};

	namespace Controls
	{
		public delegate void ItemDoubleTappedHandler(Platform::Object^ sender, ICharacter^ c);

		[Windows::Foundation::Metadata::WebHostHidden]
		public ref class CharacterGridView sealed : public Windows::UI::Xaml::Controls::GridView
		{
		public:

			CharacterGridView();

			event ItemDoubleTappedHandler^ ItemDoubleTapped;

			event TypedEventHandler<UIElement^, ContextRequestedEventArgs^>^ ItemContextRequested;

			property bool DisableItemClicks;


			#pragma region Dependency Properties

			static void RegisterDependencyProperties();

			static property DependencyProperty^ ItemSizeProperty
			{
				DependencyProperty^ get() { return _ItemSizeProperty; }
			}

			static property DependencyProperty^ RepositionAnimationCollectionProperty
			{
				DependencyProperty^ get() { return _RepositionAnimationCollectionProperty; }
			}

			static property DependencyProperty^ ShowColorGlyphsProperty
			{
				DependencyProperty^ get() { return _ShowColorGlyphsProperty; }
			}

			static property DependencyProperty^ EnableResizeAnimationProperty
			{
				DependencyProperty^ get() { return _EnableResizeAnimationProperty; }
			}

			static property DependencyProperty^ ItemFontFamilyProperty
			{
				DependencyProperty^ get() { return _ItemFontFamilyProperty; }
			}

			static property DependencyProperty^ ItemFontFaceProperty
			{
				DependencyProperty^ get() { return _ItemFontFaceProperty; }
			}

			static property DependencyProperty^ ItemTypographyProperty
			{
				DependencyProperty^ get() { return _ItemTypographyProperty; }
			}

			static property DependencyProperty^ ItemFontVariantProperty
			{
				DependencyProperty^ get() { return _ItemFontVariantProperty; }
			}

			static property DependencyProperty^ ItemAnnotationProperty
			{
				DependencyProperty^ get() { return _ItemAnnotationProperty; }
			}

			static property DependencyProperty^ ItemBackgroundTransitionProperty
			{
				DependencyProperty^ get() { return _ItemBackgroundTransitionProperty; }
			}

			


			property BrushTransition^ ItemBackgroundTransition
			{
				BrushTransition^ get() { return (BrushTransition^)GetValue(ItemBackgroundTransitionProperty); }
				void set(BrushTransition^ value) { SetValue(ItemBackgroundTransitionProperty, value); }
			}

			property ImplicitAnimationCollection^ RepositionAnimationCollection
			{
				ImplicitAnimationCollection^ get() { return (ImplicitAnimationCollection^)GetValue(RepositionAnimationCollectionProperty); }
				void set(ImplicitAnimationCollection^ value) { SetValue(RepositionAnimationCollectionProperty, value); }
			}
			
			property double ItemSize
			{
				double get() { return (double)GetValue(ItemSizeProperty); }
				void set(double value) { SetValue(ItemSizeProperty, value); }
			}

			property bool ShowColorGlyphs
			{
				bool get() { return (bool)GetValue(ShowColorGlyphsProperty); }
				void set(bool value) { SetValue(ShowColorGlyphsProperty, value); }
			}

			property bool EnableResizeAnimation
			{
				bool get() { return (bool)GetValue(EnableResizeAnimationProperty); }
				void set(bool value) { SetValue(EnableResizeAnimationProperty, value); }
			}

			property Media::FontFamily^ ItemFontFamily
			{
				Media::FontFamily^ get() { return (Media::FontFamily^)GetValue(ItemFontFamilyProperty); }
				void set(Media::FontFamily^ value) { SetValue(ItemFontFamilyProperty, value); }
			}

			property DWriteFontFace^ ItemFontFace
			{
				DWriteFontFace^ get() { return (DWriteFontFace^)GetValue(ItemFontFaceProperty); }
				void set(DWriteFontFace^ value) { SetValue(ItemFontFaceProperty, value); }
			}

			property ITypographyInfo^ ItemTypography
			{
				ITypographyInfo^ get() { return (ITypographyInfo^)GetValue(ItemTypographyProperty); }
				void set(ITypographyInfo^ value) { SetValue(ItemTypographyProperty, value); }
			}

			property IFontFace^ ItemFontVariant
			{
				IFontFace^ get() { return (IFontFace^)GetValue(ItemFontVariantProperty); }
				void set(IFontFace^ value) { SetValue(ItemFontVariantProperty, value); }
			}

			property GlyphAnnotation ItemAnnotation
			{
				GlyphAnnotation get() { return (GlyphAnnotation)GetValue(ItemAnnotationProperty); }
				void set(GlyphAnnotation value) { SetValue(ItemAnnotationProperty, value); }
			}

			#pragma endregion




			void UpdateSize(double value)
			{
				ItemSize = value;
				if (this->Items->Size == 0 || ItemsPanelRoot == nullptr)
					return;

				GridViewHelper::ApplySize(m_xamlDirect, this, value);
			}

			void UpdateAnimation(bool newValue)
			{
				if (ItemsSource == nullptr || ItemsPanelRoot == nullptr)
					return;

				for (auto iter = ItemsPanelRoot->Children->First(); iter->HasCurrent; iter->MoveNext())
				{
					auto v = ElementCompositionPreview::GetElementVisual(iter->Current);
					v->ImplicitAnimations = newValue ? RepositionAnimationCollection : nullptr;
				}
			}

			void UpdateColorFonts(bool value);

			void UpdateTypographies(ITypographyInfo^ info);

			void UpdateUnicode(GlyphAnnotation value);

		private:
			static DependencyProperty^ _ItemBackgroundTransitionProperty;
			static DependencyProperty^ _RepositionAnimationCollectionProperty;
			static DependencyProperty^ _ItemSizeProperty;
			static DependencyProperty^ _ShowColorGlyphsProperty;
			static DependencyProperty^ _EnableResizeAnimationProperty;
			static DependencyProperty^ _ItemFontFamilyProperty;
			static DependencyProperty^ _ItemFontFaceProperty;
			static DependencyProperty^ _ItemTypographyProperty;
			static DependencyProperty^ _ItemFontVariantProperty;
			static DependencyProperty^ _ItemAnnotationProperty;

			RoutedEventHandler^ m_tooltipLoadedHandler = nullptr;
			DoubleTappedEventHandler^ m_doubleTapped = nullptr;
			TypedEventHandler<UIElement^, ContextRequestedEventArgs^>^ m_contextRequested = nullptr;

			CharacterGridViewTemplateSettings^ m_templateSettings = nullptr;
			XamlDirect^ m_xamlDirect = nullptr;

			void ToolTipLoaded(Platform::Object^ sender, RoutedEventArgs^ e);
			void OnItemDoubleTapped(Platform::Object^ sender, DoubleTappedRoutedEventArgs^ e);


			void OnChoosingItemContainer(ListViewBase^ sender, ChoosingItemContainerEventArgs^ args);
			void OnContainerContentChanging(ListViewBase^ sender, ContainerContentChangingEventArgs^ args);

			void PokeZIndex(UIElement^ args);

			static void OnItemSizeChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				((CharacterGridView^)d)->m_templateSettings->Size = (double)e->NewValue;
			}

			static void OnItemFontFamilyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				((CharacterGridView^)d)->m_templateSettings->FontFamily = (Media::FontFamily^)e->NewValue;
			}

			static void OnItemFontFaceChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				((CharacterGridView^)d)->m_templateSettings->FontFace = (DWriteFontFace^)e->NewValue;
			}

			static void OnItemTypographyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				auto i = (ITypographyInfo^)e->NewValue;
				auto g = ((CharacterGridView^)d);
				g->m_templateSettings->Typography = i;
				g->UpdateTypographies(i);
			}

			static void OnItemAnnotationChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				auto i = (GlyphAnnotation)e->NewValue;
				auto g = ((CharacterGridView^)d);
				g->m_templateSettings->Annotation = i;
				g->UpdateUnicode(i);
			}

			static void OnShowColorGlyphsChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				auto v = static_cast<bool>(e->NewValue);
				auto g = ((CharacterGridView^)d);
				g->m_templateSettings->ShowColorGlyphs = v;
				g->UpdateColorFonts(v);
			}

			static void OnEnableResizeAnimationChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
			{
				auto v = static_cast<bool>(e->NewValue);
				auto g = ((CharacterGridView^)d);
				g->m_templateSettings->EnableReposition = v && GridViewHelper::UISettings->AnimationsEnabled;
				g->UpdateAnimation(v);
			}
			void OnContextRequested(Windows::UI::Xaml::UIElement^ sender, Windows::UI::Xaml::Input::ContextRequestedEventArgs^ args);
};

		void CharacterGridView::RegisterDependencyProperties()
		{
			if (_ItemBackgroundTransitionProperty == nullptr)
			{
				_ItemBackgroundTransitionProperty = DependencyProperty::Register(
					"ItemBackgroundTransition", BrushTransition::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr, nullptr));
			}

			if (_RepositionAnimationCollectionProperty == nullptr)
			{
				_RepositionAnimationCollectionProperty = DependencyProperty::Register(
					"RepositionAnimationCollection", ImplicitAnimationCollection::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr, nullptr));
			}

			if (_ItemSizeProperty == nullptr)
			{
				_ItemSizeProperty = DependencyProperty::Register(
					"ItemSize", double::typeid, CharacterGridView::typeid, ref new PropertyMetadata(0.0,
						ref new PropertyChangedCallback(&CharacterGridView::OnItemSizeChanged)));
			}

			if (_ShowColorGlyphsProperty == nullptr)
			{
				_ShowColorGlyphsProperty = DependencyProperty::Register(
					"ShowColorGlyphs", bool::typeid, CharacterGridView::typeid, ref new PropertyMetadata(false,
						ref new PropertyChangedCallback(&CharacterGridView::OnShowColorGlyphsChanged)));
			}

			if (_EnableResizeAnimationProperty == nullptr)
			{
				_EnableResizeAnimationProperty = DependencyProperty::Register(
					"EnableResizeAnimation", bool::typeid, CharacterGridView::typeid, ref new PropertyMetadata(false,
						ref new PropertyChangedCallback(&CharacterGridView::OnEnableResizeAnimationChanged)));
			}

			if (_ItemFontFamilyProperty == nullptr)
			{
				_ItemFontFamilyProperty = DependencyProperty::Register(
					"ItemFontFamily", Media::FontFamily::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr,
						ref new PropertyChangedCallback(&CharacterGridView::OnItemFontFamilyChanged)));
			}

			if (_ItemFontFaceProperty == nullptr)
			{
				_ItemFontFaceProperty = DependencyProperty::Register(
					"ItemFontFace", DWriteFontFace::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr,
						ref new PropertyChangedCallback(&CharacterGridView::OnItemFontFaceChanged)));
			}

			if (_ItemTypographyProperty == nullptr)
			{
				_ItemTypographyProperty = DependencyProperty::Register(
					"ItemTypography", ITypographyInfo::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr,
						ref new PropertyChangedCallback(&CharacterGridView::OnItemTypographyChanged)));
			}

			if (_ItemFontVariantProperty == nullptr)
			{
				_ItemFontVariantProperty = DependencyProperty::Register(
					"ItemFontVariant", IFontFace::typeid, CharacterGridView::typeid, ref new PropertyMetadata(nullptr));
			}

			if (_ItemAnnotationProperty == nullptr)
			{
				_ItemAnnotationProperty = DependencyProperty::Register(
					"ItemAnnotation", GlyphAnnotation::typeid, CharacterGridView::typeid, ref new PropertyMetadata(GlyphAnnotation::None,
						ref new PropertyChangedCallback(&CharacterGridView::OnItemAnnotationChanged)));
			}
		}
	}
}
