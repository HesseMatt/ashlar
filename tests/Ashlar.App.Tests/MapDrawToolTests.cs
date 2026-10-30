using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class MapDrawToolTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void ClickSetsTheCellToTheSelectedPaletteTile()
    {
        var viewModel = ProjectWithMap(4, 4);
        var editor = viewModel.MapEditor;
        editor.IsDrawToolActive = true;
        editor.SelectedPaletteTile = editor.PaletteTiles[2];

        Assert.True(editor.BeginStroke(1, 3));
        editor.EndStroke();

        Assert.Equal("forest_0003", editor.SelectedMap![1, 3]);
        Assert.Equal("forest_0003", editor.CellRegions[3 * 4 + 1]!.Name);
        Assert.Equal(1, editor.SelectedMap.Cells.Count(cell => cell is not null));
    }

    [AvaloniaFact]
    public void DraggingFastPaintsEveryCellAlongTheStroke()
    {
        var viewModel = ProjectWithMap(10, 10);
        var editor = viewModel.MapEditor;
        editor.IsDrawToolActive = true;
        editor.SelectedPaletteTile = editor.PaletteTiles[0];

        editor.BeginStroke(0, 0);
        editor.ContinueStroke(9, 4);
        editor.ContinueStroke(9, 9);
        editor.EndStroke();

        Assert.Equal(15, editor.SelectedMap!.Cells.Count(cell => cell == "forest_0001"));
        Assert.Equal(15, editor.CellRegions.Count(region => region is not null));
    }

    [AvaloniaFact]
    public void CellsOutsideTheMapAreIgnored()
    {
        var viewModel = ProjectWithMap(3, 3);
        var editor = viewModel.MapEditor;
        editor.IsDrawToolActive = true;
        editor.SelectedPaletteTile = editor.PaletteTiles[0];

        editor.BeginStroke(-2, 1);
        editor.ContinueStroke(6, 1);
        editor.EndStroke();

        Assert.Equal(3, editor.SelectedMap!.Cells.Count(cell => cell is not null));
    }

    [AvaloniaFact]
    public void NothingIsPaintedWithoutASelectedTileOrWithTheToolOff()
    {
        var viewModel = ProjectWithMap(3, 3);
        var editor = viewModel.MapEditor;

        editor.IsDrawToolActive = true;
        Assert.False(editor.BeginStroke(1, 1));
        editor.ContinueStroke(2, 2);

        editor.SelectedPaletteTile = editor.PaletteTiles[0];
        editor.IsDrawToolActive = false;
        Assert.False(editor.BeginStroke(1, 1));

        Assert.All(editor.SelectedMap!.Cells, Assert.Null);
    }

    [AvaloniaFact]
    public void PaintingMarksTheProjectAsChanged()
    {
        var viewModel = ProjectWithMap(3, 3);
        var editor = viewModel.MapEditor;
        viewModel.Editor.HasUnsavedChanges = false;
        editor.IsDrawToolActive = true;
        editor.SelectedPaletteTile = editor.PaletteTiles[0];

        editor.BeginStroke(1, 1);
        editor.EndStroke();

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ToolBarButtonTogglesTheDrawToolAndShowsItsState()
    {
        var viewModel = ProjectWithMap(3, 3);
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();

        var button = window.GetVisualDescendants().OfType<MapToolBar>().Single().FindControl<ToggleButton>("MapDrawButton")!;
        Assert.True(button.IsEnabled);
        Assert.False(button.IsChecked);

        viewModel.MapEditor.IsDrawToolActive = true;
        Assert.True(button.IsChecked);

        button.IsChecked = false;
        Assert.False(viewModel.MapEditor.IsDrawToolActive);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingOnTheCanvasPaintsCellsAndRightDragDoesNot()
    {
        var viewModel = ProjectWithMap(4, 4);
        var editor = viewModel.MapEditor;
        editor.IsDrawToolActive = true;
        editor.SelectedPaletteTile = editor.PaletteTiles[1];
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var content = window.GetVisualDescendants().OfType<MapEditorView>().Single().FindControl<Control>("MapCanvasContent")!;
        Point InWindow(double x, double y) => content.TranslatePoint(new Point(x, y), window)!.Value;

        window.MouseDown(InWindow(8, 8), MouseButton.Right);
        window.MouseMove(InWindow(40, 8));
        window.MouseUp(InWindow(40, 8), MouseButton.Right);
        Assert.All(editor.SelectedMap!.Cells, Assert.Null);

        window.MouseDown(InWindow(8, 8), MouseButton.Left);
        window.MouseMove(InWindow(56, 40));
        window.MouseUp(InWindow(56, 40), MouseButton.Left);

        Assert.Equal("forest_0002", editor.SelectedMap[0, 0]);
        Assert.Equal("forest_0002", editor.SelectedMap[3, 2]);
        Assert.True(editor.SelectedMap.Cells.Count(cell => cell is not null) >= 4);
        window.Close();
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
