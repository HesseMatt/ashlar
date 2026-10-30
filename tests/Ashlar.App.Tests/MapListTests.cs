using Avalonia.Controls;
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
    public void CreatingAMapAddsAndSelectsItAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.Editor.HasUnsavedChanges = false;

        var created = viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");

        Assert.True(created);
        var map = Assert.Single(viewModel.MapEditor.Maps);
        Assert.Equal(("level1", "forest", 10, 8), (map.Name, map.TilesetName, map.Width, map.Height));
        Assert.Same(map, viewModel.MapEditor.SelectedMap);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void CreatingAMapWithATakenOrEmptyNameIsRefused()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");

        Assert.False(viewModel.MapEditor.CreateMap(" level1 ", 4, 4, "dungeon"));
        Assert.False(viewModel.MapEditor.CreateMap("  ", 4, 4, "dungeon"));

        Assert.Single(viewModel.MapEditor.Maps);
        Assert.Contains("empty", viewModel.MapEditor.StatusMessage);
    }

    [AvaloniaFact]
    public void RenamingKeepsSizeTilesetAndSelectionAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");
        viewModel.Editor.HasUnsavedChanges = false;

        var renamed = viewModel.MapEditor.RenameSelectedMap(" cave ");

        Assert.True(renamed);
        var map = Assert.Single(viewModel.MapEditor.Maps);
        Assert.Equal(("cave", "forest", 10, 8), (map.Name, map.TilesetName, map.Width, map.Height));
        Assert.Same(map, viewModel.MapEditor.SelectedMap);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void RenamingToAnotherMapsNameIsRefused()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("a", 2, 2, "forest");
        viewModel.MapEditor.CreateMap("b", 2, 2, "forest");
        viewModel.Editor.HasUnsavedChanges = false;

        Assert.False(viewModel.MapEditor.RenameSelectedMap("a"));
        Assert.False(viewModel.MapEditor.RenameSelectedMap(""));

        Assert.Equal(["a", "b"], viewModel.MapEditor.Maps.Select(map => map.Name));
        Assert.False(viewModel.Editor.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void DeletingSelectsANeighbourAndMarksTheProjectChanged()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("a", 2, 2, "forest");
        viewModel.MapEditor.CreateMap("b", 2, 2, "forest");
        viewModel.Editor.HasUnsavedChanges = false;

        viewModel.MapEditor.DeleteSelectedMap();
        Assert.Equal("a", viewModel.MapEditor.SelectedMap?.Name);
        Assert.True(viewModel.Editor.HasUnsavedChanges);

        viewModel.MapEditor.DeleteSelectedMap();
        Assert.Empty(viewModel.MapEditor.Maps);
        Assert.Null(viewModel.MapEditor.SelectedMap);
    }

    [AvaloniaFact]
    public void TheSelectedTilesetIsOfferedForNewMaps()
    {
        var viewModel = TwoTilesets();
        Assert.Equal("forest", viewModel.MapEditor.PreferredTilesetName);

        viewModel.SelectTileset("dungeon");

        Assert.Equal("dungeon", viewModel.MapEditor.PreferredTilesetName);
    }

    [AvaloniaFact]
    public void MapsCanOnlyBeCreatedWhenTheProjectHasATileset()
    {
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        Assert.False(viewModel.MapEditor.CanCreateMap);

        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);
        Assert.True(viewModel.MapEditor.CanCreateMap);

        viewModel.RemoveSelectedTileset();
        Assert.False(viewModel.MapEditor.CanCreateMap);
    }

    [AvaloniaFact]
    public void TheMapsTabExplainsWhyCreatingIsUnavailableWithoutATileset()
    {
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var mapView = window.GetVisualDescendants().OfType<MapEditorView>().Single();

        var createButton = mapView.FindControl<Button>("CreateMapButton")!;
        var explanation = mapView.FindControl<TextBlock>("NoTilesetText")!;
        Assert.False(createButton.IsEnabled);
        Assert.True(explanation.IsVisible);

        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);

        Assert.True(createButton.IsEnabled);
        Assert.False(explanation.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public void TheSelectedMapIsShownAsAPlaceholderAndListed()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");
        viewModel.MapEditor.CreateMap("level2", 3, 4, "dungeon");
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var mapView = window.GetVisualDescendants().OfType<MapEditorView>().Single();
        var list = mapView.FindControl<ListBox>("MapList")!;

        list.SelectedItem = viewModel.MapEditor.Maps[0];

        Assert.Equal("level1", mapView.FindControl<TextBlock>("MapTitleText")!.Text);
        Assert.Equal("10 x 8 cells", mapView.FindControl<TextBlock>("MapSizeText")!.Text);
        Assert.Equal("Tileset: forest", mapView.FindControl<TextBlock>("MapTilesetText")!.Text);
        Assert.Equal("level1", mapView.FindControl<TextBox>("MapNameBox")!.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void MapsArePersistedInTheProjectFileAndRestoredOnOpen()
    {
        var path = Path.Combine(_directory, "p.ashlar");
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");
        viewModel.MapEditor.CreateMap("level2", 3, 4, "dungeon");
        viewModel.MapEditor.Maps[1][1, 2] = "dungeon_0001";

        viewModel.SaveProject(path);
        Assert.False(viewModel.Editor.HasUnsavedChanges);

        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        Assert.Equal(["level1", "level2"], reopened.MapEditor.Maps.Select(map => map.Name));
        Assert.Equal("dungeon", reopened.MapEditor.Maps[1].TilesetName);
        Assert.Equal((3, 4), (reopened.MapEditor.Maps[1].Width, reopened.MapEditor.Maps[1].Height));
        Assert.Equal("dungeon_0001", reopened.MapEditor.Maps[1][1, 2]);
        Assert.False(reopened.Editor.HasUnsavedChanges);
        Assert.Equal(2, ProjectFile.Load(path).Maps.Count);
    }

    [AvaloniaFact]
    public void ClosingAndStartingANewProjectDropsTheMaps()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("level1", 10, 8, "forest");

        viewModel.NewProject();

        Assert.Empty(viewModel.MapEditor.Maps);
        Assert.Null(viewModel.MapEditor.SelectedMap);
    }

    [AvaloniaFact]
    public void RenamingATilesetUpdatesTheMapsUsingIt()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("a", 2, 2, "forest");
        viewModel.MapEditor.CreateMap("b", 2, 2, "dungeon");
        viewModel.MapEditor.Maps[0][0, 0] = "forest_0001";

        viewModel.RenameSelectedTileset("woods");

        Assert.Equal(["woods", "dungeon"], viewModel.MapEditor.Maps.Select(map => map.TilesetName));
        Assert.Equal("forest_0001", viewModel.MapEditor.Maps[0][0, 0]);
    }

    [AvaloniaFact]
    public void RemovingATilesetDeletesItsMapsAndKeepsTheOthers()
    {
        var viewModel = TwoTilesets();
        viewModel.MapEditor.CreateMap("a", 2, 2, "forest");
        viewModel.MapEditor.CreateMap("b", 2, 2, "dungeon");
        viewModel.MapEditor.CreateMap("c", 2, 2, "forest");
        Assert.Equal(2, viewModel.MapEditor.CountMapsUsing("forest"));
        viewModel.Editor.HasUnsavedChanges = false;

        viewModel.RemoveSelectedTileset();

        Assert.Equal(["b"], viewModel.MapEditor.Maps.Select(map => map.Name));
        Assert.Equal("b", viewModel.MapEditor.SelectedMap?.Name);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
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
