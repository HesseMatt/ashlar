using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

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
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.CommitMoveDrag();

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void MoveWithoutNetOffsetDoesNotLockTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.UpdateMoveDrag(5, 5);
        viewModel.CommitMoveDrag();

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
    public void UneditedProjectWithEmptyCellsOpensWithEditableTileSize()
    {
        var path = Path.Combine(_directory, "sparse.png");
        using (var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
        {
            bitmap.Erase(SKColors.Transparent);
            bitmap.SetPixel(0, 0, SKColors.Red);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }

        var viewModel = new MainViewModel();
        viewModel.LoadTileset(path);
        var reopened = new MainViewModel();
        reopened.OpenProject(SaveProject(viewModel));

        Assert.Single(reopened.Tiles);
        Assert.True(reopened.CanEditTileSize);
    }

    [Fact]
    public void ProjectWithEditsOpensWithLockedTileSize()
    {
        var viewModel = OpenImage();
        MoveFirstTileOneCellRight(viewModel);
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

    // A moved tile keeps its name, so unlike a deletion (which just leaves an
    // empty cell) the result differs from a grid freshly built from the image.
    private static void MoveFirstTileOneCellRight(MainViewModel viewModel)
    {
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;
        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(1 + viewModel.TileWidth, 1);
        viewModel.CommitMoveDrag();
    }

    private string SaveProject(MainViewModel viewModel)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.ashlar.json");
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
