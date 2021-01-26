using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Core.Direct;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    internal class CharacterGridViewTemplateSettings
    {
        public FontFamily FontFamily { get; set; }
        public CanvasFontFace FontFace { get; set; }
        public TypographyFeatureInfo Typography { get; set;}
        public bool ShowColorGlyphs { get; set; }
        public double Size { get; set; }
        public bool EnableReposition { get; set; }
        public GlyphAnnotation Annotation { get; set; }
    }


    public class CharacterGridView : GridView
    {
        #region Dependency Properties

        #region ItemSize

        public double ItemSize
        {
            get { return (double)GetValue(ItemSizeProperty); }
            set { SetValue(ItemSizeProperty, value); }
        }

        public static readonly DependencyProperty ItemSizeProperty =
            DependencyProperty.Register(nameof(ItemSize), typeof(double), typeof(CharacterGridView), new PropertyMetadata(0d, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.Size = (double)e.NewValue;
            }));

        #endregion

        #region ItemFontFamily

        public FontFamily ItemFontFamily
        {
            get { return (FontFamily)GetValue(ItemFontFamilyProperty); }
            set { SetValue(ItemFontFamilyProperty, value); }
        }

        public static readonly DependencyProperty ItemFontFamilyProperty =
            DependencyProperty.Register(nameof(ItemFontFamily), typeof(FontFamily), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.FontFamily = (FontFamily)e.NewValue;
            }));

        #endregion

        #region ItemFontFace

        public CanvasFontFace ItemFontFace
        {
            get { return (CanvasFontFace)GetValue(ItemFontFaceProperty); }
            set { SetValue(ItemFontFaceProperty, value); }
        }

        public static readonly DependencyProperty ItemFontFaceProperty =
            DependencyProperty.Register(nameof(ItemFontFace), typeof(CanvasFontFace), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                ((CharacterGridView)d)._templateSettings.FontFace = (CanvasFontFace)e.NewValue;
            }));

        #endregion

        #region ItemTypography

        public TypographyFeatureInfo ItemTypography
        {
            get { return (TypographyFeatureInfo)GetValue(ItemTypographyProperty); }
            set { SetValue(ItemTypographyProperty, value); }
        }

        public static readonly DependencyProperty ItemTypographyProperty =
            DependencyProperty.Register(nameof(ItemTypography), typeof(TypographyFeatureInfo), typeof(CharacterGridView), new PropertyMetadata(null, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is TypographyFeatureInfo t)
                {
                    g._templateSettings.Typography = t;
                    g.UpdateTypographies(t);
                }
            }));

        #endregion

        #region ShowColorGlyphs

        public bool ShowColorGlyphs
        {
            get { return (bool)GetValue(ShowColorGlyphsProperty); }
            set { SetValue(ShowColorGlyphsProperty, value); }
        }

        public static readonly DependencyProperty ShowColorGlyphsProperty =
            DependencyProperty.Register(nameof(ShowColorGlyphs), typeof(bool), typeof(CharacterGridView), new PropertyMetadata(false, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is bool b)
                {
                    g._templateSettings.ShowColorGlyphs = b;
                    g.UpdateColorsFonts(b);
                }
            }));

        #endregion

        #region ShowUnicodeDescription

        public GlyphAnnotation ItemAnnotation
        {
            get { return (GlyphAnnotation)GetValue(ItemAnnotationProperty); }
            set { SetValue(ItemAnnotationProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemAnnotation.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemAnnotationProperty =
            DependencyProperty.Register("ItemAnnotation", typeof(GlyphAnnotation), typeof(CharacterGridView), new PropertyMetadata(GlyphAnnotation.None, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is GlyphAnnotation b)
                {
                    g._templateSettings.Annotation = b;
                    g.UpdateUnicode(b);
                }
            }));




        #endregion

        #region EnableResizeAnimation

        public bool EnableResizeAnimation
        {
            get { return (bool)GetValue(EnableResizeAnimationProperty); }
            set { SetValue(EnableResizeAnimationProperty, value); }
        }

        public static readonly DependencyProperty EnableResizeAnimationProperty =
            DependencyProperty.Register(nameof(EnableResizeAnimation), typeof(bool), typeof(CharacterGridView), new PropertyMetadata(false, (d, e) =>
            {
                if (d is CharacterGridView g && e.NewValue is bool b)
                {
                    g._templateSettings.EnableReposition = b && Composition.UISettings.AnimationsEnabled;
                    g.UpdateAnimation(b);
                }
            }));

        #endregion

        #endregion

        private XamlDirect _xamlDirect = null;

        private CharacterGridViewTemplateSettings _templateSettings = null;

        private ImplicitAnimationCollection _repositionCollection = null;

        public CharacterGridView()
        {
            _xamlDirect = XamlDirect.GetDefault();
            _templateSettings = new CharacterGridViewTemplateSettings();

            this.ContainerContentChanging += OnContainerContentChanging;
            this.ChoosingItemContainer += OnChoosingItemContainer;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            /* 
             * For performance reasons, we've forgone XAML bindings and
             * will update everything in code 
             */
            if (!args.InRecycleQueue && args.ItemContainer is GridViewItem item)
            {
                Character c = ((Character)args.Item);
                UpdateContainer(item, c);
                args.Handled = true;
            }

            if (_templateSettings.EnableReposition)
            {
                if (args.InRecycleQueue)
                {
                    PokeUIElementZIndex(args.ItemContainer);
                }
                else
                {
                    var v = ElementCompositionPreview.GetElementVisual(args.ItemContainer);
                    v.ImplicitAnimations = EnsureRepositionCollection(v.Compositor);
                }
            }
        }




        #region Item Template Handling

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void UpdateContainer(GridViewItem item, Character c)
        {
            // Perf considerations:
            // 1 - Batch rendering updates by suspending rendering until all properties are set
            // 2 - Use XAML direct to set new properties, rather than through DP's
            // 3 - Access any required data properties from parents through normal properties, 
            //     not DP's - DP access can be order of magnitudes slower.
            // Note : This will be faster via C++ as it avoids all marshaling costs.
            // Note: For more improved performance, do **not** use XAML ItemTemplate.
            //       Create entire template via XamlDirect, and never directly reference the 
            //       WinRT XAML object.

            // Assumed Structure:
            // -- Grid
            //    -- TextBlock
            //    -- TextBlock

            XamlBindingHelper.SuspendRendering(item);

            IXamlDirectObject go = _xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot);

            _xamlDirect.SetObjectProperty(go, XamlPropertyIndex.FrameworkElement_Tag, c);
            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Width, _templateSettings.Size);
            _xamlDirect.SetDoubleProperty(go, XamlPropertyIndex.FrameworkElement_Height, _templateSettings.Size);

            IXamlDirectObject cld = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.Panel_Children);
            IXamlDirectObject o = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 0);
            SetGlyphProperties(_xamlDirect, o, _templateSettings, c);

            IXamlDirectObject o2 = _xamlDirect.GetXamlDirectObjectFromCollectionAt(cld, 1);
            if (o2 != null)
            {
                switch(_templateSettings.Annotation)
                {
                    case GlyphAnnotation.None:
                        _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 1);
                        break;
                    default:
                        _xamlDirect.SetStringProperty(o2, XamlPropertyIndex.TextBlock_Text, c.GetAnnotation(_templateSettings.Annotation));
                        _xamlDirect.SetEnumProperty(o2, XamlPropertyIndex.UIElement_Visibility, 0);
                        break;
                }
            }

            XamlBindingHelper.ResumeRendering(item);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetGlyphProperties(XamlDirect xamlDirect, IXamlDirectObject o, CharacterGridViewTemplateSettings templateSettings, Character c)
        {
            if (o == null)
                return;

            xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontFamily, templateSettings.FontFamily);
            xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStretch, (uint)templateSettings.FontFace.Stretch);
            xamlDirect.SetEnumProperty(o, XamlPropertyIndex.TextBlock_FontStyle, (uint)templateSettings.FontFace.Style);
            xamlDirect.SetObjectProperty(o, XamlPropertyIndex.TextBlock_FontWeight, templateSettings.FontFace.Weight);
            xamlDirect.SetBooleanProperty(o, XamlPropertyIndex.TextBlock_IsColorFontEnabled, templateSettings.ShowColorGlyphs);
            xamlDirect.SetDoubleProperty(o, XamlPropertyIndex.TextBlock_FontSize, templateSettings.Size / 2d);

            UpdateColorFont(xamlDirect, null, o, templateSettings.ShowColorGlyphs);
            UpdateTypography(xamlDirect, o, templateSettings.Typography);

            xamlDirect.SetStringProperty(o, XamlPropertyIndex.TextBlock_Text, c.Char);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void UpdateColorFont(XamlDirect xamlDirect, TextBlock block, IXamlDirectObject xd, bool value)
        {
            if (xd != null)
                xamlDirect.SetBooleanProperty(xd, XamlPropertyIndex.TextBlock_IsColorFontEnabled, value);
            else
                block.IsColorFontEnabled = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateTypography(XamlDirect xamlDirect, IXamlDirectObject o, TypographyFeatureInfo info)
        {
            CanvasTypographyFeatureName f = info == null ? CanvasTypographyFeatureName.None : info.Feature;
            TypographyBehavior.SetTypography(o, f, xamlDirect);
        }

        void UpdateColorsFonts(bool value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                {
                    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                    UpdateColorFont(_xamlDirect, null, tb, value);
                }
            }
        }

        void UpdateTypographies(TypographyFeatureInfo info)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                {
                    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 0);
                    UpdateTypography(_xamlDirect, tb, info);
                }
                //if (item.ContentTemplateRoot is Grid g)
                //{
                //    TextBlock tb = (TextBlock)g.Children[0];
                //    IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(tb);
                //    UpdateTypography(_xamlDirect, o, info);
                //}
            }
        }

        void UpdateUnicode(GlyphAnnotation value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                {
                    if (_xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.FrameworkElement_Tag) is Character c)
                    {
                        var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                        IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 1);
                        _xamlDirect.SetStringProperty(tb, XamlPropertyIndex.TextBlock_Text, c.GetAnnotation(value));
                        _xamlDirect.SetEnumProperty(tb, XamlPropertyIndex.UIElement_Visibility, value != GlyphAnnotation.None ? 0 : 1);
                    }
                }

                //if (item.ContentTemplateRoot is Grid g)
                //{
                //    if (g.Tag is Character c)
                //    {
                //        TextBlock tb = (TextBlock)g.Children[1];
                //        tb.Text = c.GetAnnotation(value);
                //        tb.SetVisible(value != GlyphAnnotation.None);
                //    }
                //}
            }
        }

        public void UpdateSize(double value)
        {
            ItemSize = value;
            if (this.Items.Count == 0 || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (_xamlDirect.GetXamlDirectObject(item.ContentTemplateRoot) is IXamlDirectObject root)
                {
                    _xamlDirect.SetDoubleProperty(root, XamlPropertyIndex.FrameworkElement_Width, value);
                    _xamlDirect.SetDoubleProperty(root, XamlPropertyIndex.FrameworkElement_Height, value);
                    var childs = _xamlDirect.GetXamlDirectObjectProperty(root, XamlPropertyIndex.Panel_Children);
                    IXamlDirectObject tb = _xamlDirect.GetXamlDirectObjectFromCollectionAt(childs, 1);
                    _xamlDirect.SetDoubleProperty(tb, XamlPropertyIndex.FrameworkElement_Height, value / 2d);
                }

                //if (item.ContentTemplateRoot is Grid g)
                //{
                //    g.Width = value;
                //    g.Height = value;
                //    ((TextBlock)g.Children[0]).FontSize = value / 2d;
                //}
            }
        }

        #endregion




        #region Reposition Animation

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (_templateSettings.EnableReposition && args.ItemContainer != null)
            {
                PokeUIElementZIndex(args.ItemContainer);
            }
        }

        private void UpdateAnimation(bool newValue)
        {
            if (this.ItemsPanelRoot == null)
                return;

            if (!Composition.UISettings.AnimationsEnabled)
                return;

            foreach (var item in this.ItemsPanelRoot.Children)
            {
                var v = ElementCompositionPreview.GetElementVisual(item);
                v.ImplicitAnimations = newValue ? EnsureRepositionCollection(v.Compositor) : null;
            }
        }

        private void PokeUIElementZIndex(UIElement e)
        {
            var o = _xamlDirect.GetXamlDirectObject(e);
            var i = _xamlDirect.GetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex);
            _xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i + 1);
            _xamlDirect.SetInt32Property(o, XamlPropertyIndex.Canvas_ZIndex, i);
        }

        private ImplicitAnimationCollection EnsureRepositionCollection(Compositor c)
        {
            if (_repositionCollection == null)
            {
                var offsetAnimation = c.CreateVector3KeyFrameAnimation();
                offsetAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");
                offsetAnimation.Duration = TimeSpan.FromSeconds(Composition.DefaultOffsetDuration);
                offsetAnimation.Target = nameof(Visual.Offset);

                var g = c.CreateAnimationGroup();
                g.Add(offsetAnimation);

                var s = c.CreateImplicitAnimationCollection();
                s.Add(nameof(Visual.Offset), g);
                _repositionCollection = s;
            }

            return _repositionCollection;
        }

        #endregion
    }
}
