using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class PreviewBitmapTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void ZoomingInStopsAtThirtyTwoTimes()
    {
        var viewModel = new MainViewModel();

        for (var i = 0; i < 500; i++)
            viewModel.ZoomIn();

        Assert.Equal(32.0, viewModel.ZoomLevel);
        Assert.Equal("3,200%", viewModel.ZoomPercentageText.Replace(" ", ""));
    }

    [Fact]
    public void ZoomingOutStopsAtAQuarter()
    {
        var viewModel = new MainViewModel();

        for (var i = 0; i < 500; i++)
            viewModel.ZoomOut();

        Assert.Equal(0.25, viewModel.ZoomLevel);
    }

    [Fact]
    public void ZoomStepGrowsWithTheZoomLevel()
    {
        var viewModel = new MainViewModel { ZoomLevel = 10.0 };

        viewModel.ZoomIn();

        Assert.Equal(11.0, viewModel.ZoomLevel, 6);
    }

    [Theory]
    [InlineData(0.25, BitmapInterpolationMode.HighQuality)]
    [InlineData(0.99, BitmapInterpolationMode.HighQuality)]
    [InlineData(1.0, BitmapInterpolationMode.None)]
    [InlineData(8.0, BitmapInterpolationMode.None)]
    [InlineData(32.0, BitmapInterpolationMode.None)]
    public void PixelsAreCrispFromHundredPercentOn(double zoomLevel, BitmapInterpolationMode expected)
    {
        var viewModel = new MainViewModel { ZoomLevel = zoomLevel };

        Assert.Equal(expected, viewModel.PreviewInterpolation);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(7.9, false)]
    [InlineData(8.0, true)]
    [InlineData(32.0, true)]
    public void PixelGridAppearsFromEightTimesOn(double zoomLevel, bool expected)
    {
        var viewModel = new MainViewModel { ZoomLevel = zoomLevel };

        Assert.Equal(expected, viewModel.ShowPixelGrid);
    }

    [AvaloniaFact]
    public void DeleteUpdatesTheDisplayedBitmapInPlace()
    {
        var viewModel = OpenImage(32, 32, new SKColor(255, 0, 0, 255));
        var bitmap = viewModel.PreviewBitmap;
        var updates = 0;
        viewModel.PreviewPixelsChanged += (_, _) => updates++;
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.DeleteSelectedTiles();

        Assert.Same(bitmap, viewModel.PreviewBitmap);
        Assert.Equal(1, updates);
        Assert.Equal([0, 0, 0, 0], PixelAt(viewModel, 1, 1));
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 17, 1));
    }

    [AvaloniaFact]
    public void UndoRestoresTheDisplayedPixels()
    {
        var viewModel = OpenImage(32, 32, new SKColor(255, 0, 0, 255));
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.Undo();

        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 1, 1));
    }

    [AvaloniaFact]
    public void SemiTransparentPixelsStayUnpremultiplied()
    {
        var viewModel = OpenImage(16, 16, new SKColor(200, 100, 50, 128));

        Assert.Equal([200, 100, 50, 128], PixelAt(viewModel, 5, 5));
    }

    [AvaloniaFact]
    public void ANewBitmapIsOnlyCreatedWhenTheImageSizeChanges()
    {
        var viewModel = OpenImage(32, 32, new SKColor(255, 0, 0, 255));
        var first = viewModel.PreviewBitmap;

        viewModel.LoadTileset(WriteImage(32, 32, new SKColor(0, 255, 0, 255)));
        Assert.Same(first, viewModel.PreviewBitmap);
        Assert.Equal([0, 255, 0, 255], PixelAt(viewModel, 0, 0));

        viewModel.LoadTileset(WriteImage(48, 16, new SKColor(0, 0, 255, 255)));
        Assert.NotSame(first, viewModel.PreviewBitmap);
        Assert.Equal(new Avalonia.PixelSize(48, 16), viewModel.PreviewBitmap!.PixelSize);
    }

    private static unsafe byte[] PixelAt(MainViewModel viewModel, int x, int y)
    {
        using var framebuffer = viewModel.PreviewBitmap!.Lock();
        var pixel = (byte*)framebuffer.Address + y * framebuffer.RowBytes + x * 4;
        return [pixel[0], pixel[1], pixel[2], pixel[3]];
    }

    private MainViewModel OpenImage(int width, int height, SKColor color)
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(width, height, color));
        viewModel.TileWidth = 16;
        viewModel.TileHeight = 16;
        return viewModel;
    }

    private string WriteImage(int width, int height, SKColor color)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, color);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
