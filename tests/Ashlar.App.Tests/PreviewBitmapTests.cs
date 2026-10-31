using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.Core;

namespace Ashlar.App.Tests;

public class PreviewBitmapTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void ZoomingInStopsAtThirtyTwoTimes()
    {
        var editor = new TilesetEditorViewModel("tiles");

        for (var i = 0; i < 500; i++)
            editor.ZoomIn();

        Assert.Equal(32.0, editor.ZoomLevel);
        Assert.Equal("3,200%", editor.ZoomPercentageText.Replace(" ", ""));
    }

    [AvaloniaFact]
    public void ZoomingOutStopsAtAQuarter()
    {
        var editor = new TilesetEditorViewModel("tiles");

        for (var i = 0; i < 500; i++)
            editor.ZoomOut();

        Assert.Equal(0.25, editor.ZoomLevel);
    }

    [AvaloniaFact]
    public void ZoomStepGrowsWithTheZoomLevel()
    {
        var editor = new TilesetEditorViewModel("tiles") { ZoomLevel = 10.0 };

        editor.ZoomIn();

        Assert.Equal(11.0, editor.ZoomLevel, 6);
    }

    [Theory]
    [InlineData(0.25, BitmapInterpolationMode.HighQuality)]
    [InlineData(0.99, BitmapInterpolationMode.HighQuality)]
    [InlineData(1.0, BitmapInterpolationMode.None)]
    [InlineData(8.0, BitmapInterpolationMode.None)]
    [InlineData(32.0, BitmapInterpolationMode.None)]
    public void PixelsAreCrispFromHundredPercentOn(double zoomLevel, BitmapInterpolationMode expected)
    {
        var editor = new TilesetEditorViewModel("tiles") { ZoomLevel = zoomLevel };

        Assert.Equal(expected, editor.PreviewInterpolation);
    }

    [Theory]
    [InlineData(1.0, false)]
    [InlineData(7.9, false)]
    [InlineData(8.0, true)]
    [InlineData(32.0, true)]
    public void PixelGridAppearsFromEightTimesOn(double zoomLevel, bool expected)
    {
        var editor = new TilesetEditorViewModel("tiles") { ZoomLevel = zoomLevel };

        Assert.Equal(expected, editor.ShowPixelGrid);
    }

    [AvaloniaFact]
    public void DeleteUpdatesTheDisplayedBitmapInPlace()
    {
        var viewModel = OpenImage(32, 32, new SKColor(255, 0, 0, 255));
        var bitmap = viewModel.Editor().PreviewBitmap;
        var updates = 0;
        viewModel.Editor().PreviewPixelsChanged += (_, _) => updates++;
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.Editor().DeleteSelectedTiles();

        Assert.Same(bitmap, viewModel.Editor().PreviewBitmap);
        Assert.Equal(1, updates);
        Assert.Equal([0, 0, 0, 0], PixelAt(viewModel, 1, 1));
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 17, 1));
    }

    [AvaloniaFact]
    public void UndoRestoresTheDisplayedPixels()
    {
        var viewModel = OpenImage(32, 32, new SKColor(255, 0, 0, 255));
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().Undo();

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
        var editor = viewModel.Editor();
        var first = editor.PreviewBitmap;

        editor.HandleCanvasClick(5, 5, addToSelection: false);
        editor.DeleteSelectedTiles();
        editor.Undo();
        Assert.Same(first, editor.PreviewBitmap);
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 0, 0));

        editor.LoadNewTileset(EditableTilesetImage.Load(WriteImage(48, 16, new SKColor(0, 0, 255, 255))), "wide.png", 16, 16);
        Assert.NotSame(first, editor.PreviewBitmap);
        Assert.Equal(new Avalonia.PixelSize(48, 16), editor.PreviewBitmap!.PixelSize);
    }

    private static unsafe byte[] PixelAt(MainViewModel viewModel, int x, int y)
    {
        using var framebuffer = viewModel.Editor().PreviewBitmap!.Lock();
        var pixel = (byte*)framebuffer.Address + y * framebuffer.RowBytes + x * 4;
        return [pixel[0], pixel[1], pixel[2], pixel[3]];
    }

    private MainViewModel OpenImage(int width, int height, SKColor color)
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(width, height, color), 16, 16);
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
