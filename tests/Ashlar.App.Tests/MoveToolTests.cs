using Avalonia;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.Core.Model;

namespace Ashlar.App.Tests;

public class MoveToolTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string[] Names(IEnumerable<TileRegion> tiles) =>
        tiles.Select(t => t.Name).ToArray();

    [Fact]
    public void DraggingMovesTheWholeSelectionByTheSameWholeCellOffset()
    {
        var viewModel = OpenImage();
        SelectRow(viewModel, 0, 2);

        viewModel.BeginMoveDrag(5, 5);
        viewModel.UpdateMoveDrag(5 + 32 + 7, 5 + 16);
        viewModel.CommitMoveDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], Names(viewModel.SelectedTiles));
        Assert.Equal([(32, 16), (48, 16)], viewModel.SelectedTiles.Select(t => (t.X, t.Y)));
        Assert.Equal("Moved 2 tiles.", viewModel.StatusMessage);
    }

    [Fact]
    public void PreviewSnapsToTheGridAndTheTilesStayPutUntilRelease()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(40, 20);

        Assert.Equal(new Vector(32, 16), viewModel.MoveOffset);
        Assert.Equal((0, 0), (viewModel.Tiles[0].X, viewModel.Tiles[0].Y));
        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void PressingOnAnUnselectedTileOrEmptySpaceDoesNotStartAMove()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        Assert.False(viewModel.BeginMoveDrag(40, 40));
        Assert.False(viewModel.BeginMoveDrag(-5, -5));

        viewModel.UpdateMoveDrag(60, 60);
        viewModel.CommitMoveDrag();
        Assert.Equal(["tiles_0001"], Names(viewModel.SelectedTiles));
        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void CancellingTheDragChangesNothing()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;
        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(40, 1);

        viewModel.EndMoveDrag();
        viewModel.CommitMoveDrag();

        Assert.Null(viewModel.MoveOffset);
        Assert.Equal((0, 0), (viewModel.Tiles[0].X, viewModel.Tiles[0].Y));
        Assert.True(viewModel.CanEditTileSize);
    }

    [Fact]
    public void MovingOntoUnselectedTilesOverwritesThemInsteadOfSwapping()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.CommitMoveDrag();

        Assert.Equal(15, viewModel.Tiles.Count);
        Assert.DoesNotContain(viewModel.Tiles, t => t.Name == "tiles_0002");
        Assert.Equal(16, viewModel.Tiles.Single(t => t.Name == "tiles_0001").X);
    }

    [Fact]
    public void MovingPartlyOutsideTheTilesetDeletesOnlyThoseTiles()
    {
        var viewModel = OpenImage();
        SelectRow(viewModel, 0, 4);

        viewModel.BeginMoveDrag(5, 5);
        viewModel.UpdateMoveDrag(5 + 32, 5);
        viewModel.CommitMoveDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], Names(viewModel.SelectedTiles));
        Assert.Equal(64, viewModel.ImageWidth);
        Assert.DoesNotContain(viewModel.Tiles, t => t.Name is "tiles_0003" or "tiles_0004");
        Assert.Contains("2 moved outside the tileset and were deleted", viewModel.StatusMessage);
    }

    [Fact]
    public void MoveToolStaysActiveAfterAMove()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.CommitMoveDrag();

        Assert.True(viewModel.IsMoveToolActive);
    }

    [Fact]
    public void ActivatingAnotherToolDeactivatesTheMoveTool()
    {
        var viewModel = OpenImage();
        viewModel.IsMoveToolActive = true;

        viewModel.IsRectangleToolActive = true;

        Assert.False(viewModel.IsMoveToolActive);
        Assert.True(viewModel.IsRectangleToolActive);
    }

    [Fact]
    public void MoveToolResetsWhenANewTilesetIsOpened()
    {
        var viewModel = OpenImage();
        viewModel.IsMoveToolActive = true;

        viewModel.LoadTileset(WriteImage("other.png"));

        Assert.False(viewModel.IsMoveToolActive);
    }

    private static void SelectRow(MainViewModel viewModel, int firstColumn, int count)
    {
        viewModel.BeginSelectionDrag(firstColumn * 16 + 1, 1, addToSelection: false);
        viewModel.UpdateSelectionDrag((firstColumn + count - 1) * 16 + 1, 1);
        viewModel.EndSelectionDrag();
        viewModel.IsMoveToolActive = true;
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("tiles.png"));
        viewModel.TileWidth = 16;
        viewModel.TileHeight = 16;
        return viewModel;
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
