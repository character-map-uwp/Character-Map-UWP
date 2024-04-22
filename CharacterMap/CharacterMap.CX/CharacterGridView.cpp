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
    

    this->ContainerContentChanging +=
        ref new TypedEventHandler<ListViewBase^, ContainerContentChangingEventArgs^>(this, &CharacterGridView::OnContainerContentChanging);
}

void CharacterGridView::OnContainerContentChanging(ListViewBase^ sender, ContainerContentChangingEventArgs^ args)
{
    // 1. Handle reposition animation
    if (m_templateSettings->EnableReposition)
    {
        if (args->InRecycleQueue)
        {
            // 1.1. Poke Z-Index
            auto o = m_xamlDirect->GetXamlDirectObject(args->ItemContainer);
            auto i = m_xamlDirect->GetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex);
            m_xamlDirect->SetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex, i + 1);
            m_xamlDirect->SetInt32Property(o, XamlPropertyIndex::Canvas_ZIndex, i);
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

    // 2. Update item template
    ICharacter^ c = (ICharacter^)args->Item;
    GridViewHelper::UpdateContainer(m_xamlDirect, item, m_templateSettings, c);
    args->Handled = true;

    // 3. Ensure double tap
    if (item->Tag != nullptr)
    {
        // 3.1. Remove existing
        Windows::Foundation::EventRegistrationToken token = static_cast<Windows::Foundation::EventRegistrationToken>(item->Tag);
        item->DoubleTapped -= token;
    }

    //// 3.2. re-add
    item->Tag = item->DoubleTapped += m_doubleTapped;

    // 4. Ensure tooltip
    if (ItemFontVariant == nullptr)
        return;

    auto tt = ToolTipService::GetToolTip(item);
    ToolTip^ t = nullptr;
    if (tt == nullptr)
    {
        // 4.1. Create default tooltip
        t = ref new ToolTip();
        t->PlacementTarget = item;
        t->VerticalOffset = 4;
        t->Placement = Windows::UI::Xaml::Controls::Primitives::PlacementMode::Top;
        t->Loaded += m_tooltipLoadedHandler;
        ToolTipService::SetToolTip(item, t);
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

void CharacterGridView::OnChoosingItemContainer(ListViewBase^ sender, ChoosingItemContainerEventArgs^ args)
{

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
