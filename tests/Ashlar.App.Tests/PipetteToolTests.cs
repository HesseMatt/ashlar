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
        viewModel.Editor.IsPipetteToolActive = false;

        Assert.False(viewModel.Editor.PickColor(1, 1));
        Assert.Equal(Colors.Black, viewModel.Editor.DrawColor);
    }

    [AvaloniaFact]
    public void PickingCopiesTheExactRgbaOfThePixel()
    {
        var viewModel = OpenImage();

        Assert.True(viewModel.Editor.PickColor(1, 1));

        Assert.Equal(Color.FromArgb(128, 10, 20, 30), viewModel.Editor.DrawColor);
    }

    [AvaloniaFact]
    public void PickingATransparentPixelSetsAlphaToZero()
    {
        var viewModel = OpenImage();

        viewModel.Editor.PickColor(40, 40);

        Assert.Equal(0, viewModel.Editor.DrawColor.A);
    }

    [AvaloniaFact]
    public void LastPickWinsWhenDragging()
    {
        var viewModel = OpenImage();

        viewModel.Editor.PickColor(1, 1);
        viewModel.Editor.PickColor(2, 2);

        Assert.Equal(Color.FromArgb(255, 200, 100, 50), viewModel.Editor.DrawColor);
    }

    [AvaloniaTheory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(64, 0)]
    [InlineData(0, 64)]
    public void PositionsOutsideTheImageKeepTheLastPickedColor(int x, int y)
    {
        var viewModel = OpenImage();
        viewModel.Editor.PickColor(1, 1);

        Assert.False(viewModel.Editor.PickColor(x, y));

        Assert.Equal(Color.FromArgb(128, 10, 20, 30), viewModel.Editor.DrawColor);
    }

    [AvaloniaFact]
    public void PickingReadsInProgressEditsAndAddsNoUndoStep()
    {
        var viewModel = OpenImage();
        viewModel.Editor.IsDrawToolActive = true;
        viewModel.Editor.DrawColor = Color.FromArgb(255, 1, 2, 3);
        viewModel.Editor.BeginStroke(40, 40);
        viewModel.Editor.CommitStroke();
        viewModel.Editor.IsPipetteToolActive = true;
        var tileCount = viewModel.Editor.Tiles.Count;

        viewModel.Editor.PickColor(40, 40);

        Assert.Equal(Color.FromArgb(255, 1, 2, 3), viewModel.Editor.DrawColor);
        Assert.Equal(tileCount, viewModel.Editor.Tiles.Count);
        viewModel.Editor.Undo();
        Assert.False(viewModel.Editor.CanUndo);
        Assert.Equal(0, viewModel.Editor.Tiles.Count(t => t.X == 32 && t.Y == 32));
        viewModel.Editor.PickColor(40, 40);
        Assert.Equal(0, viewModel.Editor.DrawColor.A);
    }

    [AvaloniaFact]
    public void PickingIsNotAnEdit()
    {
        var viewModel = OpenImage();

        viewModel.Editor.PickColor(1, 1);

        Assert.False(viewModel.Editor.HasEdits);
        Assert.False(viewModel.Editor.CanUndo);
    }

    [AvaloniaFact]
    public void PipetteIsMutuallyExclusiveWithTheOtherTools()
    {
        var viewModel = OpenImage();

        viewModel.Editor.IsDrawToolActive = true;
        Assert.False(viewModel.Editor.IsPipetteToolActive);

        viewModel.Editor.IsPipetteToolActive = true;
        Assert.False(viewModel.Editor.IsDrawToolActive);
        Assert.Equal(CanvasTool.Pipette, viewModel.Editor.ActiveTool);

        viewModel.Editor.IsPipetteToolActive = false;
        Assert.Equal(CanvasTool.Select, viewModel.Editor.ActiveTool);
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(), 16, 16);
        viewModel.Editor.IsPipetteToolActive = true;
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
