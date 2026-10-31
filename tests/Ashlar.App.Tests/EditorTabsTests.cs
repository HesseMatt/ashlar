using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class EditorTabsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly MainViewModel _viewModel = new();
    private readonly MainWindow _window;

    public EditorTabsTests()
    {
        _window = new MainWindow { DataContext = _viewModel, Width = 1000, Height = 600 };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        Directory.Delete(_directory, recursive: true);
    }

    private TabStrip TabHeaders => _window.FindControl<TabStrip>("EditorTabs")!;

    private bool IsTilesetToolBarShown => _window.GetVisualDescendants().OfType<TilesetToolBar>().Any();

    private bool IsMapToolBarShown => _window.GetVisualDescendants().OfType<MapToolBar>().Any();

    private IEnumerable<MenuItem> MenuItems(string header) =>
        _window.GetLogicalDescendants().OfType<MenuItem>().Where(m => m.Header as string == header).DistinctBy(m => m);

    private void OpenProject()
    {
        _viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        _viewModel.LoadTileset(WriteImage("dungeon"), 16, 16);
        _viewModel.CreateMap("level1", 4, 4, "forest");
        _viewModel.CreateMap("level2", 4, 4, "dungeon");
    }

    [AvaloniaFact]
    public void NoTabsAreOpenInAFreshProjectAndTheEditorAreaIsEmpty()
    {
        _viewModel.NewProject();

        Assert.Empty(_viewModel.OpenTabs);
        Assert.Null(_viewModel.ActiveTab);
        Assert.False(TabHeaders.IsVisible);
        Assert.False(IsTilesetToolBarShown);
        Assert.False(IsMapToolBarShown);
    }

    [AvaloniaFact]
    public void ImportingATilesetAndCreatingAMapOpenTheirTabsRightAway()
    {
        OpenProject();

        Assert.Equal(["forest", "dungeon", "level1", "level2"], _viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.Equal(["forest", "dungeon", "level1", "level2"], TabHeaders.Items.Cast<EditorTabViewModel>().Select(tab => tab.Title));
        Assert.IsType<MapEditorViewModel>(_viewModel.ActiveTab);
        Assert.Equal("level2", _viewModel.ActiveTab!.Title);
    }

    [AvaloniaFact]
    public void OpeningAnOpenElementFocusesItsTabInsteadOfOpeningASecondOne()
    {
        OpenProject();
        var forest = _viewModel.OpenTabs[0];

        _viewModel.OpenTileset("forest");
        _viewModel.OpenMap("level1");
        _viewModel.OpenTileset("forest");

        Assert.Equal(4, _viewModel.OpenTabs.Count);
        Assert.Same(forest, _viewModel.ActiveTab);
        Assert.True(forest.IsActive);
        Assert.Single(_viewModel.OpenTabs, tab => tab.IsActive);
    }

    [AvaloniaFact]
    public void OpeningFromTheExplorerOpensTheElementsTab()
    {
        OpenProject();
        _viewModel.CloseTab(_viewModel.OpenTabs[2]);
        var node = _viewModel.MapsGroup.Children.Single(n => n.Name == "level1");

        _viewModel.OpenTab(node);

        Assert.Equal("level1", _viewModel.ActiveTab!.Title);
        Assert.Same(node, _viewModel.ExplorerSelection);
    }

    [AvaloniaFact]
    public void ClosingTheActiveTabActivatesANeighbourAndClosingTheLastLeavesAnEmptyEditorArea()
    {
        OpenProject();
        _viewModel.OpenMap("level1");

        _viewModel.CloseActiveTab();
        Assert.Equal("dungeon", _viewModel.ActiveTab!.Title);

        _viewModel.CloseActiveTab();
        _viewModel.CloseActiveTab();
        _viewModel.CloseActiveTab();
        Assert.Empty(_viewModel.OpenTabs);
        Assert.Null(_viewModel.ActiveTab);
        Assert.True(_viewModel.IsProjectOpen);
        Assert.Equal(["forest", "dungeon"], _viewModel.TilesetNames);
        Assert.Equal(["level1", "level2"], _viewModel.MapNames);
    }

    [AvaloniaFact]
    public void ClosingATabNeverAsksAndKeepsTheProjectData()
    {
        OpenProject();
        _viewModel.OpenTileset("forest");
        _viewModel.Editor().HandleCanvasClick(5, 5, addToSelection: false);
        _viewModel.Editor().DeleteSelectedTiles();
        var tileCount = _viewModel.Editor().Tiles.Count;

        _viewModel.CloseActiveTab();
        _viewModel.OpenTileset("forest");

        Assert.Equal(tileCount, _viewModel.Editor().Tiles.Count);
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void TheCloseButtonAndMiddleClickCloseATab()
    {
        OpenProject();
        _window.GetLayoutManager()?.ExecuteLayoutPass();
        var header = TabHeaders.GetVisualDescendants().OfType<Button>().First(b => b.DataContext == _viewModel.OpenTabs[0]);

        header.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.DoesNotContain(_viewModel.OpenTabs, tab => tab.Title == "forest");
        Assert.Equal(3, _viewModel.OpenTabs.Count);
    }

    [AvaloniaFact]
    public void CtrlWClosesTheActiveTabAndCtrlShiftWClosesTheProject()
    {
        OpenProject();

        _window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);

        Assert.Equal(["forest", "dungeon", "level1"], _viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.True(_viewModel.IsProjectOpen);

        _viewModel.SaveProject(Path.Combine(_directory, "p.ashlar"));
        _window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.False(_viewModel.IsProjectOpen);
        Assert.Empty(_viewModel.OpenTabs);
    }

    [AvaloniaFact]
    public void TwoTilesetTabsKeepTheirOwnZoomSelectionToolAndHistory()
    {
        OpenProject();
        var forest = _viewModel.ShowTileset("forest");
        forest.HandleCanvasClick(5, 5, addToSelection: false);
        forest.DeleteSelectedTiles();
        forest.ZoomLevel = 3;
        forest.IsDrawToolActive = true;
        var dungeon = _viewModel.ShowTileset("dungeon");

        Assert.NotSame(forest, dungeon);
        Assert.Equal(1.0, dungeon.ZoomLevel);
        Assert.False(dungeon.IsDrawToolActive);
        Assert.False(dungeon.CanUndo);
        dungeon.HandleCanvasClick(20, 5, addToSelection: false);
        Assert.Empty(forest.SelectedTiles);

        _viewModel.OpenTileset("forest");

        Assert.Equal(3, forest.ZoomLevel);
        Assert.True(forest.IsDrawToolActive);
        Assert.True(forest.CanUndo);
        Assert.True(_window.FindEditorControl<ToggleButton>("DrawButton")!.IsChecked);
        Assert.Equal(15, forest.Tiles.Count);
        Assert.Equal(16, dungeon.Tiles.Count);
    }

    [AvaloniaFact]
    public void TwoMapTabsKeepTheirOwnZoomSelectionToolAndHistory()
    {
        OpenProject();
        _viewModel.CreateMap("level3", 4, 4, "forest");
        var first = _viewModel.ShowMap("level1");
        first.ZoomLevel = 2;
        first.IsRectangleToolActive = true;
        first.HandleCanvasClick(1, 1, addToSelection: false);
        first.SelectedPaletteTile = first.PaletteTiles[0];
        first.IsDrawToolActive = true;
        first.BeginStroke(0, 0);
        first.EndStroke();
        var second = _viewModel.ShowMap("level3");

        Assert.NotSame(first, second);
        Assert.Equal(1.0, second.ZoomLevel);
        Assert.Empty(second.SelectedCells);
        Assert.False(second.IsDrawToolActive);
        Assert.False(second.CanUndo);
        Assert.Null(second.SelectedPaletteTile);

        _viewModel.OpenMap("level1");

        Assert.Equal(2, first.ZoomLevel);
        Assert.True(first.IsDrawToolActive);
        Assert.True(first.CanUndo);
        Assert.Equal("forest_0001", first.Map[0, 0]);
        Assert.Null(second.Map[0, 0]);
    }

    [AvaloniaFact]
    public void TheToolBarFollowsTheKindOfTheActiveTab()
    {
        OpenProject();
        Assert.True(IsMapToolBarShown);
        Assert.False(IsTilesetToolBarShown);
        Assert.Null(_window.FindEditorControl<ToggleButton>("DrawButton"));

        _viewModel.OpenTileset("forest");

        Assert.True(IsTilesetToolBarShown);
        Assert.False(IsMapToolBarShown);
        Assert.NotNull(_window.FindEditorControl<ToggleButton>("DrawButton"));

        _viewModel.CloseTab(_viewModel.ActiveTab!);
        _viewModel.CloseTab(_viewModel.ActiveTab!);
        _viewModel.CloseTab(_viewModel.ActiveTab!);
        _viewModel.CloseTab(_viewModel.ActiveTab!);

        Assert.False(IsTilesetToolBarShown);
        Assert.False(IsMapToolBarShown);
    }

    [AvaloniaFact]
    public void ThePaletteIsShownOnlyForAMapTabAndTheInspectorOnlyForATilesetTab()
    {
        OpenProject();
        _window.GetLayoutManager()?.ExecuteLayoutPass();
        bool IsShown<TView>(string name) where TView : Control =>
            _window.GetVisualDescendants().OfType<TView>().Any(view => view.IsEffectivelyVisible && view.FindControl<Control>(name) is { IsEffectivelyVisible: true });

        Assert.True(IsShown<MapEditorView>("PaletteList"));
        Assert.False(IsShown<TilesetEditorView>("InspectorPanel"));

        _viewModel.OpenTileset("forest");
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.False(IsShown<MapEditorView>("PaletteList"));
        Assert.True(IsShown<TilesetEditorView>("InspectorPanel"));

        _viewModel.CloseActiveTab();
        _viewModel.CloseActiveTab();
        _viewModel.CloseActiveTab();
        _viewModel.CloseActiveTab();

        Assert.False(IsShown<MapEditorView>("PaletteList"));
        Assert.False(IsShown<TilesetEditorView>("InspectorPanel"));
    }

    [AvaloniaFact]
    public void TheStatusBarShowsTheActiveTabsMessageAndZoomEntriesOnlyWhereTheyApply()
    {
        OpenProject();
        var status = _window.FindControl<TextBlock>("StatusText")!;
        _viewModel.MapEditor().StatusMessage = "map message";
        _viewModel.OpenTileset("forest");
        _viewModel.Editor().StatusMessage = "tileset message";

        Assert.Equal("tileset message", status.Text);
        Assert.Contains(StatusBarTexts(), text => text == "16x16");

        _viewModel.OpenMap("level1");
        _viewModel.MapEditor().StatusMessage = "other map message";

        Assert.Equal("other map message", status.Text);
        Assert.DoesNotContain(StatusBarTexts(), text => text == "16x16");
    }

    private IEnumerable<string?> StatusBarTexts() =>
        _window.FindControl<TextBlock>("StatusText")!.GetVisualParent()!.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible).Select(block => block.Text);

    [AvaloniaFact]
    public void MenuItemsAreEnabledByTheKindOfTheActiveTab()
    {
        OpenProject();
        string[] tilesetOnly = ["Change tile size..."];
        string[] mapOnly = [];

        _viewModel.OpenTileset("forest");
        Assert.All(tilesetOnly.SelectMany(MenuItems), item => Assert.True(item.IsEnabled, (string)item.Header!));
        Assert.True(MenuItems("Undo").Single().IsEnabled == _viewModel.ActiveTab!.CanUndo);
        Assert.All(MenuItems("Zoom In"), item => Assert.True(item.IsEnabled));

        _viewModel.OpenMap("level1");
        Assert.All(tilesetOnly.SelectMany(MenuItems), item => Assert.False(item.IsEnabled, (string)item.Header!));
        Assert.All(mapOnly.SelectMany(MenuItems), item => Assert.True(item.IsEnabled));

        while (_viewModel.ActiveTab is not null)
            _viewModel.CloseActiveTab();

        Assert.All(MenuItems("Zoom In").Concat(MenuItems("Close tab")).Concat(MenuItems("Undo")), item => Assert.False(item.IsEnabled, (string)item.Header!));
        Assert.All(MenuItems("Change tile size..."), item => Assert.False(item.IsEnabled));
        Assert.All(MenuItems("Save project").Concat(MenuItems("Add tileset...")).Concat(MenuItems("Close project")), item => Assert.True(item.IsEnabled, (string)item.Header!));
    }

    [AvaloniaFact]
    public void ToolToggleItemsOfTheEditMenuShowTheToolsOfTheActiveKind()
    {
        OpenProject();
        bool Visible(string header) => MenuItems(header).Any(item => item.IsVisible);
        int Visible_Count(string header) => MenuItems(header).Count(item => item.IsVisible);

        _viewModel.OpenTileset("forest");
        Assert.True(Visible("Bucket"));
        Assert.True(Visible("Pipette"));
        Assert.Equal(1, Visible_Count("Draw"));

        _viewModel.OpenMap("level1");
        Assert.False(Visible("Bucket"));
        Assert.False(Visible("Pipette"));
        Assert.Equal(1, Visible_Count("Draw"));
        Assert.Equal(1, Visible_Count("Rectangle selection"));
        Assert.Equal(1, Visible_Count("Move"));
    }

    [AvaloniaFact]
    public void TilesetShortcutsAreIgnoredWhileAMapTabIsActive()
    {
        OpenProject();
        var forest = _viewModel.ShowTileset("forest");
        forest.HandleCanvasClick(5, 5, addToSelection: false);
        _viewModel.OpenMap("level1");

        _window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.True(forest.HasSelectedTiles);
        Assert.Equal(16, forest.Tiles.Count);
    }

    [AvaloniaFact]
    public void RemovingATilesetClosesItsTabAndTheTabsOfItsMaps()
    {
        OpenProject();

        _viewModel.RemoveTileset("forest");

        Assert.Equal(["dungeon", "level2"], _viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.Equal("level2", _viewModel.ActiveTab!.Title);
    }

    [AvaloniaFact]
    public void DeletingAMapClosesItsTabAndRenamingUpdatesTabTitles()
    {
        OpenProject();

        _viewModel.RenameMap("level1", "renamedMap");
        _viewModel.RenameTileset("forest", "woods");

        Assert.Equal(["woods", "dungeon", "renamedMap", "level2"], _viewModel.OpenTabs.Select(tab => tab.Title));

        _viewModel.DeleteMap("renamedMap");

        Assert.Equal(["woods", "dungeon", "level2"], _viewModel.OpenTabs.Select(tab => tab.Title));
    }

    [AvaloniaFact]
    public void DeletingTilesInATilesetTabUpdatesTheOpenMapsOfThatTileset()
    {
        OpenProject();
        var map = _viewModel.ShowMap("level1");
        map.Map[0, 0] = "forest_0001";
        map.Map[1, 0] = "forest_0002";
        map.RefreshTileset();
        var other = _viewModel.ShowMap("level2");
        other.Map[0, 0] = "dungeon_0001";
        _viewModel.HasUnsavedChanges = false;

        var forest = _viewModel.ShowTileset("forest");
        forest.HandleCanvasClick(5, 5, addToSelection: false);
        forest.DeleteSelectedTiles();

        Assert.Null(map.Map[0, 0]);
        Assert.Equal("forest_0002", map.Map[1, 0]);
        Assert.Null(map.CellRegions[0]);
        Assert.Equal(15, map.PaletteTiles.Count);
        Assert.Equal("dungeon_0001", other.Map[0, 0]);
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ChangingTheTileSizeInATilesetTabIsReflectedInItsOpenMaps()
    {
        OpenProject();
        var map = _viewModel.ShowMap("level1");
        var forest = _viewModel.ShowTileset("forest");

        forest.ChangeTileSize(32, 32);
        _viewModel.OpenMap("level1");

        Assert.Equal(32, map.TileWidth);
        Assert.Equal(4, map.PaletteTiles.Count);
    }

    [AvaloniaFact]
    public void EditsInAnyTabMarkTheProjectAsUnsaved()
    {
        OpenProject();
        _viewModel.SaveProject(Path.Combine(_directory, "p.ashlar"));
        Assert.False(_viewModel.HasUnsavedChanges);

        var map = _viewModel.ShowMap("level1");
        map.SelectedPaletteTile = map.PaletteTiles[0];
        map.IsDrawToolActive = true;
        map.BeginStroke(0, 0);
        map.EndStroke();
        Assert.True(_viewModel.HasUnsavedChanges);

        _viewModel.SaveProject(Path.Combine(_directory, "p.ashlar"));
        Assert.False(_viewModel.HasUnsavedChanges);

        var forest = _viewModel.ShowTileset("forest");
        forest.HandleCanvasClick(5, 5, addToSelection: false);
        forest.DeleteSelectedTiles();
        Assert.True(_viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void TheProjectFileHoldsTheLiveStateOfOpenTabs()
    {
        OpenProject();
        var forest = _viewModel.ShowTileset("forest");
        forest.HandleCanvasClick(5, 5, addToSelection: false);
        forest.DeleteSelectedTiles();
        var map = _viewModel.ShowMap("level1");
        map.SelectedPaletteTile = map.PaletteTiles[1];
        map.IsDrawToolActive = true;
        map.BeginStroke(2, 2);
        map.EndStroke();
        var path = Path.Combine(_directory, "p.ashlar");

        _viewModel.SaveProject(path);

        var saved = Ashlar.Core.Serialization.ProjectFile.Load(path);
        Assert.Equal(15, saved.Tilesets.Single(tileset => tileset.Name == "forest").Tiles.Count);
        Assert.Equal("forest_0003", saved.Maps.Single(m => m.Name == "level1")[2, 2]);
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
