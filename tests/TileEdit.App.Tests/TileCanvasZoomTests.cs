using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using SkiaSharp;
using TileEdit.App.ViewModels;
using TileEdit.App.Views;

namespace TileEdit.App.Tests;

public class TileCanvasZoomTests
{
    // The tileset from the bug report. Paired with the window size below it
    // overflows the viewport on both axes from 200% zoom on.
    private const int ImageWidth = 330;
    private const int ImageHeight = 570;

    [AvaloniaTheory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    [InlineData(8.0)]
    public void PanningAsFarAsPossibleReachesTheImagesBottomRightPixel(double zoomLevel)
    {
        using var canvas = TileCanvas.Open(ImageWidth, ImageHeight);
        canvas.SetZoom(zoomLevel);

        // Without scroll range on both axes the assertions below would hold
        // trivially, and the test would stop covering the bug.
        Assert.True(canvas.OverflowsViewport, $"the canvas does not overflow the viewport at {zoomLevel:P0} zoom");

        canvas.ScrollAsFarAsPossible();

        var farCorner = canvas.ViewportCornerInImagePixels();
        Assert.True(farCorner.X >= ImageWidth, $"the rightmost reachable image pixel is x={farCorner.X}, expected at least {ImageWidth}");
        Assert.True(farCorner.Y >= ImageHeight, $"the lowest reachable image pixel is y={farCorner.Y}, expected at least {ImageHeight}");
    }

    [AvaloniaFact]
    public void ImageNarrowerThanTheViewportStaysHorizontallyCentered()
    {
        using var canvas = TileCanvas.Open(ImageWidth, ImageHeight);
        canvas.SetZoom(0.5);

        var leftGap = canvas.ImageOriginInViewport().X;
        var rightGap = canvas.Viewport.Width - (leftGap + (ImageWidth * 0.5));

        // A pixel of slack, because an odd viewport width can't be split evenly.
        Assert.True(Math.Abs(leftGap - rightGap) <= 1, $"image is off-center: {leftGap}px of empty space to its left, {rightGap}px to its right");
    }

    private sealed class TileCanvas : IDisposable
    {
        // Fixed instead of inherited from the window's own default size, so the
        // geometry the tests rely on doesn't change with the app's chrome.
        private const int WindowWidth = 800;
        private const int WindowHeight = 600;

        private readonly Window _window;
        private readonly MainViewModel _viewModel;
        private readonly ScrollViewer _scrollViewer;
        private readonly Panel _content;
        private readonly string _imagePath;

        private TileCanvas(Window window, MainViewModel viewModel, string imagePath)
        {
            _window = window;
            _viewModel = viewModel;
            _imagePath = imagePath;
            _scrollViewer = window.FindControl<ScrollViewer>("TileCanvasScrollViewer")!;
            _content = window.FindControl<Panel>("TileCanvasContent")!;
        }

        public Size Viewport => _scrollViewer.Viewport;

        public bool OverflowsViewport =>
            _scrollViewer.Extent.Width > _scrollViewer.Viewport.Width &&
            _scrollViewer.Extent.Height > _scrollViewer.Viewport.Height;

        public static TileCanvas Open(int width, int height)
        {
            var imagePath = CreateSampleImage(width, height);
            var viewModel = new MainViewModel();
            var window = new MainWindow
            {
                DataContext = viewModel,
                Width = WindowWidth,
                Height = WindowHeight,
            };
            window.Show();

            viewModel.LoadTileset(imagePath);

            var canvas = new TileCanvas(window, viewModel, imagePath);
            canvas.Layout();
            return canvas;
        }

        public void SetZoom(double zoomLevel)
        {
            _viewModel.ZoomLevel = zoomLevel;
            Layout();
        }

        // The ScrollViewer clamps the offset to its own scroll range, so an
        // oversized value lands exactly at "panned as far as it goes".
        public void ScrollAsFarAsPossible()
        {
            _scrollViewer.Offset = new Vector(double.MaxValue, double.MaxValue);
            Layout();
        }

        // The image pixel rendered in the viewport's bottom-right corner.
        public Point ViewportCornerInImagePixels() =>
            _scrollViewer.TranslatePoint(new Point(Viewport.Width, Viewport.Height), _content)!.Value;

        // Where the image's top-left pixel ends up within the viewport.
        public Point ImageOriginInViewport() =>
            _content.TranslatePoint(new Point(0, 0), _scrollViewer)!.Value;

        public void Dispose()
        {
            _window.Close();
            File.Delete(_imagePath);
        }

        private void Layout() => _window.GetLayoutManager()?.ExecuteLayoutPass();

        private static string CreateSampleImage(int width, int height)
        {
            var filePath = Path.GetTempFileName() + ".png";
            using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(filePath);
            data.SaveTo(stream);
            return filePath;
        }
    }
}
