// TODO : Implement to improve scrolling performance.
// Requires a lot of additional classes ported to C++/CX


//#pragma once
//
//#include <Microsoft.Graphics.Canvas.native.h>
//#include <d2d1_2.h>
//#include <d2d1_3.h>
//#include <dwrite_3.h>
//#include "ColorTextAnalyzer.h"
//#include "GlyphImageFormat.h"
//#include "DWriteFontSource.h"
//
//using namespace Microsoft::Graphics::Canvas;
//using namespace Microsoft::Graphics::Canvas::Text;
//using namespace Microsoft::WRL;
//using namespace Windows::Foundation;
//using namespace Windows::Foundation::Collections;
//using namespace Platform;
//using namespace Windows::UI::Xaml::Controls;
//using namespace Windows::UI::Xaml::Core::Direct;
//using namespace Windows::UI::Xaml::Markup;
//using namespace Windows::UI::Xaml::Media;
//
//namespace CharacterMapCX
//{
//
//    public ref class CharacterGridViewTemplateSettings sealed
//    {
//    public:
//
//        property FontFamily^ FontFamily
//        {
//            Windows::UI::Xaml::Media::FontFamily^ get() { return m_fontFamily; }
//        }
//
//        property CanvasFontFace^ FontFace
//        {
//            CanvasFontFace^ get() { return m_fontFace; }
//        }
//   /*     
//        FontFamily FontFamily{ get; set; }
//        public CanvasFontFace FontFace{ get; set; }
//        public TypographyFeatureInfo Typography{ get; set; }
//        public bool ShowColorGlyphs{ get; set; }
//        public double Size{ get; set; }
//        public bool EnableReposition{ get; set; }
//        public GlyphAnnotation Annotation{ get; set; }*/
//
//    private:
//
//        Windows::UI::Xaml::Media::FontFamily^ m_fontFamily;
//        CanvasFontFace^ m_fontFace;
//
//    }
//
//
//	public ref class GridViewHelper sealed
//	{
//	public:
//		static GridViewHelper Apply(XamlDirect^ _xamlDirect, GridViewItem^ item, String^ character, int index)
//		{
//            XamlBindingHelper::SuspendRendering(item);
//
//            IXamlDirectObject^ go = _xamlDirect->GetXamlDirectObject(item->ContentTemplateRoot);
//
//            _xamlDirect->SetObjectProperty(go, XamlPropertyIndex::FrameworkElement_Tag, c);
//            _xamlDirect->SetDoubleProperty(go, XamlPropertyIndex::FrameworkElement_Width, _templateSettings.Size);
//            _xamlDirect->SetDoubleProperty(go, XamlPropertyIndex::FrameworkElement_Height, _templateSettings.Size);
//
//            IXamlDirectObject^ cld = _xamlDirect->GetXamlDirectObjectProperty(go, XamlPropertyIndex::Panel_Children);
//            IXamlDirectObject^ o = _xamlDirect->GetXamlDirectObjectFromCollectionAt(cld, 0);
//            SetGlyphProperties(_xamlDirect, o, _templateSettings, c);
//
//            IXamlDirectObject^ o2 = _xamlDirect->GetXamlDirectObjectFromCollectionAt(cld, 1);
//            if (o2 != null)
//            {
//                switch (_templateSettings.Annotation)
//                {
//                case GlyphAnnotation.None:
//                    _xamlDirect->SetEnumProperty(o2, XamlPropertyIndex::UIElement_Visibility, 1);
//                    break;
//                case GlyphAnnotation.UnicodeHex:
//                    _xamlDirect->SetStringProperty(o2, XamlPropertyIndex::TextBlock_Text, c.UnicodeString);
//                    _xamlDirect->SetEnumProperty(o2, XamlPropertyIndex::UIElement_Visibility, 0);
//                    break;
//                case GlyphAnnotation.UnicodeIndex:
//                    _xamlDirect->SetStringProperty(o2, XamlPropertyIndex::TextBlock_Text, c.UnicodeIndex.ToString());
//                    _xamlDirect->SetEnumProperty(o2, XamlPropertyIndex::UIElement_Visibility, 0);
//                    break;
//                }
//            }
//
//
//            XamlBindingHelper::ResumeRendering(item);
//		}
//	};
//}