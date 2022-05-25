using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Provider;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using CharacterMapCX;
using Microsoft.Toolkit.Mvvm.DependencyInjection;
using Microsoft.Toolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace CharacterMap.Helpers
{
    public class FlyoutArgs
    {
        /// <summary>
        /// A window showing a folder of fonts
        /// </summary>
        public bool IsFolderView        => Folder is not null;

        /// <summary>
        /// Folder of fonts associated with the view that initiates
        /// the flyout menu
        /// </summary>
        public FolderContents Folder    { get; set; }

        /// <summary>
        /// ... menu 
        /// </summary>
        public bool ShowAdvanced        { get; set; }

        /// <summary>
        /// A stand-alone Window showing a single Font Family
        /// </summary>
        public bool Standalone          { get; set; }
        
        /// <summary>
        /// The font family is from file that is not installed in the system
        /// or imported into the app. (I.e., opened via Drag & Drop or open
        /// button)
        /// </summary>
        public bool IsExternalFile      { get; set; }
    }


    public static class FlyoutHelper
    {
        private static UserCollectionsService _collections { get; } = Ioc.Default.GetService<UserCollectionsService>();

        public static void RequestDelete(InstalledFont font)
        {
            MainViewModel main = Ioc.Default.GetService<MainViewModel>();
            var d = new ContentDialog
            {
                Title = Localization.Get("DlgDeleteFont/Title"),
                IsPrimaryButtonEnabled = true,
                IsSecondaryButtonEnabled = true,
                PrimaryButtonText = Localization.Get("DigDeleteCollection/PrimaryButtonText"),
                SecondaryButtonText = Localization.Get("DigDeleteCollection/SecondaryButtonText"),
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


        /// <summary>
        /// Creates the context menu for the Font List or the "..." button.
        /// Both of these have a font as their main target.
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="font"></param>
        /// <param name="variant"></param>
        /// <param name="headerContent"></param>
        /// <param name="standalone"></param>
        /// <param name="showAdvanced"></param>
        public static void CreateMenu(
            MenuFlyout menu,
            InstalledFont font,
            CharacterRenderingOptions options,
            FrameworkElement headerContent,
            FlyoutArgs args)
        {
            MainViewModel main = Ioc.Default.GetService<MainViewModel>();

            bool standalone = args.Standalone;
            bool showAdvanced = args.ShowAdvanced;
            bool isExternalFile = args.IsExternalFile;

            #region Handlers 

            static void OpenInNewWindow(object s, RoutedEventArgs args)
            {
                if (s is FrameworkElement f && f.Tag is InstalledFont fnt)
                    _ = FontMapView.CreateNewViewForFontAsync(fnt, null, f.DataContext as CharacterRenderingOptions);
            }

            static async void AddToSymbolFonts_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f && f.DataContext is InstalledFont fnt)
                {
                    var result = await _collections.AddToCollectionAsync(fnt, _collections.SymbolCollection);

                    WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());

                    if (result.Success)
                        WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));
                }
            }

            static void CreateCollection_Click(object sender, RoutedEventArgs e)
            {
                var d = new CreateCollectionDialog
                {
                    DataContext = (sender as FrameworkElement)?.DataContext
                };

                _ = d.ShowAsync();
            }

            static void SaveFont_Click(object sender, RoutedEventArgs e)
            {
                if (sender is MenuFlyoutItem item && item.Tag is FontVariant fnt)
                {
                    ExportManager.RequestExportFontFile(fnt);
                }
            }

            static void Print_Click(object sender, RoutedEventArgs e)
            {
                WeakReferenceMessenger.Default.Send(new PrintRequestedMessage());
            }

            static void Export_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f &&
                    f.DataContext is InstalledFont fnt
                    && f.Tag is CharacterRenderingOptions o)
                {
                    WeakReferenceMessenger.Default.Send(new ExportRequestedMessage());
                }
            }

            async void RemoveFrom_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f && f.DataContext is InstalledFont fnt)
                {
                    UserFontCollection collection = (main.SelectedCollection == null && main.FontListFilter == BasicFontFilter.SymbolFonts)
                        ? _collections.SymbolCollection
                        : main.SelectedCollection;

                    await _collections.RemoveFromCollectionAsync(fnt, collection);
                    WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, new CollectionUpdatedArgs(fnt, collection, false)));
                    WeakReferenceMessenger.Default.Send(new CollectionsUpdatedMessage());
                }
            }

            static void DeleteMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
            {
                if (sender is MenuFlyoutItem item && item.Tag is InstalledFont fnt)
                {
                    RequestDelete(fnt);
                }
            }

            #endregion


            if (menu.Items != null)
            {
                menu.Items.Clear();
                MenuFlyoutSubItem coll;

                {
                    // HORRIBLE Hacks, because MenuFlyoutSubItem never updates it's UI tree after the first
                    // render, meaning we can't dynamically update items. Instead we need to make an entirely
                    // new one.

                    if (headerContent != null && headerContent.Parent is MenuFlyoutContentHost host)
                        host.Content = null;

                    menu.Items.Add(new MenuFlyoutContentHost
                    {
                        Content = headerContent
                    });

                    menu.Items.Add(new MenuFlyoutSeparator().SetVisible(headerContent != null));

                    // Add "Open in New Window" button
                    if (!standalone)
                    {
                        MenuFlyoutItem newWindow = new()
                        {
                            Text = Localization.Get("OpenInNewWindow/Text"),
                            Icon = new FontIcon { Glyph = "\uE17C" },
                            Tag = font,
                            DataContext = options
                        };
                        newWindow.Click += OpenInNewWindow;
                        menu.Items.Add(newWindow);

                        if (showAdvanced)
                        {
                            newWindow.AddKeyboardAccelerator(VirtualKey.N, VirtualKeyModifiers.Control);
                        }
                    }

                    if (options != null && options.Variant != null && DirectWrite.IsFontLocal(options.Variant.FontFace))
                    {
                        MenuFlyoutItem saveButton = new MenuFlyoutItem()
                        {
                            Text = Localization.Get("ExportFontFileLabel/Text"),
                            Icon = new FontIcon { Glyph = "\uE792" },
                            Tag = options.Variant
                        }.AddKeyboardAccelerator(VirtualKey.S, VirtualKeyModifiers.Control);

                        saveButton.Click += SaveFont_Click;
                        menu.Items.Add(saveButton);

                        MenuFlyoutItem exportButton = new MenuFlyoutItem()
                        {
                            Text = Localization.Get("ExportCharactersLabel/Text"),
                            Icon = new FontIcon { Glyph = "\uE105" },
                            Tag = options,
                            DataContext = font
                        }.AddKeyboardAccelerator(VirtualKey.E, VirtualKeyModifiers.Control);

                        exportButton.Click += Export_Click;
                        menu.Items.Add(exportButton);
                    }

                    // Add "Add to Collection" button
                    if (isExternalFile is false && args.IsFolderView is false)
                    {
                        MenuFlyoutSubItem newColl = new()
                        {
                            Text = Localization.Get("AddToCollectionFlyout/Text"),
                            Icon = new FontIcon { Glyph = "\uE71D" }
                        };

                        // Create "New Collection" Item
                        MenuFlyoutItem newCollection = new()
                        {
                            Text = Localization.Get("NewCollectionItem/Text"),
                            Icon = new FontIcon { Glyph = "\uE109" },
                            DataContext = font
                        };
                        newCollection.Click += CreateCollection_Click;

                        if (newColl.Items != null)
                        {
                            newColl.Items.Add(newCollection);

                            // Create "Symbol Font" item
                            if (!font.IsSymbolFont)
                            {
                                newColl.Items.Add(new MenuFlyoutSeparator());

                                MenuFlyoutItem symb = new()
                                {
                                    Text = Localization.Get("OptionSymbolFonts/Text"),
                                    IsEnabled = !_collections.SymbolCollection.Fonts.Contains(font.Name),
                                    DataContext = font
                                };
                                symb.Click += AddToSymbolFonts_Click;
                                newColl.Items.Add(symb);
                            }
                        }

                        coll = newColl;
                        menu.Items.Add(coll);

                        // Add items for each user Collection
                        if (_collections.Items.Count > 0)
                        {
                            if (coll.Items != null)
                            {
                                coll.Items.Add(new MenuFlyoutSeparator());

                                foreach (var m in
                                        _collections.Items.Select(item => new MenuFlyoutItem
                                        {
                                            DataContext = item,
                                            Text = item.Name,
                                            IsEnabled = !item.Fonts.Contains(font.Name)
                                        }))
                                {
                                    if (m.IsEnabled)
                                    {
                                        m.Click += async (s, a) =>
                                        {
                                            UserFontCollection collection =
                                                (UserFontCollection)((FrameworkElement)s).DataContext;
                                            AddToCollectionResult result =
                                                await _collections.AddToCollectionAsync(font, collection);

                                            if (result.Success)
                                            {
                                                WeakReferenceMessenger.Default.Send(new AppNotificationMessage(true, result));
                                            }
                                        };
                                    }

                                    coll.Items.Add(m);
                                }
                            }
                        }
                    }
                }

                // Only show the "Remove from Collection" menu item if:
                //  -- we are not in a stand-alone window
                //  AND
                //  -- we are in a custom collection
                //  OR 
                //  -- we are in the Symbol Font collection, and this is a font that 
                //     the user has manually tagged as a symbol font
                if (!standalone && !args.IsFolderView)
                {
                    if (main.SelectedCollection != null ||
                        (main.FontListFilter == BasicFontFilter.SymbolFonts && !font.FontFace.IsSymbolFont))
                    {
                        menu.Items.Add(new MenuFlyoutSeparator());

                        MenuFlyoutItem removeItem = new()
                        {
                            Text = Localization.Get("RemoveFromCollectionItem/Text"),
                            Icon = new FontIcon { Glyph = "\uE108" },
                            Tag = font,
                            DataContext = font
                        };
                        removeItem.Click += RemoveFrom_Click;
                        menu.Items.Add(removeItem);
                    }
                }

                if (showAdvanced)
                {
                    if (Windows.Graphics.Printing.PrintManager.IsSupported())
                    {
                        MenuFlyoutItem item = new MenuFlyoutItem
                        {
                            Text = Localization.Get("BtnPrint/Content"),
                            Icon = new FontIcon { Glyph = "\uE749" }
                        }.AddKeyboardAccelerator(VirtualKey.P, VirtualKeyModifiers.Control);

                        item.Click += Print_Click;
                        menu.Items.Insert(standalone ? 2 : 3, item);
                    }
                }

                // Add "Delete Font" button
                if (!standalone && !args.IsFolderView)
                {
                    if (font.HasImportedFiles)
                    {
                        menu.Items.Add(new MenuFlyoutSeparator());

                        MenuFlyoutItem removeFont = new()
                        {
                            Text = Localization.Get("RemoveFontFlyout/Text"),
                            Icon = new FontIcon { Glyph = "\uE107" },
                            Tag = font,
                            DataContext = font
                        };

                        if (showAdvanced)
                            removeFont.AddKeyboardAccelerator(VirtualKey.Delete, VirtualKeyModifiers.Control);

                        removeFont.Click += DeleteMenuFlyoutItem_Click;
                        menu.Items.Add(removeFont);
                    }
                }

                // Handle Compare options
                // Add "Compare Fonts button"
                var qq = new MenuFlyoutItem
                {
                    Text = Localization.Get("CompareFontsButton/Text"),
                    Icon = new FontIcon { Glyph = "\uE1D3" }
                }.AddKeyboardAccelerator(VirtualKey.K, VirtualKeyModifiers.Control);

                qq.Click += (s, e) =>
                {
                    _ = QuickCompareView.CreateWindowAsync(new(false, args.Folder));
                };

                menu.Items.Add(new MenuFlyoutSeparator());
                menu.Items.Add(qq);

                // Add "Add to quick compare" button if we're viewing a variant
                if (args.IsFolderView is false && showAdvanced && isExternalFile is false)
                {
                    MenuFlyoutItem item = new MenuFlyoutItem
                    {
                        Text = Localization.Get("AddToQuickCompare/Text"),
                        Icon = new FontIcon { Glyph = "\uE109" }
                    }.AddKeyboardAccelerator(VirtualKey.Q, VirtualKeyModifiers.Control);

                    item.Click += (s, e) =>
                    {
                        _ = QuickCompareView.AddAsync(options);
                    };

                    menu.Items.Add(item);
                }
            }
        }

        /// <summary>
        /// Creates the context menu for the Character Map grid
        /// </summary>
        /// <param name="menu"></param>
        /// <param name="target"></param>
        /// <param name="viewmodel"></param>
        public static void ShowCharacterGridContext(MenuFlyout menu, FrameworkElement target, FontMapViewModel viewmodel)
        {
            if (target.Tag is Character c)
            {
                // 1. Attach the flyout to the selected grid item and apply the correct context
                FlyoutBase.SetAttachedFlyout(target, menu);
                menu.SetItemsDataContext(target.Tag);

                // 2. Analyse the character to know which options we should show in the menu
                var analysis = viewmodel.GetCharAnalysis(c);

                // 3. Handle PNG options
                var pngRoot = menu.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault(i => i.Name == "PngRoot");
                foreach (var child in pngRoot.Items.OfType<MenuFlyoutItem>())
                {
                    if (child.CommandParameter is ExportStyle s && s == ExportStyle.ColorGlyph)
                        child.SetVisible(analysis.HasColorGlyphs);
                    else
                        child.SetVisible(!analysis.ContainsBitmapGlyphs); // Bitmap glyphs must *always* be saved as colour version
                }

                // 4. Handle SVG options
                var svgRoot = menu.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault(i => i.Name == "SvgRoot");
                
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

                // 5. Handle Dev values
                if (menu.Items.OfType<MenuFlyoutSubItem>().FirstOrDefault(i => i.Name == "DevRoot") is MenuFlyoutSubItem devRoot)
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
                            var item = new MenuFlyoutSubItem { Text = p.DisplayName };
                            foreach (var o in p.GetAllOptions())
                            {
                                var i = new MenuFlyoutItem { Text = Localization.Get("ContextMenuDevCopyCommand", o.Name) };
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
                if (menu.Items.OfType<MenuFlyoutItem>().FirstOrDefault(i => i.Name == "AddSelectionButton") is MenuFlyoutItem add)
                {
                    add.SetVisible(ResourceHelper.AppSettings.EnableCopyPane);
                }

                // 7. Show complete flyout
                FlyoutBase.ShowAttachedFlyout(target);
            }
        }

        public static void SetItemsDataContext(this MenuFlyout flyout, object dataContext)
        {
            static void SetContext(IList<MenuFlyoutItemBase> items, object context)
            {
                foreach (var item in items)
                {
                    if (item is MenuFlyoutSubItem sub)
                        SetContext(sub.Items, context);

                    item.DataContext = context;
                }
            }

            SetContext(flyout.Items, dataContext);
        }
    }
}
