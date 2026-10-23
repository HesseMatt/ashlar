using Avalonia.Headless.XUnit;
using Avalonia.Media;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class PipetteToolTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void PickingIsIgnoredUnlessThePipetteToolIsActive()
    {
        var viewModel = OpenImage();
        viewModel.IsPipetteToolActive = false;

        Assert.False(viewModel.PickColor(1, 1));
        Assert.Equal(Colors.Black, viewModel.DrawColor);
    }

    [AvaloniaFact]
    public void PickingCopiesTheExactRgbaOfThePixel()
    {
        var viewModel = OpenImage();

        Assert.True(viewModel.PickColor(1, 1));

        Assert.Equal(Color.FromArgb(128, 10, 20, 30), viewModel.DrawColor);
    }

    [AvaloniaFact]
    public void PickingATransparentPixelSetsAlphaToZero()
    {
        var viewModel = OpenImage();

        viewModel.PickColor(40, 40);

        Assert.Equal(0, viewModel.DrawColor.A);
    }

    [AvaloniaFact]
    public void LastPickWinsWhenDragging()
    {
        var viewModel = OpenImage();

        viewModel.PickColor(1, 1);
        viewModel.PickColor(2, 2);

        Assert.Equal(Color.FromArgb(255, 200, 100, 50), viewModel.DrawColor);
    }

    [AvaloniaTheory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(64, 0)]
    [InlineData(0, 64)]
    public void PositionsOutsideTheImageKeepTheLastPickedColor(int x, int y)
    {
        var viewModel = OpenImage();
        viewModel.PickColor(1, 1);

        Assert.False(viewModel.PickColor(x, y));

        Assert.Equal(Color.FromArgb(128, 10, 20, 30), viewModel.DrawColor);
    }

    [AvaloniaFact]
    public void PickingReadsInProgressEditsAndAddsNoUndoStep()
    {
        var viewModel = OpenImage();
        viewModel.IsDrawToolActive = true;
        viewModel.DrawColor = Color.FromArgb(255, 1, 2, 3);
        viewModel.BeginStroke(40, 40);
        viewModel.CommitStroke();
        viewModel.IsPipetteToolActive = true;
        var tileCount = viewModel.Tiles.Count;

        viewModel.PickColor(40, 40);

        Assert.Equal(Color.FromArgb(255, 1, 2, 3), viewModel.DrawColor);
        Assert.Equal(tileCount, viewModel.Tiles.Count);
        viewModel.Undo();
        Assert.False(viewModel.CanUndo);
        Assert.Equal(0, viewModel.Tiles.Count(t => t.X == 32 && t.Y == 32));
        viewModel.PickColor(40, 40);
        Assert.Equal(0, viewModel.DrawColor.A);
    }

    [AvaloniaFact]
    public void PickingDoesNotLockTheTileSize()
    {
        var viewModel = OpenImage();

        viewModel.PickColor(1, 1);

        Assert.True(viewModel.CanEditTileSize);
        Assert.False(viewModel.CanUndo);
    }

    [AvaloniaFact]
    public void PipetteIsMutuallyExclusiveWithTheOtherTools()
    {
        var viewModel = OpenImage();

        viewModel.IsDrawToolActive = true;
        Assert.False(viewModel.IsPipetteToolActive);

        viewModel.IsPipetteToolActive = true;
        Assert.False(viewModel.IsDrawToolActive);
        Assert.Equal(CanvasTool.Pipette, viewModel.ActiveTool);

        viewModel.IsPipetteToolActive = false;
        Assert.Equal(CanvasTool.Select, viewModel.ActiveTool);
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage());
        viewModel.TileWidth = 16;
        viewModel.TileHeight = 16;
        viewModel.IsPipetteToolActive = true;
        return viewModel;
    }

    private string WriteImage()
    {
        var path = Path.Combine(_directory, "tiles.png");
        using var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Transparent);
        bitmap.SetPixel(1, 1, new SKColor(10, 20, 30, 128));
        bitmap.SetPixel(2, 2, new SKColor(200, 100, 50, 255));
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
