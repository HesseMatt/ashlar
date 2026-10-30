using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.Controls;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class MapSelectionToolsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void DraggingARectangleSelectsItsCellsAndANewOneReplacesThem()
    {
        var editor = ProjectWithMap(5, 4).MapEditor;
        editor.IsRectangleToolActive = true;

        editor.BeginSelectionDrag(1, 1, addToSelection: false);
        editor.UpdateSelectionDrag(2, 2);
        editor.EndSelectionDrag();
        Assert.Equal([(1, 1), (2, 1), (1, 2), (2, 2)], editor.SelectedCells);
        Assert.Equal(4, editor.SelectionOutlines.Count);

        editor.BeginSelectionDrag(4, 3, addToSelection: false);
        editor.UpdateSelectionDrag(4, 3);
        editor.EndSelectionDrag();
        Assert.Equal([(4, 3)], editor.SelectedCells);
    }

    [AvaloniaFact]
    public void ControlAddsToTheSelectionAndClickTogglesACell()
    {
        var editor = ProjectWithMap(5, 4).MapEditor;
        editor.IsRectangleToolActive = true;

        editor.HandleCanvasClick(0, 0, addToSelection: false);
        editor.BeginSelectionDrag(3, 0, addToSelection: true);
        editor.UpdateSelectionDrag(4, 0);
        editor.EndSelectionDrag();
        Assert.Equal([(0, 0), (3, 0), (4, 0)], editor.SelectedCells);

        editor.HandleCanvasClick(3, 0, addToSelection: true);
        Assert.Equal([(0, 0), (4, 0)], editor.SelectedCells);

        editor.HandleCanvasClick(-1, 7, addToSelection: false);
        Assert.False(editor.HasSelectedCells);
    }

    [AvaloniaFact]
    public void CancellingTheDragRestoresTheSelectionBefore()
    {
        var editor = ProjectWithMap(5, 4).MapEditor;
        editor.IsRectangleToolActive = true;
        editor.HandleCanvasClick(0, 0, addToSelection: false);

        editor.BeginSelectionDrag(2, 2, addToSelection: false);
        editor.UpdateSelectionDrag(3, 3);
        editor.CancelSelectionDrag();

        Assert.Equal([(0, 0)], editor.SelectedCells);
        Assert.Null(editor.SelectionRectangle);
    }

    [AvaloniaFact]
    public void DeleteClearsTheSelectedCellsAndMarksTheProjectChanged()
    {
        var viewModel = ProjectWithMap(3, 3);
        var editor = viewModel.MapEditor;
        PaintCells(editor, (0, 0), (1, 0), (2, 2));
        viewModel.Editor.HasUnsavedChanges = false;
        editor.IsRectangleToolActive = true;
        editor.BeginSelectionDrag(0, 0, addToSelection: false);
        editor.UpdateSelectionDrag(1, 0);
        editor.EndSelectionDrag();

        editor.DeleteSelectedCells();

        Assert.Null(editor.SelectedMap![0, 0]);
        Assert.Null(editor.SelectedMap[1, 0]);
        Assert.NotNull(editor.SelectedMap[2, 2]);
        Assert.Equal(1, editor.CellRegions.Count(region => region is not null));
        Assert.False(editor.HasSelectedCells);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void MoveShiftsTheSelectionOverwritesAndDropsCellsOutsideTheMap()
    {
        var viewModel = ProjectWithMap(4, 2);
        var editor = viewModel.MapEditor;
        editor.SelectedMap![0, 0] = "forest_0001";
        editor.SelectedMap[3, 0] = "forest_0002";
        editor.SelectedMap[2, 1] = "forest_0003";
        editor.RefreshTileset();
        editor.IsRectangleToolActive = true;
        editor.BeginSelectionDrag(0, 0, addToSelection: false);
        editor.UpdateSelectionDrag(0, 0);
        editor.EndSelectionDrag();
        editor.HandleCanvasClick(3, 0, addToSelection: true);

        editor.IsMoveToolActive = true;
        Assert.False(editor.BeginMoveDrag(1, 1));
        Assert.True(editor.BeginMoveDrag(0, 0));
        editor.UpdateMoveDrag(2, 1);
        Assert.Equal(new Vector(2 * 16, 16), editor.MoveOffset);
        editor.CommitMoveDrag();

        // (0,0) lands on (2,1) and overwrites it; (3,0) would land outside and is dropped.
        Assert.Equal("forest_0001", editor.SelectedMap[2, 1]);
        Assert.Null(editor.SelectedMap[0, 0]);
        Assert.Null(editor.SelectedMap[3, 0]);
        Assert.Equal(1, editor.SelectedMap.Cells.Count(cell => cell is not null));
        Assert.Equal("forest_0001", editor.CellRegions[1 * 4 + 2]!.Name);
        Assert.Equal([(2, 1)], editor.SelectedCells);
        Assert.Null(editor.MoveOffset);
        Assert.Equal(4, editor.SelectedMap.Width);
        Assert.Equal(2, editor.SelectedMap.Height);
    }

    [AvaloniaFact]
    public void OnlyOneToolIsActiveAtATime()
    {
        var editor = ProjectWithMap(3, 3).MapEditor;

        editor.IsDrawToolActive = true;
        editor.IsMoveToolActive = true;

        Assert.False(editor.IsDrawToolActive);
        Assert.True(editor.IsMoveToolActive);
        editor.IsMoveToolActive = false;
        Assert.Equal(CanvasTool.Select, editor.ActiveTool);
    }

    [AvaloniaFact]
    public void SelectionIsResetWhenAnotherMapIsSelected()
    {
        var editor = ProjectWithMap(3, 3).MapEditor;
        editor.IsRectangleToolActive = true;
        editor.HandleCanvasClick(1, 1, addToSelection: false);

        editor.CreateMap("level2", 2, 2, "forest");

        Assert.False(editor.HasSelectedCells);
    }

    [AvaloniaFact]
    public void ToolBarButtonsToggleTheToolsAndDeleteFollowsTheSelection()
    {
        var viewModel = ProjectWithMap(3, 3);
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var toolBar = window.GetVisualDescendants().OfType<MapToolBar>().Single();
        var rectangle = toolBar.FindControl<ToggleButton>("MapRectangleSelectButton")!;
        var move = toolBar.FindControl<ToggleButton>("MapMoveButton")!;
        var delete = toolBar.FindControl<Button>("MapDeleteButton")!;

        rectangle.IsChecked = true;
        Assert.True(viewModel.MapEditor.IsRectangleToolActive);
        move.IsChecked = true;
        Assert.True(viewModel.MapEditor.IsMoveToolActive);
        Assert.False(rectangle.IsChecked);

        Assert.False(delete.IsEnabled);
        viewModel.MapEditor.IsRectangleToolActive = true;
        viewModel.MapEditor.HandleCanvasClick(1, 1, addToSelection: false);
        Assert.True(delete.IsEnabled);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingOnTheCanvasSelectsCellsAndDeleteKeyClearsThem()
    {
        var viewModel = ProjectWithMap(4, 4);
        var editor = viewModel.MapEditor;
        PaintCells(editor, (0, 0), (1, 0), (1, 1), (3, 3));
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        editor.IsRectangleToolActive = true;
        var content = window.GetVisualDescendants().OfType<MapEditorView>().Single().FindControl<Control>("MapCanvasContent")!;
        Point InWindow(double x, double y) => content.TranslatePoint(new Point(x, y), window)!.Value;

        window.MouseDown(InWindow(2, 2), MouseButton.Left);
        window.MouseMove(InWindow(20, 20));
        window.MouseMove(InWindow(40, 24));
        window.MouseUp(InWindow(40, 24), MouseButton.Left);

        Assert.Equal(6, editor.SelectedCells.Count);
        Assert.Null(editor.SelectionRectangle);
        Assert.NotNull(window.GetVisualDescendants().OfType<TileGridOverlay>().Single().SelectedTiles);

        content.GetVisualAncestors().OfType<Border>().First(b => b.Name == "MapCanvasPadding").Focus();
        window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);

        Assert.Equal(1, editor.SelectedMap!.Cells.Count(cell => cell is not null));
        Assert.False(editor.HasSelectedCells);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingASelectedCellOnTheCanvasMovesTheSelection()
    {
        var viewModel = ProjectWithMap(4, 4);
        var editor = viewModel.MapEditor;
        PaintCells(editor, (0, 0));
        editor.IsRectangleToolActive = true;
        editor.HandleCanvasClick(0, 0, addToSelection: false);
        editor.IsMoveToolActive = true;
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var content = window.GetVisualDescendants().OfType<MapEditorView>().Single().FindControl<Control>("MapCanvasContent")!;
        Point InWindow(double x, double y) => content.TranslatePoint(new Point(x, y), window)!.Value;

        window.MouseDown(InWindow(8, 8), MouseButton.Left);
        window.MouseMove(InWindow(24, 24));
        window.MouseMove(InWindow(40, 24));
        window.MouseUp(InWindow(40, 24), MouseButton.Left);

        Assert.Null(editor.SelectedMap![0, 0]);
        Assert.Equal("forest_0001", editor.SelectedMap[2, 1]);
        window.Close();
    }

    private static void PaintCells(MapEditorViewModel editor, params (int X, int Y)[] cells)
    {
        editor.SelectedPaletteTile = editor.PaletteTiles[0];
        editor.IsDrawToolActive = true;
        foreach (var (x, y) in cells)
        {
            editor.BeginStroke(x, y);
            editor.EndStroke();
        }

        editor.IsDrawToolActive = false;
    }

    private MainViewModel ProjectWithMap(int width, int height)
    {
        var viewModel = new MainViewModel();
        var path = Path.Combine(_directory, "forest.png");
        using (var bitmap = new SKBitmap(64, 64))
        {
            bitmap.Erase(SKColors.Red);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }
        viewModel.LoadTileset(path, 16, 16);
        viewModel.MapEditor.CreateMap("level1", width, height, "forest");
        return viewModel;
    }
}
