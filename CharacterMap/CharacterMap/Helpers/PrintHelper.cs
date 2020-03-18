using CharacterMap.Controls;
using CharacterMap.ViewModels;
using CharacterMap.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Printing;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Printing;

namespace CharacterMap.Helpers
{
    public class PrintSize
    {
        public Size PortraitPageSize { get; set; }
        public string DisplayName { get; set; }


        public double HorizontalMargin { get; set; }
        public double VerticalMargin { get; set; }

        public Size GetSafeAreaSize(Orientation o)
        {
            if (o == Orientation.Vertical)
                return new Size(PortraitPageSize.Width - (HorizontalMargin * 2), PortraitPageSize.Height - (VerticalMargin * 2));
            else
                return new Size(PortraitPageSize.Height - (HorizontalMargin * 2), PortraitPageSize.Width - (VerticalMargin * 2));
        }

        public Size GetPageSize(Orientation o)
        {
            if (o == Orientation.Vertical)
                return PortraitPageSize;
            else
                return new Size(PortraitPageSize.Height, PortraitPageSize.Width);
        }

        public static PrintSize CreateA4()
        {
            return new PrintSize
            {
                PortraitPageSize = new Size(793, 1123),
                DisplayName = "A4",
                HorizontalMargin = 24,
                VerticalMargin = 24
            };
        }
    }

    public class PrintPage
    {
        public int PageNumber { get; }
        public UIElement Page { get; }

        public PrintPage(int pageNumber, UIElement content)
        {
            PageNumber = pageNumber;
            Page = content;
        }
    }

    public class PrintConfig
    {
        public double GridSize { get; set; }
    }

    public class PrintHelper
    {
        private PrintViewModel _printModel { get; }

        /// <summary>
        /// The percent of app's margin width, content is set at 85% (0.85) of the area's width
        /// </summary>
        protected double ApplicationContentMarginLeft = 0.075;

        /// <summary>
        /// The percent of app's margin height, content is set at 94% (0.94) of the area's height
        /// </summary>
        protected double ApplicationContentMarginTop = 0.03;

        /// <summary>
        /// PrintDocument is used to prepare the pages for printing.
        /// Prepare the pages to print in the handlers for the Paginate, GetPreviewPage, and AddPages events.
        /// </summary>
        protected PrintDocument printDocument;

        /// <summary>
        /// Marker interface for document source
        /// </summary>
        protected IPrintDocumentSource printDocumentSource;

        /// <summary>
        /// A list of UIElements used to store the print preview pages.  This gives easy access
        /// to any desired preview page.
        /// </summary>
        internal List<PrintPage> printPreviewPages;

        // Event callback which is called after print preview pages are generated.  Photos scenario uses this to do filtering of preview pages
        protected event EventHandler PreviewPagesCreated;

        /// <summary>
        ///  A reference back to the FontMap used to access XAML elements on the scenario page
        /// </summary>
        protected FontMapView fontMap;

        /// <summary>
        ///  A hidden canvas used to hold pages we wish to print
        /// </summary>
        protected Canvas PrintCanvas => fontMap.FindName("PrintCanvas") as Canvas;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="fontMap">The FontMap to print</param>
        public PrintHelper(FontMapView fontMap, PrintViewModel printModel)
        {
            this.fontMap = fontMap;
            _printModel = printModel;
            printPreviewPages = new List<PrintPage>();
        }

        /// <summary>
        /// This function registers the app for printing with Windows and sets up the necessary event handlers for the print process.
        /// </summary>
        public virtual void RegisterForPrinting()
        {
            printDocument = new PrintDocument();
            printDocumentSource = printDocument.DocumentSource;
            printDocument.Paginate += CreatePrintPreviewPages;
            printDocument.GetPreviewPage += GetPrintPreviewPage;
            printDocument.AddPages += AddPrintPages;

            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested += PrintTaskRequested;
        }

        /// <summary>
        /// This function unregisters the app for printing with Windows.
        /// </summary>
        public virtual void UnregisterForPrinting()
        {
            if (printDocument == null)
            {
                return;
            }

            printDocument.Paginate -= CreatePrintPreviewPages;
            printDocument.GetPreviewPage -= GetPrintPreviewPage;
            printDocument.AddPages -= AddPrintPages;

            // Remove the handler for printing initialization.
            PrintManager printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested -= PrintTaskRequested;

            PrintCanvas.Children.Clear();
        }

        public async Task ShowPrintUIAsync()
        {
            // Catch and print out any errors reported
            try
            {
                await PrintManager.ShowPrintUIAsync();
            }
            catch (Exception e)
            {
                UnhandledExceptionDialog.Show(e);
            }
        }

        /// <summary>
        /// This is the event handler for PrintManager.PrintTaskRequested.
        /// </summary>
        /// <param name="sender">PrintManager</param>
        /// <param name="e">PrintTaskRequestedEventArgs </param>
        protected virtual void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs e)
        {
            PrintTask printTask = null;
            printTask = e.Request.CreatePrintTask("Character Map UWP", sourceRequested =>
            {
                // Print Task event handler is invoked when the print job is completed.
                printTask.Completed += (s, args) =>
                {
                    Clear();

                    GC.Collect();

                    // Notify the user when the print operation fails.
                    if (args.Completion == PrintTaskCompletion.Failed)
                    {
                        // .....
                    }
                };

                sourceRequested.SetSource(printDocumentSource);
            });
        }

        public void Clear()
        {
            lock (printPreviewPages)
            {
                // Clear the cache of preview pages
                printPreviewPages.Clear();

                // Clear the print canvas of preview pages
                PrintCanvas.Children.Clear();
            }
        }

        /// <summary>
        /// This is the event handler for PrintDocument.Paginate. It creates print preview pages for the app.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Paginate Event Arguments</param>
        protected virtual void CreatePrintPreviewPages(object sender, PaginateEventArgs e)
        {
            lock (printPreviewPages)
            {
                // Clear the cache of preview pages
                printPreviewPages.Clear();

                // Clear the print canvas of preview pages
                PrintCanvas.Children.Clear();

                // Get the PrintTaskOptions
                PrintTaskOptions printingOptions = e.PrintTaskOptions;

                // Get the page description to determine how big the page is
                PrintPageDescription pageDescription = printingOptions.GetPageDescription(0);

                var pageWidth = pageDescription.PageSize.Width;
                var pageHeight = pageDescription.PageSize.Height;

                // If the ImageableRect is smaller than the app provided margins use the ImageableRect
                double marginWidth = Math.Max(pageDescription.PageSize.Width - pageDescription.ImageableRect.Width, _printModel.HorizontalMargin * 2);
                double marginHeight = Math.Max(pageDescription.PageSize.Height - pageDescription.ImageableRect.Height, _printModel.VerticalMargin * 2);

                // Set-up "printable area" on the "paper"
                var printableAreaWidth = pageWidth - marginWidth;
                var printableAreaHeight = pageHeight - marginHeight;

                int charsPerPage = FontMapPrintPage.CalculateGlyphsPerPage(
                    new Windows.Foundation.Size(Math.Floor(printableAreaWidth), Math.Floor(printableAreaHeight)), 
                    _printModel);

                var enumerator = fontMap.ViewModel.Chars.GetEnumerator();

                bool hasMore = true;
                while (hasMore)
                {
                    var page = new FontMapPrintPage(fontMap.ViewModel, _printModel, fontMap.CharGrid.ItemTemplate)
                    {
                        Width = pageWidth,
                        Height = pageHeight
                    };

                    Grid printableArea = page.PrintableArea;

                    // Set-up "printable area" on the "paper"
                    printableArea.Width = printableAreaWidth;
                    printableArea.Height = printableAreaHeight ;

                    // Layout page
                    hasMore = page.AddCharacters(printPreviewPages.Count, charsPerPage, fontMap.ViewModel.Chars);

                    // Add the (newly created) page to the print canvas which is part of the visual tree and force it to go
                    // through layout so that the linked containers correctly distribute the content inside them.
                    PrintCanvas.Children.Add(page);
                    PrintCanvas.InvalidateMeasure();
                    PrintCanvas.UpdateLayout();

                    printPreviewPages.Add(new PrintPage(printPreviewPages.Count + e.CurrentPreviewPageNumber, page));
                }

                if (PreviewPagesCreated != null)
                {
                    PreviewPagesCreated.Invoke(printPreviewPages, null);
                }

                PrintDocument printDoc = (PrintDocument)sender;

                // Report the number of preview pages created
                printDoc.SetPreviewPageCount(printPreviewPages.Count, PreviewPageCountType.Intermediate);
            }
        }

        /// <summary>
        /// This is the event handler for PrintDocument.GetPrintPreviewPage. It provides a specific print preview page,
        /// in the form of an UIElement, to an instance of PrintDocument. PrintDocument subsequently converts the UIElement
        /// into a page that the Windows print system can deal with.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Arguments containing the preview requested page</param>
        protected virtual void GetPrintPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            PrintDocument printDoc = (PrintDocument)sender;
            printDoc.SetPreviewPage(e.PageNumber, printPreviewPages[e.PageNumber -1]?.Page);
        }

        /// <summary>
        /// This is the event handler for PrintDocument.AddPages. It provides all pages to be printed, in the form of
        /// UIElements, to an instance of PrintDocument. PrintDocument subsequently converts the UIElements
        /// into pages that the Windows print system can deal with.
        /// </summary>
        /// <param name="sender">PrintDocument</param>
        /// <param name="e">Add page event arguments containing a print task options reference</param>
        protected virtual void AddPrintPages(object sender, AddPagesEventArgs e)
        {
            // Loop over all of the preview pages and add each one to  add each page to be printed
            for (int i = 0; i < printPreviewPages.Count; i++)
            {
                // We should have all pages ready at this point...
                printDocument.AddPage(printPreviewPages[i].Page);
            }

            PrintDocument printDoc = (PrintDocument)sender;

            // Indicate that all of the print pages have been provided
            printDoc.AddPagesComplete();
        }
    }
}