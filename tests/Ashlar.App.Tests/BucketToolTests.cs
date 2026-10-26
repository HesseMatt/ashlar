using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class BucketToolTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void FillIsIgnoredUnlessTheBucketToolIsActive()
    {
        var viewModel = OpenImage();
        viewModel.IsBucketToolActive = false;

        Assert.False(viewModel.Fill(40, 40));
        Assert.False(viewModel.HasEdits);
    }

    [AvaloniaFact]
    public void FillCoversTheWholeCanvasRegardlessOfTilesAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.Fill(40, 40);

        Assert.Equal([0, 0, 0, 255], PixelAt(viewModel, 63, 63));
        Assert.Equal([0, 0, 0, 255], PixelAt(viewModel, 16, 0));
        Assert.Equal([255, 0, 0, 255], PixelAt(viewModel, 1, 1));
    }

    [AvaloniaFact]
    public void FillWritesTheDrawColorAlphaIncluded()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(128, 10, 20, 30);

        viewModel.Fill(40, 40);

        Assert.Equal([10, 20, 30, 128], PixelAt(viewModel, 40, 40));
    }

    [AvaloniaFact]
    public void FillWithTheColorUnderTheClickChangesNothingAndRecordsNoStep()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(255, 255, 0, 0);
        var unsavedBefore = viewModel.HasUnsavedChanges;

        Assert.False(viewModel.Fill(1, 1));

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.HasEdits);
        Assert.Equal(unsavedBefore, viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void FillCreatesTilesForEveryCellItPaintsAndKeepsExistingOnes()
    {
        var viewModel = OpenImage();

        viewModel.Fill(40, 40);

        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.Equal("tiles_0001", viewModel.Tiles[0].Name);
        Assert.Equal((0, 0), (viewModel.Tiles[0].X, viewModel.Tiles[0].Y));
        Assert.Equal("tiles_0002", viewModel.Tiles[1].Name);
        Assert.Equal((16, 0), (viewModel.Tiles[1].X, viewModel.Tiles[1].Y));
        Assert.Equal("tiles_0016", viewModel.Tiles[^1].Name);
    }

    [AvaloniaFact]
    public void FillWithATransparentColorCreatesNoTiles()
    {
        var viewModel = OpenImage();
        viewModel.DrawColor = Color.FromArgb(0, 0, 0, 0);
        viewModel.Fill(1, 1);

        Assert.Single(viewModel.Tiles);
        Assert.Equal(0, PixelAt(viewModel, 1, 1)[3]);
        Assert.True(viewModel.CanUndo);
    }

    [AvaloniaFact]
    public void FillIsOneUndoStepThatRestoresPixelsTilesAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.Fill(40, 40);
        viewModel.Undo();

        Assert.False(viewModel.CanUndo);
        Assert.Single(viewModel.Tiles);
        Assert.Equal(0, PixelAt(viewModel, 40, 40)[3]);
        Assert.Single(viewModel.SelectedTiles);

        viewModel.Redo();

        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.Equal([0, 0, 0, 255], PixelAt(viewModel, 40, 40));
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void FillRecordsTheStepAsFillAndReportsCreatedTiles()
    {
        var viewModel = OpenImage();

        viewModel.Fill(40, 40);
        Assert.Contains("created 15 tiles", viewModel.StatusMessage);

        viewModel.Undo();
        Assert.Equal("Undid: Fill.", viewModel.StatusMessage);
    }

    [AvaloniaFact]
    public void FillUpdatesThePreviewBitmapInPlace()
    {
        var viewModel = OpenImage();
        var bitmap = viewModel.PreviewBitmap;
        var redrawn = 0;
        viewModel.PreviewPixelsChanged += (_, _) => redrawn++;

        viewModel.Fill(40, 40);

        Assert.Same(bitmap, viewModel.PreviewBitmap);
        Assert.True(redrawn > 0);
        Assert.Equal([0, 0, 0, 255], PreviewPixelAt(viewModel, 40, 40));
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(), 16, 16);
        viewModel.IsBucketToolActive = true;
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
