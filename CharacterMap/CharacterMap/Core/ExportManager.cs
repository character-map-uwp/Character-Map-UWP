using CharacterMap.Helpers;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMapCX;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Views;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Svg;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace CharacterMap.Core
{
    public enum ExportStyle
    {
        Black,
        White,
        ColorGlyph
    }

    public static class ExportManager
    {
        private static async Task<StorageFile> PickFileAsync(string fileName, string key, IList<string> values)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop
            };
            savePicker.FileTypeChoices.Add(key, values);
            savePicker.SuggestedFileName = fileName;

            try
            {
                return await savePicker.PickSaveFileAsync();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static byte[] GetGlyphBytes(CanvasFontFace fontface, int unicodeIndex, int imageType)
        {
            Interop interop = SimpleIoc.Default.GetInstance<Interop>();
            IBuffer buffer = interop.GetImageDataBuffer(fontface, 1024, (uint)unicodeIndex, (uint)imageType);
            using (DataReader reader = DataReader.FromBuffer(buffer))
            {
                byte[] bytes = new byte[buffer.Length];
                reader.ReadBytes(bytes);
                return bytes;
            }
        }

        public static async Task ExportSvgAsync(
            ExportStyle style,
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            CanvasTextLayoutAnalysis analysis,
            CanvasTypography typography)
        {
            try
            {
                string name = GetFileName(selectedFont, selectedVariant, selectedChar, "svg");
                if (await PickFileAsync(name, "SVG", new[] { ".svg" }) is StorageFile file)
                {
                    CachedFileManager.DeferUpdates(file);
                   
                    CanvasDevice device = Utils.CanvasDevice;
                    Color textColor = style == ExportStyle.Black ? Colors.Black : Colors.White;

                    /* SVG Exports render at fixed size - but a) they're vectors, and b) they're
                     * inside an auto-scaling viewport. So rendersize is *largely* pointless */
                    float canvasH = 1024f, canvasW = 1024f, fontSize = 1024f;

                    using (CanvasTextLayout layout = new CanvasTextLayout(device, $"{selectedChar.Char}", new CanvasTextFormat
                    {
                        FontSize = fontSize,
                        FontFamily = selectedVariant.Source,
                        FontStretch = selectedVariant.FontFace.Stretch,
                        FontWeight = selectedVariant.FontFace.Weight,
                        FontStyle = selectedVariant.FontFace.Style,
                        HorizontalAlignment = CanvasHorizontalAlignment.Center
                    }, canvasW, canvasH))
                    {
                        layout.SetTypography(0, 1, typography);
                        layout.Options = analysis.GlyphFormats.Contains(GlyphImageFormat.Svg) ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default;

                        using (CanvasGeometry temp = CanvasGeometry.CreateText(layout))
                        {
                            var b = temp.ComputeBounds();

                            async Task SaveMonochromeAsync()
                            {
                                double scale = Math.Min(1, Math.Min(canvasW / b.Width, canvasH / b.Height));

                                Matrix3x2 transform =
                                    Matrix3x2.CreateTranslation(new Vector2((float)-b.Left, (float)-b.Top))
                                    * Matrix3x2.CreateScale(new Vector2((float)scale));

                                using (CanvasGeometry geom = temp.Transform(transform))
                                {
                                    /* 
                                     * Unfortunately this only constructs a monochrome path, if we want color
                                     * Win2D does not yet expose the neccessary API's to get the individual glyph
                                     * layers that make up a colour glyph.
                                     * 
                                     * We'll need to handle this in C++/CX if we want to do this at some point.
                                     */

                                    SVGPathReciever rc = new SVGPathReciever();
                                    geom.SendPathTo(rc);

                                    Rect bounds = geom.ComputeBounds();
                                    using (CanvasSvgDocument document = Utils.GenerateSvgDocument(device, bounds.Width, bounds.Height, rc))
                                    {
                                        ((CanvasSvgNamedElement)document.Root.FirstChild).SetColorAttribute("fill", textColor);
                                        await Utils.WriteSvgAsync(document, file);
                                    }
                                }
                            }

                            // If the font uses SVG glyphs, we can extract the raw SVG from the font file
                            if (analysis.GlyphFormats.Contains(GlyphImageFormat.Svg))
                            {
                                byte[] bytes = GetGlyphBytes(selectedVariant.FontFace, selectedChar.UnicodeIndex, 8);
                                string str = Encoding.UTF8.GetString(bytes);
                                if (str.StartsWith("<?xml"))
                                    str = str.Remove(0, str.IndexOf(">") + 1);

                                str = str.TrimStart();

                                // We need to transform the SVG to fit within the default document bounds, as characters
                                // are based *above* the base origin of (0,0) as (0,0) is the Baseline (bottom left) position for a character, 
                                // so by default a will appear out of bounds of the default SVG viewport (towards top left).
                                try
                                {
                                    using (CanvasSvgDocument document = CanvasSvgDocument.LoadFromXml(Utils.CanvasDevice, str))
                                    {
                                        // We'll regroup all the elements inside a "g" / group tag,
                                        // and apply a transform to the "g" tag to try and put in 
                                        // in the correct place. There's probably a more accurate way
                                        // to do this by directly setting the root viewBox, if anyone
                                        // can find the correct calculation...

                                        List<ICanvasSvgElement> elements = new List<ICanvasSvgElement>();

                                        double minTop = 0;
                                        double minLeft = double.MaxValue;
                                        double maxWidth = double.MinValue;
                                        double maxHeight = double.MinValue;

                                        void ProcessChildren(CanvasSvgNamedElement root)
                                        {
                                            CanvasSvgNamedElement ele = root.FirstChild as CanvasSvgNamedElement;
                                            while (true)
                                            {
                                                CanvasSvgNamedElement next = root.GetNextSibling(ele) as CanvasSvgNamedElement;
                                                if (ele.Tag == "g")
                                                {
                                                    ProcessChildren(ele);
                                                }
                                                else if (ele.Tag == "path")
                                                {
                                                    // Create a XAML geometry to try and find the bounds of each character
                                                    // Probably more efficient to do in Win2D, but far less code to do with XAML.
                                                    Geometry gm = XamlBindingHelper.ConvertValue(typeof(Geometry), ele.GetStringAttribute("d")) as Geometry;
                                                    minTop = Math.Min(minTop, gm.Bounds.Top);
                                                    minLeft = Math.Min(minLeft, gm.Bounds.Left);
                                                    maxWidth = Math.Max(maxWidth, gm.Bounds.Width);
                                                    maxHeight = Math.Max(maxHeight, gm.Bounds.Height);
                                                }

                                                ele = next;
                                                if (ele == null)
                                                    break;
                                            }
                                        }

                                        ProcessChildren(document.Root);

                                        double top = minTop < 0 ? minTop : 0;
                                        double left = minLeft;
                                        document.Root.SetRectangleAttribute("viewBox", new Rect(left, top, b.Width, b.Height));
                                        await Utils.WriteSvgAsync(document, file);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Certain fonts seem to have their SVG glyphs encoded with... I don't evne know what encoding.
                                    // for example: https://github.com/adobe-fonts/emojione-color
                                    // In these cases, fallback to monochrome black
                                    await SaveMonochromeAsync();
                                }

                            }
                            else
                            {
                                await SaveMonochromeAsync();
                            }
                        }
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await SimpleIoc.Default.GetInstance<IDialogService>()
                    .ShowMessageBox(ex.Message, Localization.Get("SaveImageError"));
            }
        }

        public static async Task ExportPngAsync(
            ExportStyle style,
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            CanvasTextLayoutAnalysis analysis,
            CanvasTypography typography,
            AppSettings settings)
        {
            try
            {
                string name = GetFileName(selectedFont, selectedVariant, selectedChar, "png");
                if (await PickFileAsync(name, "Png Image", new[] { ".png" }) is StorageFile file)
                {
                    CachedFileManager.DeferUpdates(file);

                    if (analysis.GlyphFormats.Contains(GlyphImageFormat.Png))
                    {
                        byte[] bytes = GetGlyphBytes(selectedVariant.FontFace, selectedChar.UnicodeIndex, 16);
                        await FileIO.WriteBytesAsync(file, bytes);
                    }
                    else
                    {
                        var device = Utils.CanvasDevice;
                        var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                        var canvasH = (float)settings.PngSize;
                        var canvasW = (float)settings.PngSize;

                        var renderTarget = new CanvasRenderTarget(device, canvasW, canvasH, localDpi);

                        using (var ds = renderTarget.CreateDrawingSession())
                        {
                            ds.Clear(Colors.Transparent);
                            var d = settings.PngSize;
                            var r = settings.PngSize / 2;

                            var textColor = style == ExportStyle.Black ? Colors.Black : Colors.White;
                            var fontSize = (float)d;

                            using (CanvasTextLayout layout = new CanvasTextLayout(device, $"{selectedChar.Char}", new CanvasTextFormat
                            {
                                FontSize = fontSize,
                                FontFamily = selectedVariant.Source,
                                FontStretch = selectedVariant.FontFace.Stretch,
                                FontWeight = selectedVariant.FontFace.Weight,
                                FontStyle = selectedVariant.FontFace.Style,
                                HorizontalAlignment = CanvasHorizontalAlignment.Center,
                                Options = style == ExportStyle.ColorGlyph ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default
                            }, canvasW, canvasH))
                            {
                                if (style == ExportStyle.ColorGlyph)
                                    layout.Options = CanvasDrawTextOptions.EnableColorFont;

                                layout.SetTypography(0, 1, typography);

                                var db = layout.DrawBounds;
                                double scale = Math.Min(1, Math.Min(canvasW / db.Width, canvasH / db.Height));
                                var x = -db.Left + ((canvasW - (db.Width * scale)) / 2d);
                                var y = -db.Top + ((canvasH - (db.Height * scale)) / 2d);

                                ds.Transform =
                                    Matrix3x2.CreateTranslation(new Vector2((float)x, (float)y))
                                    * Matrix3x2.CreateScale(new Vector2((float)scale));

                                ds.DrawTextLayout(layout, new Vector2(0), textColor);
                            }
                        }

                        using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            fileStream.Size = 0;
                            await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
                        }
                    }
                    

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await SimpleIoc.Default.GetInstance<IDialogService>()
                    .ShowMessageBox(ex.Message, Localization.Get("SaveImageError"));
            }
        }

        private static string GetFileName(
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            string ext)
        {
            var chr = GlyphService.GetCharacterDescription(selectedChar.UnicodeIndex, selectedVariant) ?? selectedChar.UnicodeString;
            return $"{selectedFont.Name} {selectedVariant.PreferredName} - {chr}.{ext}";
        }





        //private static Task RenderAsync(
        //    StorageFile file,
        //    ExportStyle style,
        //    InstalledFont selectedFont,
        //    FontVariant selectedVariant,
        //    Character selectedChar,
        //    Action<CanvasTextLayout, Matrix3x2> render)
        //{
        //    CachedFileManager.DeferUpdates(file);
        //    var device = CanvasDevice.GetSharedDevice();

        //    var canvasH = (float)App.AppSettings.PngSize;
        //    var canvasW = (float)App.AppSettings.PngSize;

        //    var d = App.AppSettings.PngSize;
        //    var r = App.AppSettings.PngSize / 2;

        //    var textColor = style == ExportStyle.Black ? Colors.Black : Colors.White;
        //    var fontSize = (float)d;

        //    using (CanvasTextLayout layout = new CanvasTextLayout(device, $"{selectedChar.Char}", new CanvasTextFormat
        //    {
        //        FontSize = fontSize,
        //        FontFamily = selectedVariant.XamlFontFamily.Source,
        //        FontStretch = selectedVariant.FontFace.Stretch,
        //        FontWeight = selectedVariant.FontFace.Weight,
        //        FontStyle = selectedVariant.FontFace.Style,
        //        HorizontalAlignment = CanvasHorizontalAlignment.Center
        //    }, canvasW, canvasH))
        //    {
        //        var db = layout.DrawBounds;
        //        double scale = Math.Min(1, Math.Min(canvasW / db.Width, canvasH / db.Height));
        //        var x = -db.Left + ((canvasW - (db.Width * scale)) / 2d);
        //        var y = -db.Top + ((canvasH - (db.Height * scale)) / 2d);

        //        Matrix3x2 transform =
        //            Matrix3x2.CreateTranslation(new Vector2((float)x, (float)y))
        //            * Matrix3x2.CreateScale(new Vector2((float)scale));

        //        render(layout, transform);
        //    }

        //    return CachedFileManager.CompleteUpdatesAsync(file).AsTask();
        //}
    }
}
