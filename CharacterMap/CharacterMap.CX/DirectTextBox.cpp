#include "pch.h"
#include "DirectTextBox.h"
#include "CompositionDeviceManager.h"
#include "TypographyAnalyzer.h"
#include "DirectWrite.h"
#include "DWHelpers.h"
#include "Utils.h"

#include <cwctype>
#include <algorithm>
#include <cmath>
#include <ppltasks.h>

using namespace CharacterMapCX;
using namespace CharacterMapCX::Controls;
using namespace concurrency;
using namespace Microsoft::WRL;
using namespace Windows::ApplicationModel::DataTransfer;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Windows::Foundation::Numerics;
using namespace Windows::System;
using namespace Windows::UI;
using namespace Windows::UI::Core;
using namespace Windows::UI::Composition;
using namespace Windows::UI::Text::Core;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Hosting;
using namespace Windows::UI::Xaml::Input;
using namespace Windows::UI::Xaml::Media;

DependencyProperty^ DirectTextBox::_FontFaceProperty = nullptr;
DependencyProperty^ DirectTextBox::_TypographyProperty = nullptr;
DependencyProperty^ DirectTextBox::_StyleSimulationsProperty = nullptr;
DependencyProperty^ DirectTextBox::_IsColorFontEnabledProperty = nullptr;
DependencyProperty^ DirectTextBox::_TextProperty = nullptr;
DependencyProperty^ DirectTextBox::_PlaceholderTextProperty = nullptr;
DependencyProperty^ DirectTextBox::_PlaceholderForegroundProperty = nullptr;
DependencyProperty^ DirectTextBox::_SelectionStartProperty = nullptr;
DependencyProperty^ DirectTextBox::_SelectionLengthProperty = nullptr;
DependencyProperty^ DirectTextBox::_IsReadOnlyProperty = nullptr;
DependencyProperty^ DirectTextBox::_MaxLengthProperty = nullptr;
DependencyProperty^ DirectTextBox::_TextAlignmentProperty = nullptr;
DependencyProperty^ DirectTextBox::_TextWrappingProperty = nullptr;
DependencyProperty^ DirectTextBox::_SelectionHighlightColorProperty = nullptr;
DependencyProperty^ DirectTextBox::_CursorBrushProperty = nullptr;
DependencyProperty^ DirectTextBox::_AcceptsReturnProperty = nullptr;
DependencyProperty^ DirectTextBox::_IsDefaultContextMenuEnabledProperty = nullptr;

void DirectTextBox::EnsureDependencyProperties()
{
    if (_FontFaceProperty != nullptr)
        return;

    auto layoutCallback = ref new PropertyChangedCallback(&DirectTextBox::OnLayoutAffectingPropertyChanged);
    auto renderCallback = ref new PropertyChangedCallback(&DirectTextBox::OnRenderAffectingPropertyChanged);
    auto textCallback = ref new PropertyChangedCallback(&DirectTextBox::OnTextChangedCallback);
    auto selCallback = ref new PropertyChangedCallback(&DirectTextBox::OnSelectionChangedCallback);

    _FontFaceProperty = DependencyProperty::Register("FontFace", DWriteFontFace::typeid, DirectTextBox::typeid, ref new PropertyMetadata(nullptr, layoutCallback));
    _TypographyProperty = DependencyProperty::Register("Typography", DWriteTypographyCollection::typeid, DirectTextBox::typeid, ref new PropertyMetadata(nullptr, layoutCallback));
    _StyleSimulationsProperty = DependencyProperty::Register("StyleSimulations", Windows::UI::Xaml::Media::StyleSimulations::typeid, DirectTextBox::typeid, ref new PropertyMetadata(Windows::UI::Xaml::Media::StyleSimulations::None, layoutCallback));
    _IsColorFontEnabledProperty = DependencyProperty::Register("IsColorFontEnabled", bool::typeid, DirectTextBox::typeid, ref new PropertyMetadata(true, renderCallback));

    _TextProperty = DependencyProperty::Register("Text", Platform::String::typeid, DirectTextBox::typeid, ref new PropertyMetadata(L"", textCallback));
    _PlaceholderTextProperty = DependencyProperty::Register("PlaceholderText", Platform::String::typeid, DirectTextBox::typeid, ref new PropertyMetadata(L"", renderCallback));
    _PlaceholderForegroundProperty = DependencyProperty::Register("PlaceholderForeground", Windows::UI::Xaml::Media::Brush::typeid, DirectTextBox::typeid, ref new PropertyMetadata(nullptr, renderCallback));

    _SelectionStartProperty = DependencyProperty::Register("SelectionStart", int::typeid, DirectTextBox::typeid, ref new PropertyMetadata((Platform::Object^)(int)0, selCallback));
    _SelectionLengthProperty = DependencyProperty::Register("SelectionLength", int::typeid, DirectTextBox::typeid, ref new PropertyMetadata((Platform::Object^)(int)0, selCallback));

    _IsReadOnlyProperty = DependencyProperty::Register("IsReadOnly", bool::typeid, DirectTextBox::typeid, ref new PropertyMetadata(false));
    _MaxLengthProperty = DependencyProperty::Register("MaxLength", int::typeid, DirectTextBox::typeid, ref new PropertyMetadata((Platform::Object^)(int)0));
    _TextAlignmentProperty = DependencyProperty::Register("TextAlignment", Windows::UI::Xaml::TextAlignment::typeid, DirectTextBox::typeid, ref new PropertyMetadata(Windows::UI::Xaml::TextAlignment::Left, layoutCallback));
    _TextWrappingProperty = DependencyProperty::Register("TextWrapping", Windows::UI::Xaml::TextWrapping::typeid, DirectTextBox::typeid, ref new PropertyMetadata(Windows::UI::Xaml::TextWrapping::NoWrap, layoutCallback));

    _SelectionHighlightColorProperty = DependencyProperty::Register("SelectionHighlightColor", Windows::UI::Xaml::Media::SolidColorBrush::typeid, DirectTextBox::typeid, ref new PropertyMetadata(nullptr, renderCallback));
    _CursorBrushProperty = DependencyProperty::Register("CursorBrush", Windows::UI::Xaml::Media::Brush::typeid, DirectTextBox::typeid, ref new PropertyMetadata(nullptr, renderCallback));
    _AcceptsReturnProperty = DependencyProperty::Register("AcceptsReturn", bool::typeid, DirectTextBox::typeid, ref new PropertyMetadata(false));
    _IsDefaultContextMenuEnabledProperty = DependencyProperty::Register("IsDefaultContextMenuEnabled", bool::typeid, DirectTextBox::typeid, ref new PropertyMetadata(true));
}

void DirectTextBox::RegisterDependencyProperties()
{
    EnsureDependencyProperties();
}

DirectTextBox::DirectTextBox()
    : m_renderedWidth(0)
    , m_renderedHeight(0)
    , m_rootElement(nullptr)
    , m_hitContainer(nullptr)
    , m_isLayoutDirty(true)
    , m_isRenderDirty(true)
    , m_isFocused(false)
    , m_isPointerOver(false)
    , m_isCaretVisible(false)
    , m_isPointerPressed(false)
    , m_selectionAnchor(0)
    , m_scrollOffsetX(0.0f)
    , m_scrollOffsetY(0.0f)
    , m_internalTextUpdating(false)
    , m_isComposing(false)
    , m_compositionStart(0)
    , m_compositionLength(0)
    , m_clickCount(0)
    , m_contextMenu(nullptr)
    , m_cutCommand(nullptr)
    , m_copyCommand(nullptr)
    , m_pasteCommand(nullptr)
    , m_undoCommand(nullptr)
    , m_redoCommand(nullptr)
    , m_selectAllCommand(nullptr)
    , m_cutItem(nullptr)
    , m_copyItem(nullptr)
    , m_pasteItem(nullptr)
    , m_undoItem(nullptr)
    , m_redoItem(nullptr)
    , m_selectAllItem(nullptr)
{
    m_loadedToken.Value = 0;
    m_unloadedToken.Value = 0;
    m_themeToken.Value = 0;
    m_contextRequestedToken.Value = 0;
    m_containerContextRequestedToken.Value = 0;
    m_containerRightTappedToken.Value = 0;
    m_lastPointerDownTime.UniversalTime = 0;
    m_lastPointerDownPos = Point(0, 0);

    EnsureDependencyProperties();

    DefaultStyleKey = "CharacterMapCX.Controls.DirectTextBox";

    this->IsTabStop = true;
    this->UseSystemFocusVisuals = false;

    // Explicit background ensures XAML hit-testing ALWAYS succeeds on this control
    this->Background = ref new SolidColorBrush(Windows::UI::Colors::Transparent);

    // Default styling
    this->Padding = Thickness(8, 4, 8, 4);
    this->MinWidth = 60.0;
    this->MinHeight = 32.0;

    m_loadedToken = this->Loaded += ref new RoutedEventHandler(this, &DirectTextBox::OnLoaded);
    m_unloadedToken = this->Unloaded += ref new RoutedEventHandler(this, &DirectTextBox::OnUnloaded);
    m_contextRequestedToken = this->ContextRequested += ref new TypedEventHandler<UIElement^, ContextRequestedEventArgs^>(this, &DirectTextBox::OnContextRequested);

    Platform::WeakReference weakThis(this);
    m_themeToken = this->ActualThemeChanged += ref new TypedEventHandler<FrameworkElement^, Platform::Object^>(
        [weakThis](FrameworkElement^, Platform::Object^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
                strongThis->UpdateTheme();
        });

    // Caret blink timer (500ms)
    m_caretTimer = ref new DispatcherTimer();
    TimeSpan interval;
    interval.Duration = 500 * 10000; // 500 ms in 100ns ticks
    m_caretTimer->Interval = interval;
    m_caretTimer->Tick += ref new EventHandler<Platform::Object^>(this, &DirectTextBox::OnCaretTimerTick);

    // Watch for inherited/standard property changes
    auto standardCallback = ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
                strongThis->InvalidateLayoutAndRender();
        });

    this->RegisterPropertyChangedCallback(FontSizeProperty, standardCallback);
    this->RegisterPropertyChangedCallback(ForegroundProperty, ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
                strongThis->InvalidateRenderOnly();
        }));
    this->RegisterPropertyChangedCallback(PaddingProperty, standardCallback);
    this->RegisterPropertyChangedCallback(FlowDirectionProperty, standardCallback);
    this->RegisterPropertyChangedCallback(BackgroundProperty, ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                if (strongThis->m_rootElement != nullptr && strongThis->Background != nullptr)
                    strongThis->m_rootElement->Background = strongThis->Background;
                strongThis->InvalidateRenderOnly();
            }
        }));
    this->RegisterPropertyChangedCallback(BorderBrushProperty, ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                if (strongThis->m_rootElement != nullptr)
                    strongThis->m_rootElement->BorderBrush = strongThis->BorderBrush;
            }
        }));
    this->RegisterPropertyChangedCallback(BorderThicknessProperty, ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                if (strongThis->m_rootElement != nullptr)
                    strongThis->m_rootElement->BorderThickness = strongThis->BorderThickness;
                strongThis->InvalidateLayoutAndRender();
            }
        }));
    this->RegisterPropertyChangedCallback(CornerRadiusProperty, ref new DependencyPropertyChangedCallback(
        [weakThis](DependencyObject^, DependencyProperty^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                if (strongThis->m_rootElement != nullptr)
                    strongThis->m_rootElement->CornerRadius = strongThis->CornerRadius;
            }
        }));
}

DirectTextBox::~DirectTextBox()
{
    try
    {
        StopCaretBlink();
        UninitializeEditContext();

        if (m_loadedToken.Value != 0)
        {
            this->Loaded -= m_loadedToken;
            m_loadedToken.Value = 0;
        }
        if (m_unloadedToken.Value != 0)
        {
            this->Unloaded -= m_unloadedToken;
            m_unloadedToken.Value = 0;
        }
        if (m_themeToken.Value != 0)
        {
            this->ActualThemeChanged -= m_themeToken;
            m_themeToken.Value = 0;
        }
        if (m_contextRequestedToken.Value != 0)
        {
            this->ContextRequested -= m_contextRequestedToken;
            m_contextRequestedToken.Value = 0;
        }
        if (m_containerContextRequestedToken.Value != 0 && m_hitContainer != nullptr)
        {
            m_hitContainer->ContextRequested -= m_containerContextRequestedToken;
            m_containerContextRequestedToken.Value = 0;
        }
        if (m_containerRightTappedToken.Value != 0 && m_hitContainer != nullptr)
        {
            m_hitContainer->RightTapped -= m_containerRightTappedToken;
            m_containerRightTappedToken.Value = 0;
        }

        ReleaseDrawingSurface();

        if (m_spriteVisual != nullptr)
        {
            ElementCompositionPreview::SetElementChildVisual(this, nullptr);
            m_spriteVisual->Brush = nullptr;
            m_spriteVisual = nullptr;
        }
    }
    catch (...) {}
}

#pragma region Property Callbacks

void DirectTextBox::OnLayoutAffectingPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^)
{
    auto box = dynamic_cast<DirectTextBox^>(d);
    if (box != nullptr)
        box->InvalidateLayoutAndRender();
}

void DirectTextBox::OnRenderAffectingPropertyChanged(DependencyObject^ d, DependencyPropertyChangedEventArgs^)
{
    auto box = dynamic_cast<DirectTextBox^>(d);
    if (box != nullptr)
        box->InvalidateRenderOnly();
}

void DirectTextBox::OnTextChangedCallback(DependencyObject^ d, DependencyPropertyChangedEventArgs^ e)
{
    auto box = dynamic_cast<DirectTextBox^>(d);
    if (box == nullptr)
        return;

    if (!box->m_internalTextUpdating)
    {
        box->PushUndoState();
        int len = box->Text != nullptr ? box->Text->Length() : 0;
        if (box->SelectionStart > len)
            box->SelectionStart = len;
        if (box->SelectionStart + box->SelectionLength > len)
            box->SelectionLength = (std::max)(0, len - box->SelectionStart);

        if (box->m_editContext != nullptr)
        {
            CoreTextRange range;
            range.StartCaretPosition = 0;
            range.EndCaretPosition = len;
            CoreTextRange selRange;
            selRange.StartCaretPosition = box->SelectionStart;
            selRange.EndCaretPosition = box->SelectionStart + box->SelectionLength;
            box->m_editContext->NotifyTextChanged(range, len, selRange);
        }
    }

    box->InvalidateLayoutAndRender();
    box->TextChanged(box, ref new RoutedEventArgs());
}

void DirectTextBox::OnSelectionChangedCallback(DependencyObject^ d, DependencyPropertyChangedEventArgs^)
{
    auto box = dynamic_cast<DirectTextBox^>(d);
    if (box == nullptr)
        return;

    int len = box->Text != nullptr ? box->Text->Length() : 0;
    int start = (std::max)(0, (std::min)(box->SelectionStart, len));
    int length = (std::max)(0, (std::min)(box->SelectionLength, len - start));

    start = box->SnapToClusterBoundary(start, true);
    int end = box->SnapToClusterBoundary(start + length, true);
    length = (std::max)(0, end - start);

    if (start != box->SelectionStart || length != box->SelectionLength)
    {
        box->SelectionStart = start;
        box->SelectionLength = length;
        return;
    }

    box->EnsureCaretVisible();
    box->ResetCaretBlink();
    box->InvalidateRenderOnly();
    box->SelectionChanged(box, ref new RoutedEventArgs());
}

#pragma endregion

#pragma region SelectedText

Platform::String^ DirectTextBox::SelectedText::get()
{
    if (Text == nullptr || SelectionLength <= 0)
        return L"";

    int len = Text->Length();
    int start = (std::max)(0, (std::min)(SelectionStart, len));
    int count = (std::max)(0, (std::min)(SelectionLength, len - start));
    if (count <= 0)
        return L"";

    std::wstring str(Text->Data() + start, count);
    return ref new Platform::String(str.c_str());
}

void DirectTextBox::SelectedText::set(Platform::String^ value)
{
    if (IsReadOnly)
        return;

    InsertText(value != nullptr ? value : L"");
}

#pragma endregion

#pragma region Lifecycle & Theme

void DirectTextBox::OnLoaded(Platform::Object^, RoutedEventArgs^)
{
    if (m_hitContainer == nullptr)
    {
        Platform::String^ templateXaml =
            L"<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">"
            L"<Border x:Name=\"RootElement\" Background=\"Transparent\">"
            L"<Grid x:Name=\"HitContainer\" Background=\"Transparent\" />"
            L"</Border>"
            L"</ControlTemplate>";
        try
        {
            this->Template = dynamic_cast<ControlTemplate^>(Windows::UI::Xaml::Markup::XamlReader::Load(templateXaml));
            this->ApplyTemplate();
        }
        catch (...) {}
    }

    InitializeEditContext();
    InvalidateLayoutAndRender();
}

void DirectTextBox::OnUnloaded(Platform::Object^, RoutedEventArgs^)
{
    StopCaretBlink();
    UninitializeEditContext();
    ReleaseDrawingSurface();
}

void DirectTextBox::UpdateTheme()
{
    InvalidateRenderOnly();
}

void DirectTextBox::InvalidateLayoutAndRender()
{
    m_isLayoutDirty = true;
    m_isRenderDirty = true;
    this->InvalidateMeasure();
    this->InvalidateArrange();
    RenderToSurface();
}

void DirectTextBox::InvalidateRenderOnly()
{
    m_isRenderDirty = true;
    RenderToSurface();
}

#pragma endregion

#pragma region Measure & Arrange

void DirectTextBox::OnApplyTemplate()
{
    Control::OnApplyTemplate();

    m_rootElement = dynamic_cast<Border^>(GetTemplateChild("RootElement"));
    m_hitContainer = dynamic_cast<Grid^>(GetTemplateChild("HitContainer"));

    if (m_hitContainer != nullptr)
    {
        m_hitContainer->Background = ref new SolidColorBrush(Windows::UI::Colors::Transparent);
        m_hitContainer->IsHitTestVisible = true;
        WireContainerEvents();

        if (m_spriteVisual != nullptr)
        {
            ElementCompositionPreview::SetElementChildVisual(m_hitContainer, m_spriteVisual);
        }
    }

    if (m_rootElement != nullptr)
    {
        m_rootElement->BorderBrush = this->BorderBrush;
        m_rootElement->BorderThickness = this->BorderThickness;
        m_rootElement->CornerRadius = this->CornerRadius;
        if (this->Background != nullptr)
            m_rootElement->Background = this->Background;
    }

    UpdateVisualStates(false);
    InvalidateLayoutAndRender();
}

void DirectTextBox::UpdateVisualStates(bool useTransitions)
{
    VisualStateManager::GoToState(this, !IsEnabled ? "Disabled" : (m_isFocused ? "Focused" : (m_isPointerOver ? "PointerOver" : "Normal")), useTransitions);

    if (m_rootElement != nullptr)
    {
        try
        {
            auto res = Application::Current->Resources;
            if (!IsEnabled)
            {
                m_rootElement->Opacity = 0.6;
                if (res->HasKey("TextControlBorderBrushDisabled"))
                    m_rootElement->BorderBrush = dynamic_cast<Brush^>(res->Lookup("TextControlBorderBrushDisabled"));
            }
            else
            {
                m_rootElement->Opacity = 1.0;
                if (m_isFocused)
                {
                    if (res->HasKey("TextControlBorderBrushFocused"))
                        m_rootElement->BorderBrush = dynamic_cast<Brush^>(res->Lookup("TextControlBorderBrushFocused"));
                }
                else if (m_isPointerOver)
                {
                    if (res->HasKey("TextControlBorderBrushPointerOver"))
                        m_rootElement->BorderBrush = dynamic_cast<Brush^>(res->Lookup("TextControlBorderBrushPointerOver"));
                }
                else
                {
                    m_rootElement->BorderBrush = this->BorderBrush;
                }
            }
        }
        catch (...)
        {
            m_rootElement->BorderBrush = this->BorderBrush;
        }
    }
}

Size DirectTextBox::MeasureOverride(Size availableSize)
{
    Control::MeasureOverride(availableSize);

    CreateTextLayout();

    double width = 0.0;
    double height = 0.0;

    auto padding = this->Padding;
    auto border = this->BorderThickness;

    if (m_textLayout != nullptr)
    {
        DWRITE_TEXT_METRICS metrics{};
        if (SUCCEEDED(m_textLayout->GetMetrics(&metrics)))
        {
            width = metrics.widthIncludingTrailingWhitespace;
            height = metrics.height;
        }
    }
    else if (FontFace != nullptr)
    {
        auto m = FontFace->GetMetrics();
        double em = (std::max)(1.0, (double)m.designUnitsPerEm);
        double sz = FontSize > 0.0 ? FontSize : 15.0;
        height = (m.ascent + m.descent + m.lineGap) * (sz / em);
    }
    else
    {
        height = FontSize > 0.0 ? FontSize * 1.2 : 20.0;
    }

    width += padding.Left + padding.Right + border.Left + border.Right + 4.0;
    height += padding.Top + padding.Bottom + border.Top + border.Bottom;

    width = (std::max)(width, MinWidth);
    height = (std::max)(height, MinHeight);

    if (availableSize.Width < width && !std::isinf(availableSize.Width))
        width = availableSize.Width;

    return Size(static_cast<float>(width), static_cast<float>(height));
}

Size DirectTextBox::ArrangeOverride(Size finalSize)
{
    Control::ArrangeOverride(finalSize);

    m_isLayoutDirty = true;
    m_isRenderDirty = true;
    RenderToSurface();
    return finalSize;
}

#pragma endregion

#pragma region Caret Blink

void DirectTextBox::StartCaretBlink()
{
    m_isCaretVisible = true;
    if (m_caretTimer != nullptr && !m_caretTimer->IsEnabled)
        m_caretTimer->Start();
}

void DirectTextBox::StopCaretBlink()
{
    m_isCaretVisible = false;
    if (m_caretTimer != nullptr && m_caretTimer->IsEnabled)
        m_caretTimer->Stop();
}

void DirectTextBox::ResetCaretBlink()
{
    m_isCaretVisible = true;
    if (m_caretTimer != nullptr)
    {
        m_caretTimer->Stop();
        if (m_isFocused)
            m_caretTimer->Start();
    }
}

void DirectTextBox::OnCaretTimerTick(Platform::Object^, Platform::Object^)
{
    if (!m_isFocused || SelectionLength > 0)
    {
        if (m_isCaretVisible)
        {
            m_isCaretVisible = false;
            InvalidateRenderOnly();
        }
        return;
    }

    m_isCaretVisible = !m_isCaretVisible;
    InvalidateRenderOnly();
}

#pragma endregion

#pragma region CoreTextEditContext (IME & Touch Keyboard)

void DirectTextBox::InitializeEditContext()
{
    if (m_editContext != nullptr)
        return;

    auto manager = CoreTextServicesManager::GetForCurrentView();
    if (manager == nullptr)
        return;

    m_editContext = manager->CreateEditContext();
    if (m_editContext == nullptr)
        return;

    m_editContext->InputScope = CoreTextInputScope::Text;
    m_editContext->InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy::Automatic;

    m_textRequestedToken = m_editContext->TextRequested += ref new TypedEventHandler<CoreTextEditContext^, CoreTextTextRequestedEventArgs^>(this, &DirectTextBox::OnTextRequested);
    m_selectionRequestedToken = m_editContext->SelectionRequested += ref new TypedEventHandler<CoreTextEditContext^, CoreTextSelectionRequestedEventArgs^>(this, &DirectTextBox::OnSelectionRequested);
    m_textUpdatingToken = m_editContext->TextUpdating += ref new TypedEventHandler<CoreTextEditContext^, CoreTextTextUpdatingEventArgs^>(this, &DirectTextBox::OnTextUpdating);
    m_selectionUpdatingToken = m_editContext->SelectionUpdating += ref new TypedEventHandler<CoreTextEditContext^, CoreTextSelectionUpdatingEventArgs^>(this, &DirectTextBox::OnSelectionUpdating);
    m_formatUpdatingToken = m_editContext->FormatUpdating += ref new TypedEventHandler<CoreTextEditContext^, CoreTextFormatUpdatingEventArgs^>(this, &DirectTextBox::OnFormatUpdating);
    m_layoutRequestedToken = m_editContext->LayoutRequested += ref new TypedEventHandler<CoreTextEditContext^, CoreTextLayoutRequestedEventArgs^>(this, &DirectTextBox::OnLayoutRequested);
    m_compositionStartedToken = m_editContext->CompositionStarted += ref new TypedEventHandler<CoreTextEditContext^, CoreTextCompositionStartedEventArgs^>(this, &DirectTextBox::OnCompositionStarted);
    m_compositionCompletedToken = m_editContext->CompositionCompleted += ref new TypedEventHandler<CoreTextEditContext^, CoreTextCompositionCompletedEventArgs^>(this, &DirectTextBox::OnCompositionCompleted);
    m_focusRemovedToken = m_editContext->FocusRemoved += ref new TypedEventHandler<CoreTextEditContext^, Platform::Object^>(this, &DirectTextBox::OnFocusRemoved);
}

void DirectTextBox::UninitializeEditContext()
{
    if (m_editContext == nullptr)
        return;

    try
    {
        if (m_textRequestedToken.Value != 0) m_editContext->TextRequested -= m_textRequestedToken;
        if (m_selectionRequestedToken.Value != 0) m_editContext->SelectionRequested -= m_selectionRequestedToken;
        if (m_textUpdatingToken.Value != 0) m_editContext->TextUpdating -= m_textUpdatingToken;
        if (m_selectionUpdatingToken.Value != 0) m_editContext->SelectionUpdating -= m_selectionUpdatingToken;
        if (m_formatUpdatingToken.Value != 0) m_editContext->FormatUpdating -= m_formatUpdatingToken;
        if (m_layoutRequestedToken.Value != 0) m_editContext->LayoutRequested -= m_layoutRequestedToken;
        if (m_compositionStartedToken.Value != 0) m_editContext->CompositionStarted -= m_compositionStartedToken;
        if (m_compositionCompletedToken.Value != 0) m_editContext->CompositionCompleted -= m_compositionCompletedToken;
        if (m_focusRemovedToken.Value != 0) m_editContext->FocusRemoved -= m_focusRemovedToken;
    }
    catch (...) {}

    m_textRequestedToken.Value = 0;
    m_selectionRequestedToken.Value = 0;
    m_textUpdatingToken.Value = 0;
    m_selectionUpdatingToken.Value = 0;
    m_formatUpdatingToken.Value = 0;
    m_layoutRequestedToken.Value = 0;
    m_compositionStartedToken.Value = 0;
    m_compositionCompletedToken.Value = 0;
    m_focusRemovedToken.Value = 0;

    m_editContext = nullptr;
}

void DirectTextBox::OnTextRequested(CoreTextEditContext^, CoreTextTextRequestedEventArgs^ args)
{
    auto request = args->Request;
    if (request == nullptr)
        return;

    Platform::String^ currentText = Text != nullptr ? Text : L"";
    int len = currentText->Length();

    int start = (std::max)(0, (std::min)(request->Range.StartCaretPosition, len));
    int end = (std::max)(start, (std::min)(request->Range.EndCaretPosition, len));

    std::wstring sub(currentText->Data() + start, end - start);
    request->Text = ref new Platform::String(sub.c_str());
}

void DirectTextBox::OnSelectionRequested(CoreTextEditContext^, CoreTextSelectionRequestedEventArgs^ args)
{
    auto request = args->Request;
    if (request == nullptr)
        return;

    CoreTextRange sel;
    sel.StartCaretPosition = SelectionStart;
    sel.EndCaretPosition = SelectionStart + SelectionLength;
    request->Selection = sel;
}

void DirectTextBox::OnTextUpdating(CoreTextEditContext^, CoreTextTextUpdatingEventArgs^ args)
{
    if (IsReadOnly)
    {
        args->Result = CoreTextTextUpdatingResult::Failed;
        return;
    }

    PushUndoState();

    Platform::String^ currentText = Text != nullptr ? Text : L"";
    int len = currentText->Length();

    int start = (std::max)(0, (std::min)(args->Range.StartCaretPosition, len));
    int end = (std::max)(start, (std::min)(args->Range.EndCaretPosition, len));

    std::wstring textStr(currentText->Data());
    Platform::String^ newChunk = args->Text != nullptr ? args->Text : L"";
    textStr.replace(start, end - start, newChunk->Data());

    if (MaxLength > 0 && (int)textStr.length() > MaxLength)
        textStr = textStr.substr(0, MaxLength);

    m_internalTextUpdating = true;
    this->Text = ref new Platform::String(textStr.c_str());
    m_internalTextUpdating = false;

    int newSelStart = (std::max)(0, (std::min)(args->NewSelection.StartCaretPosition, (int)textStr.length()));
    int newSelEnd = (std::max)(newSelStart, (std::min)(args->NewSelection.EndCaretPosition, (int)textStr.length()));
    SetSelectionInternal(newSelStart, newSelEnd - newSelStart, false);

    args->Result = CoreTextTextUpdatingResult::Succeeded;
    EnsureCaretVisible();
    InvalidateLayoutAndRender();
}

void DirectTextBox::OnSelectionUpdating(CoreTextEditContext^, CoreTextSelectionUpdatingEventArgs^ args)
{
    int len = Text != nullptr ? Text->Length() : 0;
    int start = (std::max)(0, (std::min)(args->Selection.StartCaretPosition, len));
    int end = (std::max)(start, (std::min)(args->Selection.EndCaretPosition, len));

    SetSelectionInternal(start, end - start, false);
    args->Result = CoreTextSelectionUpdatingResult::Succeeded;
}

void DirectTextBox::OnFormatUpdating(CoreTextEditContext^, CoreTextFormatUpdatingEventArgs^ args)
{
    args->Result = CoreTextFormatUpdatingResult::Succeeded;
}

void DirectTextBox::OnLayoutRequested(CoreTextEditContext^, CoreTextLayoutRequestedEventArgs^ args)
{
    auto request = args->Request;
    if (request == nullptr)
        return;

    try
    {
        auto transform = TransformToVisual(nullptr);
        Point origin = transform->TransformPoint(Point(0, 0));

        Rect controlBounds(origin.X, origin.Y, static_cast<float>(ActualWidth), static_cast<float>(ActualHeight));
        request->LayoutBounds->ControlBounds = controlBounds;

        Rect caretRect = GetCaretScreenRect();
        request->LayoutBounds->TextBounds = caretRect;
    }
    catch (...) {}
}

void DirectTextBox::OnCompositionStarted(CoreTextEditContext^, CoreTextCompositionStartedEventArgs^)
{
    m_isComposing = true;
    m_compositionStart = SelectionStart;
    m_compositionLength = 0;
}

void DirectTextBox::OnCompositionCompleted(CoreTextEditContext^, CoreTextCompositionCompletedEventArgs^)
{
    m_isComposing = false;
    m_compositionStart = 0;
    m_compositionLength = 0;
    InvalidateRenderOnly();
}

void DirectTextBox::OnFocusRemoved(CoreTextEditContext^, Platform::Object^)
{
    // Focus removed by text server
}

#pragma endregion

#pragma region Focus & Keyboard

void DirectTextBox::OnGotFocus(RoutedEventArgs^ e)
{
    m_isFocused = true;
    UpdateVisualStates(true);
    if (m_editContext != nullptr)
        m_editContext->NotifyFocusEnter();

    StartCaretBlink();
    InvalidateRenderOnly();
    Control::OnGotFocus(e);
}

void DirectTextBox::OnLostFocus(RoutedEventArgs^ e)
{
    m_isFocused = false;
    UpdateVisualStates(true);
    if (m_editContext != nullptr)
        m_editContext->NotifyFocusLeave();

    StopCaretBlink();
    InvalidateRenderOnly();
    Control::OnLostFocus(e);
}

void DirectTextBox::OnKeyDown(KeyRoutedEventArgs^ e)
{
    if (e->Handled || m_isComposing)
    {
        Control::OnKeyDown(e);
        return;
    }

    auto coreWin = CoreWindow::GetForCurrentThread();
    bool ctrl = coreWin != nullptr && ((coreWin->GetKeyState(VirtualKey::Control) & CoreVirtualKeyStates::Down) == CoreVirtualKeyStates::Down);
    bool shift = coreWin != nullptr && ((coreWin->GetKeyState(VirtualKey::Shift) & CoreVirtualKeyStates::Down) == CoreVirtualKeyStates::Down);

    switch (e->Key)
    {
    case VirtualKey::Left:
        MoveCaret(-1, shift, ctrl);
        e->Handled = true;
        break;

    case VirtualKey::Right:
        MoveCaret(1, shift, ctrl);
        e->Handled = true;
        break;

    case VirtualKey::Home:
        if (shift)
        {
            int oldEnd = SelectionStart + SelectionLength;
            SetSelectionInternal(0, oldEnd);
        }
        else
        {
            SetSelectionInternal(0, 0);
        }
        e->Handled = true;
        break;

    case VirtualKey::End:
    {
        int len = Text != nullptr ? Text->Length() : 0;
        if (shift)
        {
            SetSelectionInternal(SelectionStart, len - SelectionStart);
        }
        else
        {
            SetSelectionInternal(len, 0);
        }
        e->Handled = true;
        break;
    }

    case VirtualKey::Back:
        if (!IsReadOnly)
            DeleteSelectionOrChar(false, ctrl);
        e->Handled = true;
        break;

    case VirtualKey::Delete:
        if (!IsReadOnly)
            DeleteSelectionOrChar(true, ctrl);
        e->Handled = true;
        break;

    case VirtualKey::Enter:
        if (!IsReadOnly && AcceptsReturn)
        {
            InsertText(L"\r\n");
            e->Handled = true;
        }
        break;

    case VirtualKey::A:
        if (ctrl)
        {
            SelectAll();
            e->Handled = true;
        }
        break;

    case VirtualKey::C:
        if (ctrl)
        {
            Copy();
            e->Handled = true;
        }
        break;

    case VirtualKey::X:
        if (ctrl && !IsReadOnly)
        {
            Cut();
            e->Handled = true;
        }
        break;

    case VirtualKey::V:
        if (ctrl && !IsReadOnly)
        {
            Paste();
            e->Handled = true;
        }
        break;

    case VirtualKey::Z:
        if (ctrl && !IsReadOnly)
        {
            Undo();
            e->Handled = true;
        }
        break;

    case VirtualKey::Y:
        if (ctrl && !IsReadOnly)
        {
            Redo();
            e->Handled = true;
        }
        break;

    case VirtualKey::Application:
        if (IsDefaultContextMenuEnabled && ContextFlyout == nullptr)
        {
            ShowContextMenu(GetCaretLocalPosition());
            e->Handled = true;
        }
        break;

    case VirtualKey::F10:
        if (shift && IsDefaultContextMenuEnabled && ContextFlyout == nullptr)
        {
            ShowContextMenu(GetCaretLocalPosition());
            e->Handled = true;
        }
        break;

    default:
        break;
    }

    Control::OnKeyDown(e);
}

void DirectTextBox::OnKeyUp(KeyRoutedEventArgs^ e)
{
    Control::OnKeyUp(e);
}

void DirectTextBox::OnCharacterReceived(CharacterReceivedRoutedEventArgs^ e)
{
    if (e->Handled || IsReadOnly || m_isComposing)
    {
        Control::OnCharacterReceived(e);
        return;
    }

    auto coreWin = CoreWindow::GetForCurrentThread();
    bool ctrl = coreWin != nullptr && ((coreWin->GetKeyState(VirtualKey::Control) & CoreVirtualKeyStates::Down) == CoreVirtualKeyStates::Down);
    if (ctrl)
    {
        Control::OnCharacterReceived(e);
        return;
    }

    wchar_t ch = e->Character;
    if (ch >= 32 || (ch == L'\t' && AcceptsReturn) || (ch == L'\r' && AcceptsReturn))
    {
        wchar_t buf[2] = { ch, 0 };
        InsertText(ref new Platform::String(buf));
        e->Handled = true;
    }

    Control::OnCharacterReceived(e);
}

#pragma endregion

#pragma region Pointer & Mouse

void DirectTextBox::WireContainerEvents()
{
    if (m_hitContainer == nullptr)
        return;

    m_hitContainer->PointerPressed += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerPressed);
    m_hitContainer->PointerMoved += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerMoved);
    m_hitContainer->PointerReleased += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerReleased);
    m_hitContainer->PointerCanceled += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerCanceled);
    m_hitContainer->PointerCaptureLost += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerCaptureLost);
    m_hitContainer->PointerEntered += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerEntered);
    m_hitContainer->PointerExited += ref new PointerEventHandler(this, &DirectTextBox::OnContainerPointerExited);

    if (m_containerContextRequestedToken.Value == 0)
        m_containerContextRequestedToken = m_hitContainer->ContextRequested += ref new TypedEventHandler<UIElement^, ContextRequestedEventArgs^>(this, &DirectTextBox::OnContainerContextRequested);
    if (m_containerRightTappedToken.Value == 0)
        m_containerRightTappedToken = m_hitContainer->RightTapped += ref new RightTappedEventHandler(this, &DirectTextBox::OnContainerRightTapped);
}

void DirectTextBox::OnContainerPointerPressed(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerPressed(e);
}

void DirectTextBox::OnContainerPointerMoved(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerMoved(e);
}

void DirectTextBox::OnContainerPointerReleased(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerReleased(e);
}

void DirectTextBox::OnContainerPointerCanceled(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerCanceled(e);
}

void DirectTextBox::OnContainerPointerCaptureLost(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerCaptureLost(e);
}

void DirectTextBox::OnContainerPointerEntered(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerEntered(e);
}

void DirectTextBox::OnContainerPointerExited(Platform::Object^, PointerRoutedEventArgs^ e)
{
    OnPointerExited(e);
}


void DirectTextBox::OnPointerPressed(PointerRoutedEventArgs^ e)
{
    UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
    auto ptrPt = e->GetCurrentPoint(target);

    if (ptrPt->Properties->IsRightButtonPressed)
    {
        this->Focus(Windows::UI::Xaml::FocusState::Pointer);
        m_isFocused = true;

        int pos = HitTestPointToTextPosition(ptrPt->Position);
        if (SelectionLength == 0 || pos < SelectionStart || pos > SelectionStart + SelectionLength)
        {
            m_selectionAnchor = pos;
            SetSelectionInternal(pos, 0);
        }

        EnsureCaretVisible();
        ResetCaretBlink();
        UpdateVisualStates(true);
        InvalidateRenderOnly();

        Control::OnPointerPressed(e);
        return;
    }

    this->Focus(Windows::UI::Xaml::FocusState::Pointer);
    if (m_hitContainer != nullptr)
        m_hitContainer->CapturePointer(e->Pointer);
    else
        this->CapturePointer(e->Pointer);

    m_isPointerPressed = true;
    m_isFocused = true;

    auto pt = ptrPt->Position;
    int pos = HitTestPointToTextPosition(pt);

    // Multi-click detection
    auto now = Windows::Foundation::DateTime();
    FILETIME ft;
    GetSystemTimeAsFileTime(&ft);
    ULARGE_INTEGER uli;
    uli.LowPart = ft.dwLowDateTime;
    uli.HighPart = ft.dwHighDateTime;
    now.UniversalTime = uli.QuadPart;

    int64 delta = now.UniversalTime - m_lastPointerDownTime.UniversalTime;
    double distSq = (pt.X - m_lastPointerDownPos.X) * (pt.X - m_lastPointerDownPos.X) +
                    (pt.Y - m_lastPointerDownPos.Y) * (pt.Y - m_lastPointerDownPos.Y);

    if (delta < 5000000 && distSq < 16.0) // 500ms and within 4 pixels
        m_clickCount++;
    else
        m_clickCount = 1;

    m_lastPointerDownTime = now;
    m_lastPointerDownPos = pt;

    if (m_clickCount == 1)
    {
        m_selectionAnchor = pos;
        SetSelectionInternal(pos, 0);
    }
    else if (m_clickCount == 2)
    {
        // Select word
        int start = FindWordBoundary(pos, false);
        int end = FindWordBoundary(pos, true);
        m_selectionAnchor = start;
        SetSelectionInternal(start, end - start);
    }
    else if (m_clickCount >= 3)
    {
        SelectAll();
    }

    if (m_editContext != nullptr)
        m_editContext->NotifyFocusEnter();

    EnsureCaretVisible();
    ResetCaretBlink();
    UpdateVisualStates(true);
    InvalidateRenderOnly();

    e->Handled = true;
    Control::OnPointerPressed(e);
}

void DirectTextBox::OnPointerMoved(PointerRoutedEventArgs^ e)
{
    if (m_isPointerPressed)
    {
        UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
        auto pt = e->GetCurrentPoint(target)->Position;
        int currentPos = HitTestPointToTextPosition(pt);

        int start = (std::min)(m_selectionAnchor, currentPos);
        int length = std::abs(currentPos - m_selectionAnchor);
        SetSelectionInternal(start, length);

        EnsureCaretVisible();
        ResetCaretBlink();
        InvalidateRenderOnly();
        e->Handled = true;
    }

    Control::OnPointerMoved(e);
}

void DirectTextBox::OnPointerReleased(PointerRoutedEventArgs^ e)
{
    if (m_isPointerPressed)
    {
        if (m_hitContainer != nullptr)
            m_hitContainer->ReleasePointerCapture(e->Pointer);
        else
            this->ReleasePointerCapture(e->Pointer);

        m_isPointerPressed = false;
        e->Handled = true;
    }

    Control::OnPointerReleased(e);
}

void DirectTextBox::OnPointerCanceled(PointerRoutedEventArgs^ e)
{
    if (m_isPointerPressed)
    {
        if (m_hitContainer != nullptr)
            m_hitContainer->ReleasePointerCapture(e->Pointer);
        else
            this->ReleasePointerCapture(e->Pointer);

        m_isPointerPressed = false;
    }
    Control::OnPointerCanceled(e);
}

void DirectTextBox::OnPointerCaptureLost(PointerRoutedEventArgs^ e)
{
    m_isPointerPressed = false;
    Control::OnPointerCaptureLost(e);
}

void DirectTextBox::OnPointerEntered(PointerRoutedEventArgs^ e)
{
    m_isPointerOver = true;
    UpdateVisualStates(true);
    try
    {
        auto coreWin = CoreWindow::GetForCurrentThread();
        if (coreWin != nullptr)
            coreWin->PointerCursor = ref new CoreCursor(CoreCursorType::IBeam, 0);
    }
    catch (...) {}
    Control::OnPointerEntered(e);
}

void DirectTextBox::OnPointerExited(PointerRoutedEventArgs^ e)
{
    m_isPointerOver = false;
    UpdateVisualStates(true);
    try
    {
        auto coreWin = CoreWindow::GetForCurrentThread();
        if (coreWin != nullptr)
            coreWin->PointerCursor = ref new CoreCursor(CoreCursorType::Arrow, 0);
    }
    catch (...) {}
    Control::OnPointerExited(e);
}

#pragma endregion

#pragma region Text Manipulation Helpers

void DirectTextBox::InsertText(Platform::String^ str)
{
    if (str == nullptr || str->Length() == 0 || IsReadOnly)
        return;

    PushUndoState();

    Platform::String^ current = Text != nullptr ? Text : L"";
    std::wstring s(current->Data());

    int start = SnapToClusterBoundary((std::max)(0, (std::min)(SelectionStart, (int)s.length())), true);
    int end = SnapToClusterBoundary(start + (std::max)(0, (std::min)(SelectionLength, (int)s.length() - start)), true);
    int len = end - start;

    s.replace(start, len, str->Data());

    if (MaxLength > 0 && (int)s.length() > MaxLength)
        s = s.substr(0, MaxLength);

    m_internalTextUpdating = true;
    this->Text = ref new Platform::String(s.c_str());
    m_internalTextUpdating = false;

    int newPos = (std::min)((int)s.length(), start + (int)str->Length());
    SetSelectionInternal(newPos, 0);
    EnsureCaretVisible();
    InvalidateLayoutAndRender();
}

void DirectTextBox::DeleteSelectionOrChar(bool forward, bool byWord)
{
    if (IsReadOnly)
        return;

    Platform::String^ current = Text != nullptr ? Text : L"";
    std::wstring s(current->Data());
    if (s.empty())
        return;

    PushUndoState();

    int start = (std::max)(0, (std::min)(SelectionStart, (int)s.length()));
    int len = (std::max)(0, (std::min)(SelectionLength, (int)s.length() - start));

    if (len > 0)
    {
        s.erase(start, len);
        m_internalTextUpdating = true;
        this->Text = ref new Platform::String(s.c_str());
        m_internalTextUpdating = false;
        SetSelectionInternal(start, 0);
    }
    else
    {
        if (forward)
        {
            if (start < (int)s.length())
            {
                int endPos = byWord ? FindWordBoundary(start, true) : SnapToClusterBoundary((std::min)((int)s.length(), start + 1), true);
                if (endPos <= start)
                    endPos = (std::min)((int)s.length(), start + 1);
                int delCount = (std::max)(1, endPos - start);

                s.erase(start, delCount);
                m_internalTextUpdating = true;
                this->Text = ref new Platform::String(s.c_str());
                m_internalTextUpdating = false;
                SetSelectionInternal(start, 0);
            }
        }
        else
        {
            if (start > 0)
            {
                int erasePos = byWord ? FindWordBoundary(start, false) : SnapToClusterBoundary((std::max)(0, start - 1), false);
                if (erasePos >= start)
                    erasePos = (std::max)(0, start - 1);
                int delCount = (std::max)(1, start - erasePos);

                s.erase(erasePos, delCount);
                m_internalTextUpdating = true;
                this->Text = ref new Platform::String(s.c_str());
                m_internalTextUpdating = false;
                SetSelectionInternal(erasePos, 0);
            }
        }
    }

    EnsureCaretVisible();
    InvalidateLayoutAndRender();
}

void DirectTextBox::MoveCaret(int offset, bool extendSelection, bool byWord)
{
    Platform::String^ current = Text != nullptr ? Text : L"";
    int textLen = current->Length();
    if (textLen == 0)
    {
        SetSelectionInternal(0, 0);
        return;
    }

    int currentAnchor = extendSelection ? m_selectionAnchor : (offset < 0 ? SelectionStart : SelectionStart + SelectionLength);
    int activeEnd = (offset < 0) ? SelectionStart : SelectionStart + SelectionLength;

    int newPos = activeEnd;
    if (byWord)
    {
        newPos = FindWordBoundary(activeEnd, offset > 0);
    }
    else
    {
        if (offset < 0)
        {
            newPos = SnapToClusterBoundary((std::max)(0, activeEnd - 1), false);
        }
        else
        {
            newPos = SnapToClusterBoundary((std::min)(textLen, activeEnd + 1), true);
        }
    }

    if (extendSelection)
    {
        int s = (std::min)(currentAnchor, newPos);
        int l = std::abs(newPos - currentAnchor);
        SetSelectionInternal(s, l);
    }
    else
    {
        m_selectionAnchor = newPos;
        SetSelectionInternal(newPos, 0);
    }

    EnsureCaretVisible();
}

void DirectTextBox::SetSelectionInternal(int start, int length, bool notifyEditContext)
{
    int textLen = Text != nullptr ? Text->Length() : 0;
    start = (std::max)(0, (std::min)(start, textLen));
    length = (std::max)(0, (std::min)(length, textLen - start));

    start = SnapToClusterBoundary(start, true);
    int end = SnapToClusterBoundary(start + length, true);
    length = (std::max)(0, end - start);

    if (SelectionStart != start || SelectionLength != length)
    {
        SelectionStart = start;
        SelectionLength = length;

        if (notifyEditContext && m_editContext != nullptr)
        {
            CoreTextRange sel;
            sel.StartCaretPosition = start;
            sel.EndCaretPosition = start + length;
            m_editContext->NotifySelectionChanged(sel);
        }
    }
}

int DirectTextBox::FindWordBoundary(int position, bool forward)
{
    Platform::String^ current = Text != nullptr ? Text : L"";
    int len = current->Length();
    if (len == 0)
        return 0;

    const wchar_t* data = current->Data();
    position = (std::max)(0, (std::min)(position, len));

    if (forward)
    {
        int p = position;
        while (p < len && iswspace(data[p])) p++;
        while (p < len && !iswspace(data[p]) && !iswpunct(data[p])) p++;
        if (p == position && p < len) p++;
        return (std::min)(len, p);
    }
    else
    {
        int p = position;
        while (p > 0 && iswspace(data[p - 1])) p--;
        while (p > 0 && !iswspace(data[p - 1]) && !iswpunct(data[p - 1])) p--;
        if (p == position && p > 0) p--;
        return (std::max)(0, p);
    }
}

int DirectTextBox::SnapToClusterBoundary(int position, bool forward)
{
    if (Text == nullptr)
        return 0;

    int textLen = Text->Length();
    if (position <= 0)
        return 0;
    if (position >= textLen)
        return textLen;

    // 1. Fast check for UTF-16 surrogate pairs
    const wchar_t* str = Text->Data();
    if (str[position] >= 0xDC00 && str[position] <= 0xDFFF && str[position - 1] >= 0xD800 && str[position - 1] <= 0xDBFF)
    {
        return forward ? position + 1 : position - 1;
    }

    // 2. DirectWrite cluster check for multi-codepoint grapheme clusters (emojis with skin tones, ZWJ sequences, etc.)
    if (m_textLayout != nullptr)
    {
        DWRITE_HIT_TEST_METRICS metrics{};
        float x = 0.0f, y = 0.0f;
        if (SUCCEEDED(m_textLayout->HitTestTextPosition(position, FALSE, &x, &y, &metrics)))
        {
            if (position > (int)metrics.textPosition && position < (int)(metrics.textPosition + metrics.length))
            {
                return forward ? (int)(metrics.textPosition + metrics.length) : (int)metrics.textPosition;
            }
        }
    }

    return position;
}

int DirectTextBox::HitTestPointToTextPosition(Point point)
{
    if (m_textLayout == nullptr)
        return 0;

    auto padding = this->Padding;
    float relX = static_cast<float>(point.X - padding.Left - m_scrollOffsetX);
    float relY = static_cast<float>(point.Y - padding.Top - m_scrollOffsetY);

    BOOL isTrailingHit = FALSE;
    BOOL isInside = FALSE;
    DWRITE_HIT_TEST_METRICS metrics{};

    HRESULT hr = m_textLayout->HitTestPoint(relX, relY, &isTrailingHit, &isInside, &metrics);
    if (SUCCEEDED(hr))
    {
        return isTrailingHit ? metrics.textPosition + metrics.length : metrics.textPosition;
    }

    return 0;
}

Rect DirectTextBox::GetCaretScreenRect()
{
    try
    {
        auto transform = TransformToVisual(nullptr);
        Point origin = transform->TransformPoint(Point(0, 0));

        auto padding = this->Padding;
        float caretX = 0.0f;
        float caretY = 0.0f;
        float caretH = static_cast<float>(FontSize > 0.0 ? FontSize * 1.2 : 20.0);

        if (m_textLayout != nullptr)
        {
            DWRITE_HIT_TEST_METRICS metrics{};
            m_textLayout->HitTestTextPosition(SelectionStart, FALSE, &caretX, &caretY, &metrics);
            caretH = metrics.height;
        }

        float screenX = static_cast<float>(origin.X + padding.Left + m_scrollOffsetX + caretX);
        float screenY = static_cast<float>(origin.Y + padding.Top + m_scrollOffsetY + caretY);

        return Rect(screenX, screenY, 2.0f, caretH);
    }
    catch (...)
    {
        return Rect(0, 0, 0, 0);
    }
}

void DirectTextBox::EnsureCaretVisible()
{
    float caretX = 0.0f;
    float caretY = 0.0f;
    if (m_textLayout != nullptr)
    {
        DWRITE_HIT_TEST_METRICS metrics{};
        m_textLayout->HitTestTextPosition(SelectionStart, FALSE, &caretX, &caretY, &metrics);
    }

    auto padding = this->Padding;
    float availWidth = (std::max)(1.0f, static_cast<float>(ActualWidth - padding.Left - padding.Right));

    if (caretX + m_scrollOffsetX > availWidth - 4.0f)
        m_scrollOffsetX = availWidth - 4.0f - caretX;
    else if (caretX + m_scrollOffsetX < 0.0f)
        m_scrollOffsetX = -caretX;

    DWRITE_TEXT_METRICS textMetrics{};
    if (m_textLayout != nullptr && SUCCEEDED(m_textLayout->GetMetrics(&textMetrics)))
    {
        if (textMetrics.widthIncludingTrailingWhitespace <= availWidth)
            m_scrollOffsetX = 0.0f;
    }
}

#pragma endregion

#pragma region Undo / Redo

void DirectTextBox::PushUndoState()
{
    EditSnapshot s;
    s.Text = Text != nullptr ? Text : L"";
    s.SelectionStart = SelectionStart;
    s.SelectionLength = SelectionLength;

    m_undoStack.push_back(s);
    if (m_undoStack.size() > 100)
        m_undoStack.erase(m_undoStack.begin());

    m_redoStack.clear();
}

void DirectTextBox::Undo()
{
    if (m_undoStack.empty() || IsReadOnly)
        return;

    EditSnapshot current;
    current.Text = Text != nullptr ? Text : L"";
    current.SelectionStart = SelectionStart;
    current.SelectionLength = SelectionLength;
    m_redoStack.push_back(current);

    EditSnapshot prev = m_undoStack.back();
    m_undoStack.pop_back();

    m_internalTextUpdating = true;
    this->Text = prev.Text;
    m_internalTextUpdating = false;

    SetSelectionInternal(prev.SelectionStart, prev.SelectionLength);
    EnsureCaretVisible();
    InvalidateLayoutAndRender();
}

void DirectTextBox::Redo()
{
    if (m_redoStack.empty() || IsReadOnly)
        return;

    EditSnapshot current;
    current.Text = Text != nullptr ? Text : L"";
    current.SelectionStart = SelectionStart;
    current.SelectionLength = SelectionLength;
    m_undoStack.push_back(current);

    EditSnapshot next = m_redoStack.back();
    m_redoStack.pop_back();

    m_internalTextUpdating = true;
    this->Text = next.Text;
    m_internalTextUpdating = false;

    SetSelectionInternal(next.SelectionStart, next.SelectionLength);
    EnsureCaretVisible();
    InvalidateLayoutAndRender();
}

#pragma endregion

#pragma region Clipboard & Selection Commands

void DirectTextBox::Select(int start, int length)
{
    SetSelectionInternal(start, length);
}

void DirectTextBox::SelectAll()
{
    int len = Text != nullptr ? Text->Length() : 0;
    m_selectionAnchor = 0;
    SetSelectionInternal(0, len);
}

void DirectTextBox::Clear()
{
    if (IsReadOnly)
        return;
    PushUndoState();
    this->Text = L"";
    SetSelectionInternal(0, 0);
}

void DirectTextBox::Copy()
{
    Platform::String^ sel = SelectedText;
    if (sel != nullptr && sel->Length() > 0)
    {
        auto dataPackage = ref new DataPackage();
        dataPackage->RequestedOperation = DataPackageOperation::Copy;
        dataPackage->SetText(sel);
        Clipboard::SetContent(dataPackage);
    }
}

void DirectTextBox::Cut()
{
    if (IsReadOnly)
        return;
    Copy();
    DeleteSelectionOrChar(false);
}

void DirectTextBox::Paste()
{
    if (IsReadOnly)
        return;

    try
    {
        auto content = Clipboard::GetContent();
        if (content != nullptr && content->Contains(StandardDataFormats::Text))
        {
            Platform::WeakReference weakThis(this);
            create_task(content->GetTextAsync()).then([weakThis](Platform::String^ text) {
                if (auto strongThis = weakThis.Resolve<DirectTextBox>())
                {
                    if (text != nullptr)
                        strongThis->InsertText(text);
                }
            });
        }
    }
    catch (...) {}
}

#pragma endregion

#pragma region DirectWrite Layout Creation

void DirectTextBox::CreateTextLayout()
{
    auto factory = CompositionDeviceManager::GetDWriteFactory();
    if (factory == nullptr)
        return;

    m_textLayout = nullptr;
    m_placeholderLayout = nullptr;
    m_simulatedFontFace = nullptr;

    double size = FontSize > 0.0 ? FontSize : 15.0;
    Platform::String^ textToFormat = Text != nullptr ? Text : L"";

    // Handle StyleSimulations
    auto simulations = StyleSimulations;
    if (FontFace != nullptr)
    {
        if (simulations != Windows::UI::Xaml::Media::StyleSimulations::None)
        {
            auto faceRef = FontFace->GetReference();
            if (faceRef != nullptr)
            {
                ComPtr<IDWriteFontFace3> simFace;
                if (SUCCEEDED(faceRef->CreateFontFaceWithSimulations(ToDWriteFontSimulations(simulations), &simFace)))
                    m_simulatedFontFace = simFace;
            }
        }
        if (m_simulatedFontFace == nullptr)
            m_simulatedFontFace = FontFace->GetFontFace();
    }

    // Prepare format
    ComPtr<IDWriteTextFormat> tempFormat;
    HRESULT hr = S_OK;

    if (FontFace != nullptr)
    {
        const auto& axisValues = FontFace->GetAxisValues();
        if (!axisValues.empty())
        {
            ComPtr<IDWriteTextFormat3> format3;
            hr = factory->CreateTextFormat(
                FontFace->Properties->FamilyName->Data(),
                FontFace->GetFontCollection().Get(),
                axisValues.data(),
                static_cast<UINT32>(axisValues.size()),
                static_cast<FLOAT>(size),
                L"en-us",
                &format3);
            if (SUCCEEDED(hr))
                tempFormat = format3;
        }

        if (tempFormat == nullptr)
        {
            auto weight = FontFace->Properties->Weight;
            auto style = FontFace->Properties->Style;
            auto stretch = FontFace->Properties->Stretch;

            if (simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldSimulation ||
                simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
            {
                weight.Weight = (std::max)(weight.Weight, static_cast<unsigned short>(700));
            }
            if (simulations == Windows::UI::Xaml::Media::StyleSimulations::ItalicSimulation ||
                simulations == Windows::UI::Xaml::Media::StyleSimulations::BoldItalicSimulation)
            {
                style = Windows::UI::Text::FontStyle::Italic;
            }

            hr = factory->CreateTextFormat(
                FontFace->Properties->FamilyName->Data(),
                FontFace->GetFontCollection().Get(),
                static_cast<DWRITE_FONT_WEIGHT>(weight.Weight),
                static_cast<DWRITE_FONT_STYLE>(style),
                static_cast<DWRITE_FONT_STRETCH>(stretch),
                static_cast<FLOAT>(size),
                L"en-us",
                &tempFormat);
        }
    }

    if (tempFormat == nullptr)
    {
        hr = factory->CreateTextFormat(
            L"Segoe UI",
            nullptr,
            DWRITE_FONT_WEIGHT_NORMAL,
            DWRITE_FONT_STYLE_NORMAL,
            DWRITE_FONT_STRETCH_NORMAL,
            static_cast<FLOAT>(size),
            L"en-us",
            &tempFormat);
    }

    if (tempFormat == nullptr)
        return;

    // Reading direction (RTL support)
    if (this->FlowDirection == Windows::UI::Xaml::FlowDirection::RightToLeft)
        tempFormat->SetReadingDirection(DWRITE_READING_DIRECTION_RIGHT_TO_LEFT);
    else
        tempFormat->SetReadingDirection(DWRITE_READING_DIRECTION_LEFT_TO_RIGHT);

    // Text alignment
    switch (TextAlignment)
    {
    case Windows::UI::Xaml::TextAlignment::Center:
        tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_CENTER);
        break;
    case Windows::UI::Xaml::TextAlignment::Right:
        tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_TRAILING);
        break;
    case Windows::UI::Xaml::TextAlignment::Justify:
        tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_JUSTIFIED);
        break;
    default:
        tempFormat->SetTextAlignment(DWRITE_TEXT_ALIGNMENT_LEADING);
        break;
    }

    FLOAT maxLayoutWidth = TextWrapping == Windows::UI::Xaml::TextWrapping::Wrap
        ? static_cast<FLOAT>((std::max)(1.0, ActualWidth - Padding.Left - Padding.Right))
        : 100000.0f;
    FLOAT maxLayoutHeight = 100000.0f;

    // Create main text layout
    if (textToFormat->Length() > 0)
    {
        hr = factory->CreateTextLayout(
            textToFormat->Data(),
            textToFormat->Length(),
            tempFormat.Get(),
            maxLayoutWidth,
            maxLayoutHeight,
            &m_textLayout);

        if (SUCCEEDED(hr) && m_textLayout != nullptr)
        {
            // Apply variable font axis values
            if (FontFace != nullptr)
            {
                const auto& axisValues = FontFace->GetAxisValues();
                if (!axisValues.empty())
                {
                    ComPtr<IDWriteTextLayout4> layout4;
                    if (SUCCEEDED(m_textLayout.As(&layout4)))
                    {
                        layout4->SetFontAxisValues(
                            axisValues.data(),
                            static_cast<UINT32>(axisValues.size()),
                            DWRITE_TEXT_RANGE{ 0, textToFormat->Length() });
                    }
                }
            }

            // Apply Typography
            auto typo = Typography;
            if (typo != nullptr && typo->FeatureCount > 0)
            {
                ComPtr<IDWriteTypography> dwriteTypo;
                if (SUCCEEDED(factory->CreateTypography(&dwriteTypo)))
                {
                    for (const auto& f : typo->GetDWriteFontFeatures())
                        dwriteTypo->AddFontFeature(f);

                    m_textLayout->SetTypography(dwriteTypo.Get(), DWRITE_TEXT_RANGE{ 0, textToFormat->Length() });
                }
            }

            m_textLayout->SetWordWrapping(TextWrapping == Windows::UI::Xaml::TextWrapping::Wrap
                ? DWRITE_WORD_WRAPPING_WRAP
                : DWRITE_WORD_WRAPPING_NO_WRAP);
        }
    }

    // Create placeholder layout if placeholder exists
    if (PlaceholderText != nullptr && PlaceholderText->Length() > 0)
    {
        factory->CreateTextLayout(
            PlaceholderText->Data(),
            PlaceholderText->Length(),
            tempFormat.Get(),
            maxLayoutWidth,
            maxLayoutHeight,
            &m_placeholderLayout);
    }
}

#pragma endregion

#pragma region Rendering via CompositionDrawingSurface

void DirectTextBox::ReleaseDrawingSurface()
{
    m_drawingSurface = nullptr;
    if (m_surfaceBrush != nullptr)
        m_surfaceBrush->Surface = nullptr;
    m_renderedWidth = 0;
    m_renderedHeight = 0;
}

void DirectTextBox::RenderToSurface()
{
    UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
    float actualW = static_cast<float>(target->RenderSize.Width > 0 ? target->RenderSize.Width : ActualWidth);
    float actualH = static_cast<float>(target->RenderSize.Height > 0 ? target->RenderSize.Height : ActualHeight);
    if (actualW <= 0.0f || actualH <= 0.0f)
        return;

    auto visual = ElementCompositionPreview::GetElementVisual(target);
    auto compositor = visual != nullptr ? visual->Compositor : nullptr;
    if (compositor == nullptr)
        return;

    if (m_spriteVisual == nullptr)
    {
        m_spriteVisual = compositor->CreateSpriteVisual();
        ElementCompositionPreview::SetElementChildVisual(target, m_spriteVisual);
    }

    float dpiScale = 1.0f;
    try
    {
        if (this->Dispatcher != nullptr)
        {
            auto displayInfo = Windows::Graphics::Display::DisplayInformation::GetForCurrentView();
            if (displayInfo != nullptr)
                dpiScale = displayInfo->LogicalDpi / 96.0f;
        }
    }
    catch (...) {}

    LONG requiredWidth = (std::max)(1L, static_cast<LONG>(std::ceil(actualW * dpiScale)));
    LONG requiredHeight = (std::max)(1L, static_cast<LONG>(std::ceil(actualH * dpiScale)));

    auto graphicsDevice = CompositionDeviceManager::GetGraphicsDevice(compositor);
    if (graphicsDevice == nullptr)
        return;

    if (m_drawingSurface == nullptr || m_renderedWidth != requiredWidth || m_renderedHeight != requiredHeight)
    {
        ReleaseDrawingSurface();
        m_drawingSurface = graphicsDevice->CreateDrawingSurface(
            Size(static_cast<float>(requiredWidth), static_cast<float>(requiredHeight)),
            Windows::Graphics::DirectX::DirectXPixelFormat::B8G8R8A8UIntNormalized,
            Windows::Graphics::DirectX::DirectXAlphaMode::Premultiplied);
        m_renderedWidth = requiredWidth;
        m_renderedHeight = requiredHeight;
    }

    if (m_drawingSurface == nullptr)
        return;

    if (m_isLayoutDirty)
    {
        CreateTextLayout();
        m_isLayoutDirty = false;
    }

    ComPtr<ICompositionDrawingSurfaceInterop> surfaceInterop;
    if (FAILED(reinterpret_cast<IUnknown*>(m_drawingSurface)->QueryInterface(IID_PPV_ARGS(&surfaceInterop))))
        return;

    POINT updateOffset{};
    ComPtr<ID2D1DeviceContext> d2dContext;

    {
        std::lock_guard<std::mutex> renderLock(CompositionDeviceManager::GetRenderMutex());

        HRESULT hr = surfaceInterop->BeginDraw(nullptr, IID_PPV_ARGS(&d2dContext), &updateOffset);
        if (FAILED(hr))
        {
            if (CompositionDeviceManager::IsDeviceLostHResult(hr))
                CompositionDeviceManager::HandleDeviceLost();
            return;
        }

        if (d2dContext != nullptr)
        {
            d2dContext->Clear(D2D1::ColorF(0, 0, 0, 0));

            // Background brush
            if (auto bgScb = dynamic_cast<SolidColorBrush^>(this->Background))
            {
                auto c = bgScb->Color;
                D2D1_COLOR_F bgColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(bgScb->Opacity));
                ComPtr<ID2D1SolidColorBrush> bgBrush;
                d2dContext->CreateSolidColorBrush(bgColor, &bgBrush);
                d2dContext->FillRectangle(D2D1::RectF(0, 0, static_cast<FLOAT>(requiredWidth), static_cast<FLOAT>(requiredHeight)), bgBrush.Get());
            }

            // Set transform for DPI scale and update offset
            d2dContext->SetTransform(
                D2D1::Matrix3x2F::Scale(dpiScale, dpiScale) *
                D2D1::Matrix3x2F::Translation(static_cast<FLOAT>(updateOffset.x), static_cast<FLOAT>(updateOffset.y)));

            auto padding = this->Padding;
            float padLeft = static_cast<float>(padding.Left);
            float padTop = static_cast<float>(padding.Top);
            float padRight = static_cast<float>(padding.Right);
            float padBottom = static_cast<float>(padding.Bottom);

            // Clip text within padding
            D2D1_RECT_F clipRect = D2D1::RectF(padLeft, padTop, actualW - padRight, actualH - padBottom);
            d2dContext->PushAxisAlignedClip(clipRect, D2D1_ANTIALIAS_MODE_PER_PRIMITIVE);

            float originX = padLeft + m_scrollOffsetX;
            float originY = padTop + m_scrollOffsetY;

            // Selection Highlight
            if (SelectionLength > 0 && m_textLayout != nullptr)
            {
                D2D1_COLOR_F selColor = D2D1::ColorF(0.0f, 0.47f, 0.84f, 0.35f);
                if (SelectionHighlightColor != nullptr)
                {
                    auto c = SelectionHighlightColor->Color;
                    selColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(SelectionHighlightColor->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> selBrush;
                d2dContext->CreateSolidColorBrush(selColor, &selBrush);

                UINT32 hitCount = 0;
                DWRITE_HIT_TEST_METRICS hitMetrics[32];
                if (SUCCEEDED(m_textLayout->HitTestTextRange(SelectionStart, SelectionLength, originX, originY, hitMetrics, 32, &hitCount)))
                {
                    for (UINT32 i = 0; i < hitCount; ++i)
                    {
                        D2D1_RECT_F r = D2D1::RectF(
                            hitMetrics[i].left,
                            hitMetrics[i].top,
                            hitMetrics[i].left + hitMetrics[i].width,
                            hitMetrics[i].top + hitMetrics[i].height);
                        d2dContext->FillRectangle(r, selBrush.Get());
                    }
                }
            }

            // Draw text
            bool hasText = Text != nullptr && Text->Length() > 0;
            if (hasText && m_textLayout != nullptr)
            {
                D2D1_COLOR_F fgColor = D2D1::ColorF(D2D1::ColorF::White);
                if (auto fgScb = dynamic_cast<SolidColorBrush^>(this->Foreground))
                {
                    auto c = fgScb->Color;
                    fgColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(fgScb->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> d2dBrush;
                d2dContext->CreateSolidColorBrush(fgColor, &d2dBrush);

                GlyphImageFormat preferredFormat = IsColorFontEnabled ? GlyphImageFormat::None : GlyphImageFormat::TrueType;

                CustomColorTextRenderer renderer(
                    d2dContext.Get(),
                    CompositionDeviceManager::GetDWriteFactory().Get(),
                    d2dBrush.Get(),
                    preferredFormat,
                    m_simulatedFontFace.Get());

                m_textLayout->Draw(nullptr, &renderer, originX, originY);
            }
            else if (!hasText && m_placeholderLayout != nullptr)
            {
                // Placeholder
                D2D1_COLOR_F phColor = D2D1::ColorF(0.5f, 0.5f, 0.5f, 0.6f);
                if (auto phScb = dynamic_cast<SolidColorBrush^>(PlaceholderForeground))
                {
                    auto c = phScb->Color;
                    phColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(phScb->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> phBrush;
                d2dContext->CreateSolidColorBrush(phColor, &phBrush);

                CustomColorTextRenderer renderer(
                    d2dContext.Get(),
                    CompositionDeviceManager::GetDWriteFactory().Get(),
                    phBrush.Get(),
                    GlyphImageFormat::TrueType,
                    m_simulatedFontFace.Get());

                m_placeholderLayout->Draw(nullptr, &renderer, originX, originY);
            }

            // Draw Caret
            if (m_isFocused && m_isCaretVisible && SelectionLength == 0)
            {
                float caretX = 0.0f;
                float caretY = 0.0f;
                float caretH = static_cast<float>(FontSize > 0.0 ? FontSize * 1.2 : 20.0);

                if (m_textLayout != nullptr && hasText)
                {
                    DWRITE_HIT_TEST_METRICS metrics{};
                    m_textLayout->HitTestTextPosition(SelectionStart, FALSE, &caretX, &caretY, &metrics);
                    caretH = metrics.height;
                }
                else if (FontFace != nullptr)
                {
                    auto m = FontFace->GetMetrics();
                    double em = (std::max)(1.0, (double)m.designUnitsPerEm);
                    double sz = FontSize > 0.0 ? FontSize : 15.0;
                    caretH = static_cast<float>((m.ascent + m.descent) * (sz / em));
                }

                D2D1_COLOR_F caretColor = D2D1::ColorF(D2D1::ColorF::White);
                if (auto curScb = dynamic_cast<SolidColorBrush^>(CursorBrush))
                {
                    auto c = curScb->Color;
                    caretColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(curScb->Opacity));
                }
                else if (auto fgScb = dynamic_cast<SolidColorBrush^>(this->Foreground))
                {
                    auto c = fgScb->Color;
                    caretColor = D2D1::ColorF(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, (c.A / 255.0f) * static_cast<float>(fgScb->Opacity));
                }

                ComPtr<ID2D1SolidColorBrush> caretBrush;
                d2dContext->CreateSolidColorBrush(caretColor, &caretBrush);

                D2D1_RECT_F caretRect = D2D1::RectF(
                    originX + caretX,
                    originY + caretY,
                    originX + caretX + 1.5f,
                    originY + caretY + caretH);
                d2dContext->FillRectangle(caretRect, caretBrush.Get());
            }

            d2dContext->PopAxisAlignedClip();

            surfaceInterop->EndDraw();
        }
    }

    if (m_surfaceBrush == nullptr)
    {
        m_surfaceBrush = compositor->CreateSurfaceBrush(m_drawingSurface);
        m_surfaceBrush->Stretch = CompositionStretch::None;
        m_surfaceBrush->HorizontalAlignmentRatio = 0.0f;
        m_surfaceBrush->VerticalAlignmentRatio = 0.0f;
    }
    else if (m_surfaceBrush->Surface != m_drawingSurface)
    {
        m_surfaceBrush->Surface = m_drawingSurface;
    }

    m_surfaceBrush->Scale = float2(1.0f / dpiScale, 1.0f / dpiScale);
    m_spriteVisual->Size = float2(actualW, actualH);
    if (m_spriteVisual->Brush != m_surfaceBrush)
        m_spriteVisual->Brush = m_surfaceBrush;
}

#pragma endregion

#pragma region Context Menu

void DirectTextBox::EnsureContextMenu()
{
    if (m_contextMenu != nullptr)
        return;

    m_contextMenu = ref new MenuFlyout();

    Platform::WeakReference weakThis(this);

    // 1. Cut
    m_cutCommand = ref new StandardUICommand(StandardUICommandKind::Cut);
    m_cutCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->Cut();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_cutCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                args->CanExecute = !strongThis->IsReadOnly && (strongThis->SelectionLength > 0);
            }
        });
    m_cutItem = ref new MenuFlyoutItem();
    m_cutItem->Command = m_cutCommand;
    m_contextMenu->Items->Append(m_cutItem);

    // 2. Copy
    m_copyCommand = ref new StandardUICommand(StandardUICommandKind::Copy);
    m_copyCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->Copy();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_copyCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                args->CanExecute = strongThis->SelectionLength > 0;
            }
        });
    m_copyItem = ref new MenuFlyoutItem();
    m_copyItem->Command = m_copyCommand;
    m_contextMenu->Items->Append(m_copyItem);

    // 3. Paste
    m_pasteCommand = ref new StandardUICommand(StandardUICommandKind::Paste);
    m_pasteCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->Paste();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_pasteCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                args->CanExecute = !strongThis->IsReadOnly && strongThis->ClipboardHasText();
            }
        });
    m_pasteItem = ref new MenuFlyoutItem();
    m_pasteItem->Command = m_pasteCommand;
    m_contextMenu->Items->Append(m_pasteItem);

    // Separator
    m_contextMenu->Items->Append(ref new MenuFlyoutSeparator());

    // 4. Undo
    m_undoCommand = ref new StandardUICommand(StandardUICommandKind::Undo);
    m_undoCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->Undo();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_undoCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                args->CanExecute = !strongThis->IsReadOnly && !strongThis->m_undoStack.empty();
            }
        });
    m_undoItem = ref new MenuFlyoutItem();
    m_undoItem->Command = m_undoCommand;
    m_contextMenu->Items->Append(m_undoItem);

    // 5. Redo
    m_redoCommand = ref new StandardUICommand(StandardUICommandKind::Redo);
    m_redoCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->Redo();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_redoCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                args->CanExecute = !strongThis->IsReadOnly && !strongThis->m_redoStack.empty();
            }
        });
    m_redoItem = ref new MenuFlyoutItem();
    m_redoItem->Command = m_redoCommand;
    m_contextMenu->Items->Append(m_redoItem);

    // Separator
    m_contextMenu->Items->Append(ref new MenuFlyoutSeparator());

    // 6. Select all
    m_selectAllCommand = ref new StandardUICommand(StandardUICommandKind::SelectAll);
    m_selectAllCommand->ExecuteRequested += ref new TypedEventHandler<XamlUICommand^, ExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, ExecuteRequestedEventArgs^) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                strongThis->SelectAll();
                strongThis->Focus(Windows::UI::Xaml::FocusState::Programmatic);
            }
        });
    m_selectAllCommand->CanExecuteRequested += ref new TypedEventHandler<XamlUICommand^, CanExecuteRequestedEventArgs^>(
        [weakThis](XamlUICommand^, CanExecuteRequestedEventArgs^ args) {
            if (auto strongThis = weakThis.Resolve<DirectTextBox>())
            {
                int textLen = strongThis->Text != nullptr ? strongThis->Text->Length() : 0;
                args->CanExecute = (textLen > 0) && (strongThis->SelectionLength < textLen);
            }
        });
    m_selectAllItem = ref new MenuFlyoutItem();
    m_selectAllItem->Command = m_selectAllCommand;
    m_contextMenu->Items->Append(m_selectAllItem);
}

bool DirectTextBox::ClipboardHasText()
{
    try
    {
        auto content = Clipboard::GetContent();
        if (content != nullptr)
            return content->Contains(StandardDataFormats::Text);
    }
    catch (...) {}
    return false;
}

void DirectTextBox::UpdateContextMenuItems()
{
    if (m_contextMenu == nullptr)
        return;

    bool isReadOnly = IsReadOnly;
    int selLen = SelectionLength;
    int textLen = Text != nullptr ? Text->Length() : 0;
    bool hasSelection = selLen > 0;
    bool hasText = textLen > 0;
    bool clipboardHasText = ClipboardHasText();

    if (m_cutCommand != nullptr)
        m_cutCommand->NotifyCanExecuteChanged();
    if (m_cutItem != nullptr)
        m_cutItem->IsEnabled = !isReadOnly && hasSelection;

    if (m_copyCommand != nullptr)
        m_copyCommand->NotifyCanExecuteChanged();
    if (m_copyItem != nullptr)
        m_copyItem->IsEnabled = hasSelection;

    if (m_pasteCommand != nullptr)
        m_pasteCommand->NotifyCanExecuteChanged();
    if (m_pasteItem != nullptr)
        m_pasteItem->IsEnabled = !isReadOnly && clipboardHasText;

    if (m_undoCommand != nullptr)
        m_undoCommand->NotifyCanExecuteChanged();
    if (m_undoItem != nullptr)
        m_undoItem->IsEnabled = !isReadOnly && !m_undoStack.empty();

    if (m_redoCommand != nullptr)
        m_redoCommand->NotifyCanExecuteChanged();
    if (m_redoItem != nullptr)
        m_redoItem->IsEnabled = !isReadOnly && !m_redoStack.empty();

    if (m_selectAllCommand != nullptr)
        m_selectAllCommand->NotifyCanExecuteChanged();
    if (m_selectAllItem != nullptr)
        m_selectAllItem->IsEnabled = hasText && (selLen < textLen);
}

Point DirectTextBox::GetCaretLocalPosition()
{
    auto padding = this->Padding;
    float caretX = 0.0f;
    float caretY = 0.0f;
    float caretH = static_cast<float>(FontSize > 0.0 ? FontSize * 1.2 : 20.0);

    if (m_textLayout != nullptr)
    {
        DWRITE_HIT_TEST_METRICS metrics{};
        m_textLayout->HitTestTextPosition(SelectionStart + SelectionLength, FALSE, &caretX, &caretY, &metrics);
        caretH = metrics.height;
    }

    float localX = static_cast<float>(padding.Left) + m_scrollOffsetX + caretX;
    float localY = static_cast<float>(padding.Top) + m_scrollOffsetY + caretY + caretH;

    float actualW = static_cast<float>(ActualWidth);
    float actualH = static_cast<float>(ActualHeight);

    if (actualW > 0.0f)
        localX = (std::max)(0.0f, (std::min)(localX, actualW));
    if (actualH > 0.0f)
        localY = (std::max)(0.0f, (std::min)(localY, actualH));

    return Point(localX, localY);
}

void DirectTextBox::ShowContextMenu(Windows::Foundation::Point point)
{
    EnsureContextMenu();

    if (m_contextMenu == nullptr)
        return;

    UpdateContextMenuItems();

    UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
    m_contextMenu->ShowAt(target, point);
}

void DirectTextBox::OnContextRequested(UIElement^, ContextRequestedEventArgs^ args)
{
    if (args->Handled || (m_contextMenu != nullptr && m_contextMenu->IsOpen))
        return;

    if (!IsDefaultContextMenuEnabled || ContextFlyout != nullptr)
        return;

    UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
    Point point;
    if (args->TryGetPosition(target, &point))
    {
        int pos = HitTestPointToTextPosition(point);
        if (SelectionLength == 0 || pos < SelectionStart || pos > SelectionStart + SelectionLength)
        {
            m_selectionAnchor = pos;
            SetSelectionInternal(pos, 0);
            EnsureCaretVisible();
            ResetCaretBlink();
            InvalidateRenderOnly();
        }

        ShowContextMenu(point);
        args->Handled = true;
    }
    else
    {
        ShowContextMenu(GetCaretLocalPosition());
        args->Handled = true;
    }
}

void DirectTextBox::OnContainerContextRequested(UIElement^ sender, ContextRequestedEventArgs^ args)
{
    OnContextRequested(sender, args);
}

void DirectTextBox::OnRightTapped(RightTappedRoutedEventArgs^ e)
{
    if (e->Handled || (m_contextMenu != nullptr && m_contextMenu->IsOpen))
        return;

    if (!IsDefaultContextMenuEnabled || ContextFlyout != nullptr)
    {
        Control::OnRightTapped(e);
        return;
    }

    UIElement^ target = m_hitContainer != nullptr ? (UIElement^)m_hitContainer : (UIElement^)this;
    Point point = e->GetPosition(target);

    int pos = HitTestPointToTextPosition(point);
    if (SelectionLength == 0 || pos < SelectionStart || pos > SelectionStart + SelectionLength)
    {
        m_selectionAnchor = pos;
        SetSelectionInternal(pos, 0);
        EnsureCaretVisible();
        ResetCaretBlink();
        InvalidateRenderOnly();
    }

    ShowContextMenu(point);
    e->Handled = true;
    Control::OnRightTapped(e);
}

void DirectTextBox::OnContainerRightTapped(Platform::Object^, RightTappedRoutedEventArgs^ e)
{
    OnRightTapped(e);
}

#pragma endregion
