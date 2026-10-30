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

        viewModel.Editor.BeginMoveDrag(5, 5);
        viewModel.Editor.UpdateMoveDrag(5 + 32 + 7, 5 + 16);
        viewModel.Editor.CommitMoveDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], Names(viewModel.Editor.SelectedTiles));
        Assert.Equal([(32, 16), (48, 16)], viewModel.Editor.SelectedTiles.Select(t => (t.X, t.Y)));
        Assert.Equal("Moved 2 tiles.", viewModel.Editor.StatusMessage);
    }

    [Fact]
    public void PreviewSnapsToTheGridAndTheTilesStayPutUntilRelease()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.Editor.BeginMoveDrag(1, 1);
        viewModel.Editor.UpdateMoveDrag(40, 20);

        Assert.Equal(new Vector(32, 16), viewModel.Editor.MoveOffset);
        Assert.Equal((0, 0), (viewModel.Editor.Tiles[0].X, viewModel.Editor.Tiles[0].Y));
        Assert.False(viewModel.Editor.HasEdits);
    }

    [Fact]
    public void PressingOnAnUnselectedTileOrEmptySpaceDoesNotStartAMove()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;

        Assert.False(viewModel.Editor.BeginMoveDrag(40, 40));
        Assert.False(viewModel.Editor.BeginMoveDrag(-5, -5));

        viewModel.Editor.UpdateMoveDrag(60, 60);
        viewModel.Editor.CommitMoveDrag();
        Assert.Equal(["tiles_0001"], Names(viewModel.Editor.SelectedTiles));
        Assert.False(viewModel.Editor.HasEdits);
    }

    [Fact]
    public void CancellingTheDragChangesNothing()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;
        viewModel.Editor.BeginMoveDrag(1, 1);
        viewModel.Editor.UpdateMoveDrag(40, 1);

        viewModel.Editor.EndMoveDrag();
        viewModel.Editor.CommitMoveDrag();

        Assert.Null(viewModel.Editor.MoveOffset);
        Assert.Equal((0, 0), (viewModel.Editor.Tiles[0].X, viewModel.Editor.Tiles[0].Y));
        Assert.False(viewModel.Editor.HasEdits);
    }

    [Fact]
    public void MovingOntoUnselectedTilesOverwritesThemInsteadOfSwapping()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.Editor.BeginMoveDrag(1, 1);
        viewModel.Editor.UpdateMoveDrag(17, 1);
        viewModel.Editor.CommitMoveDrag();

        Assert.Equal(15, viewModel.Editor.Tiles.Count);
        Assert.DoesNotContain(viewModel.Editor.Tiles, t => t.Name == "tiles_0002");
        Assert.Equal(16, viewModel.Editor.Tiles.Single(t => t.Name == "tiles_0001").X);
    }

    [Fact]
    public void MovingPartlyOutsideTheTilesetDeletesOnlyThoseTiles()
    {
        var viewModel = OpenImage();
        SelectRow(viewModel, 0, 4);

        viewModel.Editor.BeginMoveDrag(5, 5);
        viewModel.Editor.UpdateMoveDrag(5 + 32, 5);
        viewModel.Editor.CommitMoveDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], Names(viewModel.Editor.SelectedTiles));
        Assert.Equal(64, viewModel.Editor.ImageWidth);
        Assert.DoesNotContain(viewModel.Editor.Tiles, t => t.Name is "tiles_0003" or "tiles_0004");
        Assert.Contains("2 moved outside the tileset and were deleted", viewModel.Editor.StatusMessage);
    }

    [Fact]
    public void MoveToolStaysActiveAfterAMove()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.Editor.BeginMoveDrag(1, 1);
        viewModel.Editor.UpdateMoveDrag(17, 1);
        viewModel.Editor.CommitMoveDrag();

        Assert.True(viewModel.Editor.IsMoveToolActive);
    }

    [Fact]
    public void ActivatingAnotherToolDeactivatesTheMoveTool()
    {
        var viewModel = OpenImage();
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.Editor.IsRectangleToolActive = true;

        Assert.False(viewModel.Editor.IsMoveToolActive);
        Assert.True(viewModel.Editor.IsRectangleToolActive);
    }

    [Fact]
    public void MoveToolResetsWhenANewTilesetIsOpened()
    {
        var viewModel = OpenImage();
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.LoadTileset(WriteImage("other.png"), 16, 16);

        Assert.False(viewModel.Editor.IsMoveToolActive);
    }

    private static void SelectRow(MainViewModel viewModel, int firstColumn, int count)
    {
        viewModel.Editor.BeginSelectionDrag(firstColumn * 16 + 1, 1, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag((firstColumn + count - 1) * 16 + 1, 1);
        viewModel.Editor.EndSelectionDrag();
        viewModel.Editor.IsMoveToolActive = true;
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("tiles.png"), 16, 16);
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
