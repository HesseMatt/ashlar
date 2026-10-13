using SkiaSharp;
using TileEdit.App.ViewModels;

namespace TileEdit.App.Tests;

public class TileSizeLockTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void TileSizeIsEditableAfterImportingAnImage()
    {
        var viewModel = OpenImage();

        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void DeletingATileLocksTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.DeleteSelectedTiles();

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void MovingATileLocksTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.ToggleMove();

        viewModel.HandleCanvasClick(17, 1, addToSelection: false);

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void CompactingLocksTheTileSizeEvenWithoutPriorEdits()
    {
        var viewModel = OpenImage();

        viewModel.Compact();

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void CompactingAfterADeleteShrinksTheCanvasAndKeepsTileNames()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.Compact();

        Assert.Equal(15, viewModel.Tiles.Count);
        Assert.Equal(64, viewModel.ImageWidth);
        Assert.Equal(64, viewModel.ImageHeight);
        Assert.Equal("tiles_0002", viewModel.Tiles[0].Name);
        Assert.Equal((0, 0), (viewModel.Tiles[0].X, viewModel.Tiles[0].Y));
    }

    [Fact]
    public void CompactingKeepsTheSelectedTileSelected()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.HandleCanvasClick(33, 1, addToSelection: false);

        viewModel.Compact();

        Assert.Equal("tiles_0003", viewModel.SelectedTiles.Single().Name);
    }

    [Fact]
    public void CancelledMoveDoesNotLockTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.ToggleMove();

        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void TileSizeStaysLockedAfterFurtherEdits()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void ImportingANewImageUnlocksTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.LoadTileset(WriteImage("other.png"));

        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void ProjectWithoutEditsOpensWithEditableTileSize()
    {
        var projectPath = SaveProject(OpenImage());

        var reopened = new MainViewModel();
        reopened.OpenProject(projectPath);

        Assert.True(reopened.CanEditTileSize);
    }

    [Fact]
    public void ProjectWithEditsOpensWithLockedTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        var projectPath = SaveProject(viewModel);

        var reopened = new MainViewModel();
        reopened.OpenProject(projectPath);

        Assert.False(reopened.CanEditTileSize);
    }

    [Fact]
    public void OpeningAnUneditedProjectUnlocksTheTileSize()
    {
        var unedited = SaveProject(OpenImage());
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.OpenProject(unedited);

        Assert.True(viewModel.CanEditTileSize);
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("tiles.png"));
        viewModel.TileWidth = 16;
        viewModel.TileHeight = 16;
        return viewModel;
    }

    private string SaveProject(MainViewModel viewModel)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.tileproj.json");
        viewModel.SaveProject(path);
        return path;
    }

    private string WriteImage(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
