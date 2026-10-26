using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class RectangleSelectionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string[] SelectedNames(MainViewModel viewModel) =>
        viewModel.SelectedTiles.Select(t => t.Name).ToArray();

    [Fact]
    public void DraggingSelectsEveryTouchedTile()
    {
        var viewModel = OpenImage();

        viewModel.BeginSelectionDrag(10, 10, addToSelection: false);
        viewModel.UpdateSelectionDrag(20, 20);
        viewModel.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0002", "tiles_0005", "tiles_0006"], SelectedNames(viewModel));
        Assert.Equal("Selected 4 tiles.", viewModel.StatusMessage);
    }

    [Fact]
    public void ShrinkingTheRectangleDeselectsTilesItNoLongerTouches()
    {
        var viewModel = OpenImage();
        viewModel.BeginSelectionDrag(1, 1, addToSelection: false);

        viewModel.UpdateSelectionDrag(40, 1);
        Assert.Equal(3, viewModel.SelectedTiles.Count);

        viewModel.UpdateSelectionDrag(20, 1);
        Assert.Equal(["tiles_0001", "tiles_0002"], SelectedNames(viewModel));
    }

    [Fact]
    public void WithoutCtrlTheRectangleReplacesThePreviousSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(49, 49, addToSelection: false);

        viewModel.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.UpdateSelectionDrag(20, 1);
        viewModel.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], SelectedNames(viewModel));
    }

    [Fact]
    public void WithCtrlTheRectangleAddsToThePreviousSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.BeginSelectionDrag(1, 1, addToSelection: true);
        viewModel.UpdateSelectionDrag(20, 1);
        viewModel.UpdateSelectionDrag(1, 1);
        viewModel.EndSelectionDrag();

        Assert.Equal(["tiles_0001"], SelectedNames(viewModel));

        viewModel.BeginSelectionDrag(33, 33, addToSelection: true);
        viewModel.UpdateSelectionDrag(40, 40);
        viewModel.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0011"], SelectedNames(viewModel));
    }

    [Fact]
    public void CancellingTheDragRestoresTheSelectionFromBeforeIt()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(49, 49, addToSelection: false);
        viewModel.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.UpdateSelectionDrag(40, 40);

        viewModel.CancelSelectionDrag();

        Assert.Equal(["tiles_0016"], SelectedNames(viewModel));
        Assert.Null(viewModel.SelectionRectangle);
    }

    [Fact]
    public void RectangleOutsideTheImageIsAllowedAndSelectsTheOverlappedTiles()
    {
        var viewModel = OpenImage();

        viewModel.BeginSelectionDrag(-30, -30, addToSelection: false);
        viewModel.UpdateSelectionDrag(5, 5);
        viewModel.EndSelectionDrag();

        Assert.Equal(["tiles_0001"], SelectedNames(viewModel));
    }

    [Fact]
    public void DeleteRemovesAllTilesSelectedByRectangle()
    {
        var viewModel = OpenImage();
        viewModel.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.UpdateSelectionDrag(20, 20);
        viewModel.EndSelectionDrag();

        viewModel.DeleteSelectedTiles();

        Assert.Equal(12, viewModel.Tiles.Count);
        Assert.Empty(viewModel.SelectedTiles);
    }

    [Fact]
    public void DeletingDuringADragEndsTheDragInsteadOfRestoringDeletedTiles()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.BeginSelectionDrag(33, 33, addToSelection: true);
        viewModel.DeleteSelectedTiles();

        viewModel.UpdateSelectionDrag(40, 40);

        Assert.Empty(viewModel.SelectedTiles);
        Assert.Null(viewModel.SelectionRectangle);
    }

    [Fact]
    public void RectangleToolResetsWhenANewTilesetIsOpened()
    {
        var viewModel = OpenImage();
        viewModel.IsRectangleToolActive = true;

        viewModel.LoadTileset(WriteImage("other.png"), 16, 16);

        Assert.False(viewModel.IsRectangleToolActive);
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
