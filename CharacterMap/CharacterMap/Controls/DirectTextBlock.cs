using CharacterMap.Core;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Controls
{
    public sealed class DirectTextBlock : Control
    {
        public CanvasFontFace FontFace
        {
            get { return (CanvasFontFace)GetValue(FontFaceProperty); }
            set { SetValue(FontFaceProperty, value); }
        }

        public static readonly DependencyProperty FontFaceProperty =
            DependencyProperty.Register(nameof(FontFace), typeof(CanvasFontFace), typeof(DirectTextBlock), new PropertyMetadata(null, (d, e) =>
            {
                ((DirectTextBlock)d).Update();
            }));


        public TypographyFeatureInfo Typography
        {
            get { return (TypographyFeatureInfo)GetValue(TypographyProperty); }
            set { SetValue(TypographyProperty, value); }
        }

        public static readonly DependencyProperty TypographyProperty =
            DependencyProperty.Register(nameof(Typography), typeof(TypographyFeatureInfo), typeof(DirectTextBlock), new PropertyMetadata(null, (d, e) =>
            {
                ((DirectTextBlock)d).Update();
            }));

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(DirectTextBlock), new PropertyMetadata(null, (d,e) =>
            {
                ((DirectTextBlock)d).Update();
            }));

        private CanvasControl m_canvas = null;
        private CanvasTextLayout m_layout = null;
        bool m_isStale = true;
        bool m_render = true;

        public DirectTextBlock()
        {
            this.DefaultStyleKey = typeof(DirectTextBlock);
            this.Loaded += DirectTextBlock_Loaded;
        }

        private void DirectTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            Update();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (this.GetTemplateChild("TextCanvas") is CanvasControl canvas)
            {
                m_canvas = canvas;

                m_canvas.Draw -= _canvas_Draw;
                m_canvas.CreateResources -= _canvas_CreateResources;

                m_canvas.Draw += _canvas_Draw;
                m_canvas.CreateResources += _canvas_CreateResources;
            }

            Update();
        }

        private void _canvas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            Update();
        }

        void Update()
        {
            m_canvas?.Invalidate();
        }

        //protected override Size MeasureOverride(Size availableSize)
        //{
        //    if (FontFace == null || Typography == null || m_canvas == null || !m_canvas.ReadyToDraw)
        //        return base.MeasureOverride(availableSize);


        //    if (m_layout == null || m_isStale)
        //    {
        //        m_isStale = false;

        //        if (m_layout != null)
        //            m_layout?.Dispose();

        //        var text = Text;

        //        /* 
        //            FILTER UNSUPPORTED CHARACTERS.
        //            - This is a bad way of doing this, should be done with
        //              custom text renderer
        //        */
        //        /*    if (UnicodeIndex > 0)
        //            {
        //                wchar_t* newData = new wchar_t[2];
        //                newData[0] = UnicodeIndex;
        //                text = ref new Platform::String(newData, 1);
        //            }
        //            else
        //            {
        //                text = Text;

        //                auto data = text->Data();
        //                auto l = text->Length();
        //                wchar_t* newData = new wchar_t[l];
        //                for (int i = 0; i < l; i++)
        //                {
        //                    wchar_t c = data[i];
        //                    if (fontFace->HasCharacter(c))
        //                        newData[i] = c;
        //                    else
        //                        newData[i] = 0;
        //                }

        //                text = ref new Platform::String(newData, l);
        //            }*/

        //        var format = new CanvasTextFormat();
        //        format.FontFamily = FontFamily.Source;
        //        format.FontSize = (float)FontSize;
        //        format.FontWeight = FontWeight;
        //        format.FontStyle = FontStyle;
        //        format.FontStretch = FontStretch;
        //        format.Options = CanvasDrawTextOptions.EnableColorFont | CanvasDrawTextOptions.Clip;

        //        var typography = new CanvasTypography();

        //        if (Typography.Feature != CanvasTypographyFeatureName.None)
        //            typography.AddFeature(Typography.Feature, 1);

        //        var device = m_canvas.Device;
        //        var layout = new CanvasTextLayout(device, text, format, (float)availableSize.Width, (float)availableSize.Height);
        //        layout.SetTypography(0, layout.LineMetrics[0].CharacterCount, typography);
        //        layout.Options = CanvasDrawTextOptions.EnableColorFont | CanvasDrawTextOptions.Clip;

        //        m_layout = layout;
        //        m_render = true;
        //    }

        //    var minh = Math.Min(m_layout.DrawBounds.Top, m_layout.LayoutBounds.Top);
        //    var maxh = Math.Max(m_layout.DrawBounds.Bottom, m_layout.LayoutBounds.Bottom);

        //    var minw = Math.Min(m_layout.DrawBounds.Left, m_layout.LayoutBounds.Left);
        //    var maxw = Math.Max(m_layout.DrawBounds.Right, m_layout.LayoutBounds.Right);

        //    var targetsize = new Size(Math.Ceiling(maxw - minw), Math.Ceiling(maxh - minh));
        //    return targetsize;
        //}

        protected override Size ArrangeOverride(Size finalSize)
        {
           try
            {
                return base.ArrangeOverride(finalSize);

            }
            finally
            {
                if (m_render)
                {
                    m_canvas?.Invalidate();
                }
            }
        }

        private void _canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            //if (m_layout == null)
            //    return;

            //m_render = false;

            //var offset = new Vector2(
            //    (float)-Math.Min(m_layout.DrawBounds.Left, m_layout.LayoutBounds.Left),
            //    (float)-Math.Min(m_layout.DrawBounds.Top, m_layout.LayoutBounds.Top));

            var format = new CanvasTextFormat();
            format.FontFamily = FontFamily.Source;
            format.FontSize = (float)FontSize;
            format.FontWeight = FontWeight;
            format.FontStyle = FontStyle;
            format.FontStretch = FontStretch;
            format.Options = CanvasDrawTextOptions.EnableColorFont | CanvasDrawTextOptions.Clip;

            args.DrawingSession.DrawText(Text, 0, 0, Colors.Black, format);
            //args.DrawingSession.DrawTextLayout(
            //    m_layout, offset, Colors.Green);
            //args.DrawingSession.Flush();
        }
    }
}
