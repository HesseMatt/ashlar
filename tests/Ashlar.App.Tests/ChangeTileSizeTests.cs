using Avalonia.Headless.XUnit;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class ChangeTileSizeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void LoadingATilesetUsesTheChosenTileSize()
    {
        var viewModel = new MainViewModel();

        viewModel.LoadTileset(WriteImage(), 32, 16);

        Assert.Equal("32x16", viewModel.Editor().TileSizeText);
        Assert.Equal(8, viewModel.Editor().Tiles.Count);
    }

    [AvaloniaFact]
    public void ReadingAnImageLeavesTheOpenTilesetUntouched()
    {
        var viewModel = OpenImage();

        using var image = viewModel.ReadImage(WriteImage("other.png"));

        Assert.NotNull(image);
        Assert.Equal(16, viewModel.Editor().Tiles.Count);
        Assert.Equal("16x16", viewModel.Editor().TileSizeText);
    }

    [AvaloniaFact]
    public void ReadingAnUnreadableImageReportsItAndKeepsTheOpenTileset()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "broken.png");
        File.WriteAllText(path, "not an image");

        var image = viewModel.ReadImage(path);

        Assert.Null(image);
        Assert.Contains("Could not open", viewModel.Editor().StatusMessage);
        Assert.Equal(16, viewModel.Editor().Tiles.Count);
    }

    [AvaloniaFact]
    public void ChangingTheSizeRebuildsTheGridWithoutAnEditsWarning()
    {
        var viewModel = OpenImage();

        Assert.False(viewModel.Editor().NeedsTileSizeConfirmation(32, 32));
        viewModel.Editor().ChangeTileSize(32, 32);

        Assert.Equal(4, viewModel.Editor().Tiles.Count);
        Assert.Equal("32x32", viewModel.Editor().TileSizeText);
    }

    [AvaloniaFact]
    public void ChangingTheSizeOfAnEditedTilesetNeedsConfirmation()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        Assert.True(viewModel.Editor().NeedsTileSizeConfirmation(32, 32));
    }

    [AvaloniaFact]
    public void ConfirmingTheSameSizeChangesNothingAndNeedsNoConfirmation()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        Assert.False(viewModel.Editor().NeedsTileSizeConfirmation(16, 16));
        viewModel.Editor().ChangeTileSize(16, 16);

        Assert.Equal(15, viewModel.Editor().Tiles.Count);
        Assert.True(viewModel.Editor().CanUndo);
    }

    [AvaloniaFact]
    public void AcceptedChangeClearsTheUndoHistoryAndTheEditedState()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().ChangeTileSize(32, 32);

        Assert.False(viewModel.Editor().CanUndo);
        Assert.False(viewModel.Editor().CanRedo);
        Assert.False(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ChangingTheSizeOfAProjectOpenedWithEditsClearsTheEditedState()
    {
        var edited = OpenImage();
        edited.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        edited.Editor().IsMoveToolActive = true;
        edited.Editor().BeginMoveDrag(1, 1);
        edited.Editor().UpdateMoveDrag(17, 1);
        edited.Editor().CommitMoveDrag();
        var path = Path.Combine(_directory, "edited.ashlar");
        edited.SaveProject(path);
        var reopened = new MainViewModel();
        reopened.OpenProject(path);
        reopened.OpenFirstTileset();

        reopened.Editor().ChangeTileSize(32, 32);

        Assert.False(reopened.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ChangingTheSizeKeepsTheImagePixels()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().ChangeTileSize(8, 8);

        // The deleted tile's four 8x8 cells stay empty, so the grid skips them.
        Assert.Equal(60, viewModel.Editor().Tiles.Count);
    }

    [AvaloniaFact]
    public void OpeningAProjectUsesTheSavedTileSize()
    {
        var saved = new MainViewModel();
        saved.LoadTileset(WriteImage(), 32, 32);
        var path = Path.Combine(_directory, "project.ashlar");
        saved.SaveProject(path);

        var reopened = OpenImage();
        reopened.OpenProject(path);
        reopened.OpenFirstTileset();

        Assert.Equal("32x32", reopened.Editor().TileSizeText);
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(), 16, 16);
        return viewModel;
    }

    private string WriteImage(string fileName = "tiles.png")
    {
        var path = Path.Combine(_directory, fileName);
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
