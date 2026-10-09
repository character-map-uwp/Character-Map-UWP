using CharacterMap.Controls;
using CharacterMap.Views;
using System.Linq;
using System.Reflection;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using static CharacterMap.Helpers.FlyoutHelper;

namespace CharacterMap.Helpers;


public static class FlyoutHelper
{
    private static UserCollectionsService _collections { get; } = Ioc.Default.GetService<UserCollectionsService>();

    public static void RequestDelete(CMFontFamily font)
    {
        MainViewModel main = Ioc.Default.GetService<MainViewModel>();
        var d = new ContentDialog
        {
            Title = Localization.Get("DlgDeleteFont/Title"),
            IsPrimaryButtonEnabled = true,
            IsSecondaryButtonEnabled = true,
            PrimaryButtonText = Localization.Get("Delete"),
            SecondaryButtonText = Localization.Get("Cancel"),
        };

        d.PrimaryButtonClick += (ds, de) =>
        {
            MainPage.MainDispatcher.Enqueue(() => main.TryRemoveFont(font));
        };
        _ = d.ShowAsync();
    }

    public static void PrintRequested()
    {
        WeakReferenceMessenger.Default.Send(new PrintRequestedMessage());
    }


    /// <summary>
    /// Creates the context menu for the Font List or the "..." button.
    /// Both of these have a font as their main target.
    /// </summary>
    /// <param name="menu"></param>
    /// <param name="font"></param>
    /// <param name="variant"></param>
    /// <param name="headerContent"></param>
    public static void CreateMenu(
        MenuFlyout menu,
        CMFontFamily font,
        CharacterRenderingOptions options,
        FrameworkElement headerContent,
        FlyoutArgs args)
    {
        bool standalone = args.Standalone;
        bool showAdvanced = args.ShowAdvanced;
        bool isExternalFile = args.IsExternalFile;

        #region Handlers 

        static void OpenInNewWindow(object s, RoutedEventArgs args)
        {
            if (s is FrameworkElement { Tag: CMFontFamily fnt } f
                && Properties.GetTag(f) is CharacterRenderingOptions o)
                _ = FontMapView.CreateNewViewForFontAsync(fnt, null, o);
        }

        static void OpenInNewTab(object s, RoutedEventArgs args)
        {
            if (s is FrameworkElement { Tag: CMFontFamily fnt })
                WeakReferenceMessenger.Default.Send(new OpenTabMessage(fnt));
        }

        static void SaveFont_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && Properties.GetTag(item) is CharacterRenderingOptions opts)
            {
                ExportManager.RequestExportFont(opts, true);
            }
        }

        static void Print_Click(object sender, RoutedEventArgs e)
        {
            PrintRequested();
        }

        static void Export_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: CMFontFamily fnt } f
                && Properties.GetTag(f) is CharacterRenderingOptions o)
            {
                WeakReferenceMessenger.Default.Send(new ExportRequestedMessage());
            }
        }

        static void DeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem { Tag: CMFontFamily fnt })
            {
                RequestDelete(fnt);
            }
        }

        static void AddToQuickCompare(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f
                && Properties.GetTag(f) is CharacterRenderingOptions o)
            {
                _ = QuickCompareView.AddAsync(o);
            }
        }

        static void AddToQuickCompareMulti(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f
                && Properties.GetTag(f) is CharacterRenderingOptions o)
            {
                _ = QuickCompareView.AddAsync(o, true);
            }
        }

        void OpenCalligraphy(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f
                && Properties.GetTag(f) is CharacterRenderingOptions o)
            {
                _ = CalligraphyView.CreateWindowAsync(o, args?.PreviewText);
            }
        }

        static void OpenFaceCompare(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: CMFontFamily fnt })
            {
                _ = QuickCompareView.CreateWindowAsync(new(false, new(fnt.Variants.ToList()) { IsFamilyCompare = true }));
            }
        }

        #endregion

        MenuFlyoutFactory factory = new (menu, args);

        if (menu.Items != null)
        {
            // HORRIBLE Hacks, because MenuFlyoutSubItem never updates it's UI tree after the first
            // render meaning we can't dynamically update items. Instead we need to make an entirely
            // menu every time it opens.

            factory.Clear();
            MenuFlyoutSubItem coll;
            MenuFlyoutFactory.CreateArgs arg = new() { Tag = options.Family, PropertyTag = options };
            bool qc = args.IsFolderView is false && isExternalFile is false;
            factory.AddHeaderObject(headerContent, out _);

            // 1. Add "Open in New Tab/Window" buttons
            if (!standalone)
            {
                // 1.1. Only show "Open in New Tab" if this is Font List context menu
                //      and supported by theme
                if (showAdvanced is false && ResourceHelper.SupportsTabs)
                    factory.Create("OpenInNewTab/Text", ThemeIcon.NewTab, OpenInNewTab, arg);

                // 1.2. Create "Open in New Window"
                MenuFlyoutItem newWindow = factory.Create("OpenInNewWindow/Text", ThemeIcon.NewWindow, OpenInNewWindow, arg);
                if (showAdvanced)
                    newWindow.AddKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control);
            }

            // 2. Add Save Font File & Export Font Glyphs options
            if (options != null && options.Face != null && DirectWrite.IsFontLocal(options.Face.Face))
            {
                factory.Create("ExportFontFileLabel/Text", ThemeIcon.Save, SaveFont_Click, arg.WithKey(VirtualKey.S));
                if (showAdvanced && !args.IsTabContext)
                    factory.Create("ExportCharactersLabel/Text", ThemeIcon.Save, Export_Click, arg.WithKey(VirtualKey.Q));
            }

            // 3. Add "Add to quick compare" button if we're viewing a variant
            if (qc)
                factory.Create("AddToQuickCompare/Text", ThemeIcon.AddTo, AddToQuickCompare, arg.WithKey(VirtualKey.E));

            // 4. Add "Add to Collection" button
            if (isExternalFile is false && args.IsFolderView is false)
            {
                coll = FlyoutHelper.AddCollectionItems(menu, font, null, args: args);
                coll.Style = factory.DefaultSubItemStyle;
            }

            // 5. Add "Remove from Collection" item
            // Only show the "Remove from Collection" menu item if:
            //  -- we are not in a stand-alone window
            //  AND
            //  -- we are in a custom collection
            //  OR 
            //  -- we are in the Symbol Font collection, and this is a font that 
            //     the user has manually tagged as a symbol font
            if (!standalone && !args.IsFolderView)
            {
                MainViewModel main = Ioc.Default.GetService<MainViewModel>();
                TryAddRemoveFromCollection(menu, font, main.SelectedCollection, main.FontListFilter);
            }

            // 6. Add "Print" Button
            if (showAdvanced && args.IsTabContext is false)
            {
                if (Windows.Graphics.Printing.PrintManager.IsSupported())
                    factory.Create("BtnPrint/Content", ThemeIcon.Print, Print_Click, arg with { AcceleratorKey = VirtualKey.P, Index = standalone ? 2 : 3 });
            }

            // 7. Add "Delete Font" button
            if (!standalone
                && !args.IsFolderView
                && !args.IsTabContext
                && font.HasImportedFiles)
            {
                menu.AddSeparator();
                MenuFlyoutItem del = factory.Create("RemoveFontFlyout/Text", ThemeIcon.Delete, DeleteClick, arg);
                if (showAdvanced)
                    del.AddKeyboardAccelerator(VirtualKey.Delete, VirtualKeyModifiers.Control);
            }

            // 8. Handle compare options
            if (font.HasVariants)
                menu.AddSeparator();

            if (font.HasVariants)
            {
                // 8.1. Add "Compare Fonts button"
                // NOTE: count is not used on updated translation, left because old translations may still use it
                factory.Create($"~{string.Format(Localization.Get("CompareFacesCountLabel/Text"), font.Variants.Count)}", ThemeIcon.CompareFonts, OpenFaceCompare, arg);

                // 8.2. Add "Add all to quick compare" button
                factory.Create("AddMultiToQuickCompare/Text", ThemeIcon.Add, AddToQuickCompareMulti, arg);
            }

            // 9. Add Calligraphy button
            menu.AddSeparator();
            factory.Create("CalligraphyLabel/Text", ThemeIcon.Calligraphy, OpenCalligraphy, arg.WithKey(VirtualKey.I));
        }
    }

    public static T SetAttachedTag<T>(this T item, object o) where T : MenuFlyoutItemBase
    {
        Properties.SetTag(item, o);
        return item;
    }

    public static T SetAnimation<T>(this T item) where T : MenuFlyoutItemBase
    {
        if (ResourceHelper.AllowAnimation && ResourceHelper.SupportFluentAnimation)
        {
            //Properties.SetPointerOverAnimation(item, "IconRoot");
            Properties.SetClickAnimationOffset(item, 0.95);
            FluentAnimation.SetPointerOverAxis(item, Orientation.Horizontal);

            Properties.SetPointerPressedAnimation(item, "ContentRoot|Scale");
            Properties.SetClickAnimation(item, "ContentRoot|Scale");
        }

        return item;
    }

    public static void TryAddRemoveFromCollection(MenuFlyout menu, CMFontFamily font, IFontCollection col, BasicFontFilter filter)
    {
        if (col is not UserFontCollection collection)
            return;

        if ((collection != null || (filter == BasicFontFilter.SymbolFonts && !font.IsSymbolFont))
            && collection.Fonts.Contains(font.Name))
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");

            MenuFlyoutItem removeItem = new()
            {
                Text = Localization.Get("RemoveFromCollectionItem/Text"),
                Icon = ThemeIconGlyph.CreateIcon(ThemeIcon.Remove),
                Tag = collection == null && filter == BasicFontFilter.SymbolFonts ? _collections.SymbolCollection : collection,
                DataContext = font,
                Style = style
            };
            removeItem.Click += RemoveFrom_Click;
            menu.Items.Add(removeItem);

            async void RemoveFrom_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement { DataContext: CMFontFamily fnt, Tag: UserFontCollection collection })
                {
                    await _collections.RemoveFromCollectionAsync(fnt, collection);
                        WeakReferenceMessenger.Default.Send(
                            new AppNotificationMessage(true,
                                new CollectionUpdatedArgs([fnt], collection, false)));
                    WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage { SourceCollection = collection });
                }
            }
        }
    }

    /// <summary>
    /// Adds the "Add To Collection" item to a menu with all user collections
    /// and the ability to create a new collection.
    /// </summary>
    /// <param name="menu"></param>
    /// <param name="font"></param>
    /// <returns></returns>
    public static MenuFlyoutSubItem AddCollectionItems(MenuFlyout menu, CMFontFamily font, IReadOnlyList<CMFontFamily> fonts, string key = null, FlyoutArgs args = null)
    {
        #region Event Handlers

        static async void AddToSymbolFonts_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && Properties.GetTag(f) is IReadOnlyList<CMFontFamily> fnts)
            {
                var result = await _collections.AddToCollectionAsync(
                    fnts,
                    _collections.SymbolCollection,
                    f.Tag as Action);

                WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
                if (result.Success)
                {
                    WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));
                }
            }
        }

        static void CreateCollection_Click(object sender, RoutedEventArgs e)
        {
            _ = new CreateCollectionDialog()
                   .SetDataContext(Properties.GetTag((FrameworkElement)sender))
                   .ShowAsync();
        }

        #endregion

        bool multiMode = font is null && fonts is not null;
        IReadOnlyList<CMFontFamily> items = fonts ?? [font];
        Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
        Style substyle = ResourceHelper.Get<Style>("ThemeMenuFlyoutSubItemStyle");

        // 1. Add "Add To Collection" item
        MenuFlyoutSubItem parent = new()
        {
            Text = Localization.Get(key ?? "AddToCollectionFlyout/Text"),
            Icon = ThemeIconGlyph.CreateIcon(ThemeIcon.Collections),
            Style = substyle
        };

        // 2. Add "New Collection" Item
        MenuFlyoutItem newCollection = new()
        {
            Text = Localization.Get("NewCollectionItem/Text"),
            Icon = ThemeIconGlyph.CreateIcon(ThemeIcon.Add),
            Style = style
        };

        newCollection.Click += CreateCollection_Click;
        newCollection.SetAttachedTag(items);

        if (parent.Items != null)
        {
            parent.Items.Add(newCollection.SetAnimation());

            // 3. Create "Symbol Font" item
            if (font is null || !font.IsSymbolFont)
            {
                parent.Items.Add(new MenuFlyoutSeparator());

                MenuFlyoutItem symb = new()
                {
                    Text = Localization.Get("OptionSymbolFonts/Text"),
                    IsEnabled = multiMode || !_collections.SymbolCollection.Fonts.Contains(font.Name),
                    Style = style,
                    Tag = args?.AddToCollectionCommand
                };
                symb.SetAttachedTag(items);
                symb.Click += AddToSymbolFonts_Click;
                parent.Items.Add(symb);
            }
        }

        menu.Items.Add(parent);

        // 4. Add items for each user Collection
        if (_collections.Items.Count > 0)
        {
            if (parent.Items != null)
            {
                parent.Items.Add(new MenuFlyoutSeparator());
                foreach (var m in
                        _collections.Items.Select(item => new MenuFlyoutItem
                        {
                            Tag = item,
                            Text = item.Name,
                            Style = style,
                            IsEnabled = multiMode || !item.Fonts.Contains(font.Name)
                        }.SetAttachedTag(items).SetAnimation()))
                {
                    if (m.IsEnabled)
                    {
                        m.Click += async (s, a) =>
                        {
                            if (s is FrameworkElement f
                                && Properties.GetTag(f) is IReadOnlyList<CMFontFamily> fnts
                                && f.Tag is UserFontCollection clct)
                            {
                                AddToCollectionResult result = await _collections.AddToCollectionAsync(fnts, clct);

                                if (result.Success)
                                {
                                    WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));
                                }
                            }
                        };
                    }

                    parent.Items.Add(m);
                }
            }
        }

        return parent;
    }

    /// <summary>
    /// Creates the context menu for the Character Map grid
    /// </summary>
    /// <param name="menu"></param>
    /// <param name="target"></param>
    /// <param name="viewmodel"></param>
    public static void ShowCharacterGridContext(MenuFlyout menu, FrameworkElement target, FontMapView view, bool isStandalone, object context = null)
    {
        T Child<T>(string name) where T : MenuFlyoutItemBase => menu.Items.OfType<T>().FirstOrDefault(c => c.Name == name);
        context ??= target.Tag;

        var viewmodel = view.ViewModel;

        if (context is uint or int)
            context = new GlyphCharacter(Convert.ToUInt16(context));

        if (context is GlyphCharacter gc && !gc.IsValidUnicode && viewmodel.SelectedFace is not null && viewmodel.SelectedFace.TryGetCharacterForGlyph(gc.GlyphIndex, out Character mapped))
        {
            gc = new(gc.GlyphIndex, gc.PaletteIndex, gc.Color, mapped.UnicodeIndex);
            context = gc;
        }

        if (context is Character c)
        {
            // 1. Attach the flyout to the selected grid item and apply the correct context
            FlyoutBase.SetAttachedFlyout(target, menu);

            // 2. Analyse the character to know which options we should show in the menu
            DWriteTextLayoutAnalysis analysis = 
                viewmodel.SelectedChar.GetCharAnalysis(c, viewmodel.SelectedFaceAnalysis.ActiveFace);

            MenuFlyoutFactory factory = new(menu, new FlyoutArgs { Standalone = isStandalone });
            FlyoutContextArg arg = new() { ParentView = view, Character = c, Analysis = analysis };

            MenuFlyoutItem add = factory.Child<MenuFlyoutItem>("AddSelectionButton")
                                        .SetVisible(c is not GlyphCharacter);

            // 3. Handle copy options
            // 3.1. Remove existing
            while (menu.Items[0] != add)
                menu.Items.RemoveAt(0);

            // 3.2. Use the factory to rebuild the menu
            var copyAsImage = factory
                .CreateSubItem(ThemeIcon.Copy, "CopyAsImageItem/Text", insertIndex: 0)
                .AddHeader("CopyAsPngItem/Text", "Ctrl+Alt+C")
                .AddColorOptions(analysis, true, arg with { CopyType = CopyDataType.PNG });

            if (analysis.IsFullVectorBased)
            {
                copyAsImage
                    .AddHeader("CopyAsSvgItem/Text", "Ctrl+Shift+C")
                    .AddColorOptions(analysis, true, arg with { CopyType = CopyDataType.SVG });
            }

            // 3.3. Add the "Copy as Text" item to the top of the menu if supported.
            //      Some glyph don't map to a Unicode character, and so can't be copied as text.
            LigatureModel mappedLigature = null;
            if (context is not GlyphCharacter gc2 || (gc2.IsValidUnicode || viewmodel.SelectedFaceAnalysis.TryGetLigature(gc2.GlyphIndex, out mappedLigature)))
            {
                // If a glyph maps to a ligature, we copy the ligature text instead
                object tag = (object)mappedLigature ?? arg;
                factory.Create("BtnCopy/Text", ThemeIcon.Copy, MenuFlyoutFactory.CopyHandler, new() { Index = 0, Tag = tag, PropertyTag = view });

            }

            //// 3. Handle PNG options
            //var pngRoot = Child<MenuFlyoutSubItem>("PngRoot");
            //foreach (var child in pngRoot.Items.OfType<MenuFlyoutItem>())
            //{
            //    if (child.CommandParameter is ExportStyle s && s == ExportStyle.ColorGlyph)
            //        child.SetVisible(analysis.HasColorGlyphs);
            //    else
            //        child.SetVisible(!analysis.ContainsBitmapGlyphs); // Bitmap glyphs must *always* be saved as colour version
            //}

            //// 4. Handle SVG options
            //var svgRoot = Child<MenuFlyoutSubItem>("SvgRoot");

            //// 4.1. We can only save as SVG if all layers of the glyph are created with vectors
            //svgRoot.SetVisible(analysis.IsFullVectorBased);
            //if (analysis.IsFullVectorBased)
            //{
            //    // Glyphs that are actually stored as individual SVG files inside a font, and not
            //    // typical font vector data, must always be saved as colourised / raw SVG.
            //    bool svgChar = analysis.GlyphFormats.Has(GlyphImageFormat.Svg);

            //    foreach (var child in svgRoot.Items.OfType<MenuFlyoutItem>())
            //    {
            //        if (child.CommandParameter is ExportStyle s && s == ExportStyle.ColorGlyph)
            //        {
            //            child.Text = svgChar ? Localization.Get("ExportSVGGlyphLabel/Text") : Localization.Get("ColoredGlyphLabel/Text");
            //            child.SetVisible(svgChar || (analysis.IsFullVectorBased && analysis.HasColorGlyphs));
            //        }
            //        else
            //        {
            //            child.SetVisible(!svgChar);
            //        }
            //    }
            //}

            //// 4.2. Relabel the "Copy as SVG" coloured item for SVG-based chars (e.g. Noto Color Emoji)
            ////      to say "SVG Glyph" instead of "Coloured", matching the Save SVG submenu behaviour.
            //if (Child<MenuFlyoutItem>("CopySvgColouredItem") is { } copySvgItem)
            //{
            //    bool svgChar = analysis.GlyphFormats.Has(GlyphImageFormat.Svg);
            //    copySvgItem.Text = svgChar
            //        ? Localization.Get("ExportSVGGlyphLabel/Text")
            //        : Localization.Get("ColoredGlyphLabel/Text");
            //}

            // 4.3. Find in other fonts is only supported in MainView right now
            Child<MenuFlyoutItem>("FindCharButton")?.SetVisible(!isStandalone && context is not GlyphCharacter);


            // 5. Handle Dev values
            var devRoot = Child<MenuFlyoutSubItem>("DevRoot");
            if (devRoot is not null && context is not GlyphCharacter)
            {
                devRoot.SetVisible(true);

                // 5.0. Prepare click handler
                static void CopyItemClick(object sender, RoutedEventArgs e)
                {
                    if (sender is MenuFlyoutItem item
                        && Properties.GetDevOption(item) is DevOption option)
                    {
                        Utils.CopyToClipboard(option.Value);
                        WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, Localization.Get("NotificationCopied"), 2000));
                    }
                }

                // 5.1. Get providers for the grid character
                var options = viewmodel.RenderingOptions with { Typography = viewmodel.TypographyFeatures.Select(f => f.Feature).ToList(), Axis = viewmodel.SelectedFaceAnalysis.VariationAxis.Copy() };
                var providers = DevProviderBase.GetProviders(options, c);

                // 5.2. Create child items.
                //      As menus only update their visual tree once and ignore
                //      any future updates, we can only do this once.
                if (devRoot.Items.Count == 0)
                {
                    foreach (var p in providers.Where(p => p.Type != DevProviderType.None))
                    {
                        MenuFlyoutSubItem item = new() { Text = p.DisplayName };
                        foreach (var o in p.GetAllOptions())
                        {
                            MenuFlyoutItem i = new() { Text = Localization.Get("ContextMenuDevCopyCommand", o.Name), Style = factory.DefaultItemStyle };
                            i.Click += CopyItemClick;
                            Properties.SetDevOption(i, o);
                            item.Items.Add(i);
                        }
                        devRoot.Items.Add(item);
                    }
                }

                // 5.3. Update data in child items.
                foreach (var item in devRoot.Items.Cast<MenuFlyoutSubItem>())
                {
                    if (context is GlyphCharacter)
                    {
                        item.SetVisible(false);
                        continue;
                    }

                    item.SetVisible(true);
                    var p = providers.FirstOrDefault(p => p.DisplayName == item.Text);
                    var ops = p.GetContextOptions();
                    foreach (var child in item.Items.Cast<MenuFlyoutItem>())
                    {
                        var o = ops.FirstOrDefault(o => o.Name == Properties.GetDevOption(child)?.Name);
                        if (o != null)
                            Properties.SetDevOption(child, o);
                        child.SetVisible(o is not null);
                    }
                }
            }
            else
                devRoot?.SetVisible(false);

            // 6.1. Handle visibility of "Add to Selection" Button
            Child<MenuFlyoutItem>("AddSelectionButton")?
                .SetVisible(ResourceHelper.AppSettings.EnableCopyPane && context is not GlyphCharacter);

            // 6.2.
            Child<MenuFlyoutItem>("CalligraphyButton")?.SetVisible(context is not GlyphCharacter);

            // 6.3.
            bool canCopyText = context is not GlyphCharacter || (context is GlyphCharacter { IsValidUnicode: true });
            Child<MenuFlyoutItem>("CopyItem")?.SetVisible(canCopyText);

            // 7. Set item context
            menu.SetItemsDataContext(context, factory.DefaultSubItemStyle);

            // 7. Show complete flyout
            FlyoutBase.ShowAttachedFlyout(target);
        }
    }

    public static void SetItemsDataContext(this MenuFlyout flyout, object dataContext, Style subStyle = null)
    {
        static void SetContext(IList<MenuFlyoutItemBase> items, object context, Style subStyle)
        {
            foreach (var item in items)
            {
                if (item is MenuFlyoutSubItem sub)
                {
                    if (subStyle is not null)
                        sub.Style = subStyle;

                    SetContext(sub.Items, context, subStyle);
                }

                item.DataContext = context;
            }
        }

        SetContext(flyout.Items, dataContext, subStyle);
    }

    public static T SetCommandParameters<T>(this T flyout, object dataContext, Style subStyle = null) where T: MenuFlyout
    {
        static void SetContext(IList<MenuFlyoutItemBase> items, object context, Style subStyle)
        {
            foreach (var item in items)
            {
                if (item is MenuFlyoutSubItem sub)
                {
                    if (subStyle is not null)
                        sub.Style = subStyle;

                    SetContext(sub.Items, context, subStyle);
                }

                if (item is MenuFlyoutItem mfi && mfi.Command is not null)
                    mfi.CommandParameter = context;
            }
        }

        SetContext(flyout.Items, dataContext, subStyle);
        return flyout;
    }

    //public static string GetGlyphFormatLabel(FaceAnalysisModel model, Character c)
    //{
    //    if (model == null || c == null)
    //        return string.Empty;

    //    List<string> formats = [];

    //    try
    //    {
    //        ushort glyphIndex = c is GlyphCharacter gc
    //            ? gc.GlyphIndex
    //            : (ushort)model.Face.GetGlyphIndex(c);

    //        if (model.Face != null
    //            && Utils.GetInterop().AnalyzeGlyphLayout(model.Face.Face, glyphIndex) is { } analysis
    //            && analysis.GlyphFormats != null
    //            && analysis.GlyphFormats.Count > 0)
    //        {
    //            foreach (var fmt in analysis.GlyphFormats)
    //            {
    //                switch (fmt)
    //                {
    //                    case GlyphImageFormat.Svg:
    //                        if (!formats.Contains("SVG")) formats.Add("SVG");
    //                        break;
    //                    case GlyphImageFormat.Colr:
    //                        FontAnalysis colrFa = model.Analysis;
    //                        string colrVer = colrFa != null && colrFa.COLRVersion >= 1 ? "COLRv1" : "COLRv0";
    //                        if (!formats.Contains(colrVer)) formats.Add(colrVer);
    //                        break;
    //                    case GlyphImageFormat.Png:
    //                    case GlyphImageFormat.Jpeg:
    //                    case GlyphImageFormat.Tiff:
    //                    case GlyphImageFormat.PremultipliedB8G8R8A8:
    //                        if (!formats.Contains("Bitmap")) formats.Add("Bitmap");
    //                        break;
    //                    case GlyphImageFormat.TrueType:
    //                        if (!formats.Contains("TTF")) formats.Add("TTF");
    //                        break;
    //                    case GlyphImageFormat.Cff:
    //                        if (!formats.Contains("CFF")) formats.Add("CFF");
    //                        break;
    //                }
    //            }
    //        }
    //    }
    //    catch { }

    //    if (formats.Count == 0 && model.Analysis is { } fa)
    //    {
    //        if (fa.HasCOLRGlyphs && fa.COLRVersion >= 1) formats.Add("COLRv1");
    //        else if (fa.HasSVGGlyphs) formats.Add("SVG");
    //        else if (fa.HasCOLRGlyphs) formats.Add("COLRv0");
    //        else if (fa.HasBitmapGlyphs) formats.Add("Bitmap");
    //        else formats.Add("TTF");
    //    }

    //    // DirectWrite active rendering precedence: COLRv1 > SVG > COLRv0 > Bitmap > CFF > TTF
    //    if (formats.Contains("COLRv1")) return "COLRv1";
    //    if (formats.Contains("SVG")) return "SVG";
    //    if (formats.Contains("COLRv0")) return "COLRv0";
    //    if (formats.Contains("Bitmap")) return "Bitmap";
    //    if (formats.Contains("CFF")) return "CFF";
    //    if (formats.Contains("TTF")) return "TTF";

    //    return formats.Count > 0 ? string.Join(", ", formats) : string.Empty;
    //}


    public static void ShowLigatureFlyout(UIElement sender, ContextRequestedEventArgs args, FontMapView view)
    {
        if (sender is ContentPresenter { Content: LigatureModel target })
        {
            #region handlers

            static void NavigateToGlyphHandler(object s, RoutedEventArgs e)
            {
                if (s is FrameworkElement f && f.Tag is LigatureModel lig && Properties.GetTag(f) is FontMapView view)
                    view.NavigateToGlyph((ushort)lig.LigatureGlyph);
            }

            #endregion


            var viewModel = view.ViewModel;

            MenuFlyoutFactory factory = new(new FlyoutArgs { Standalone = true, ShowAdvanced = true });

            if (!string.IsNullOrEmpty(target.CombinedString))
            {
                factory.Create("CopySequenceMessage", target.CombinedString, ThemeIcon.Copy, MenuFlyoutFactory.CopyHandler, new () { Tag = target, PropertyTag = view });
                factory.AddSeparator(out _);
            }

            ushort glyphIndex = (ushort)target.LigatureGlyph;
            GlyphCharacter gc = viewModel.SelectedFace is not null && viewModel.SelectedFace.TryGetCharacterForGlyph(glyphIndex, out Character mapped)
                ? new(glyphIndex, mapped.UnicodeIndex)
                : new(glyphIndex);

            DWriteTextLayoutAnalysis analysis = Utils.GetInterop().AnalyzeGlyphLayout(viewModel.SelectedFace.Face, gc.GlyphIndex);
            FlyoutContextArg arg = new() { ParentView = view, Character = gc, Ligature = target, Analysis = analysis };

            var copyAsImage = factory
                .CreateSubItem(ThemeIcon.Copy, "CopyAsImageItem/Text")
                .AddHeader("CopyAsPngItem/Text", "Ctrl+Alt+C")
                .AddColorOptions(analysis, true, arg with { CopyType = CopyDataType.PNG });

            if (analysis.IsFullVectorBased)
            {
                copyAsImage
                    .AddHeader("CopyAsSvgItem/Text", "Ctrl+Shift+C")
                    .AddColorOptions(analysis, true, arg with { CopyType = CopyDataType.SVG });
            }

            var savePng = factory
                .CreateSubItem(ThemeIcon.Save, "ExportPNGLabel/Text")
                .AddColorOptions(analysis, false, arg with { CopyType = CopyDataType.PNG });

            if (analysis.IsFullVectorBased)
            {
                factory.CreateSubItem(ThemeIcon.Save, "ExportSVGLabel/Text")
                       .AddColorOptions(analysis, false, arg with { CopyType = CopyDataType.SVG });
            }

            factory
                .AddSeparator(out _)
                .Create("ViewInGlyphMapMessage", target.LigatureGlyph, ThemeIcon.GlyphMapView, NavigateToGlyphHandler, new() { Tag = target, PropertyTag = view });

            factory.Show(sender, args);
        }
    }
}







public class FlyoutArgs
{
    /// <summary>
    /// A window showing a folder of fonts
    /// </summary>
    public bool IsFolderView => Folder is not null;

    /// <summary>
    /// Folder of fonts associated with the view that initiates
    /// the flyout menu
    /// </summary>
    public FolderContents Folder { get; set; }

    /// <summary>
    /// ... menu 
    /// </summary>
    public bool ShowAdvanced { get; set; }

    /// <summary>
    /// A stand-alone Window showing a single Font Family
    /// </summary>
    public bool Standalone { get; set; }

    /// <summary>
    /// Context menu for font tab headers
    /// </summary>
    public bool IsTabContext { get; set; }

    /// <summary>
    /// The font family is from file that is not installed in the system
    /// or imported into the app. (I.e., opened via Drag & Drop or open
    /// button)
    /// </summary>
    public bool IsExternalFile { get; set; }

    public string PreviewText { get; set; }

    public Action AddToCollectionCommand { get; set; }

    public object Header { get; set; }
}

public record class FlyoutContextArg
{
    public Character Character { get; set; }
    public LigatureModel Ligature { get; set; }
    public ExportStyle ExportStyle { get; set; }
    public FontMapView ParentView { get; set; }
    public CopyDataType CopyType { get; set; }
    public DWriteTextLayoutAnalysis Analysis { get; set; }
    public GlyphImageFormat PreferredExportType { get; set; } = GlyphImageFormat.None;
}

public class MenuItemHost
{
    public object Host => (object)_flyout ?? _subFlyout;
    public bool IsSubItem => _subFlyout is not null;
    public IList<MenuFlyoutItemBase> Items => _flyout?.Items ?? _subFlyout.Items;

    private MenuFlyout _flyout;
    private MenuFlyoutSubItem _subFlyout;


    public MenuItemHost(MenuFlyout flyout) => _flyout = flyout;

    public MenuItemHost(MenuFlyoutSubItem flyout) => _subFlyout = flyout;
}

public class MenuFlyoutFactory
{
    public readonly MenuItemHost Menu;
    private readonly FlyoutArgs _args;

    public MenuFlyoutFactory(FlyoutArgs args)
    {
        MenuFlyout menu = new()
        {
            AreOpenCloseAnimationsEnabled = ResourceHelper.AllowAnimation
        };

        if (ResourceHelper.Get<Style>("DefaultFlyoutStyle") is Style defaultFlyoutStyle)
            menu.MenuFlyoutPresenterStyle = defaultFlyoutStyle;

        Menu = new(menu);
        _args = args;
    }

    public MenuFlyoutFactory(MenuFlyout menu, FlyoutArgs args)
    {
        Menu = new(menu);
        _args = args;
    }

    public MenuFlyoutFactory(MenuFlyoutSubItem menu, FlyoutArgs args)
    {
        Menu = new(menu);
        _args = args;
    }

    public Style DefaultItemStyle => field ??= ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
    public Style DefaultSubItemStyle => field ??= ResourceHelper.Get<Style>("ThemeMenuFlyoutSubItemStyle");
    public Style DefaultHeaderStyle => field ??= ResourceHelper.Get<Style>("MenuFlyoutItemReadOnlyHeaderStyle");


    public MenuFlyoutFactory Clear()
    {
        Menu.Items?.Clear();
        return this;
    }

    public T Child<T>(string name) where T : MenuFlyoutItemBase => Menu.Items.OfType<T>().FirstOrDefault(c => c.Name == name);

    public MenuFlyoutFactory Add(MenuFlyoutItemBase item)
    {
        Menu.Items.Add(item);
        return this;
    }

    public MenuFlyoutFactory AddHeader(string key, string acceleratorText, MenuItemHost parent = null)
    {
        MenuFlyoutItem item = new()
        {
            Text = Localization.Get(key),
            Style = DefaultHeaderStyle,
            KeyboardAcceleratorTextOverride = acceleratorText
        };

        if (parent is null)
            Menu.Items.Add(item);
        else
            parent.Items.Add(item);

        return this;
    }

    public MenuFlyoutFactory AddHeaderObject(object headerContent, out MenuFlyoutContentHost HeaderHost)
    {
        HeaderHost = null;
        headerContent ??= _args.Header;

        if (headerContent is null) return this;

        if (headerContent is FrameworkElement { Parent: MenuFlyoutContentHost host })
            host.Content = null;

        HeaderHost = new() { Content = headerContent };

        return Add(HeaderHost).AddSeparator(out _);
    }

    public MenuFlyoutFactory AddSeparator(out MenuFlyoutSeparator separator)
    {
        separator = new MenuFlyoutSeparator();
        Menu.Items.Add(separator);
        return this;
    }

    public MenuFlyoutFactory AddSeparator()
    {
        Menu.Items.Add(new MenuFlyoutSeparator());
        return this;
    }

    public MenuFlyoutItem Create(string key, object arg, ThemeIcon icon, RoutedEventHandler handler, CreateArgs args = null)
        => Create($"~{Localization.Get(key, arg)}", icon, handler, args);

    public MenuFlyoutItem Create(string key, ThemeIcon icon, RoutedEventHandler handler, CreateArgs args = null, MenuItemHost parent = null)
    {
        args ??= CreateArgs.Default;
        MenuFlyoutItem item = new()
        {
            Text = key.StartsWith("~") ? key.Remove(0, 1) : Localization.Get(key),
            Icon = ThemeIconGlyph.CreateIcon(icon),
            Tag = args.Tag,
            Style = DefaultItemStyle
        };

        Properties.SetTag(item, args.PropertyTag);
        item.Click += handler;

        if (args.AcceleratorKey != VirtualKey.None)
            item.AddKeyboardAccelerator(args.AcceleratorKey, VirtualKeyModifiers.Control);

        if (string.IsNullOrWhiteSpace(args.AcceleratorText) is false)
            item.KeyboardAcceleratorTextOverride = args.AcceleratorText;

        if (args.Add)
        {
            var target = parent?.Items ?? Menu.Items;

            if (args.Index >= 0)
                target.Insert(args.Index, item);
            else
                target.Add(item);
        }

        return item.SetAnimation();
    }

    public MenuFlyoutFactory CreateSubItem(ThemeIcon icon, string key, object arg = null, int insertIndex = -1)
    {
        MenuFlyoutSubItem item = new()
        {
            Text = Localization.Get(key, arg),
            Icon = ThemeIconGlyph.CreateIcon(icon),
            Style = DefaultSubItemStyle
        };

        if (insertIndex == -1)
            Menu.Items.Add(item);
        else if (insertIndex >= 0 && insertIndex < Menu.Items.Count)
            Menu.Items.Insert(insertIndex, item);

        return new MenuFlyoutFactory(item, _args);
    }

    public MenuFlyoutFactory AddColorOptions(DWriteTextLayoutAnalysis analysis, bool isCopy, FlyoutContextArg arg)
    {
        AddColorOptions(Menu, analysis, isCopy, arg);
        return this;
    }

    public void AddColorOptions(MenuItemHost parent, DWriteTextLayoutAnalysis analysis, bool isCopy, FlyoutContextArg arg)
    {
        static void RequestCopy(object s, RoutedEventArgs e)
        {
            if (s is FrameworkElement { Tag: FlyoutContextArg ctx })
                _ = ctx.ParentView.ViewModel.RequestCopyToClipboardAsync(
                    new(DevValueType.Char, ctx.Character, ctx.Analysis, ctx.ParentView.ViewModel.SelectedFaceAnalysis, ctx.CopyType) { Style = ctx.ExportStyle, PreferredColorType = ctx.PreferredExportType });
        }

        static void SaveHandler(object s, RoutedEventArgs e)
        {
            if (s is FrameworkElement { Tag: FlyoutContextArg ctx })
            {
                ExportParameters p = new() { Style = ctx.ExportStyle, Typography = new(ctx.Ligature.Feature), Character = ctx.Character };
                if (ctx.CopyType == CopyDataType.PNG)
                    _ = ctx.ParentView.ViewModel.SavePngAsync(p);
                else if (ctx.CopyType == CopyDataType.SVG)
                    _ = ctx.ParentView.ViewModel.SaveSvgAsync(p);
            }
        }

        RoutedEventHandler handler = isCopy ? RequestCopy : SaveHandler;
        FlyoutContextArg W(ExportStyle style) => arg with { ExportStyle = style };
        bool svgChar = analysis.GlyphFormats.Has(GlyphImageFormat.Svg);

        if (arg.CopyType == CopyDataType.SVG && analysis.IsFullVectorBased && svgChar)
        {
            Create(
                svgChar ? "ExportSVGGlyphLabel/Text" : "ColoredGlyphLabel/Text",
                icon: ThemeIcon.ColorGlyph, handler, new() { Tag = W(ExportStyle.ColorGlyph) }, parent);
        }
        else if (analysis.HasColorGlyphs)
        {
            if (analysis.SupportsColrV0)
                Create("~COLRv0 Glyph", ThemeIcon.ColorGlyph, handler, args: new() { Tag = W(ExportStyle.ColorGlyph) with { PreferredExportType = GlyphImageFormat.Colr } }, parent);

            if (analysis.SupportsColrV1)
                Create("~COLRv1 Glyph", ThemeIcon.ColorGlyph, handler, args: new() { Tag = W(ExportStyle.ColorGlyph) with { PreferredExportType = GlyphImageFormat.ColrPaintTree } }, parent);

            if (analysis.SupportsColrV0 is false && analysis.SupportsColrV1 is false)
            {
                // Probably a bitmap
                Create("ColoredGlyphLabel/Text", ThemeIcon.ColorGlyph, handler, args: new() { Tag = W(ExportStyle.ColorGlyph) }, parent);
            }
        }


        // Glyphs that are entirely SVG backed don't have CFF outlines and can't be exported as monochrome.
        // Bitmaps have the same issue.
        if (!analysis.ContainsBitmapGlyphs && !svgChar)
        {
            Create("BlackFill/Text", ThemeIcon.FilledSquareBlack, handler, new() { Tag = W(ExportStyle.Black) }, parent);
            Create("WhiteFill/Text", ThemeIcon.FilledSquareWhite, handler, new() { Tag = W(ExportStyle.White) }, parent);
        }
    }

    public bool Show(UIElement target, ContextRequestedEventArgs args)
    {
        if (Menu.Host is MenuFlyout menu && args.TryGetPosition(target, out Point p))
        {
            menu.ShowAt(target, p);
            args.Handled = true;
            return true;
        }
        return false;
    }

    public static async void CopyHandler(object s, RoutedEventArgs e)
    {
        if (s is FrameworkElement f && Properties.GetTag(f) is FontMapView view)
        {
            if (f.Tag is LigatureModel lig)
                Utils.CopyToClipboard(lig.ClipboardText);
            else if (f.Tag is FlyoutContextArg { Character: { } c })
            {
                if (c is GlyphCharacter { IsValidUnicode: false } gc && view.ViewModel.SelectedFaceAnalysis.TryGetLigature(gc.GlyphIndex, out LigatureModel lig1))
                    Utils.CopyToClipboard(lig1.ClipboardText);

                if (!await Utils.TryCopyToClipboardAsync(c, view.ViewModel))
                    return;
            }
            else
                return;

            view.GetNotifier().Show(Localization.Get("NotificationCopied"), 2000);
        }
    }


    public record class CreateArgs
    {
        public static CreateArgs Default { get; } = new();

        public bool Add { get; init; } = true;
        // Index to insert the child item at
        public int Index { get; init; } = -1;

        public VirtualKey AcceleratorKey { get; init; } = VirtualKey.None;
        public object Tag { get; init; }
        public object PropertyTag { get; init; }
        public string AcceleratorText { get; init; }
        public Style Style { get; init; }

        public CreateArgs() { }
        public CreateArgs(VirtualKey key) { AcceleratorKey = key; }

        public CreateArgs WithKey(VirtualKey key)
        {
            return this with { AcceleratorKey = key };
        }
    }
}
