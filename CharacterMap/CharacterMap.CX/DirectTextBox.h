#pragma once

#include <d3d11_2.h>
#include <d2d1_3.h>
#include <dwrite_3.h>
#include <wrl.h>
#include <vector>
#include <string>

#include "DWriteFontFace.h"
#include "DWriteTypographyCollection.h"

namespace CharacterMapCX
{
    namespace Controls
    {
        [Windows::Foundation::Metadata::WebHostHidden]
        public ref class DirectTextBox sealed : public Windows::UI::Xaml::Controls::Control
        {
        public:
            DirectTextBox();
            virtual ~DirectTextBox();

            #pragma region Dependency Properties

            static void RegisterDependencyProperties();

            static property Windows::UI::Xaml::DependencyProperty^ FontFaceProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _FontFaceProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ TypographyProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _TypographyProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ StyleSimulationsProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _StyleSimulationsProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ IsColorFontEnabledProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _IsColorFontEnabledProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ TextProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _TextProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ PlaceholderTextProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _PlaceholderTextProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ PlaceholderForegroundProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _PlaceholderForegroundProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ SelectionStartProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _SelectionStartProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ SelectionLengthProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _SelectionLengthProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ IsReadOnlyProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _IsReadOnlyProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ MaxLengthProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _MaxLengthProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ TextAlignmentProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _TextAlignmentProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ TextWrappingProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _TextWrappingProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ SelectionHighlightColorProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _SelectionHighlightColorProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ CursorBrushProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _CursorBrushProperty; }
            }

            static property Windows::UI::Xaml::DependencyProperty^ AcceptsReturnProperty
            {
                Windows::UI::Xaml::DependencyProperty^ get() { return _AcceptsReturnProperty; }
            }

            #pragma endregion

            #pragma region Properties

            property DWriteFontFace^ FontFace
            {
                DWriteFontFace^ get() { return (DWriteFontFace^)GetValue(FontFaceProperty); }
                void set(DWriteFontFace^ value) { SetValue(FontFaceProperty, value); }
            }

            property DWriteTypographyCollection^ Typography
            {
                DWriteTypographyCollection^ get() { return (DWriteTypographyCollection^)GetValue(TypographyProperty); }
                void set(DWriteTypographyCollection^ value) { SetValue(TypographyProperty, value); }
            }

            property Windows::UI::Xaml::Media::StyleSimulations StyleSimulations
            {
                Windows::UI::Xaml::Media::StyleSimulations get() { return (Windows::UI::Xaml::Media::StyleSimulations)GetValue(StyleSimulationsProperty); }
                void set(Windows::UI::Xaml::Media::StyleSimulations value) { SetValue(StyleSimulationsProperty, value); }
            }

            property bool IsColorFontEnabled
            {
                bool get() { return (bool)GetValue(IsColorFontEnabledProperty); }
                void set(bool value) { SetValue(IsColorFontEnabledProperty, value); }
            }

            property Platform::String^ Text
            {
                Platform::String^ get() { return (Platform::String^)GetValue(TextProperty); }
                void set(Platform::String^ value) { SetValue(TextProperty, value); }
            }

            property Platform::String^ PlaceholderText
            {
                Platform::String^ get() { return (Platform::String^)GetValue(PlaceholderTextProperty); }
                void set(Platform::String^ value) { SetValue(PlaceholderTextProperty, value); }
            }

            property Windows::UI::Xaml::Media::Brush^ PlaceholderForeground
            {
                Windows::UI::Xaml::Media::Brush^ get() { return (Windows::UI::Xaml::Media::Brush^)GetValue(PlaceholderForegroundProperty); }
                void set(Windows::UI::Xaml::Media::Brush^ value) { SetValue(PlaceholderForegroundProperty, value); }
            }

            property int SelectionStart
            {
                int get() { return (int)GetValue(SelectionStartProperty); }
                void set(int value) { SetValue(SelectionStartProperty, value); }
            }

            property int SelectionLength
            {
                int get() { return (int)GetValue(SelectionLengthProperty); }
                void set(int value) { SetValue(SelectionLengthProperty, value); }
            }

            property Platform::String^ SelectedText
            {
                Platform::String^ get();
                void set(Platform::String^ value);
            }

            property bool IsReadOnly
            {
                bool get() { return (bool)GetValue(IsReadOnlyProperty); }
                void set(bool value) { SetValue(IsReadOnlyProperty, value); }
            }

            property int MaxLength
            {
                int get() { return (int)GetValue(MaxLengthProperty); }
                void set(int value) { SetValue(MaxLengthProperty, value); }
            }

            property Windows::UI::Xaml::TextAlignment TextAlignment
            {
                Windows::UI::Xaml::TextAlignment get() { return (Windows::UI::Xaml::TextAlignment)GetValue(TextAlignmentProperty); }
                void set(Windows::UI::Xaml::TextAlignment value) { SetValue(TextAlignmentProperty, value); }
            }

            property Windows::UI::Xaml::TextWrapping TextWrapping
            {
                Windows::UI::Xaml::TextWrapping get() { return (Windows::UI::Xaml::TextWrapping)GetValue(TextWrappingProperty); }
                void set(Windows::UI::Xaml::TextWrapping value) { SetValue(TextWrappingProperty, value); }
            }

            property Windows::UI::Xaml::Media::SolidColorBrush^ SelectionHighlightColor
            {
                Windows::UI::Xaml::Media::SolidColorBrush^ get() { return (Windows::UI::Xaml::Media::SolidColorBrush^)GetValue(SelectionHighlightColorProperty); }
                void set(Windows::UI::Xaml::Media::SolidColorBrush^ value) { SetValue(SelectionHighlightColorProperty, value); }
            }

            property Windows::UI::Xaml::Media::Brush^ CursorBrush
            {
                Windows::UI::Xaml::Media::Brush^ get() { return (Windows::UI::Xaml::Media::Brush^)GetValue(CursorBrushProperty); }
                void set(Windows::UI::Xaml::Media::Brush^ value) { SetValue(CursorBrushProperty, value); }
            }

            property bool AcceptsReturn
            {
                bool get() { return (bool)GetValue(AcceptsReturnProperty); }
                void set(bool value) { SetValue(AcceptsReturnProperty, value); }
            }

            #pragma endregion

            #pragma region Methods

            void Select(int start, int length);
            void SelectAll();
            void Clear();
            void Cut();
            void Copy();
            void Paste();
            void Undo();
            void Redo();

            #pragma endregion

            #pragma region Events

            event Windows::UI::Xaml::RoutedEventHandler^ TextChanged;
            event Windows::UI::Xaml::RoutedEventHandler^ SelectionChanged;

            #pragma endregion

        protected:
            virtual void OnApplyTemplate() override;
            virtual Windows::Foundation::Size MeasureOverride(Windows::Foundation::Size availableSize) override;
            virtual Windows::Foundation::Size ArrangeOverride(Windows::Foundation::Size finalSize) override;

            virtual void OnGotFocus(Windows::UI::Xaml::RoutedEventArgs^ e) override;
            virtual void OnLostFocus(Windows::UI::Xaml::RoutedEventArgs^ e) override;
            virtual void OnKeyDown(Windows::UI::Xaml::Input::KeyRoutedEventArgs^ e) override;
            virtual void OnKeyUp(Windows::UI::Xaml::Input::KeyRoutedEventArgs^ e) override;
            virtual void OnCharacterReceived(Windows::UI::Xaml::Input::CharacterReceivedRoutedEventArgs^ e) override;
            virtual void OnPointerPressed(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerMoved(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerReleased(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerCanceled(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerCaptureLost(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerEntered(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;
            virtual void OnPointerExited(Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e) override;

        private:
            static void EnsureDependencyProperties();

            static Windows::UI::Xaml::DependencyProperty^ _FontFaceProperty;
            static Windows::UI::Xaml::DependencyProperty^ _TypographyProperty;
            static Windows::UI::Xaml::DependencyProperty^ _StyleSimulationsProperty;
            static Windows::UI::Xaml::DependencyProperty^ _IsColorFontEnabledProperty;
            static Windows::UI::Xaml::DependencyProperty^ _TextProperty;
            static Windows::UI::Xaml::DependencyProperty^ _PlaceholderTextProperty;
            static Windows::UI::Xaml::DependencyProperty^ _PlaceholderForegroundProperty;
            static Windows::UI::Xaml::DependencyProperty^ _SelectionStartProperty;
            static Windows::UI::Xaml::DependencyProperty^ _SelectionLengthProperty;
            static Windows::UI::Xaml::DependencyProperty^ _IsReadOnlyProperty;
            static Windows::UI::Xaml::DependencyProperty^ _MaxLengthProperty;
            static Windows::UI::Xaml::DependencyProperty^ _TextAlignmentProperty;
            static Windows::UI::Xaml::DependencyProperty^ _TextWrappingProperty;
            static Windows::UI::Xaml::DependencyProperty^ _SelectionHighlightColorProperty;
            static Windows::UI::Xaml::DependencyProperty^ _CursorBrushProperty;
            static Windows::UI::Xaml::DependencyProperty^ _AcceptsReturnProperty;

            static void OnLayoutAffectingPropertyChanged(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            static void OnRenderAffectingPropertyChanged(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            static void OnTextChangedCallback(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);
            static void OnSelectionChangedCallback(Windows::UI::Xaml::DependencyObject^ d, Windows::UI::Xaml::DependencyPropertyChangedEventArgs^ e);

            void OnLoaded(Platform::Object^ sender, Windows::UI::Xaml::RoutedEventArgs^ e);
            void OnUnloaded(Platform::Object^ sender, Windows::UI::Xaml::RoutedEventArgs^ e);
            void UpdateTheme();
            void UpdateVisualStates(bool useTransitions);

            void CreateTextLayout();
            void RenderToSurface();
            void ReleaseDrawingSurface();
            void InvalidateLayoutAndRender();
            void InvalidateRenderOnly();
            void EnsureCaretVisible();

            // Caret blink
            void StartCaretBlink();
            void StopCaretBlink();
            void ResetCaretBlink();
            void OnCaretTimerTick(Platform::Object^ sender, Platform::Object^ e);

            // IME and Touch Keyboard
            void InitializeEditContext();
            void UninitializeEditContext();
            void OnTextRequested(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextTextRequestedEventArgs^ args);
            void OnSelectionRequested(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextSelectionRequestedEventArgs^ args);
            void OnTextUpdating(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextTextUpdatingEventArgs^ args);
            void OnSelectionUpdating(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextSelectionUpdatingEventArgs^ args);
            void OnFormatUpdating(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextFormatUpdatingEventArgs^ args);
            void OnLayoutRequested(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextLayoutRequestedEventArgs^ args);
            void OnCompositionStarted(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextCompositionStartedEventArgs^ args);
            void OnCompositionCompleted(Windows::UI::Text::Core::CoreTextEditContext^ sender, Windows::UI::Text::Core::CoreTextCompositionCompletedEventArgs^ args);
            void OnFocusRemoved(Windows::UI::Text::Core::CoreTextEditContext^ sender, Platform::Object^ args);

            // Text editing helpers
            void InsertText(Platform::String^ str);
            void DeleteSelectionOrChar(bool forward, bool byWord = false);
            void MoveCaret(int offset, bool extendSelection, bool byWord = false);
            void SetSelectionInternal(int start, int length, bool notifyEditContext = true);
            int FindWordBoundary(int position, bool forward);
            int HitTestPointToTextPosition(Windows::Foundation::Point point);
            Windows::Foundation::Rect GetCaretScreenRect();

            // Undo / Redo
            struct EditSnapshot
            {
                Platform::String^ Text;
                int SelectionStart;
                int SelectionLength;
            };
            void PushUndoState();
            std::vector<EditSnapshot> m_undoStack;
            std::vector<EditSnapshot> m_redoStack;

            // Context Menu
            void ShowContextMenu(Windows::Foundation::Point point);
            Windows::UI::Xaml::Controls::MenuFlyout^ m_contextMenu;

            // Composition and Direct2D resources
            Windows::UI::Composition::SpriteVisual^ m_spriteVisual;
            Windows::UI::Composition::CompositionSurfaceBrush^ m_surfaceBrush;
            Windows::UI::Composition::CompositionDrawingSurface^ m_drawingSurface;
            LONG m_renderedWidth;
            LONG m_renderedHeight;

            // DirectWrite layout
            Microsoft::WRL::ComPtr<IDWriteTextLayout> m_textLayout;
            Microsoft::WRL::ComPtr<IDWriteTextLayout> m_placeholderLayout;
            Microsoft::WRL::ComPtr<IDWriteFontFace> m_simulatedFontFace;

            // Template elements
            Windows::UI::Xaml::Controls::Border^ m_rootElement;
            Windows::UI::Xaml::Controls::Grid^ m_hitContainer;

            void WireContainerEvents();
            void OnContainerPointerPressed(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerMoved(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerReleased(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerCanceled(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerCaptureLost(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerEntered(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);
            void OnContainerPointerExited(Platform::Object^ sender, Windows::UI::Xaml::Input::PointerRoutedEventArgs^ e);

            // State
            bool m_isLayoutDirty;
            bool m_isRenderDirty;
            bool m_isFocused;
            bool m_isPointerOver;
            bool m_isCaretVisible;
            bool m_isPointerPressed;
            int m_selectionAnchor;
            float m_scrollOffsetX;
            float m_scrollOffsetY;
            bool m_internalTextUpdating;

            // IME composition
            bool m_isComposing;
            int m_compositionStart;
            int m_compositionLength;

            // Double / triple click tracking
            Windows::Foundation::DateTime m_lastPointerDownTime;
            Windows::Foundation::Point m_lastPointerDownPos;
            int m_clickCount;

            // Timer & Events tokens
            Windows::UI::Xaml::DispatcherTimer^ m_caretTimer;
            Windows::UI::Text::Core::CoreTextEditContext^ m_editContext;
            Windows::Foundation::EventRegistrationToken m_loadedToken;
            Windows::Foundation::EventRegistrationToken m_unloadedToken;
            Windows::Foundation::EventRegistrationToken m_themeToken;

            Windows::Foundation::EventRegistrationToken m_textRequestedToken;
            Windows::Foundation::EventRegistrationToken m_selectionRequestedToken;
            Windows::Foundation::EventRegistrationToken m_textUpdatingToken;
            Windows::Foundation::EventRegistrationToken m_selectionUpdatingToken;
            Windows::Foundation::EventRegistrationToken m_formatUpdatingToken;
            Windows::Foundation::EventRegistrationToken m_layoutRequestedToken;
            Windows::Foundation::EventRegistrationToken m_compositionStartedToken;
            Windows::Foundation::EventRegistrationToken m_compositionCompletedToken;
            Windows::Foundation::EventRegistrationToken m_focusRemovedToken;
        };
    }
}
