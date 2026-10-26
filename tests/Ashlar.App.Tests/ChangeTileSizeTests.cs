using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class ChangeTileSizeTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void LoadingATilesetUsesTheChosenTileSize()
    {
        var viewModel = new MainViewModel();

        viewModel.LoadTileset(WriteImage(), 32, 16);

        Assert.Equal("32x16", viewModel.TileSizeText);
        Assert.Equal(8, viewModel.Tiles.Count);
    }

    [Fact]
    public void ReadingAnImageLeavesTheOpenTilesetUntouched()
    {
        var viewModel = OpenImage();

        using var image = viewModel.ReadImage(WriteImage("other.png"));

        Assert.NotNull(image);
        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.Equal("16x16", viewModel.TileSizeText);
    }

    [Fact]
    public void ReadingAnUnreadableImageReportsItAndKeepsTheOpenTileset()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "broken.png");
        File.WriteAllText(path, "not an image");

        var image = viewModel.ReadImage(path);

        Assert.Null(image);
        Assert.Contains("Could not open", viewModel.StatusMessage);
        Assert.Equal(16, viewModel.Tiles.Count);
    }

    [Fact]
    public void ChangingTheSizeRebuildsTheGridWithoutAnEditsWarning()
    {
        var viewModel = OpenImage();

        Assert.False(viewModel.NeedsTileSizeConfirmation(32, 32));
        viewModel.ChangeTileSize(32, 32);

        Assert.Equal(4, viewModel.Tiles.Count);
        Assert.Equal("32x32", viewModel.TileSizeText);
    }

    [Fact]
    public void ChangingTheSizeOfAnEditedTilesetNeedsConfirmation()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        Assert.True(viewModel.NeedsTileSizeConfirmation(32, 32));
    }

    [Fact]
    public void ConfirmingTheSameSizeChangesNothingAndNeedsNoConfirmation()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        Assert.False(viewModel.NeedsTileSizeConfirmation(16, 16));
        viewModel.ChangeTileSize(16, 16);

        Assert.Equal(15, viewModel.Tiles.Count);
        Assert.True(viewModel.CanUndo);
    }

    [Fact]
    public void AcceptedChangeClearsTheUndoHistoryAndTheEditedState()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.ChangeTileSize(32, 32);

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.False(viewModel.HasEdits);
    }

    [Fact]
    public void ChangingTheSizeOfAProjectOpenedWithEditsClearsTheEditedState()
    {
        var edited = OpenImage();
        edited.HandleCanvasClick(1, 1, addToSelection: false);
        edited.IsMoveToolActive = true;
        edited.BeginMoveDrag(1, 1);
        edited.UpdateMoveDrag(17, 1);
        edited.CommitMoveDrag();
        var path = Path.Combine(_directory, "edited.ashlar");
        edited.SaveProject(path);
        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        reopened.ChangeTileSize(32, 32);

        Assert.False(reopened.HasEdits);
    }

    [Fact]
    public void ChangingTheSizeKeepsTheImagePixels()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.ChangeTileSize(8, 8);

        // The deleted tile's four 8x8 cells stay empty, so the grid skips them.
        Assert.Equal(60, viewModel.Tiles.Count);
    }

    [Fact]
    public void OpeningAProjectUsesTheSavedTileSize()
    {
        var saved = new MainViewModel();
        saved.LoadTileset(WriteImage(), 32, 32);
        var path = Path.Combine(_directory, "project.ashlar");
        saved.SaveProject(path);

        var reopened = OpenImage();
        reopened.OpenProject(path);

        Assert.Equal("32x32", reopened.TileSizeText);
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
