using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SkiaSharp;
using TileEdit.App.ViewModels;
using TileEdit.Core.Model;

namespace TileEdit.App.Tests;

public class DrawToolTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void StrokeIsIgnoredUnlessTheDrawToolIsActive()
    {
        var viewModel = OpenImage();
        viewModel.IsDrawToolActive = false;

        Assert.False(viewModel.BeginStroke(1, 1));
        Assert.True(viewModel.CanEditTileSize);
    }

    [AvaloniaFact]
    public void ClickPaintsOnePixelWithThePickedColorWithoutBlending()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(128, 10, 20, 30);

        viewModel.BeginStroke(1, 1);
        viewModel.CommitStroke();

        Assert.Equal([10, 20, 30, 128], PixelAt(viewModel, 1, 1));
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 2, 1));
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 1, 2));
    }

    [AvaloniaFact]
    public void DraggingFastLeavesNoGaps()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(2, 2);
        viewModel.ContinueStroke(12, 7);
        viewModel.CommitStroke();

        var painted = Enumerable.Range(0, 64).SelectMany(y => Enumerable.Range(0, 64).Select(x => (x, y)))
            .Where(p => PixelAt(viewModel, p.x, p.y) is [0, 0, 0, 255])
            .ToList();
        Assert.Equal(11, painted.Count);
        Assert.Contains((2, 2), painted);
        Assert.Contains((12, 7), painted);
    }

    [AvaloniaFact]
    public void PaintingWithAlphaZeroErasesThePixel()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(0, 0, 0, 0);

        viewModel.BeginStroke(1, 1);
        viewModel.CommitStroke();

        Assert.Equal(0, PixelAt(viewModel, 1, 1)[3]);
    }

    [AvaloniaFact]
    public void StrokeOutsideTheCanvasIsClipped()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(-5, 3);
        viewModel.ContinueStroke(70, 3);
        viewModel.CommitStroke();

        Assert.Equal(0, PixelAt(viewModel, 0, 3)[0]);
        Assert.Equal(0, PixelAt(viewModel, 63, 3)[0]);
        Assert.Equal(64, viewModel.ImageWidth);
    }

    [AvaloniaFact]
    public void StrokeOutsideTheCanvasOnlyRecordsNothing()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(-5, -5);
        viewModel.ContinueStroke(-1, -1);
        viewModel.CommitStroke();

        Assert.False(viewModel.CanUndo);
    }

    [AvaloniaFact]
    public void PaintingOpaquePixelsIntoEmptyCellsCreatesTilesWithTheNextFreeNumber()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(40, 40);
        viewModel.ContinueStroke(40, 20);
        viewModel.CommitStroke();

        Assert.Equal(["tiles_0001", "tiles_0002", "tiles_0003"], viewModel.Tiles.Select(t => t.Name));
        Assert.Equal([(0, 0), (32, 32), (32, 16)], viewModel.Tiles.Select(t => (t.X, t.Y)));
    }

    [AvaloniaFact]
    public void PaintingAlphaZeroIntoAnEmptyCellCreatesNoTile()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(0, 0, 0, 0);

        viewModel.BeginStroke(40, 40);
        viewModel.CommitStroke();

        Assert.Single(viewModel.Tiles);
    }

    [AvaloniaFact]
    public void ErasingAllPixelsOfATileKeepsTheTile()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(0, 0, 0, 0);

        viewModel.BeginStroke(0, 0);
        viewModel.ContinueStroke(15, 15);
        viewModel.CommitStroke();

        Assert.Single(viewModel.Tiles);
    }

    [AvaloniaFact]
    public void WholeStrokeIsOneUndoStepThatRestoresPixelsAndTiles()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(20, 40);
        viewModel.ContinueStroke(50, 40);
        viewModel.CommitStroke();
        Assert.Equal(4, viewModel.Tiles.Count);

        viewModel.Undo();

        Assert.False(viewModel.CanUndo);
        Assert.Single(viewModel.Tiles);
        Assert.Equal(0, PixelAt(viewModel, 30, 40)[3]);

        viewModel.Redo();

        Assert.Equal(4, viewModel.Tiles.Count);
        Assert.Equal([0, 0, 0, 255], PixelAt(viewModel, 30, 40));
    }

    [AvaloniaFact]
    public void CancellingAStrokeRestoresPixelsAndTiles()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(20, 40);
        viewModel.ContinueStroke(50, 40);
        viewModel.CancelStroke();
        viewModel.CommitStroke();

        Assert.False(viewModel.CanUndo);
        Assert.Single(viewModel.Tiles);
        Assert.Equal(0, PixelAt(viewModel, 30, 40)[3]);
    }

    [AvaloniaFact]
    public void DrawingLocksTheTileSize()
    {
        var viewModel = OpenImage();

        viewModel.BeginStroke(1, 1);
        viewModel.CommitStroke();

        Assert.False(viewModel.CanEditTileSize);
    }

    [AvaloniaFact]
    public void PreviewBitmapIsUpdatedInPlaceDuringTheStroke()
    {
        var viewModel = OpenImage();
        var bitmap = viewModel.PreviewBitmap;
        var redrawn = 0;
        viewModel.PreviewPixelsChanged += (_, _) => redrawn++;

        viewModel.BeginStroke(1, 1);
        viewModel.ContinueStroke(5, 1);

        Assert.Same(bitmap, viewModel.PreviewBitmap);
        Assert.Equal(2, redrawn);
        Assert.Equal([0, 0, 0, 255], PreviewPixelAt(viewModel, 5, 1));
    }

    [AvaloniaFact]
    public void UndoIsIgnoredWhileAStrokeIsInProgress()
    {
        var viewModel = OpenImage();
        viewModel.BeginStroke(1, 1);
        viewModel.CommitStroke();

        viewModel.BeginStroke(5, 5);
        viewModel.Undo();
        viewModel.CommitStroke();

        Assert.Equal([0, 0, 0, 255], PixelAt(viewModel, 5, 5));
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage());
        viewModel.TileWidth = 16;
        viewModel.TileHeight = 16;
        viewModel.IsDrawToolActive = true;
        return viewModel;
    }

    // Opaque red in the top-left cell, transparent everywhere else.
    private string WriteImage()
    {
        var path = Path.Combine(_directory, "tiles.png");
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent);
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
                bitmap.SetPixel(x, y, SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // Reads from the saved image so it also covers what the history restores.
    private byte[] PixelAt(MainViewModel viewModel, int x, int y)
    {
        var path = Path.Combine(_directory, "read.png");
        viewModel.SaveImage(path);
        using var bitmap = SKBitmap.Decode(path, new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        var color = bitmap.GetPixel(x, y);
        return [color.Red, color.Green, color.Blue, color.Alpha];
    }

    private static unsafe byte[] PreviewPixelAt(MainViewModel viewModel, int x, int y)
    {
        using var framebuffer = viewModel.PreviewBitmap!.Lock();
        var pixel = (byte*)framebuffer.Address + y * framebuffer.RowBytes + x * 4;
        return [pixel[0], pixel[1], pixel[2], pixel[3]];
    }
}
