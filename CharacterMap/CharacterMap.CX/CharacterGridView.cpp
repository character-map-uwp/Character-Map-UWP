//
// CharacterGridView.cpp
// Implementation of the CharacterGridView class.
//

#pragma once
#include "pch.h"
#include "DWriteFallbackFont.h"
#include "NativeInterop.h"
#include "CharacterGridView.h"

using namespace CharacterMapCX;
using namespace CharacterMapCX::Controls;
using namespace Platform;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Data;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Interop;
using namespace Windows::UI::Xaml::Media;
using namespace Microsoft::WRL;
using namespace Windows::ApplicationModel;

DependencyProperty^ CharacterGridView::_ItemBackgroundTransitionProperty = nullptr;
DependencyProperty^ CharacterGridView::_RepositionAnimationCollectionProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemSizeProperty = nullptr;
DependencyProperty^ CharacterGridView::_ShowColorGlyphsProperty = nullptr;
DependencyProperty^ CharacterGridView::_EnableResizeAnimationProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemFontFamilyProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemFontFaceProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemTypographyProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemFontVariantProperty = nullptr;
DependencyProperty^ CharacterGridView::_ItemAnnotationProperty = nullptr;

CharacterGridView::CharacterGridView()
{
    m_xamlDirect = XamlDirect::GetDefault();
    m_templateSettings = ref new CharacterGridViewTemplateSettings();

    m_tooltipLoadedHandler = ref new RoutedEventHandler(this, &CharacterGridView::ToolTipLoaded);
    m_doubleTapped = ref new DoubleTappedEventHandler(this, &CharacterGridView::OnItemDoubleTapped);
    m_contextRequested = ref new TypedEventHandler<UIElement^, ContextRequestedEventArgs^>(this, &CharacterGridView::OnContextRequested);

    this->ChoosingItemContainer += ref new TypedEventHandler<ListViewBase^, ChoosingItemContainerEventArgs^>(this, &CharacterGridView::OnChoosingItemContainer);
    this->ContainerContentChanging += ref new TypedEventHandler<ListViewBase^, ContainerContentChangingEventArgs^>(this, &CharacterGridView::OnContainerContentChanging);
}

void CharacterGridView::OnChoosingItemContainer(ListViewBase^ sender, ChoosingItemContainerEventArgs^ args)
{
    if (m_templateSettings->EnableReposition && args->ItemContainer != nullptr)
        PokeZIndex(args->ItemContainer);
}

void CharacterGridView::OnContainerContentChanging(ListViewBase^ sender, ContainerContentChangingEventArgs^ args)
{
    // 1. Handle reposition animation
    if (m_templateSettings->EnableReposition)
    {
        if (args->InRecycleQueue)
        {
            // 1.1. Poke Z-Index
            PokeZIndex(args->ItemContainer);
        }
        else
        {
            auto visual = ElementCompositionPreview::GetElementVisual(args->ItemContainer);
            visual->ImplicitAnimations = RepositionAnimationCollection;
        }
    }

    /*
     * For performance reasons, we've forgone XAML bindings and
     * will update everything in code
     */
    if (args->InRecycleQueue || args->ItemContainer == nullptr)
        return;

    GridViewItem^ item = (GridViewItem^)args->ItemContainer;
    m_templateSettings->BackgroundTransition = ItemBackgroundTransition;

    // 2. Update item template
    ICharacter^ c = (ICharacter^)args->Item;
    GridViewHelper::UpdateContainer(m_xamlDirect, item, m_templateSettings, c);
    args->Handled = true;

    // 3. Ensure double tap
    if (!DisableItemClicks && item->Tag == nullptr)
    {
        //// 3.1. Remove existing
        //Windows::Foundation::EventRegistrationToken token = static_cast<Windows::Foundation::EventRegistrationToken>(item->Tag);
        //item->DoubleTapped -= token;
        auto token = item->DoubleTapped += m_doubleTapped;
        item->Tag = token;
        item->ContextRequested += m_contextRequested;
    }

    ////// 3.2. re-add
    //item->Tag = item->DoubleTapped += m_doubleTapped;

    // 4. Ensure tooltip
    if (ItemFontVariant == nullptr)
        return;

    auto tt = ToolTipService::GetToolTip(item);
    ToolTip^ t = nullptr;
    if (tt == nullptr)
    {
        // 4.1. Create default tooltip
        auto ot = m_xamlDirect->CreateInstance(XamlTypeIndex::ToolTip);
        m_xamlDirect->SetObjectProperty(ot, XamlPropertyIndex::ToolTip_PlacementTarget, item);
        m_xamlDirect->SetDoubleProperty(ot, XamlPropertyIndex::ToolTip_VerticalOffset, 4);
        m_xamlDirect->SetEnumProperty(ot, XamlPropertyIndex::ToolTip_Placement, (unsigned int)Windows::UI::Xaml::Controls::Primitives::PlacementMode::Top);
        m_xamlDirect->SetXamlDirectObjectProperty(m_xamlDirect->GetXamlDirectObject(item), XamlPropertyIndex::ToolTipService_ToolTip, ot);
       
        t = (ToolTip^)m_xamlDirect->GetObject(ot);
        t->Loaded += m_tooltipLoadedHandler;
    }
    else
        t = (ToolTip^)tt;

    // 4.2. Set correct tooltip data
    ItemTooltipData^ d = ref new ItemTooltipData();
    d->Char = c;
    d->Container = item;
    d->Variant = ItemFontVariant;
    t->Tag = d;
}

void CharacterMapCX::Controls::CharacterGridView::PokeZIndex(UIElement^ item)
{
    auto o = m_xamlDirect->GetXamlDirectObject(item);
    auto i = m_xamlDirect->GetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex);
    m_xamlDirect->SetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex, i + 1);
    m_xamlDirect->SetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex, i);
}

void CharacterGridView::ToolTipLoaded(Platform::Object^ sender, RoutedEventArgs^ e)
{
    ToolTip^ t = (ToolTip^)sender;
    ItemTooltipData^ data = (ItemTooltipData^)t->Tag;

    t->PlacementRect = RectHelper::FromCoordinatesAndDimensions(0, 0, data->Container->ActualWidth, data->Container->ActualHeight);

    // Do not use object initializer here, results in random NullReferenceExceptions.
    TextBlock^ tb = ref new TextBlock();
    tb->TextWrapping = TextWrapping::Wrap;
    String^ txt = nullptr;
    if (data != nullptr)
        txt = data->Variant->GetDescription(data->Char, true);
    else
        txt = data->Char->UnicodeString;

    tb->Text = txt;
    t->Content = tb;
}

void CharacterGridView::OnItemDoubleTapped(Platform::Object^ sender, DoubleTappedRoutedEventArgs^ e)
{
    ItemDoubleTapped(sender, (ICharacter^)((GridViewItem^)sender)->DataContext);
}




void CharacterGridView::UpdateColorFonts(bool value)
{
    if (ItemsSource == nullptr || ItemsPanelRoot == nullptr)
        return;

    for (auto iter = ItemsPanelRoot->Children->First(); iter->HasCurrent; iter->MoveNext())
    {
        auto item = dynamic_cast<GridViewItem^>(iter->Current);
        if (item == nullptr)
            continue;

        IXamlDirectObject^ root = m_xamlDirect->GetXamlDirectObject(item->ContentTemplateRoot);
        auto childs = m_xamlDirect->GetXamlDirectObjectProperty(root, XamlPropertyIndex::Panel_Children);
        IXamlDirectObject^ tb = m_xamlDirect->GetXamlDirectObjectFromCollectionAt(childs, 0);
        GridViewHelper::UpdateColorFont(m_xamlDirect, nullptr, tb, value);
    }
}

void CharacterGridView::UpdateTypographies(ITypographyInfo^ info)
{
    if (ItemsSource == nullptr || ItemsPanelRoot == nullptr)
        return;

    for (auto iter = ItemsPanelRoot->Children->First(); iter->HasCurrent; iter->MoveNext())
    {
        auto item = dynamic_cast<GridViewItem^>(iter->Current);
        if (item == nullptr)
            continue;

        IXamlDirectObject^ root = m_xamlDirect->GetXamlDirectObject(item->ContentTemplateRoot);
        auto childs = m_xamlDirect->GetXamlDirectObjectProperty(root, XamlPropertyIndex::Panel_Children);
        IXamlDirectObject^ tb = m_xamlDirect->GetXamlDirectObjectFromCollectionAt(childs, 0);
        GridViewHelper::UpdateTypography(m_xamlDirect, tb, info);
    }
}

void CharacterGridView::UpdateUnicode(GlyphAnnotation value)
{
    if (ItemsSource == nullptr || ItemsPanelRoot == nullptr)
        return;

    for (auto iter = ItemsPanelRoot->Children->First(); iter->HasCurrent; iter->MoveNext())
    {
        auto item = dynamic_cast<GridViewItem^>(iter->Current);
        if (item == nullptr)
            continue;

        IXamlDirectObject^ root = m_xamlDirect->GetXamlDirectObject(item->ContentTemplateRoot);
        auto childs = m_xamlDirect->GetXamlDirectObjectProperty(root, XamlPropertyIndex::Panel_Children);
        IXamlDirectObject^ tb = m_xamlDirect->GetXamlDirectObjectFromCollectionAt(childs, 1);
        ICharacter^ c = (ICharacter^)m_xamlDirect->GetObjectProperty(root, XamlPropertyIndex::FrameworkElement_Tag);
        m_xamlDirect->SetStringProperty(tb, XamlPropertyIndex::TextBlock_Text, GridViewHelper::GetAnnotation(c, value));
        m_xamlDirect->SetEnumProperty(tb, XamlPropertyIndex::UIElement_Visibility, (unsigned int)(value != GlyphAnnotation::None ? 0 : 1));
    }
}


void CharacterGridView::OnContextRequested(UIElement^ sender, ContextRequestedEventArgs^ args)
{
    ItemContextRequested(sender, args);
}