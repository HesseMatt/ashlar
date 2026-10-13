using SkiaSharp;
using TileEdit.App.ViewModels;
using TileEdit.Core.Model;

namespace TileEdit.App.Tests;

public class UndoRedoTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void NothingToUndoOrRedoInitially()
    {
        var viewModel = OpenImage();

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void UndoingADeleteRestoresTilesPixelsAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.HandleCanvasClick(17, 1, addToSelection: true);
        var tilesBefore = viewModel.Tiles.ToList();

        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        Assert.Equal(tilesBefore, viewModel.Tiles);
        Assert.Equal(["tiles_0001", "tiles_0002"], viewModel.SelectedTiles.Select(t => t.Name));
        Assert.Equal("Undid: Delete 2 tiles.", viewModel.StatusMessage);
        Assert.Equal(SKColors.Red, PixelAt(viewModel, 1, 1));
    }

    [Fact]
    public void DeleteIsOneStepEvenForManyTiles()
    {
        var viewModel = OpenImage();
        SelectAll(viewModel);

        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.False(viewModel.CanUndo);
    }

    [Fact]
    public void RedoReappliesTheDelete()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        viewModel.Redo();

        Assert.Equal(15, viewModel.Tiles.Count);
        Assert.Empty(viewModel.SelectedTiles);
        Assert.Equal("Redid: Delete 1 tile.", viewModel.StatusMessage);
        Assert.Equal(SKColor.Empty, PixelAt(viewModel, 1, 1));
    }

    [Fact]
    public void UndoingAMoveRestoresPositionsAndOverwrittenTiles()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;
        var tilesBefore = viewModel.Tiles.ToList();

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.CommitMoveDrag();
        Assert.Equal(15, viewModel.Tiles.Count);

        viewModel.Undo();

        Assert.Equal(tilesBefore, viewModel.Tiles);
        Assert.Equal("tiles_0001", viewModel.SelectedTiles.Single().Name);
        Assert.Equal((0, 0), (viewModel.SelectedTiles[0].X, viewModel.SelectedTiles[0].Y));
    }

    [Fact]
    public void UndoingCompactRestoresCanvasSizeAndLayout()
    {
        var viewModel = OpenImage();
        SelectRow(viewModel, 2);
        SelectRow(viewModel, 3);
        viewModel.DeleteSelectedTiles();
        var tilesBeforeCompact = viewModel.Tiles.ToList();
        viewModel.Compact();
        Assert.Equal(32, viewModel.ImageHeight);

        viewModel.Undo();

        Assert.Equal(64, viewModel.ImageWidth);
        Assert.Equal(64, viewModel.ImageHeight);
        Assert.Equal(tilesBeforeCompact, viewModel.Tiles);

        viewModel.Redo();

        Assert.Equal(32, viewModel.ImageHeight);
    }

    [Fact]
    public void UndoingAGroupAssignmentRestoresNamesAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.GroupNameInput = "enemies";
        viewModel.ConfirmGroupNameInput();
        Assert.Equal("enemies_0001", viewModel.Tiles[0].Name);

        viewModel.Undo();

        Assert.Equal("tiles_0001", viewModel.Tiles[0].Name);
        Assert.Equal("tiles_0001", viewModel.SelectedTiles.Single().Name);
        Assert.Empty(viewModel.GroupNames);

        viewModel.Redo();

        Assert.Equal("enemies_0001", viewModel.SelectedTiles.Single().Name);
        Assert.Equal(["enemies"], viewModel.GroupNames);
    }

    [Fact]
    public void ConsecutiveUndosWalkBackThroughEveryStep()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.Undo();
        Assert.Equal(15, viewModel.Tiles.Count);
        viewModel.Undo();
        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.False(viewModel.CanUndo);

        viewModel.Redo();
        viewModel.Redo();
        Assert.Equal(14, viewModel.Tiles.Count);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void NewEditAfterUndoDiscardsTheRedoSteps()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.Undo();
        Assert.True(viewModel.CanRedo);

        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void SelectionAfterUndoOmitsTilesThatNoLongerExist()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.Undo();
        viewModel.Undo();
        viewModel.Redo();

        Assert.Empty(viewModel.SelectedTiles);
    }

    [Fact]
    public void OpeningAnImageClearsTheHistory()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        viewModel.LoadTileset(WriteImage("other.png"));

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void OpeningAProjectClearsTheHistory()
    {
        var project = SaveProject(OpenImage());
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.OpenProject(project);

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void SavingAndExportingKeepTheHistory()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        viewModel.SaveImage(Path.Combine(_directory, "out.png"));
        SaveProject(viewModel);
        viewModel.ExportXml(Path.Combine(_directory, "out.xml"));

        Assert.True(viewModel.CanRedo);
    }

    [Fact]
    public void UndoingEveryEditUnlocksTheTileSize()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        Assert.False(viewModel.CanEditTileSize);

        viewModel.Undo();

        Assert.True(viewModel.CanEditTileSize);

        viewModel.Redo();

        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void ProjectOpenedWithEditsStaysLockedAfterUndoingEveryStep()
    {
        var edited = OpenImage();
        edited.HandleCanvasClick(1, 1, addToSelection: false);
        edited.DeleteSelectedTiles();
        var viewModel = new MainViewModel();
        viewModel.OpenProject(SaveProject(edited));
        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.Undo();

        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanEditTileSize);
    }

    [Fact]
    public void ChangingTheTileSizeAfterUndoDiscardsTheRedoSteps()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.Undo();

        viewModel.TileWidth = 32;

        Assert.False(viewModel.CanRedo);
    }

    [Fact]
    public void UndoAndRedoAreIgnoredWhileADragIsInProgress()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;
        viewModel.BeginMoveDrag(17, 1);

        viewModel.Undo();

        Assert.Equal(15, viewModel.Tiles.Count);
        Assert.True(viewModel.CanUndo);

        viewModel.EndMoveDrag();
        viewModel.Undo();
        viewModel.BeginSelectionDrag(1, 1, addToSelection: false);

        viewModel.Redo();

        Assert.Equal(16, viewModel.Tiles.Count);
        Assert.True(viewModel.CanRedo);
    }

    private static void SelectRow(MainViewModel viewModel, int row)
    {
        for (var x = 0; x < 4; x++)
            viewModel.HandleCanvasClick(x * 16 + 1, row * 16 + 1, addToSelection: true);
    }

    private static void SelectAll(MainViewModel viewModel)
    {
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                viewModel.HandleCanvasClick(x * 16 + 1, y * 16 + 1, addToSelection: true);
        }
    }

    private SKColor PixelAt(MainViewModel viewModel, int x, int y)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.png");
        viewModel.SaveImage(path);
        using var bitmap = SKBitmap.Decode(path);
        return bitmap.GetPixel(x, y);
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
