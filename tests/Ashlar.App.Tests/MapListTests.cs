using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core.Serialization;

namespace Ashlar.App.Tests;

public class MapListTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void CreatingAMapAddsItListsItOpensItsTabAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.HasUnsavedChanges = false;

        var created = viewModel.CreateMap("level1", 10, 8, "forest");

        Assert.True(created);
        var map = viewModel.GetMap("level1")!;
        Assert.Equal(("level1", "forest", 10, 8), (map.Name, map.TilesetName, map.Width, map.Height));
        Assert.Equal(["level1"], viewModel.MapNames);
        Assert.Same(map, viewModel.MapEditor().Map);
        Assert.Equal("level1", viewModel.SelectedMapName);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void CreatingAMapWithATakenOrEmptyNameIsRefused()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 10, 8, "forest");

        Assert.False(viewModel.CreateMap(" level1 ", 4, 4, "dungeon"));
        Assert.False(viewModel.CreateMap("  ", 4, 4, "dungeon"));

        Assert.Single(viewModel.MapNames);
        Assert.Contains("empty", viewModel.StatusText);
    }

    [AvaloniaFact]
    public void RenamingKeepsSizeTilesetAndTabAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 10, 8, "forest");
        var editor = viewModel.MapEditor();
        viewModel.HasUnsavedChanges = false;

        var renamed = viewModel.RenameMap("level1", " cave ");

        Assert.True(renamed);
        var map = viewModel.GetMap("cave")!;
        Assert.Equal(("cave", "forest", 10, 8), (map.Name, map.TilesetName, map.Width, map.Height));
        Assert.Equal(["cave"], viewModel.MapNames);
        Assert.Same(editor, viewModel.MapEditor());
        Assert.Equal("cave", editor.Title);
        Assert.Same(map, editor.Map);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void RenamingToAnotherMapsNameIsRefused()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        viewModel.CreateMap("b", 2, 2, "forest");
        viewModel.HasUnsavedChanges = false;

        Assert.False(viewModel.RenameMap("b", "a"));
        Assert.False(viewModel.RenameMap("b", ""));

        Assert.Equal(["a", "b"], viewModel.MapNames);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ARefusedRenameShowsItsMessageInTheStatusBar()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        viewModel.CreateMap("b", 2, 2, "forest");
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();

        viewModel.RenameMap("b", "a");

        Assert.Contains("already exists", window.FindControl<TextBlock>("StatusText")!.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void SavingReportsInTheStatusBarOfTheActiveTab()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var status = window.FindControl<TextBlock>("StatusText")!;

        viewModel.SaveProject(Path.Combine(_directory, "p.ashlar"));
        Assert.Contains("Saved project", status.Text);

        viewModel.SaveProject(Path.Combine(_directory, "missing", "x.ashlar"));
        Assert.Contains("Could not save project", status.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void DeletingClosesTheTabAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        viewModel.CreateMap("b", 2, 2, "forest");
        viewModel.HasUnsavedChanges = false;

        viewModel.DeleteMap("b");
        Assert.Equal(["a"], viewModel.MapNames);
        Assert.DoesNotContain(viewModel.OpenTabs, tab => tab.Title == "b");
        Assert.True(viewModel.HasUnsavedChanges);

        viewModel.DeleteMap("a");
        Assert.Empty(viewModel.MapNames);
        Assert.Null(viewModel.ActiveMap);
    }

    [AvaloniaFact]
    public void ANewMapDefaultsToTheSelectedTileset()
    {
        var viewModel = TwoTilesets();
        Assert.Equal("forest", viewModel.SuggestedTilesetName);

        viewModel.OpenTileset("dungeon");

        Assert.Equal("dungeon", viewModel.SuggestedTilesetName);
    }

    [AvaloniaFact]
    public void MapsCanOnlyBeCreatedWhenTheProjectHasATileset()
    {
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        Assert.False(viewModel.CanCreateMap);

        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);
        Assert.True(viewModel.CanCreateMap);

        viewModel.RemoveTileset("dungeon");
        Assert.False(viewModel.CanCreateMap);
    }

    [AvaloniaFact]
    public void TheMapTabShowsTitleSizeAndTileset()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 10, 8, "forest");
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var mapView = window.GetVisualDescendants().OfType<MapEditorView>().Single();

        Assert.Equal("level1", mapView.FindControl<TextBlock>("MapTitleText")!.Text);
        Assert.Equal("10 x 8 cells", mapView.FindControl<TextBlock>("MapSizeText")!.Text);
        Assert.Equal("Tileset: forest", mapView.FindControl<TextBlock>("MapTilesetText")!.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void MapsArePersistedInTheProjectFileAndRestoredOnOpen()
    {
        var path = Path.Combine(_directory, "p.ashlar");
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 10, 8, "forest");
        viewModel.CreateMap("level2", 3, 4, "dungeon");
        viewModel.GetMap("level2")![1, 2] = "dungeon_0001";

        viewModel.SaveProject(path);
        Assert.False(viewModel.HasUnsavedChanges);

        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        Assert.Equal(["level1", "level2"], reopened.MapNames);
        var level2 = reopened.GetMap("level2")!;
        Assert.Equal("dungeon", level2.TilesetName);
        Assert.Equal((3, 4), (level2.Width, level2.Height));
        Assert.Equal("dungeon_0001", level2[1, 2]);
        Assert.False(reopened.HasUnsavedChanges);
        Assert.Equal(2, ProjectFile.Load(path).Maps.Count);
    }

    [AvaloniaFact]
    public void ClosingAndStartingANewProjectDropsTheMapsAndTabs()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 10, 8, "forest");

        viewModel.NewProject();

        Assert.Empty(viewModel.MapNames);
        Assert.Empty(viewModel.OpenTabs);
        Assert.Null(viewModel.ActiveTab);
    }

    [AvaloniaFact]
    public void RenamingATilesetUpdatesTheMapsUsingIt()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        viewModel.CreateMap("b", 2, 2, "dungeon");
        viewModel.GetMap("a")![0, 0] = "forest_0001";

        viewModel.RenameTileset("forest", "woods");

        Assert.Equal(["woods", "dungeon"], viewModel.MapNames.Select(name => viewModel.GetMap(name)!.TilesetName));
        Assert.Equal("forest_0001", viewModel.GetMap("a")![0, 0]);
    }

    [AvaloniaFact]
    public void RemovingATilesetDeletesItsMapsAndTabsAndKeepsTheOthers()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("a", 2, 2, "forest");
        viewModel.CreateMap("b", 2, 2, "dungeon");
        viewModel.CreateMap("c", 2, 2, "forest");
        Assert.Equal(2, viewModel.CountMapsUsing("forest"));
        viewModel.HasUnsavedChanges = false;

        viewModel.RemoveTileset("forest");

        Assert.Equal(["b"], viewModel.MapNames);
        Assert.Equal(["dungeon", "b"], viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void TheCreateDialogValidatesNameSizeAndTileset()
    {
        var dialog = new CreateMapDialogViewModel("Map 1", ["dungeon"], "dungeon", ["taken"]);
        Assert.True(dialog.IsValid);

        dialog.Name = "taken";
        Assert.False(dialog.IsValid);
        dialog.Name = "  ";
        Assert.False(dialog.IsValid);
        dialog.Name = "fresh";
        dialog.Width = 0;
        Assert.False(dialog.IsValid);
        dialog.Width = CreateMapDialogViewModel.MaxMapSize + 1;
        Assert.False(dialog.IsValid);
        dialog.Width = 5;
        dialog.SelectedTileset = null;
        Assert.False(dialog.IsValid);
        dialog.SelectedTileset = "dungeon";

        Assert.True(dialog.IsValid);
        Assert.Equal(new NewMapSettings("fresh", 5, 15, "dungeon"), dialog.ToSettings());
    }

    [AvaloniaFact]
    public void ResizingKeepsTheOverlapMarksTheProjectChangedAndKeepsTheTabsMap()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 3, 2, "forest");
        var editor = viewModel.MapEditor();
        editor.Map[2, 1] = "forest_0001";
        editor.Map[0, 0] = "forest_0002";
        editor.SelectedCells = [(0, 0), (2, 1)];
        viewModel.HasUnsavedChanges = false;

        viewModel.ResizeMap("level1", 5, 2);

        var map = viewModel.GetMap("level1")!;
        Assert.Same(map, editor.Map);
        Assert.Equal((5, 2), (map.Width, map.Height));
        Assert.Equal("forest_0002", map[0, 0]);
        Assert.Equal("forest_0001", map[2, 1]);
        Assert.Equal(2, map.Cells.Count(cell => cell is not null));
        Assert.Equal("5 x 2 cells", editor.SelectedMapSizeText);
        Assert.Equal(10, editor.CellRegions.Length);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ShrinkingCropsTheMapAndDropsSelectedCellsOutsideIt()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 3, 2, "forest");
        var editor = viewModel.MapEditor();
        editor.SelectedCells = [(0, 0), (2, 1)];

        viewModel.ResizeMap("level1", 2, 2);

        Assert.Equal((2, 2), (editor.Map.Width, editor.Map.Height));
        Assert.Equal([(0, 0)], editor.SelectedCells);
    }

    [AvaloniaFact]
    public void ResizingAMapWithoutATabChangesTheProject()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 3, 2, "forest");
        viewModel.CloseTab(viewModel.ActiveTab!);
        viewModel.HasUnsavedChanges = false;

        viewModel.ResizeMap("level1", 4, 4);

        Assert.Equal((4, 4), (viewModel.GetMap("level1")!.Width, viewModel.GetMap("level1")!.Height));
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.DoesNotContain(viewModel.OpenTabs, tab => tab is MapEditorViewModel);
    }

    [AvaloniaFact]
    public void OnlyTilesInTheCroppedAreaCountAsLost()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 3, 2, "forest");
        var editor = viewModel.MapEditor();
        editor.Map[0, 0] = "forest_0001";

        Assert.Equal(0, viewModel.CountTilesLostByResize("level1", 1, 1));
        editor.Map[2, 0] = "forest_0001";
        Assert.Equal(1, viewModel.CountTilesLostByResize("level1", 2, 2));
    }

    [AvaloniaFact]
    public void ResizingToTheSameSizeDoesNotMarkTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.CreateMap("level1", 3, 2, "forest");
        viewModel.HasUnsavedChanges = false;

        viewModel.ResizeMap("level1", 3, 2);

        Assert.False(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void TheResizeDialogValidatesTheSize()
    {
        var dialog = new ResizeMapDialogViewModel(4, 3);
        Assert.True(dialog.IsValid);

        dialog.Width = 0;
        Assert.False(dialog.IsValid);
        dialog.Width = 5;
        dialog.Height = CreateMapDialogViewModel.MaxMapSize + 1;
        Assert.False(dialog.IsValid);
        dialog.Height = 1;
        Assert.True(dialog.IsValid);
    }

    [AvaloniaFact]
    public async Task TheResizeDialogReturnsTheEnteredSizeAndCancelReturnsNothing()
    {
        var owner = new Window();
        owner.Show();
        var dialog = new ResizeMapDialog(new ResizeMapDialogViewModel(4, 3));
        var result = dialog.ShowDialog<(int Width, int Height)?>(owner);
        ((ResizeMapDialogViewModel)dialog.DataContext!).Width = 7;
        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal((7, 3), await result);

        var cancelled = new ResizeMapDialog(new ResizeMapDialogViewModel(4, 3));
        var cancelResult = cancelled.ShowDialog<(int Width, int Height)?>(owner);
        cancelled.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(await cancelResult);
    }

    private MainViewModel TwoTilesets()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);
        viewModel.LoadTileset(WriteImage("forest", 64, 64), 16, 16);
        return viewModel;
    }

    private string WriteImage(string name, int width, int height)
    {
        var path = Path.Combine(_directory, name + ".png");
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
