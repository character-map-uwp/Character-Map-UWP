using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Core;

public static class XamlDirectExtensions
{
    extension(XamlDirect d)
    {
        public XamlDirectWrapper GetWrapper(UIElement element)
        {
            return new XamlDirectWrapper(element, d);
        }

        public XamlDirectWrapper GetWrapperForChild(Windows.UI.Xaml.Controls.Panel element, uint index)
        {
            return new XamlDirectWrapper(element.Children[(int)index], d);
        }

        public XamlDirectWrapper GetWrapperForCollectionIndex(IXamlDirectObject collection, uint index, UIElement source = null)
        {
            return new XamlDirectWrapper(d.GetXamlDirectObjectFromCollectionAt(collection, index), d) { Source = source };
        }
    }
}

public class XamlDirectWrapper
{
    public XamlDirect X { get; }
    public IXamlDirectObject Object { get; }
    public UIElement Source { get; init; }

    /// <summary>
    /// Forces XamlDirect to use TextBlock properties, rather than generic UIElement properties
    /// </summary>
    public bool IsTextBlock { get; set; }

    public XamlDirectWrapper(UIElement element, XamlDirect x)
    {
        X = x;
        Object = x.GetXamlDirectObject(element);
        Source = element;

        IsTextBlock = element is Windows.UI.Xaml.Controls.TextBlock;
    }

    public XamlDirectWrapper(IXamlDirectObject d, XamlDirect x)
    {
        X = x;
        Object = d;
    }

    public XamlDirectWrapper SetDouble(XamlPropertyIndex index, double value)
    {
        X.SetDoubleProperty(Object, index, value);
        return this;
    }

    public XamlDirectWrapper SetEnum(XamlPropertyIndex index, uint value)
    {
        X.SetEnumProperty(Object, index, value);
        return this;
    }

    public XamlDirectWrapper SetObject(XamlPropertyIndex index, object value)
    {
        X.SetObjectProperty(Object, index, value);
        return this;
    }

    public XamlDirectWrapper SetString(XamlPropertyIndex index, string value)
    {
        X.SetStringProperty(Object, index, value);
        return this;
    }

    public XamlDirectWrapper SetBoolean(XamlPropertyIndex index, bool value)
    {
        X.SetBooleanProperty(Object, index, value);
        return this;
    }

    public XamlDirectWrapper SetVisibility(bool visible) => SetEnum(XamlPropertyIndex.UIElement_Visibility, visible ? 0u : 1u);
    public XamlDirectWrapper SetWidth(double size) => SetDouble(XamlPropertyIndex.FrameworkElement_Width, size);
    public XamlDirectWrapper SetHeight(double size) => SetDouble(XamlPropertyIndex.FrameworkElement_Height, size);

    public XamlDirectWrapper SetFontFamily(FontFamily family) => SetObject(IsTextBlock ? XamlPropertyIndex.TextBlock_FontFamily : XamlPropertyIndex.Control_FontFamily, family);
    public XamlDirectWrapper SetFontSize(double size) => SetDouble(IsTextBlock ? XamlPropertyIndex.TextBlock_FontSize : XamlPropertyIndex.Control_FontSize, size);
    public XamlDirectWrapper SetFontStretch(FontStretch stretch) => SetEnum(IsTextBlock ? XamlPropertyIndex.TextBlock_FontStretch : XamlPropertyIndex.Control_FontStretch, (uint)stretch);
    public XamlDirectWrapper SetFontStyle(FontStyle style) => SetEnum(IsTextBlock ? XamlPropertyIndex.TextBlock_FontStyle : XamlPropertyIndex.Control_FontStyle, (uint)style);
    public XamlDirectWrapper SetFontWeight(FontWeight weight) => SetObject(IsTextBlock ? XamlPropertyIndex.TextBlock_FontWeight : XamlPropertyIndex.Control_FontWeight, weight);
}
