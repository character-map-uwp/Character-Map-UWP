#pragma once

#include "ITypographyInfo.h"
#include "DWriteFontFace.h"
#include "GlyphAnnotation.h"

using namespace Microsoft::Graphics::Canvas;
using namespace Microsoft::Graphics::Canvas::Text;
using namespace Windows::Foundation;
using namespace Platform;
using namespace Windows::UI::Xaml;
using namespace Windows::UI::Xaml::Controls;
using namespace Windows::UI::Xaml::Core::Direct;
using namespace Windows::UI::Xaml::Markup;
using namespace Windows::UI::Xaml::Media;

namespace CharacterMapCX
{
    [Windows::Foundation::Metadata::WebHostHidden]
    public ref class CharacterGridViewTemplateSettings sealed
    {
    public:
        property FontFamily^ FontFamily;
        property DWriteFontFace^ FontFace;
        property ITypographyInfo^ Typography;
        property bool ShowColorGlyphs;
        property bool EnableReposition;
        property double Size;
        property BrushTransition^ BackgroundTransition;

        property GlyphAnnotation Annotation
        {
            GlyphAnnotation get() { return m_annotation; }
            void set(GlyphAnnotation value) { m_annotation = value; }
        }

    private:

        GlyphAnnotation m_annotation = GlyphAnnotation::None;
    };
}