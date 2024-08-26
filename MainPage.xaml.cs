using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Skia; 
using System.Net;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.IO;

namespace NewPDF
{
    public partial class MainPage : ContentPage
    {
        private PathF _path;
        private string _openedPdfPath;
        private string _savedImagePath;
        public MainPage()
        {
            InitializeComponent();

            _path = new PathF();

            // Methods that bind drawing events
            drawingView.StartInteraction += OnStartInteraction;
            drawingView.DragInteraction += OnDragInteraction;
            drawingView.EndInteraction += OnEndInteraction;

            drawingView.Drawable = new MyDrawable(_path);
            // Webiview permissions for android
#if ANDROID
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("pdfview", (handler, View) =>
            {
                handler.PlatformView.Settings.AllowFileAccess = true;
                handler.PlatformView.Settings.AllowFileAccessFromFileURLs = true;
                handler.PlatformView.Settings.AllowUniversalAccessFromFileURLs = true;
                handler.PlatformView.AddJavascriptInterface(new JSBridge(this), "jsBridge");
            });
#endif

        }

        private double _selectedX;
        private double _selectedY;
        private int currentPageNumber;
        public void OnPdfTapped(double x, double y)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                _selectedX = x;
                _selectedY = y;
                DisplayAlert("Coordinates", $"X: {x}, Y: {y}", "Ok");

            });
        }
        // Drawing methods
        private void OnStartInteraction(object sender, TouchEventArgs e)
        {
            _path.MoveTo(e.Touches[0].X, e.Touches[0].Y);
            drawingView.Invalidate();
        }

        private void OnDragInteraction(object sender, TouchEventArgs e)
        {
            _path.LineTo(e.Touches[0].X, e.Touches[0].Y);
            drawingView.Invalidate();
        }

        private void OnEndInteraction(object sender, TouchEventArgs e)
        {
            _path.LineTo(e.Touches[0].X, e.Touches[0].Y);
            drawingView.Invalidate();
        }

        private void OnVisiblePanelButton(object sender, EventArgs e)
        {
            // If the drawing panel and buttons are visible, hide them
            if (drawingView.IsVisible)
            {
                drawingView.IsVisible = false;
                saveButton.IsVisible = false;
                addToPdfButton.IsVisible = false;
            }
            // If the drawing panel and buttons are hidden, show them
            else
            {
                drawingView.IsVisible = true;
                saveButton.IsVisible = true;
                addToPdfButton.IsVisible = true;
            }
        }

        private async void OnPickPdfButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose a PDF File",
                    FileTypes = FilePickerFileType.Pdf
                });

                if (result != null)
                {
                    _openedPdfPath = result.FullPath; // PDF path is set here
#if ANDROID
                    pdfview.Source = $"file:///android_asset/pdfjs/web/viewer.html?file=file://{WebUtility.UrlEncode(_openedPdfPath)}";
#else
                    pdfview.Source = _openedPdfPath;
#endif
                }
                if (string.IsNullOrEmpty(_openedPdfPath))
                {
                    await DisplayAlert("Error", "PDF file is not selected!", "Ok");
                    return;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occured while choosing PDF file: {ex.Message}", "Tamam");
            }
        }

        private async void OnSaveDrawingButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var fileName = $"drawing_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                _savedImagePath = Path.Combine(FileSystem.AppDataDirectory, fileName); // Çizim yolu burada ayarlanır

                using (var bitmap = new SkiaBitmapExportContext((int)drawingView.Width, (int)drawingView.Height, 1.0f))
                {
                    var canvas = bitmap.Canvas;
                    var drawable = new MyDrawable(_path);
                    drawable.Draw(canvas, new RectF(0, 0, (float)drawingView.Width, (float)drawingView.Height));
                    bitmap.WriteToFile(_savedImagePath);
                }
                
            if (string.IsNullOrEmpty(_savedImagePath))
            {
                 await DisplayAlert("Error", "Drawing is not saved!", "Ok");
                 return;
            }

                await DisplayAlert("Successful", $"Drawing saved!: {_savedImagePath}", "Ok");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occured while saving drawing: {ex.Message}", "Ok");
            }
        }

        private async void OnAddImageToPdfButtonClicked(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_openedPdfPath) || string.IsNullOrEmpty(_savedImagePath))
            {
                await DisplayAlert("Error", "LPlease select a PDF file and drawing first.", "Ok");
                return;
            }

            try
            {
                // Open popup window to get PDF filename from user
                string fileName = await DisplayPromptAsync("Save PDF", "Please enter a name for the PDF file:", initialValue: $"updated_{DateTime.Now:yyyyMMdd_HHmmss}");

                if (string.IsNullOrEmpty(fileName))
                {
                    await DisplayAlert("Error", "Please enter a valid filename.", "Ok");
                    return;
                }

                // Create path to save to Downloads folder on Android
                string outputFilePath;

#if ANDROID
                outputFilePath = Path.Combine(Android.OS.Environment.ExternalStorageDirectory.AbsolutePath, Android.OS.Environment.DirectoryDownloads, $"{fileName}.pdf");
#else
        outputFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), $"{fileName}.pdf");
#endif

                // Make sure the specified path exists
                var directoryPath = Path.GetDirectoryName(outputFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (var inputPdfStream = File.OpenRead(_openedPdfPath))
                using (var outputPdfStream = File.Create(outputFilePath))
                using (var imageStream = File.OpenRead(_savedImagePath))
                {
                    var document = PdfReader.Open(inputPdfStream, PdfDocumentOpenMode.Modify);
                    var page = document.Pages[0]; // We are working on the first page, but you can use a different page if you wish
                    var graphics = XGraphics.FromPdfPage(page);

                    var image = XImage.FromStream(() => imageStream);

                    // Adjust the size of the signature (for example, reduce by 50%)
                    double imageWidth = image.PixelWidth * 0.4;
                    double imageHeight = image.PixelHeight * 0.4;

                    // Place signature using user-selected coordinates
                    double xPosition = _selectedX - 14;  // To align the signature center to the clicked point
                    double yPosition = _selectedY + 40; // Use page height to fix y axis

                    graphics.DrawImage(image, xPosition, yPosition, imageWidth, imageHeight);

                    document.Save(outputPdfStream);
                }

                await DisplayAlert("Successful", $"PDF Saved: {outputFilePath}", "Ok");

                // PDF dosyasını göster
#if ANDROID
                pdfview.Source = $"file:///android_asset/pdfjs/web/viewer.html?file=file://{WebUtility.UrlEncode(outputFilePath)}";
#else
        pdfview.Source = outputFilePath;
#endif
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred while inserting an image into the PDF: {ex.Message}", "Ok");
            }
        }

        private class MyDrawable : IDrawable
        {
            private readonly PathF _path;

            public MyDrawable(PathF path)
            {
                _path = path;
            }

            public void Draw(ICanvas canvas, RectF dirtyRect)
            {
                canvas.StrokeColor = Colors.Black;
                canvas.StrokeSize = 2;
                canvas.DrawPath(_path);
            }
        }

#if ANDROID
        public class JSBridge : Java.Lang.Object
        {
            readonly MainPage _page;

            public JSBridge(MainPage page)
            {
                _page = page;
            }

            [Android.Webkit.JavascriptInterface]
            [Java.Interop.Export("invokeAction")]
            public void InvokeAction(double x, double y)
            {
                _page.OnPdfTapped(x, y);
            }
        }
#endif
    }
}