using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
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

            var Settings = ResourceHelper.Get<AppSettings>(nameof(AppSettings));
            //Disable if preview pane is enable
            if (Settings.EnablePreviewPane)
            {
                //Disable the context flyout
                Windows.UI.Xaml.Style style = new Style(typeof(MenuFlyoutPresenter));
                style.Setters.Add(new Setter(MenuFlyoutPresenter.VisibilityProperty, Visibility.Collapsed));
                //.ContextFlyout
                IXamlDirectObject flyout = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.UIElement_ContextFlyout);
                _xamlDirect.SetObjectProperty(flyout, XamlPropertyIndex.MenuFlyout_MenuFlyoutPresenterStyle, style);
            }
            if (!Settings.EnablePreviewPane)
            {
                //Attemp adding bunch of bindings into Context Flyout item
                /* Copy character | [Separator] | Save PNG | Save SVG | [Separator] | Dev kit*/
                //Forge the command to send message
                RelayCommand<SaveAsPictureMessage> savemsg = new RelayCommand<SaveAsPictureMessage>((SaveAsPictureMessage msg) =>
                {
                    Messenger.Default.Send(msg);
                });
                RelayCommand<CopyToClipboardMessage> copymsg = new RelayCommand<CopyToClipboardMessage>((CopyToClipboardMessage msg) =>
                {
                    Messenger.Default.Send(msg);
                });
                //Data for export
                CanvasTextLayout layout = new CanvasTextLayout(Utils.CanvasDevice, c.Char, new CanvasTextFormat()
                {
                    FontSize = (float)Core.Converters.GetFontSize(Settings.GridSize),
                    FontFamily = _templateSettings.FontFamily.Source,
                    FontStretch = _templateSettings.FontFace.Stretch,
                    FontWeight = _templateSettings.FontFace.Weight,
                    FontStyle = _templateSettings.FontFace.Style,
                    HorizontalAlignment = CanvasHorizontalAlignment.Left,
                }, Settings.GridSize, Settings.GridSize)
                {
                    Options = CanvasDrawTextOptions.EnableColorFont
                };
                using var type = _templateSettings.Typography.GetEffectiveTypography();
                layout.SetTypography(0, 1, type);
                CharacterMapCX.CanvasTextLayoutAnalysis analysis = Utils.GetInterop().AnalyzeCharacterLayout(layout);

                //Insert Tooltip with name
                _xamlDirect.SetObjectProperty(go, XamlPropertyIndex.ToolTipService_ToolTip, GlyphService.GetCharacterDescription(c.UnicodeIndex, FontVariant.CreateDefault(_templateSettings.FontFace)));

                //.ContextFlyout
                IXamlDirectObject flyout = _xamlDirect.GetXamlDirectObjectProperty(go, XamlPropertyIndex.UIElement_ContextFlyout);
                //<MenuFlyout>
                IXamlDirectObject subFlyout = _xamlDirect.GetXamlDirectObjectProperty(flyout, XamlPropertyIndex.MenuFlyout_Items);
                //-Copy character
                IXamlDirectObject flyout_copyChar = _xamlDirect.GetXamlDirectObjectFromCollectionAt(subFlyout, 0);
                _xamlDirect.SetObjectProperty(flyout_copyChar, XamlPropertyIndex.MenuFlyoutItem_Command, copymsg);
                _xamlDirect.SetObjectProperty(flyout_copyChar, XamlPropertyIndex.MenuFlyoutItem_CommandParameter, new CopyToClipboardMessage(c));
                //-Save PNG
                IXamlDirectObject fo_spng = _xamlDirect.GetXamlDirectObjectFromCollectionAt(subFlyout, 2);
                //-Save PNG.Items
                IXamlDirectObject fo_spng_i = _xamlDirect.GetXamlDirectObjectProperty(fo_spng, XamlPropertyIndex.MenuFlyoutSubItem_Items);
                //Save PNG > Colored Glyph
                IXamlDirectObject fo_spng_cf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_spng_i, 0);
                //Save PNG > Colored Glyph.Visibility
                _xamlDirect.SetObjectProperty(fo_spng_cf, XamlPropertyIndex.UIElement_Visibility, analysis.HasColorGlyphs ? Visibility.Visible : Visibility.Collapsed);
                if (analysis.HasColorGlyphs)
                {
                    //Save PNG > Colored Glyph.Command & .CommandParameter
                    _xamlDirect.SetObjectProperty(fo_spng_cf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                    _xamlDirect.SetObjectProperty(fo_spng_cf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter, new SaveAsPictureMessage(c,
                        analysis,
                        SaveAsPictureMessage.SaveAs.PNG,
                        ExportStyle.ColorGlyph));
                }

                //Save PNG > Black Fill
                IXamlDirectObject fo_spng_bf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_spng_i, 1);
                _xamlDirect.SetObjectProperty(fo_spng_bf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                _xamlDirect.SetObjectProperty(fo_spng_bf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter, new SaveAsPictureMessage(c,
                    analysis,
                    SaveAsPictureMessage.SaveAs.PNG,
                    ExportStyle.Black));
                //Save PNG > White Fill
                IXamlDirectObject fo_spng_wf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_spng_i, 2);
                _xamlDirect.SetObjectProperty(fo_spng_wf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                _xamlDirect.SetObjectProperty(fo_spng_wf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                    new SaveAsPictureMessage(c,
                    analysis,
                    SaveAsPictureMessage.SaveAs.PNG,
                    ExportStyle.White));

                //-Save SVG
                IXamlDirectObject fo_ssvg = _xamlDirect.GetXamlDirectObjectFromCollectionAt(subFlyout, 3);
                //-Save SVG.Items
                IXamlDirectObject fo_ssvg_i = _xamlDirect.GetXamlDirectObjectProperty(fo_ssvg, XamlPropertyIndex.MenuFlyoutSubItem_Items);
                //Save SVG > Colored Glyph
                IXamlDirectObject fo_ssvg_cf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_ssvg_i, 0);
                //Save SVG > Colored Glyph.Visibility
                _xamlDirect.SetObjectProperty(fo_ssvg_cf, XamlPropertyIndex.UIElement_Visibility, analysis.HasColorGlyphs ? Visibility.Visible : Visibility.Collapsed);
                if (analysis.HasColorGlyphs)
                {
                    //Save SVG > Colored Glyph.Command & .CommandParameter
                    _xamlDirect.SetObjectProperty(fo_ssvg_cf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                    _xamlDirect.SetObjectProperty(fo_ssvg_cf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                        new SaveAsPictureMessage(c,
                        analysis,
                        SaveAsPictureMessage.SaveAs.SVG,
                        ExportStyle.ColorGlyph));
                }

                //Save SVG > Black Fill
                IXamlDirectObject fo_ssvg_bf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_ssvg_i, 1);
                _xamlDirect.SetObjectProperty(fo_ssvg_bf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                _xamlDirect.SetObjectProperty(fo_ssvg_bf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                    new SaveAsPictureMessage(c,
                    analysis,
                    SaveAsPictureMessage.SaveAs.SVG,
                    ExportStyle.Black));
                //Save SVG > White Fill
                IXamlDirectObject fo_ssvg_wf = _xamlDirect.GetXamlDirectObjectFromCollectionAt(fo_ssvg_i, 2);
                _xamlDirect.SetObjectProperty(fo_ssvg_wf, XamlPropertyIndex.MenuFlyoutItem_Command, savemsg);
                _xamlDirect.SetObjectProperty(fo_ssvg_wf, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                    new SaveAsPictureMessage(c,
                    analysis,
                    SaveAsPictureMessage.SaveAs.SVG,
                    ExportStyle.White));

                //Separator before devkit
                IXamlDirectObject dk_separator = _xamlDirect.GetXamlDirectObjectFromCollectionAt(subFlyout, 4);
                _xamlDirect.SetObjectProperty(dk_separator, XamlPropertyIndex.UIElement_Visibility, Settings.ShowDevUtils ? Visibility.Visible : Visibility.Collapsed);

                //Developer Utilities
                IXamlDirectObject dk = _xamlDirect.GetXamlDirectObjectFromCollectionAt(subFlyout, 5);
                _xamlDirect.SetObjectProperty(dk, XamlPropertyIndex.UIElement_Visibility, Settings.ShowDevUtils ? Visibility.Visible : Visibility.Collapsed);

                if (Settings.ShowDevUtils)
                {
                    //Developer Utilities.Items
                    IXamlDirectObject dk_items = _xamlDirect.GetXamlDirectObjectProperty(dk, XamlPropertyIndex.MenuFlyoutSubItem_Items);
                    //Developer Utilities > Glyph Code
                    IXamlDirectObject dk_glyph = _xamlDirect.GetXamlDirectObjectFromCollectionAt(dk_items, 0);
                    _xamlDirect.SetObjectProperty(dk_glyph, XamlPropertyIndex.MenuFlyoutItem_Command, copymsg);
                    _xamlDirect.SetObjectProperty(dk_glyph, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                        new CopyToClipboardMessage(CopyToClipboardMessage.MessageType.DevGlyph, c, analysis));
                    //Developer Utilities > Font Icon
                    IXamlDirectObject dk_font = _xamlDirect.GetXamlDirectObjectFromCollectionAt(dk_items, 1);
                    _xamlDirect.SetObjectProperty(dk_font, XamlPropertyIndex.MenuFlyoutItem_Command, copymsg);
                    _xamlDirect.SetObjectProperty(dk_font, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                        new CopyToClipboardMessage(CopyToClipboardMessage.MessageType.DevFont, c, analysis));
                    //Developer Utilities > Path Icon
                    IXamlDirectObject dk_path = _xamlDirect.GetXamlDirectObjectFromCollectionAt(dk_items, 2);
                    _xamlDirect.SetObjectProperty(dk_path, XamlPropertyIndex.MenuFlyoutItem_Command, copymsg);
                    _xamlDirect.SetObjectProperty(dk_path, XamlPropertyIndex.MenuFlyoutItem_CommandParameter,
                        new CopyToClipboardMessage(CopyToClipboardMessage.MessageType.DevPath, c, analysis));
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
                Grid g = (Grid)item.ContentTemplateRoot;
                TextBlock tb = (TextBlock)g.Children[0];
                UpdateColorFont(_xamlDirect, tb, null, value);
            }
        }

        void UpdateTypographies(TypographyFeatureInfo info)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (item.ContentTemplateRoot is Grid g)
                {
                    TextBlock tb = (TextBlock)g.Children[0];
                    IXamlDirectObject o = _xamlDirect.GetXamlDirectObject(tb);
                    UpdateTypography(_xamlDirect, o, info);
                }
            }
        }

        void UpdateUnicode(GlyphAnnotation value)
        {
            if (ItemsSource == null || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (item.ContentTemplateRoot is Grid g)
                {
                    if (g.Tag is Character c)
                    {
                        TextBlock tb = (TextBlock)g.Children[1];
                        tb.Text = c.GetAnnotation(value);
                        tb.SetVisible(value != GlyphAnnotation.None);
                    }
                }
            }
        }

        public void UpdateSize(double value)
        {
            ItemSize = value;
            if (this.Items.Count == 0 || ItemsPanelRoot == null)
                return;

            foreach (GridViewItem item in ItemsPanelRoot.Children.Cast<GridViewItem>())
            {
                if (item.ContentTemplateRoot is Grid g)
                {
                    g.Width = value;
                    g.Height = value;
                    ((TextBlock)g.Children[0]).FontSize = value / 2d;
                }
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
