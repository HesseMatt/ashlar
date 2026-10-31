using Avalonia.Headless.XUnit;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.Core.Model;

namespace Ashlar.App.Tests;

public class UndoRedoTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void NothingToUndoOrRedoInitially()
    {
        var viewModel = OpenImage();

        Assert.False(viewModel.Editor().CanUndo);
        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void UndoingADeleteRestoresTilesPixelsAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: true);
        var tilesBefore = viewModel.Editor().Tiles.ToList();

        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        Assert.Equal(tilesBefore, viewModel.Editor().Tiles);
        Assert.Equal(["tiles_0001", "tiles_0002"], viewModel.Editor().SelectedTiles.Select(t => t.Name));
        Assert.Equal("Undid: Delete 2 tiles.", viewModel.Editor().StatusMessage);
        Assert.Equal(SKColors.Red, PixelAt(viewModel, 1, 1));
    }

    [AvaloniaFact]
    public void DeleteIsOneStepEvenForManyTiles()
    {
        var viewModel = OpenImage();
        SelectAll(viewModel);

        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        Assert.Equal(16, viewModel.Editor().Tiles.Count);
        Assert.False(viewModel.Editor().CanUndo);
    }

    [AvaloniaFact]
    public void RedoReappliesTheDelete()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        viewModel.Editor().Redo();

        Assert.Equal(15, viewModel.Editor().Tiles.Count);
        Assert.Empty(viewModel.Editor().SelectedTiles);
        Assert.Equal("Redid: Delete 1 tile.", viewModel.Editor().StatusMessage);
        Assert.Equal(SKColor.Empty, PixelAt(viewModel, 1, 1));
    }

    [AvaloniaFact]
    public void UndoingAMoveRestoresPositionsAndOverwrittenTiles()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;
        var tilesBefore = viewModel.Editor().Tiles.ToList();

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(17, 1);
        viewModel.Editor().CommitMoveDrag();
        Assert.Equal(15, viewModel.Editor().Tiles.Count);

        viewModel.Editor().Undo();

        Assert.Equal(tilesBefore, viewModel.Editor().Tiles);
        Assert.Equal("tiles_0001", viewModel.Editor().SelectedTiles.Single().Name);
        Assert.Equal((0, 0), (viewModel.Editor().SelectedTiles[0].X, viewModel.Editor().SelectedTiles[0].Y));
    }

    [AvaloniaFact]
    public void UndoingAGroupAssignmentRestoresNamesAndSelection()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().GroupNameInput = "enemies";
        viewModel.Editor().ConfirmGroupNameInput();
        Assert.Equal("enemies_0001", viewModel.Editor().Tiles[0].Name);

        viewModel.Editor().Undo();

        Assert.Equal("tiles_0001", viewModel.Editor().Tiles[0].Name);
        Assert.Equal("tiles_0001", viewModel.Editor().SelectedTiles.Single().Name);
        Assert.Empty(viewModel.Editor().GroupNames);

        viewModel.Editor().Redo();

        Assert.Equal("enemies_0001", viewModel.Editor().SelectedTiles.Single().Name);
        Assert.Equal(["enemies"], viewModel.Editor().GroupNames);
    }

    [AvaloniaFact]
    public void ConsecutiveUndosWalkBackThroughEveryStep()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().Undo();
        Assert.Equal(15, viewModel.Editor().Tiles.Count);
        viewModel.Editor().Undo();
        Assert.Equal(16, viewModel.Editor().Tiles.Count);
        Assert.False(viewModel.Editor().CanUndo);

        viewModel.Editor().Redo();
        viewModel.Editor().Redo();
        Assert.Equal(14, viewModel.Editor().Tiles.Count);
        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void NewEditAfterUndoDiscardsTheRedoSteps()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();
        Assert.True(viewModel.Editor().CanRedo);

        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void SelectionAfterUndoOmitsTilesThatNoLongerExist()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().Undo();
        viewModel.Editor().Undo();
        viewModel.Editor().Redo();

        Assert.Empty(viewModel.Editor().SelectedTiles);
    }

    [AvaloniaFact]
    public void OpeningAnImageClearsTheHistory()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        viewModel.LoadTileset(WriteImage("other.png"), 16, 16);

        Assert.False(viewModel.Editor().CanUndo);
        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void OpeningAProjectClearsTheHistory()
    {
        var project = SaveProject(OpenImage());
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.OpenProject(project);

        viewModel.OpenFirstTileset();

        Assert.False(viewModel.Editor().CanUndo);
        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void SavingAndExportingKeepTheHistory()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        viewModel.SaveImage(Path.Combine(_directory, "out.png"));
        SaveProject(viewModel);
        viewModel.ExportXml(Path.Combine(_directory, "out.xml"));

        Assert.True(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void UndoingEveryEditClearsTheEdits()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        Assert.True(viewModel.Editor().HasEdits);

        viewModel.Editor().Undo();

        Assert.False(viewModel.Editor().HasEdits);

        viewModel.Editor().Redo();

        Assert.True(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ProjectOpenedWithEditsStaysEditedAfterUndoingEveryStep()
    {
        var edited = OpenImage();
        edited.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        edited.Editor().IsMoveToolActive = true;
        edited.Editor().BeginMoveDrag(1, 1);
        edited.Editor().UpdateMoveDrag(1 + edited.Editor().TileWidth, 1);
        edited.Editor().CommitMoveDrag();
        var viewModel = new MainViewModel();
        viewModel.OpenProject(SaveProject(edited));
        viewModel.OpenFirstTileset();
        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().Undo();

        Assert.False(viewModel.Editor().CanUndo);
        Assert.True(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ChangingTheTileSizeAfterUndoDiscardsTheRedoSteps()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();

        viewModel.Editor().ChangeTileSize(32, 16);

        Assert.False(viewModel.Editor().CanRedo);
    }

    [AvaloniaFact]
    public void UndoAndRedoAreIgnoredWhileADragIsInProgress()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;
        viewModel.Editor().BeginMoveDrag(17, 1);

        viewModel.Editor().Undo();

        Assert.Equal(15, viewModel.Editor().Tiles.Count);
        Assert.True(viewModel.Editor().CanUndo);

        viewModel.Editor().EndMoveDrag();
        viewModel.Editor().Undo();
        viewModel.Editor().BeginSelectionDrag(1, 1, addToSelection: false);

        viewModel.Editor().Redo();

        Assert.Equal(16, viewModel.Editor().Tiles.Count);
        Assert.True(viewModel.Editor().CanRedo);
    }

    private static void SelectRow(MainViewModel viewModel, int row)
    {
        for (var x = 0; x < 4; x++)
            viewModel.Editor().HandleCanvasClick(x * 16 + 1, row * 16 + 1, addToSelection: true);
    }

    private static void SelectAll(MainViewModel viewModel)
    {
        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 4; x++)
                viewModel.Editor().HandleCanvasClick(x * 16 + 1, y * 16 + 1, addToSelection: true);
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
        viewModel.LoadTileset(WriteImage("tiles.png"), 16, 16);
        return viewModel;
    }

    private string SaveProject(MainViewModel viewModel)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.ashlar");
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
