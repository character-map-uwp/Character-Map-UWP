#pragma once

#include "ITypographyInfo.h"
#include "CharacterGridViewTemplateSettings.h"
#include "CharacterGridViewTemplateSettings.h"
#include "GlyphAnnotation.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Windows::Foundation;
using namespace Windows::Foundation::Collections;
using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Core::Direct;
using namespace Windows::UI::Xaml::Markup;
using namespace Windows::UI::Xaml::Media;
using namespace Windows::UI::ViewManagement;

namespace CharacterMapCX
{
    [Windows::Foundation::Metadata::WebHostHidden]
	public ref class GridViewHelper sealed
	{
	public:
        static property UISettings^ UISettings
        {
            Windows::UI::ViewManagement::UISettings^ get() {
                if (m_settings == nullptr)
                    m_settings = ref new Windows::UI::ViewManagement::UISettings();

                return m_settings;
            }
        }

        static String^ GetAnnotation(ICharacter^ c, GlyphAnnotation a)
        {
            if (a == GlyphAnnotation::UnicodeIndex)
                return safe_cast<Object^>(c->UnicodeIndex)->ToString();

            if (a == GlyphAnnotation::UnicodeHex)
                return c->UnicodeString;

            return ref new String();
        }

        static void SetGlyphProperties(XamlDirect^ x, IXamlDirectObject^ o, CharacterGridViewTemplateSettings^ settings, ICharacter^ c)
        {
            if (o == nullptr || settings->FontFace == nullptr)
                return;

            x->SetObjectProperty(o, XamlPropertyIndex::TextBlock_FontFamily, settings->FontFamily);
            x->SetEnumProperty(o, XamlPropertyIndex::TextBlock_FontStretch, (unsigned int)settings->FontFace->Properties->Stretch);
            x->SetEnumProperty(o, XamlPropertyIndex::TextBlock_FontStyle, (unsigned int)settings->FontFace->Properties->Style);
            x->SetObjectProperty(o, XamlPropertyIndex::TextBlock_FontWeight, settings->FontFace->Properties->Weight);
            x->SetBooleanProperty(o, XamlPropertyIndex::TextBlock_IsColorFontEnabled, settings->ShowColorGlyphs);
            x->SetDoubleProperty(o, XamlPropertyIndex::TextBlock_FontSize, settings->Size / 2.0);

            UpdateColorFont(x, nullptr, o, settings->ShowColorGlyphs);


            if (settings->Typography == nullptr)
                SetTypography(o, CanvasTypographyFeatureName::None, x);
            else
                SetTypography(o, settings->Typography->Feature, x);

            x->SetStringProperty(o, XamlPropertyIndex::TextBlock_Text, c->Char);
        }

        static void UpdateColorFont(XamlDirect^ xamlDirect, TextBlock^ block, IXamlDirectObject^ xd, bool value)
        {
            if (xd != nullptr)
                xamlDirect->SetBooleanProperty(xd, XamlPropertyIndex::TextBlock_IsColorFontEnabled, value);
            else
                block->IsColorFontEnabled = value;
        }

        static void UpdateContainer(XamlDirect^ x, GridViewItem^ item, CharacterGridViewTemplateSettings^ settings, ICharacter^ c)
        {
            // Perf considerations:
            // 1 - Batch rendering updates by suspending rendering until all properties are set
            // 2 - Use XAML direct to set new properties, rather than through DP's
            // 3 - Access any required data properties from parents through normal properties, 
            //     not DP's - DP access can be order of magnitudes slower.
            // Note : This will be faster via C++ as it avoids all marshalling costs.
            // Note: For more improved performance, do **not** use XAML ItemTemplate.
            //       Create entire template via XamlDirect, and never directly reference the 
            //       WinRT XAML object.

            // Assumed Structure:
            // -- Grid
            //    -- TextBlock
            //    -- TextBlock

            //XamlBindingHelper::SuspendRendering(item);

            IXamlDirectObject^ b = x->GetXamlDirectObject(VisualTreeHelper::GetChild(VisualTreeHelper::GetChild(item, 0), 0));
            x->SetObjectProperty(b, XamlPropertyIndex::Border_BackgroundTransition, settings->BackgroundTransition);

            IXamlDirectObject^ go = x->GetXamlDirectObject(item->ContentTemplateRoot);
            x->SetObjectProperty(go, XamlPropertyIndex::FrameworkElement_Tag, c);
            x->SetDoubleProperty(go, XamlPropertyIndex::FrameworkElement_Width, settings->Size);
            x->SetDoubleProperty(go, XamlPropertyIndex::FrameworkElement_Height, settings->Size);

            IXamlDirectObject^ cld = x->GetXamlDirectObjectProperty(go, XamlPropertyIndex::Panel_Children);
            IXamlDirectObject^ o = x->GetXamlDirectObjectFromCollectionAt(cld, 0);
            SetGlyphProperties(x, o, settings, c);

            IXamlDirectObject^ o2 = x->GetXamlDirectObjectFromCollectionAt(cld, 1);
            if (o2 != nullptr)
            {
                switch (settings->Annotation)
                {
                case GlyphAnnotation::None:
                    x->SetEnumProperty(o2, XamlPropertyIndex::UIElement_Visibility, 1);
                    break;
                default:
                    x->SetStringProperty(o2, XamlPropertyIndex::TextBlock_Text, GetAnnotation(c, settings->Annotation));
                    x->SetEnumProperty(o2, XamlPropertyIndex::UIElement_Visibility, 0);
                    break;
                }
            }

            item->DataContext = c;

            //XamlBindingHelper::ResumeRendering(item);
        }

		static void ApplySize(XamlDirect^ _xamlDirect, GridView^ panel, double value)
		{
            XamlBindingHelper::SuspendRendering(panel);

            for (auto iter = panel->ItemsPanelRoot->Children->First(); iter->HasCurrent; iter->MoveNext())
            {
                auto item = dynamic_cast<GridViewItem^>(iter->Current);
                if (item == nullptr)
                    continue;

                IXamlDirectObject^ root = _xamlDirect->GetXamlDirectObject(item->ContentTemplateRoot);

                _xamlDirect->SetDoubleProperty(root, XamlPropertyIndex::FrameworkElement_Width, value);
                _xamlDirect->SetDoubleProperty(root, XamlPropertyIndex::FrameworkElement_Height, value);
                auto childs = _xamlDirect->GetXamlDirectObjectProperty(root, XamlPropertyIndex::Panel_Children);
                IXamlDirectObject^ tb = _xamlDirect->GetXamlDirectObjectFromCollectionAt(childs, 0);
                _xamlDirect->SetDoubleProperty(tb, XamlPropertyIndex::Control_FontSize, value / 2.0);
            }

            XamlBindingHelper::ResumeRendering(panel);
		}

        static void UpdateTypography(XamlDirect^ xamlDirect, IXamlDirectObject^ o, ITypographyInfo^ info)
        {
            CanvasTypographyFeatureName f = CanvasTypographyFeatureName::None;
            if (info != nullptr)
                f = info->Feature;

            SetTypography(o, f, xamlDirect);
        }

        static void SetTypography(IXamlDirectObject^ o, CanvasTypographyFeatureName f, XamlDirect^ x)
        {
            /* Set SWASHES */
            x->SetInt32Property(o, XamlPropertyIndex::Typography_StandardSwashes, f == CanvasTypographyFeatureName::Swash);
            x->SetInt32Property(o, XamlPropertyIndex::Typography_ContextualSwashes, f == CanvasTypographyFeatureName::ContextualSwash);

            /* Set ALTERNATES */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_AnnotationAlternates, f == CanvasTypographyFeatureName::AlternateAnnotationForms);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticAlternates, f == CanvasTypographyFeatureName::StylisticAlternates);

            /* Contextual Alternates applies to combinations of characters, and as such has no purpose here yet */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_ContextualAlternates, f == CanvasTypographyFeatureName::ContextualAlternates);

            /* Set MATHEMATICAL GREEK */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_MathematicalGreek, f == CanvasTypographyFeatureName::MathematicalGreek);

            /* Set FORMS */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_HistoricalForms, f == CanvasTypographyFeatureName::HistoricalForms);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_CaseSensitiveForms, f == CanvasTypographyFeatureName::CaseSensitiveForms);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_EastAsianExpertForms, f == CanvasTypographyFeatureName::ExpertForms);


            /* Set SLASHED ZERO */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_SlashedZero, f == CanvasTypographyFeatureName::SlashedZero);

            /* Set LIGATURES */
            /* Ligatures only apply to combinations of characters, and as such have no purpose here yet */
            // Set(XamlPropertyIndex.Typography_StandardLigatures, f == CanvasTypographyFeatureName.StandardLigatures);
            // Set(XamlPropertyIndex.Typography_ContextualLigatures, f == CanvasTypographyFeatureName.ContextualLigatures);
            // Set(XamlPropertyIndex.Typography_HistoricalLigatures, f == CanvasTypographyFeatureName.HistoricalLigatures);
            // Set(XamlPropertyIndex.Typography_DiscretionaryLigatures, f == CanvasTypographyFeatureName.DiscretionaryLigatures);

            /* Set CAPITALS */
            if (f == CanvasTypographyFeatureName::SmallCapitals)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::SmallCaps));
            else if (f == CanvasTypographyFeatureName::SmallCapitalsFromCapitals)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::AllSmallCaps));
            else if (f == CanvasTypographyFeatureName::PetiteCapitals)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::PetiteCaps));
            else if (f == CanvasTypographyFeatureName::PetiteCapitalsFromCapitals)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::AllPetiteCaps));
            else if (f == CanvasTypographyFeatureName::Titling)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::Titling));
            else if (f == CanvasTypographyFeatureName::Unicase)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::Unicase));
            else
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Capitals, static_cast<unsigned int>(FontCapitals::Normal));


            /* Set NUMERAL ALIGNMENT */
            /* Numeral Alignment only apply to combinations of characters, and as such have no purpose here yet */
            //if (f == CanvasTypographyFeatureName.ProportionalFigures)
            //    SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Proportional);
            //else if (f == CanvasTypographyFeatureName.TabularFigures)
            //    SetE(XamlPropertyIndex.Typography_NumeralAlignment, (uint)FontNumeralAlignment.Tabular);
            //else
            x->SetEnumProperty(o, XamlPropertyIndex::Typography_NumeralAlignment, static_cast<unsigned int>(FontNumeralAlignment::Normal));

            /* Set NUMERAL STYLE */
            if (f == CanvasTypographyFeatureName::OldStyleFigures)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_NumeralStyle, static_cast<unsigned int>(FontNumeralStyle::OldStyle));
            else if (f == CanvasTypographyFeatureName::LiningFigures)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_NumeralStyle, static_cast<unsigned int>(FontNumeralStyle::Lining));
            else
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_NumeralStyle, static_cast<unsigned int>(FontNumeralStyle::Normal));

            /* Set VARIANTS */
            if (f == CanvasTypographyFeatureName::Ordinals)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Ordinal));
            else if (f == CanvasTypographyFeatureName::Superscript)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Superscript));
            else if (f == CanvasTypographyFeatureName::Subscript)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Subscript));
            else if (f == CanvasTypographyFeatureName::RubyNotationForms)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Ruby));
            else if (f == CanvasTypographyFeatureName::ScientificInferiors)
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Inferior));
            else
                x->SetEnumProperty(o, XamlPropertyIndex::Typography_Variants, static_cast<unsigned int>(FontVariants::Normal));


            /* Set STLYISTIC SETS */
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet1, f == CanvasTypographyFeatureName::StylisticSet1);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet2, f == CanvasTypographyFeatureName::StylisticSet2);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet3, f == CanvasTypographyFeatureName::StylisticSet3);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet4, f == CanvasTypographyFeatureName::StylisticSet4);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet5, f == CanvasTypographyFeatureName::StylisticSet5);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet6, f == CanvasTypographyFeatureName::StylisticSet6);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet7, f == CanvasTypographyFeatureName::StylisticSet7);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet8, f == CanvasTypographyFeatureName::StylisticSet8);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet9, f == CanvasTypographyFeatureName::StylisticSet9);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet10, f == CanvasTypographyFeatureName::StylisticSet10);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet11, f == CanvasTypographyFeatureName::StylisticSet11);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet12, f == CanvasTypographyFeatureName::StylisticSet12);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet13, f == CanvasTypographyFeatureName::StylisticSet13);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet14, f == CanvasTypographyFeatureName::StylisticSet14);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet15, f == CanvasTypographyFeatureName::StylisticSet15);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet16, f == CanvasTypographyFeatureName::StylisticSet16);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet17, f == CanvasTypographyFeatureName::StylisticSet17);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet18, f == CanvasTypographyFeatureName::StylisticSet18);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet19, f == CanvasTypographyFeatureName::StylisticSet19);
            x->SetBooleanProperty(o, XamlPropertyIndex::Typography_StylisticSet20, f == CanvasTypographyFeatureName::StylisticSet20);
        }


        private:
            static Windows::UI::ViewManagement::UISettings^ m_settings;
	};
}