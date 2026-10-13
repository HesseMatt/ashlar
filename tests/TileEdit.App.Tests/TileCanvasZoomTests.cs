using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

    [AvaloniaTheory]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void PanningAsFarAsPossibleLeavesOneTileRowAndColumnVisible(double zoomLevel)
    {
        const int tileSize = 30;
        using var canvas = TileCanvas.Open(ImageWidth, ImageHeight);
        canvas.SetTileSize(tileSize);
        canvas.SetZoom(zoomLevel);
        Assert.True(canvas.OverflowsViewport, $"the canvas does not overflow the viewport at {zoomLevel:P0} zoom");
        var scaledTile = tileSize * zoomLevel;

        canvas.ScrollAsFarAsPossible();
        var farBounds = canvas.ImageBoundsInViewport();
        Assert.True(Math.Abs(farBounds.Right - scaledTile) <= 1, $"{farBounds.Right}px of the image's right edge remain, expected {scaledTile}");
        Assert.True(Math.Abs(farBounds.Bottom - scaledTile) <= 1, $"{farBounds.Bottom}px of the image's bottom edge remain, expected {scaledTile}");

        canvas.ScrollToStart();
        var nearBounds = canvas.ImageBoundsInViewport();
        Assert.True(Math.Abs(canvas.Viewport.Width - nearBounds.X - scaledTile) <= 1, $"image starts at x={nearBounds.X}, expected one {scaledTile}px column visible");
        Assert.True(Math.Abs(canvas.Viewport.Height - nearBounds.Y - scaledTile) <= 1, $"image starts at y={nearBounds.Y}, expected one {scaledTile}px row visible");
    }

    [AvaloniaFact]
    public void ImageFittingTheViewportHasNoScrollRange()
    {
        using var canvas = TileCanvas.Open(ImageWidth, ImageHeight);
        canvas.SetTileSize(30);
        canvas.SetZoom(0.5);

        Assert.True(canvas.Extent.Width <= canvas.Viewport.Width, "unexpected horizontal scroll range");
        Assert.True(canvas.Extent.Height <= canvas.Viewport.Height, "unexpected vertical scroll range");
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

    [AvaloniaFact]
    public void OverflowingTilesetStartsWithItsTopLeftCornerAtTheViewportsTopLeftCorner()
    {
        using var canvas = TileCanvas.Open(1200, 900);

        Assert.True(canvas.OverflowsViewport, "the canvas does not overflow the viewport");
        AssertImageOrigin(canvas, 0, 0);
    }

    [AvaloniaFact]
    public void FittingTilesetStartsCenteredWithoutScrolling()
    {
        using var canvas = TileCanvas.Open(200, 100);

        Assert.Equal(default, canvas.Offset);
        AssertCenteredHorizontally(canvas, 200);
        AssertCenteredVertically(canvas, 100);
    }

    [AvaloniaFact]
    public void TilesetOverflowingOnlyVerticallyIsAlignedToTheTopAndCenteredHorizontally()
    {
        using var canvas = TileCanvas.Open(ImageWidth, ImageHeight);

        Assert.True(canvas.Extent.Width <= canvas.Viewport.Width, "unexpected horizontal scroll range");
        Assert.True(canvas.Extent.Height > canvas.Viewport.Height, "the canvas does not overflow vertically");

        AssertCenteredHorizontally(canvas, ImageWidth);
        Assert.Equal(0, canvas.ImageOriginInViewport().Y, 1);
    }

    [AvaloniaFact]
    public void ResetZoomPositionsTheTilesetAtTheTopLeftCorner()
    {
        using var canvas = TileCanvas.Open(1200, 900);
        canvas.SetZoom(3.0);
        canvas.ScrollAsFarAsPossible();

        canvas.ResetZoomWithShortcut();

        AssertImageOrigin(canvas, 0, 0);
    }

    private static void AssertCenteredHorizontally(TileCanvas canvas, double imageWidth)
    {
        var leftGap = canvas.ImageOriginInViewport().X;
        var rightGap = canvas.Viewport.Width - (leftGap + imageWidth);
        Assert.True(Math.Abs(leftGap - rightGap) <= 1, $"image is off-center horizontally: {leftGap}px left, {rightGap}px right");
    }

    private static void AssertCenteredVertically(TileCanvas canvas, double imageHeight)
    {
        var topGap = canvas.ImageOriginInViewport().Y;
        var bottomGap = canvas.Viewport.Height - (topGap + imageHeight);
        Assert.True(Math.Abs(topGap - bottomGap) <= 1, $"image is off-center vertically: {topGap}px top, {bottomGap}px bottom");
    }

    private static void AssertImageOrigin(TileCanvas canvas, double x, double y)
    {
        var origin = canvas.ImageOriginInViewport();
        Assert.True(Math.Abs(origin.X - x) <= 1 && Math.Abs(origin.Y - y) <= 1, $"image starts at ({origin.X}, {origin.Y}), expected ({x}, {y})");
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

        public Vector Offset => _scrollViewer.Offset;

        public Size Extent => _scrollViewer.Extent;

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

        public void ResetZoomWithShortcut()
        {
            _window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.Control);
            Layout();
        }

        public void SetTileSize(int size)
        {
            _viewModel.TileWidth = size;
            _viewModel.TileHeight = size;
            Layout();
        }

        public void ScrollToStart()
        {
            _scrollViewer.Offset = default;
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

        public Rect ImageBoundsInViewport() =>
            new(ImageOriginInViewport(), new Size(_content.Bounds.Width * _viewModel.ZoomLevel, _content.Bounds.Height * _viewModel.ZoomLevel));

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
