using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class RectangleSelectionTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string[] SelectedNames(MainViewModel viewModel) =>
        viewModel.Editor.SelectedTiles.Select(t => t.Name).ToArray();

    [Fact]
    public void DraggingSelectsEveryTouchedTile()
    {
        var viewModel = OpenImage();

        viewModel.Editor.BeginSelectionDrag(10, 10, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag(20, 20);
        viewModel.Editor.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0002", "tiles_0005", "tiles_0006"], SelectedNames(viewModel));
        Assert.Equal("Selected 4 tiles.", viewModel.Editor.StatusMessage);
    }

    [Fact]
    public void ShrinkingTheRectangleDeselectsTilesItNoLongerTouches()
    {
        var viewModel = OpenImage();
        viewModel.Editor.BeginSelectionDrag(1, 1, addToSelection: false);

        viewModel.Editor.UpdateSelectionDrag(40, 1);
        Assert.Equal(3, viewModel.Editor.SelectedTiles.Count);

        viewModel.Editor.UpdateSelectionDrag(20, 1);
        Assert.Equal(["tiles_0001", "tiles_0002"], SelectedNames(viewModel));
    }

    [Fact]
    public void WithoutCtrlTheRectangleReplacesThePreviousSelection()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(49, 49, addToSelection: false);

        viewModel.Editor.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag(20, 1);
        viewModel.Editor.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0002"], SelectedNames(viewModel));
    }

    [Fact]
    public void WithCtrlTheRectangleAddsToThePreviousSelection()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.Editor.BeginSelectionDrag(1, 1, addToSelection: true);
        viewModel.Editor.UpdateSelectionDrag(20, 1);
        viewModel.Editor.UpdateSelectionDrag(1, 1);
        viewModel.Editor.EndSelectionDrag();

        Assert.Equal(["tiles_0001"], SelectedNames(viewModel));

        viewModel.Editor.BeginSelectionDrag(33, 33, addToSelection: true);
        viewModel.Editor.UpdateSelectionDrag(40, 40);
        viewModel.Editor.EndSelectionDrag();

        Assert.Equal(["tiles_0001", "tiles_0011"], SelectedNames(viewModel));
    }

    [Fact]
    public void CancellingTheDragRestoresTheSelectionFromBeforeIt()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(49, 49, addToSelection: false);
        viewModel.Editor.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag(40, 40);

        viewModel.Editor.CancelSelectionDrag();

        Assert.Equal(["tiles_0016"], SelectedNames(viewModel));
        Assert.Null(viewModel.Editor.SelectionRectangle);
    }

    [Fact]
    public void RectangleOutsideTheImageIsAllowedAndSelectsTheOverlappedTiles()
    {
        var viewModel = OpenImage();

        viewModel.Editor.BeginSelectionDrag(-30, -30, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag(5, 5);
        viewModel.Editor.EndSelectionDrag();

        Assert.Equal(["tiles_0001"], SelectedNames(viewModel));
    }

    [Fact]
    public void DeleteRemovesAllTilesSelectedByRectangle()
    {
        var viewModel = OpenImage();
        viewModel.Editor.BeginSelectionDrag(1, 1, addToSelection: false);
        viewModel.Editor.UpdateSelectionDrag(20, 20);
        viewModel.Editor.EndSelectionDrag();

        viewModel.Editor.DeleteSelectedTiles();

        Assert.Equal(12, viewModel.Editor.Tiles.Count);
        Assert.Empty(viewModel.Editor.SelectedTiles);
    }

    [Fact]
    public void DeletingDuringADragEndsTheDragInsteadOfRestoringDeletedTiles()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.BeginSelectionDrag(33, 33, addToSelection: true);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.Editor.UpdateSelectionDrag(40, 40);

        Assert.Empty(viewModel.Editor.SelectedTiles);
        Assert.Null(viewModel.Editor.SelectionRectangle);
    }

    [Fact]
    public void RectangleToolResetsWhenANewTilesetIsOpened()
    {
        var viewModel = OpenImage();
        viewModel.Editor.IsRectangleToolActive = true;

        viewModel.LoadTileset(WriteImage("other.png"), 16, 16);

        Assert.False(viewModel.Editor.IsRectangleToolActive);
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
