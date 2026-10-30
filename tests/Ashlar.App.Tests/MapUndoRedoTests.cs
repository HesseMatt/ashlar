using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class MapUndoRedoTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void NothingToUndoOrRedoInitially()
    {
        var editor = ProjectWithMap().MapEditor;

        Assert.False(editor.CanUndo);
        Assert.False(editor.CanRedo);
    }

    [AvaloniaFact]
    public void AWholeDrawStrokeIsOneUndoStep()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0), (1, 0), (2, 0));

        Assert.True(editor.CanUndo);
        editor.Undo();

        Assert.All(editor.SelectedMap!.Cells, cell => Assert.Null(cell));
        Assert.All(editor.CellRegions, region => Assert.Null(region));
        Assert.False(editor.CanUndo);
        Assert.True(editor.CanRedo);
        Assert.Equal("Undid: Draw.", editor.StatusMessage);

        editor.Redo();

        Assert.Equal(["forest_0001", "forest_0001", "forest_0001"], editor.SelectedMap.Cells.Take(3));
        Assert.Equal(3, editor.CellRegions.Count(region => region is not null));
        Assert.False(editor.CanRedo);
    }

    [AvaloniaFact]
    public void AStrokeThatChangesNothingIsNotRecorded()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0));
        Stroke(editor, (0, 0));

        editor.Undo();

        Assert.Null(editor.SelectedMap![0, 0]);
        Assert.False(editor.CanUndo);
    }

    [AvaloniaFact]
    public void UndoingADeleteRestoresCellsAndSelection()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0), (1, 0));
        Select(editor, (0, 0), (1, 0));
        editor.DeleteSelectedCells();
        Assert.False(editor.HasSelectedCells);

        editor.Undo();

        Assert.Equal("forest_0001", editor.SelectedMap![0, 0]);
        Assert.Equal("forest_0001", editor.SelectedMap[1, 0]);
        Assert.Equal([(0, 0), (1, 0)], editor.SelectedCells);
        Assert.Equal("Undid: Delete 2 cells.", editor.StatusMessage);

        editor.Redo();

        Assert.Null(editor.SelectedMap[0, 0]);
        Assert.False(editor.HasSelectedCells);
    }

    [AvaloniaFact]
    public void UndoingAMoveRestoresCellsIncludingOverwrittenOnesAndSelection()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0), (2, 0));
        editor.SelectedPaletteTile = editor.PaletteTiles[1];
        Stroke(editor, (1, 0));
        Select(editor, (1, 0));
        editor.IsMoveToolActive = true;
        editor.BeginMoveDrag(1, 0);
        editor.UpdateMoveDrag(2, 0);
        editor.CommitMoveDrag();
        Assert.Equal("forest_0002", editor.SelectedMap![2, 0]);

        editor.Undo();

        Assert.Equal(["forest_0001", "forest_0002", "forest_0001"], editor.SelectedMap.Cells.Take(3));
        Assert.Equal([(1, 0)], editor.SelectedCells);
        Assert.Equal("Undid: Move 1 cell.", editor.StatusMessage);

        editor.Redo();

        Assert.Equal([null, "forest_0002"], editor.SelectedMap.Cells.Skip(1).Take(2));
        Assert.Equal([(2, 0)], editor.SelectedCells);
    }

    [AvaloniaFact]
    public void UndoingAResizeRestoresTheSizeAndTheCutOffTiles()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (3, 2));

        editor.ResizeSelectedMap(2, 2);
        Assert.Equal(4, editor.SelectedMap!.Cells.Length);

        editor.Undo();

        Assert.Equal((4, 3), (editor.SelectedMap.Width, editor.SelectedMap.Height));
        Assert.Equal("forest_0001", editor.SelectedMap[3, 2]);
        Assert.Equal(4 * 16, editor.MapPixelWidth);
        Assert.Equal(12, editor.CellRegions.Length);
        Assert.Equal("Undid: Resize map.", editor.StatusMessage);

        editor.Redo();

        Assert.Equal((2, 2), (editor.SelectedMap.Width, editor.SelectedMap.Height));
        Assert.Equal(2 * 16, editor.MapPixelWidth);
        Assert.Single(editor.Maps);
    }

    [AvaloniaFact]
    public void ANewEditAfterUndoDiscardsTheRedoSteps()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0));
        editor.Undo();
        Assert.True(editor.CanRedo);

        Stroke(editor, (1, 1));

        Assert.False(editor.CanRedo);
    }

    [AvaloniaFact]
    public void DirtyStateFollowsEditUndoRedoAndSave()
    {
        var viewModel = ProjectWithMap();
        var editor = viewModel.MapEditor;
        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);
        Assert.False(viewModel.Editor.HasUnsavedChanges);

        Stroke(editor, (0, 0));
        Assert.True(viewModel.Editor.HasUnsavedChanges);

        // As in the tileset editor, going back to the saved state still counts as a change.
        viewModel.SaveProject(path);
        editor.Undo();
        Assert.True(viewModel.Editor.HasUnsavedChanges);

        viewModel.SaveProject(path);
        Assert.False(viewModel.Editor.HasUnsavedChanges);
        editor.Redo();
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void EachMapKeepsItsOwnHistoryWhenSwitchingMaps()
    {
        var viewModel = ProjectWithMap();
        var editor = viewModel.MapEditor;
        Stroke(editor, (0, 0));
        editor.CreateMap("level2", 3, 3, "forest");
        Assert.False(editor.CanUndo);
        Stroke(editor, (1, 1));
        Stroke(editor, (2, 2));

        editor.SelectedMap = editor.Maps[0];
        Assert.True(editor.CanUndo);
        editor.Undo();
        Assert.Null(editor.Maps[0][0, 0]);
        Assert.False(editor.CanUndo);

        editor.SelectedMap = editor.Maps[1];
        editor.Undo();
        Assert.Equal("forest_0001", editor.Maps[1][1, 1]);
        Assert.Null(editor.Maps[1][2, 2]);
        Assert.True(editor.CanUndo);

        viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        Assert.True(editor.CanUndo);
        Assert.True(editor.CanRedo);
    }

    [AvaloniaFact]
    public void RenamingAMapKeepsItsHistory()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0));

        editor.RenameSelectedMap("renamed");
        Assert.True(editor.CanUndo);
        editor.Undo();

        Assert.Equal("renamed", editor.SelectedMap!.Name);
        Assert.Null(editor.SelectedMap[0, 0]);
    }

    [AvaloniaFact]
    public void RenamingTheTilesetKeepsTheMapHistory()
    {
        var viewModel = ProjectWithMap();
        Stroke(viewModel.MapEditor, (0, 0));

        viewModel.RenameSelectedTileset("woods");
        viewModel.MapEditor.Undo();

        Assert.Equal("woods", viewModel.MapEditor.SelectedMap!.TilesetName);
        Assert.Null(viewModel.MapEditor.SelectedMap[0, 0]);
    }

    [AvaloniaFact]
    public void DeletingAMapDropsItsHistoryEvenIfAMapWithTheSameNameIsCreatedLater()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0));
        editor.DeleteSelectedMap();

        editor.CreateMap("level1", 2, 2, "forest");

        Assert.False(editor.CanUndo);
    }

    [AvaloniaFact]
    public void RemovingTheTilesetDropsTheHistoryOfItsMaps()
    {
        var viewModel = ProjectWithMap();
        Stroke(viewModel.MapEditor, (0, 0));

        viewModel.RemoveSelectedTileset();
        viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        viewModel.MapEditor.CreateMap("level1", 2, 2, "forest");

        Assert.False(viewModel.MapEditor.CanUndo);
    }

    [AvaloniaFact]
    public void ClearingDeletedTilesDropsTheHistoryOfTheTilesetsMapsOnly()
    {
        var viewModel = ProjectWithMap();
        var editor = viewModel.MapEditor;
        // Painted and erased again, so no cell holds the tile, but the history still does.
        Stroke(editor, (0, 0));
        Select(editor, (0, 0));
        editor.DeleteSelectedCells();
        viewModel.LoadTileset(WriteImage("dungeon"), 16, 16);
        editor.CreateMap("other", 2, 2, "dungeon");
        Stroke(editor, (0, 0));
        viewModel.SelectTileset("forest");
        editor.SelectedMap = editor.Maps[0];
        Stroke(editor, (1, 1));

        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        Assert.Null(editor.Maps[0][1, 1]);
        Assert.False(editor.CanUndo);
        editor.SelectedMap = editor.Maps[1];
        Assert.True(editor.CanUndo);
    }

    [AvaloniaFact]
    public void ClosingTheProjectDropsTheHistory()
    {
        var viewModel = ProjectWithMap();
        Stroke(viewModel.MapEditor, (0, 0));

        viewModel.CloseProject();
        viewModel.NewProject();
        viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        viewModel.MapEditor.CreateMap("level1", 2, 2, "forest");

        Assert.False(viewModel.MapEditor.CanUndo);
    }

    [AvaloniaFact]
    public void UndoIsIgnoredWhileAStrokeOrDragIsInProgress()
    {
        var editor = ProjectWithMap().MapEditor;
        Stroke(editor, (0, 0));
        editor.IsDrawToolActive = true;
        editor.BeginStroke(1, 0);

        editor.Undo();

        Assert.Equal("forest_0001", editor.SelectedMap![0, 0]);
        editor.EndStroke();
        editor.Undo();
        Assert.Null(editor.SelectedMap[1, 0]);
        Assert.Equal("forest_0001", editor.SelectedMap[0, 0]);
    }

    [AvaloniaFact]
    public void MainViewModelUndoActsOnTheActiveTab()
    {
        var viewModel = ProjectWithMap();
        var editor = viewModel.MapEditor;
        editor.SelectedPaletteTile = editor.PaletteTiles[1];
        Stroke(editor, (0, 0));
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
        Assert.True(viewModel.CanUndo);
        viewModel.Undo();
        Assert.Equal(16, viewModel.Editor.Tiles.Count);
        Assert.Equal("forest_0002", editor.SelectedMap![0, 0]);

        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        Assert.True(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        viewModel.Undo();
        Assert.Null(editor.SelectedMap[0, 0]);
        Assert.True(viewModel.CanRedo);
        Assert.False(viewModel.CanUndo);
        viewModel.Redo();
        Assert.Equal("forest_0002", editor.SelectedMap[0, 0]);

        viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
        Assert.True(viewModel.CanRedo);
    }

    [AvaloniaFact]
    public void ShortcutsAndMenuItemsUndoAndRedoOnTheMapsTab()
    {
        var viewModel = ProjectWithMap();
        Stroke(viewModel.MapEditor, (0, 0));
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Null(viewModel.MapEditor.SelectedMap![0, 0]);

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        Assert.Equal("forest_0001", viewModel.MapEditor.SelectedMap[0, 0]);

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("forest_0001", viewModel.MapEditor.SelectedMap[0, 0]);
        window.Close();
    }

    private static void Stroke(MapEditorViewModel editor, params (int X, int Y)[] cells)
    {
        editor.SelectedPaletteTile ??= editor.PaletteTiles[0];
        editor.IsDrawToolActive = true;
        editor.BeginStroke(cells[0].X, cells[0].Y);
        foreach (var (x, y) in cells.Skip(1))
            editor.ContinueStroke(x, y);
        editor.EndStroke();
        editor.IsDrawToolActive = false;
    }

    private static void Select(MapEditorViewModel editor, params (int X, int Y)[] cells)
    {
        editor.IsRectangleToolActive = true;
        editor.HandleCanvasClick(cells[0].X, cells[0].Y, addToSelection: false);
        foreach (var (x, y) in cells.Skip(1))
            editor.HandleCanvasClick(x, y, addToSelection: true);
    }

    private MainViewModel ProjectWithMap()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        viewModel.MapEditor.CreateMap("level1", 4, 3, "forest");
        return viewModel;
    }

    private string WriteImage(string name)
    {
        var path = Path.Combine(_directory, name + ".png");
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
