using CharacterMap.Controls;
using CharacterMap.Core;
using CharacterMap.Helpers;
using CharacterMap.Models;
using CharacterMap.Services;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using CommonServiceLocator;
using GalaSoft.MvvmLight.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace CharacterMap.Helpers
{
    public static class FlyoutHelper
    {
        private static UserCollectionsService _collections { get; } = ServiceLocator.Current.GetInstance<UserCollectionsService>();

        public static void CreateMenu(
            MenuFlyout menu,
            InstalledFont font,
            bool standalone)
        {
            MainViewModel main = ResourceHelper.Get<ViewModelLocator>("Locator").Main;

            void OpenInNewWindow(object s, RoutedEventArgs args)
            {
                if (s is FrameworkElement f && f.Tag is InstalledFont fnt)
                    _ = FontMapView.CreateNewViewForFontAsync(fnt);
            }

            async void AddToSymbolFonts_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f && f.DataContext is InstalledFont fnt)
                {
                    var result = await _collections.AddToCollectionAsync(fnt, _collections.SymbolCollection);

                    Messenger.Default.Send(new CollectionsUpdatedMessage());

                    if (result.Success)
                        Messenger.Default.Send(new AppNotificationMessage(true, result));
                    
                }
            }

            void CreateCollection_Click(object sender, RoutedEventArgs e)
            {
                var d = new CreateCollectionDialog
                {
                    DataContext = (sender as FrameworkElement)?.DataContext
                };

                _ = d.ShowAsync();
            }

            async void RemoveFrom_Click(object sender, RoutedEventArgs e)
            {
                if (sender is FrameworkElement f && f.DataContext is InstalledFont fnt)
                {
                    UserFontCollection collection = (main.SelectedCollection == null && main.FontListFilter == 1)
                        ? _collections.SymbolCollection
                        : main.SelectedCollection;

                    await _collections.RemoveFromCollectionAsync(fnt, collection);
                    Messenger.Default.Send(new CollectionsUpdatedMessage());

                }
            }

            void DeleteMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
            {
                if (sender is MenuFlyoutItem item && item.Tag is InstalledFont fnt)
                {
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
                            main.TryRemoveFont(fnt);
                        });
                    };
                    _ = d.ShowAsync();
                }
            }

            if (menu.Items != null)
            {
                menu.Items.Clear();
                MenuFlyoutSubItem coll;

                {
                    // HORRIBLE Hacks, because MenuFlyoutSubItem never updates it's UI tree after the first
                    // render, meaning we can't dynamically update items. Instead we need to make an entirely
                    // new one.

                    // Add "Open in New Window" button
                    if (!standalone)
                    {
                        var newWindow = new MenuFlyoutItem
                        {
                            Text = Localization.Get("OpenInNewWindow/Text"),
                            Icon = new SymbolIcon {Symbol = Symbol.NewWindow},
                            Tag = font
                        };
                        newWindow.Click += OpenInNewWindow;
                        menu.Items.Add(newWindow);
                    }

                    // Add "Delete Font" button
                    if (!standalone)
                    {
                        if (font.HasImportedFiles)
                        {
                            var removeFont = new MenuFlyoutItem
                            {
                                Text = Localization.Get("RemoveFontFlyout/Text"),
                                Icon = new SymbolIcon {Symbol = Symbol.Delete},
                                Tag = font
                            };
                            removeFont.Click += DeleteMenuFlyoutItem_Click;
                            menu.Items.Add(removeFont);
                        }
                    }


                    // Add "Add to Collection" button
                    MenuFlyoutSubItem newColl = new MenuFlyoutSubItem
                    {
                        Text = Localization.Get("AddToCollectionFlyout/Text"),
                        Icon = new SymbolIcon {Symbol = Symbol.AllApps}
                    };

                    // Create "New Collection" Item
                    var newCollection = new MenuFlyoutItem
                    {
                        Text = Localization.Get("NewCollectionItem/Text"),
                        Icon = new SymbolIcon
                        {
                            Symbol = Symbol.Add
                        }
                    };
                    newCollection.Click += CreateCollection_Click;
                    if (newColl.Items != null)
                    {
                        newColl.Items.Add(newCollection);

                        // Create "Symbol Font" item
                        if (!font.IsSymbolFont)
                        {
                            newColl.Items.Add(new MenuFlyoutSeparator());

                            var symb = new MenuFlyoutItem
                            {
                                Text = Localization.Get("OptionSymbolFonts/Text"),
                                IsEnabled = !_collections.SymbolCollection.Fonts.Contains(font.Name)
                            };
                            symb.Click += AddToSymbolFonts_Click;
                            newColl.Items.Add(symb);
                        }
                    }

                    coll = newColl;
                    menu.Items.Add(coll);
                }

                // Only show the "Remove from Collection" menu item if:
                //  -- we are not in a standalone window
                //  AND
                //  -- we are in a custom collection
                //  OR 
                //  -- we are in the Symbol Font collection, and this is a font that 
                //     the user has manually tagged as a symbol font
                if (!standalone)
                {
                    if (main.SelectedCollection != null ||
                        (main.FontListFilter == 1 && !font.FontFace.IsSymbolFont))
                    {
                        var removeItem = new MenuFlyoutItem
                        {
                            Text = Localization.Get("RemoveFromCollectionItem/Text"),
                            Icon = new SymbolIcon {Symbol = Symbol.Remove},
                            Tag = font
                        };
                        removeItem.Click += RemoveFrom_Click;
                        menu.Items.Add(removeItem);
                    }
                }

                // Add items for each user Collection
                if (_collections.Items.Count > 0)
                {
                    if (coll.Items != null)
                    {
                        coll.Items.Add(new MenuFlyoutSeparator());

                        foreach (var m in 
                                _collections.Items.Select(item => new MenuFlyoutItem
                                {
                                    DataContext = item, Text = item.Name, IsEnabled = !item.Fonts.Contains(font.Name)
                                }))
                        {
                            if (m.IsEnabled)
                            {
                                m.Click += async (s, a) =>
                                {
                                    UserFontCollection collection =
                                        (UserFontCollection) ((FrameworkElement) s).DataContext;
                                    AddToCollectionResult result =
                                        await _collections.AddToCollectionAsync(font, collection);

                                    if (result.Success)
                                    {
                                        Messenger.Default.Send(new AppNotificationMessage(true, result));
                                    }
                                };
                            }

                            coll.Items.Add(m);
                        }
                    }
                }
            }
        }

    }
}
