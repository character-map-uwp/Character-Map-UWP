using CharacterMap.Controls;
using CharacterMap.Views;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace CharacterMap.Helpers;

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
}


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
            _ = MainPage.MainDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                main.TryRemoveFont(font);
            });
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

        Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
        Style subStyle = ResourceHelper.Get<Style>("ThemeMenuFlyoutSubItemStyle");

        #region Handlers 

        static void OpenInNewWindow(object s, RoutedEventArgs args)
        {
            if (s is FrameworkElement f && f.Tag is CMFontFamily fnt)
                _ = FontMapView.CreateNewViewForFontAsync(fnt, null, f.DataContext as CharacterRenderingOptions);
        }

        static void OpenInNewTab(object s, RoutedEventArgs args)
        {
            if (s is FrameworkElement f && f.Tag is CMFontFamily fnt)
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
            if (sender is FrameworkElement f &&
                f.Tag is CMFontFamily fnt
                && Properties.GetTag(f) is CharacterRenderingOptions o)
            {
                WeakReferenceMessenger.Default.Send(new ExportRequestedMessage());
            }
        }

        static void DeleteClick(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is CMFontFamily fnt)
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

        //void OpenCompare(object sender, RoutedEventArgs e)
        //{
        //    _ = QuickCompareView.CreateWindowAsync(new(false, args.Folder));
        //}

        void OpenFaceCompare(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement f && f.Tag is CMFontFamily fnt)
            {
                _ = QuickCompareView.CreateWindowAsync(new(false, new(fnt.Variants.ToList()) { IsFamilyCompare = true }));
            }
        }

        MenuFlyoutItem Create(string key, ThemeIcon icon, RoutedEventHandler handler, VirtualKey accel = VirtualKey.None, bool add = true)
        {
            MenuFlyoutItem item = new()
            {
                Text = key.StartsWith("~") ? key.Remove(0, 1) : Localization.Get(key),
                Icon = Icon(icon),
                Tag = font,
                Style = style
            };

            Properties.SetTag(item, options);

            item.Click += handler;

            if (accel != VirtualKey.None)
                item.AddKeyboardAccelerator(accel, VirtualKeyModifiers.Control);

            if (add)
                menu.Items.Add(item);

            return item.SetAnimation();
        }

        #endregion

        if (menu.Items != null)
        {
            menu.Items.Clear();
            MenuFlyoutSubItem coll;

            bool qc = args.IsFolderView is false && isExternalFile is false;
            {

                // HORRIBLE Hacks, because MenuFlyoutSubItem never updates it's UI tree after the first
                // render meaning we can't dynamically update items. Instead we need to make an entirely
                // menu every time it opens.

                if (headerContent != null && headerContent.Parent is MenuFlyoutContentHost host)
                    host.Content = null;

                menu.Items.Add(new MenuFlyoutContentHost
                {
                    Content = headerContent
                });

                menu.AddSeparator(headerContent != null);

                // 1. Add "Open in New Tab/Window" buttons
                if (!standalone)
                {
                    // 1.1. Only show "Open in New Tab" if this is Font List context menu
                    //      and supported by theme
                    if (showAdvanced is false && ResourceHelper.SupportsTabs)
                        Create("OpenInNewTab/Text", ThemeIcon.NewTab, OpenInNewTab);

                    // 1.2. Create "Open in New Window"
                    MenuFlyoutItem newWindow = Create("OpenInNewWindow/Text", ThemeIcon.NewWindow, OpenInNewWindow);
                    if (showAdvanced)
                        newWindow.AddKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control);
                }

                // 2. Add Save Font File & Export Font Glyphs options
                if (options != null && options.Variant != null && DirectWrite.IsFontLocal(options.Variant.Face))
                {
                    Create("ExportFontFileLabel/Text", ThemeIcon.Save, SaveFont_Click, VirtualKey.S);
                    if (showAdvanced && !args.IsTabContext)
                        Create("ExportCharactersLabel/Text", ThemeIcon.Save, Export_Click, VirtualKey.E);
                }

                // 3. Add "Add to quick compare" button if we're viewing a variant
                if (qc)
                    Create("AddToQuickCompare/Text", ThemeIcon.AddTo, AddToQuickCompare, VirtualKey.Q);

                // 4. Add "Add to Collection" button
                if (isExternalFile is false && args.IsFolderView is false)
                {
                    coll = AddCollectionItems(menu, font, null, args: args);
                    coll.Style = subStyle;
                }
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
                {
                    MenuFlyoutItem print = Create("BtnPrint/Content", ThemeIcon.Print, Print_Click, VirtualKey.P, false);
                    menu.Items.Insert(standalone ? 2 : 3, print);
                }
            }

            // 7. Add "Delete Font" button
            if (!standalone
                && !args.IsFolderView
                && !args.IsTabContext
                && font.HasImportedFiles)
            {
                menu.AddSeparator();
                MenuFlyoutItem del = Create("RemoveFontFlyout/Text", ThemeIcon.Delete, DeleteClick);
                if (showAdvanced)
                    del.AddKeyboardAccelerator(VirtualKey.Delete, VirtualKeyModifiers.Control);
            }

            // 8. Handle compare options
            if (font.HasVariants)
                menu.AddSeparator();

            if (font.HasVariants)
            {
                // 8.1. Add "Compare Fonts button"
                Create($"~{string.Format(Localization.Get("CompareFacesCountLabel/Text"), font.Variants.Count)}", ThemeIcon.CompareFonts, OpenFaceCompare);
                
                // 8.2. Add "Add all to quick compare" button
                Create($"~{string.Format(Localization.Get("AddMultiToQuickCompare/Text"), font.Variants.Count)}", ThemeIcon.Add, AddToQuickCompareMulti);
            }

            // 9. Add Calligraphy button
            menu.AddSeparator();
            Create("CalligraphyLabel/Text", ThemeIcon.Calligraphy, OpenCalligraphy, VirtualKey.I);
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
                Icon = Icon(ThemeIcon.Remove),
                Tag = collection == null && filter == BasicFontFilter.SymbolFonts ? _collections.SymbolCollection : collection,
                DataContext = font,
                Style = style
            };
            removeItem.Click += RemoveFrom_Click;
            menu.Items.Add(removeItem);

            async void RemoveFrom_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f
                    && f.DataContext is CMFontFamily fnt
                    && f.Tag is UserFontCollection collection)
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

    static FontIcon Icon(ThemeIcon icon)
    {
        FontIcon f = new();
        Properties.SetThemeIcon(f, icon);
        return f;
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
            Icon = Icon(ThemeIcon.Collections),
            Style = substyle
        };

        // 2. Add "New Collection" Item
        MenuFlyoutItem newCollection = new()
        {
            Text = Localization.Get("NewCollectionItem/Text"),
            Icon = Icon(ThemeIcon.Add),
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
    public static void ShowCharacterGridContext(MenuFlyout menu, FrameworkElement target, FontMapViewModel viewmodel, bool isStandalone)
    {
        T Child<T>(string name) where T : MenuFlyoutItemBase => menu.Items.OfType<T>().FirstOrDefault(c => c.Name == name);

        if (target.Tag is Character c)
        {
            Style style = ResourceHelper.Get<Style>("ThemeMenuFlyoutItemStyle");
            Style subStyle = ResourceHelper.Get<Style>("ThemeMenuFlyoutSubItemStyle");

            // 1. Attach the flyout to the selected grid item and apply the correct context
            FlyoutBase.SetAttachedFlyout(target, menu);

            // 2. Analyse the character to know which options we should show in the menu
            var analysis = viewmodel.GetCharAnalysis(c);

            // 3. Handle PNG options
            var pngRoot = Child<MenuFlyoutSubItem>("PngRoot");
            foreach (var child in pngRoot.Items.OfType<MenuFlyoutItem>())
            {
                if (child.CommandParameter is ExportStyle s && s == ExportStyle.ColorGlyph)
                    child.SetVisible(analysis.HasColorGlyphs);
                else
                    child.SetVisible(!analysis.ContainsBitmapGlyphs); // Bitmap glyphs must *always* be saved as colour version
            }

            // 4. Handle SVG options
            var svgRoot = Child<MenuFlyoutSubItem>("SvgRoot");

            // 4.1. We can only save as SVG if all layers of the glyph are created with vectors
            svgRoot.SetVisible(analysis.IsFullVectorBased);
            if (analysis.IsFullVectorBased)
            {
                // Glyphs that are actually stored as individual SVG files inside a font, and not
                // typical font vector data, must always be saved as colourised / raw SVG.
                bool svgChar = analysis.GlyphFormats.Contains(GlyphImageFormat.Svg);

                foreach (var child in svgRoot.Items.OfType<MenuFlyoutItem>())
                {
                    if (child.CommandParameter is ExportStyle s && s == ExportStyle.ColorGlyph)
                    {
                        child.Text = svgChar ? Localization.Get("ExportSVGGlyphLabel/Text") : Localization.Get("ColoredGlyphLabel/Text");
                        child.SetVisible(svgChar || (analysis.IsFullVectorBased && analysis.HasColorGlyphs));
                    }
                    else
                    {
                        child.SetVisible(!svgChar);
                    }
                }
            }

            // 4.2. Find in other fonts is only supported in MainView right now
            Child<MenuFlyoutItem>("FindCharButton")?.SetVisible(!isStandalone);

            // 5. Handle Dev values
            if (Child<MenuFlyoutSubItem>("DevRoot") is { } devRoot)
            {
                // 5.0. Prepare click handler
                static void CopyItemClick(object sender, RoutedEventArgs e)
                {
                    if (sender is MenuFlyoutItem item
                        && Properties.GetDevOption(item) is DevOption option)
                    {
                        Utils.CopyToClipBoard(option.Value);
                        WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, Localization.Get("NotificationCopied"), 2000));
                    }
                }

                // 5.1. Get providers for the grid character
                var options = viewmodel.RenderingOptions with { Typography = viewmodel.TypographyFeatures, Axis = viewmodel.VariationAxis.Copy() };
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
                            MenuFlyoutItem i = new() { Text = Localization.Get("ContextMenuDevCopyCommand", o.Name), Style = style };
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
            };

            // 6. Handle visibility of "Add to Selection" Button
            if (Child<MenuFlyoutItem>("AddSelectionButton") is { } add)
                add.SetVisible(ResourceHelper.AppSettings.EnableCopyPane);

            // 7. Set item context
            menu.SetItemsDataContext(target.Tag, subStyle);

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
}
