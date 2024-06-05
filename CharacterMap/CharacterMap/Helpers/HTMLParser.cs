// Copyright (c) Rudy Huyn. All rights reserved.
// Licensed under the MIT License.
// Source: https://github.com/DotNetPlus/ReswPlus

// Updated 2024 by https://github.com/JohnnyWestlake

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers;

public static class HTMLParser
{
    public record HTMLParserArgs
    {
        public Brush Foreground { get; init; }
        public FontStyle FontStyle { get; init; }
        public FontFamily FontFamily { get; init; }
        public FontWeight FontWeight { get; init; }
        public FontVariants FontVariants { get; init; }
        public TextDecorations TextDecoration { get; init; }
    }


    private static IEnumerable<Inline> Parse(
        IEnumerable<XNode> nodes, 
        HTMLParserArgs args)
    {
        foreach (var current in nodes)
        {
            switch (current)
            {
                case XText xText:
                    Run run = new () { Text = xText.Value, FontWeight = args.FontWeight, FontStyle = args.FontStyle, TextDecorations = args.TextDecoration };
                    
                    if (args.FontFamily is not null)
                        run.FontFamily = args.FontFamily;

                    if (args.Foreground is not null)
                        run.Foreground = args.Foreground;

                    Typography.SetVariants(run, args.FontVariants);
                    yield return run;
                    break;
                case XElement xElement:
                    {
                        switch (xElement.Name.LocalName.ToLower())
                        {
                            case "b":
                            case "strong":
                                {
                                    FontWeight newFontWeight = FontWeights.Bold;
                                    if (xElement.Attribute("weight")?.Value is { Length: >0 } weight)
                                    {
                                        if (ushort.TryParse(weight, out ushort w))
                                            newFontWeight = new FontWeight { Weight = w };
                                        else
                                        {
                                            switch (weight.ToLower())
                                            {
                                                case "extrablack":
                                                    newFontWeight = FontWeights.ExtraBlack;
                                                    break;
                                                case "black":
                                                    newFontWeight = FontWeights.Black;
                                                    break;
                                                case "extrabold":
                                                    newFontWeight = FontWeights.ExtraBold;
                                                    break;
                                                case "bold":
                                                    newFontWeight = FontWeights.Bold;
                                                    break;
                                                case "semibold":
                                                    newFontWeight = FontWeights.SemiBold;
                                                    break;
                                                case "medium":
                                                    newFontWeight = FontWeights.Medium;
                                                    break;
                                                case "normal":
                                                    newFontWeight = FontWeights.Normal;
                                                    break;
                                                case "semilight":
                                                    newFontWeight = FontWeights.SemiLight;
                                                    break;
                                                case "light":
                                                    newFontWeight = FontWeights.Light;
                                                    break;
                                                case "extralight":
                                                    newFontWeight = FontWeights.ExtraLight;
                                                    break;
                                                case "thin":
                                                    newFontWeight = FontWeights.Thin;
                                                    break;
                                            }
                                        }
                                    }
                                   
                                    foreach (var item in Parse(xElement.Nodes(), args with { FontWeight = newFontWeight }))
                                        yield return item;
                                    break;
                                }
                            case "em":
                            case "i":
                            case "cite":
                            case "dfn":
                                foreach (var item in Parse(xElement.Nodes(), args with { FontStyle = FontStyle.Italic }))
                                    yield return item;
                                break;
                            case "u":
                                foreach (var item in Parse(xElement.Nodes(), args with { TextDecoration = TextDecorations.Underline }))
                                    yield return item;
                                break;
                            case "s":
                            case "strike":
                            case "del":
                                foreach (var item in Parse(xElement.Nodes(), args with { TextDecoration = TextDecorations.Strikethrough }))
                                    yield return item;
                                break;
                            case "font":
                                {
                                    Brush newFontColor = null;
                                    FontFamily newFontFamily = null;

                                    if (xElement.Attribute("color")?.Value is { Length: > 0 } colorStr)
                                    {
                                        try
                                        {
                                            var color = XamlBindingHelper.ConvertValue(typeof(Windows.UI.Color), colorStr) as Color?;
                                            if (color.HasValue)
                                                newFontColor = new SolidColorBrush() { Color = color.Value };
                                        }
                                        catch { }
                                    }

                                    if (xElement.Attribute("face")?.Value is { Length: >0 } faceStr)
                                        newFontFamily = new (faceStr);

                                    foreach (var item in Parse(xElement.Nodes(), args with { 
                                        FontFamily= newFontFamily ?? args.FontFamily,
                                        Foreground = newFontColor ?? args.Foreground
                                    }))
                                        yield return item;
                                }
                                break;
                            case "tt":
                                {
                                    foreach (var item in Parse(xElement.Nodes(), args with { FontFamily = new("Consolas") }))
                                        yield return item;
                                }
                                break;
                            case "sup":
                                {
                                    foreach (var item in Parse(xElement.Nodes(), args with { FontVariants = FontVariants.Superscript }))
                                        yield return item;
                                }
                                break;
                            case "sub":
                                {
                                    foreach (var item in Parse(xElement.Nodes(), args with { FontVariants = FontVariants.Subscript }))
                                        yield return item;
                                }
                                break;
                            case "br":
                                {
                                    yield return new LineBreak();
                                }
                                break;
                            case "a":
                                {
                                    if (xElement.Attribute("href")?.Value is {  Length: >0 } href)
                                    {
                                        Hyperlink hyperlink = new ()
                                        {
                                            NavigateUri = new (href),
                                            UnderlineStyle = UnderlineStyle.None
                                        };

                                        foreach (var item in Parse(xElement.Nodes(), args))
                                            hyperlink.Inlines.Add(item);

                                        yield return hyperlink;
                                    }
                                    else
                                    {
                                        //ignore the hyperlink
                                        foreach (var item in Parse(xElement.Nodes(), args))
                                            yield return item;
                                    }
                                }
                                break;
                            //case "ul":
                            //    {
                            //        if (xElement.Attribute("href")?.Value is { Length: > 0 } href)
                            //        {
                            //            Span span = new()
                            //            {
                                            
                            //            };

                            //            InlineUIContainer container = new InlineUIContainer()
                            //            {

                            //            }

                            //            foreach (var item in Parse(xElement.Nodes(), args))
                            //                hyperlink.Inlines.Add(item);

                            //            yield return span;
                            //        }
                            //        else
                            //        {
                            //            //ignore the hyperlink
                            //            foreach (var item in Parse(xElement.Nodes(), args))
                            //                yield return item;
                            //        }
                            //    }
                            //    break;
                            default:
                                //ignore the element
                                foreach (var item in Parse(xElement.Nodes(), args))
                                    yield return item;
                                break;
                        }
                    }
                    break;
            }
        }
    }

    public static IEnumerable<Inline> Parse(string source)
    {
        if (!string.IsNullOrEmpty(source))
        {
            XDocument xDocument = null;
            try
            {
                xDocument = XDocument.Parse($"<str>{source}</str>", LoadOptions.PreserveWhitespace);
            }
            catch
            {
            }
            if (xDocument == null)
            {
                yield return new Run() { Text = source };
            }
            else
            {
                HTMLParserArgs args = new()
                {
                    FontFamily = null,
                    Foreground = null,
                    FontStyle = FontStyle.Normal,
                    FontWeight = FontWeights.Normal,
                    TextDecoration = TextDecorations.None,
                    FontVariants = FontVariants.Normal
                };

                foreach (var item in Parse(((XElement)xDocument.FirstNode).Nodes(), args))
                    yield return item;
            }
        }
    }
}