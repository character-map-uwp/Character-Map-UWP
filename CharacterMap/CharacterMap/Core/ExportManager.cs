using CharacterMap.ViewModels;
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
using Windows.UI;

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

        public static async Task ExportSvgAsync(
            ExportStyle style,
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            CanvasTypography typography)
        {
            try
            {
                string name = $"{selectedFont.Name} - {selectedVariant.PreferredName} - {selectedChar.UnicodeString}.svg";
                if (await PickFileAsync(name, "SVG", new[] { ".svg" }) is StorageFile file)
                {
                    CachedFileManager.DeferUpdates(file);
                    var device = CanvasDevice.GetSharedDevice();

                    var textColor = style == ExportStyle.Black ? Colors.Black : Colors.White;

                    /* SVG Exports render at fixed size - but a) they're vectors, andb) they're
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

                        using (CanvasGeometry temp = CanvasGeometry.CreateText(layout))
                        {
                            var b = temp.ComputeBounds();
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
                    }

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await SimpleIoc.Default.GetInstance<IDialogService>()
                    .ShowMessageBox(ex.Message, "Error Saving Image");
            }
        }

        public static async Task ExportPngAsync(
            ExportStyle style,
            InstalledFont selectedFont,
            FontVariant selectedVariant,
            Character selectedChar,
            CanvasTypography typography)
        {
            try
            {
                string name = $"{selectedFont.Name} - {selectedVariant.PreferredName} - {selectedChar.UnicodeString}.png";
                if (await PickFileAsync(name, "Png Image", new[] { ".png" }) 
                    is StorageFile file)
                {
                    CachedFileManager.DeferUpdates(file);
                    var device = CanvasDevice.GetSharedDevice();
                    var localDpi = 96; //Windows.Graphics.Display.DisplayInformation.GetForCurrentView().LogicalDpi;

                    var canvasH = (float)App.AppSettings.PngSize;
                    var canvasW = (float)App.AppSettings.PngSize;

                    var renderTarget = new CanvasRenderTarget(device, canvasW, canvasH, localDpi);

                    using (var ds = renderTarget.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        var d = App.AppSettings.PngSize;
                        var r = App.AppSettings.PngSize / 2;

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

                    await CachedFileManager.CompleteUpdatesAsync(file);
                }
            }
            catch (Exception ex)
            {
                await SimpleIoc.Default.GetInstance<IDialogService>()
                    .ShowMessageBox(ex.Message, "Error Saving Image");
            }
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
