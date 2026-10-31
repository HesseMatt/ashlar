using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core.Serialization;

namespace Ashlar.App.Tests;

public class MultipleTilesetsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void ImportingAddsTilesetsNamedAfterTheirFilesAndSelectsTheLatest()
    {
        var viewModel = TwoTilesets();

        Assert.Equal(["dungeon", "forest"], viewModel.TilesetNames);
        Assert.Equal("forest", viewModel.SelectedTilesetName);
        Assert.Equal(64, viewModel.Editor().ImageWidth);
        Assert.StartsWith("forest_", viewModel.Editor().Tiles[0].Name);
    }

    [AvaloniaFact]
    public void ImportingTheSameFileNameTwiceKeepsNamesUnique()
    {
        var viewModel = new MainViewModel();
        var path = WriteImage("dungeon", 64, 64);

        viewModel.LoadTileset(path, 16, 16);
        viewModel.LoadTileset(path, 16, 16);

        Assert.Equal(["dungeon", "dungeon 2"], viewModel.TilesetNames);
    }

    [AvaloniaFact]
    public void ATilesetKeepsItsEditsWhileItsTabIsClosed()
    {
        var viewModel = TwoTilesets();
        viewModel.OpenTileset("dungeon");
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        var tileCount = viewModel.Editor().Tiles.Count;

        viewModel.CloseTab(viewModel.ActiveTab!);
        viewModel.OpenTileset("forest");
        Assert.Equal(64, viewModel.Editor().ImageWidth);
        Assert.Equal(16, viewModel.Editor().Tiles.Count);

        viewModel.OpenTileset("dungeon");
        Assert.Equal(32, viewModel.Editor().ImageWidth);
        Assert.Equal(tileCount, viewModel.Editor().Tiles.Count);
        Assert.Equal(3, tileCount);
    }

    [AvaloniaFact]
    public void ClosingATabResetsItsUndoHistoryButKeepsUnsavedChanges()
    {
        var viewModel = TwoTilesets();
        viewModel.OpenTileset("dungeon");
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        Assert.True(viewModel.Editor().CanUndo);

        viewModel.CloseTab(viewModel.ActiveTab!);
        viewModel.OpenTileset("dungeon");

        Assert.False(viewModel.Editor().CanUndo);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void RenamingChangesTheNameButNotTheTileNames()
    {
        var viewModel = TwoTilesets();
        var firstTileName = viewModel.Editor().Tiles[0].Name;

        var renamed = viewModel.RenameTileset("forest", "  woods ");

        Assert.True(renamed);
        Assert.Equal(["dungeon", "woods"], viewModel.TilesetNames);
        Assert.Equal("woods", viewModel.SelectedTilesetName);
        Assert.Equal("woods", viewModel.ActiveTab!.Title);
        Assert.Equal(firstTileName, viewModel.Editor().Tiles[0].Name);
    }

    [AvaloniaFact]
    public void RenamingToAnExistingNameIsRefusedWithAMessage()
    {
        var viewModel = TwoTilesets();

        var renamed = viewModel.RenameTileset("forest", "dungeon");

        Assert.False(renamed);
        Assert.Equal(["dungeon", "forest"], viewModel.TilesetNames);
        Assert.Contains("already exists", viewModel.Editor().StatusMessage);
    }

    [AvaloniaFact]
    public void RenamingToAnEmptyNameIsRefused()
    {
        var viewModel = TwoTilesets();

        Assert.False(viewModel.RenameTileset("forest", "   "));
        Assert.Equal("forest", viewModel.SelectedTilesetName);
    }

    [AvaloniaFact]
    public void RemovingClosesItsTabAndRemovingTheLastLeavesAnEmptyProject()
    {
        var viewModel = TwoTilesets();

        viewModel.RemoveTileset("forest");
        Assert.Equal(["dungeon"], viewModel.TilesetNames);
        Assert.Equal(["dungeon"], viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.Equal(32, viewModel.Editor().ImageWidth);

        viewModel.RemoveTileset("dungeon");
        Assert.Empty(viewModel.TilesetNames);
        Assert.Empty(viewModel.OpenTabs);
        Assert.True(viewModel.IsProjectEmpty);
    }

    [AvaloniaFact]
    public void ExportsActOnTheActiveTileset()
    {
        var viewModel = TwoTilesets();
        viewModel.OpenTileset("dungeon");
        var xmlPath = Path.Combine(_directory, "out.xml");
        var pngPath = Path.Combine(_directory, "out.png");

        viewModel.ExportXml(xmlPath);
        viewModel.SaveImage(pngPath);

        var xml = File.ReadAllText(xmlPath);
        Assert.Contains("dungeon_0001", xml);
        Assert.DoesNotContain("forest_", xml);
        Assert.Equal(4, xml.Split("<Region ").Length - 1);
        using var saved = SKBitmap.Decode(pngPath);
        Assert.Equal(32, saved.Width);
    }

    [AvaloniaFact]
    public void SavingAndReopeningRestoresAllTilesetsWithTheirEditsAndOpensNoTabs()
    {
        var path = Path.Combine(_directory, "p.ashlar");
        var viewModel = TwoTilesets();
        viewModel.OpenTileset("dungeon");
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.SaveProject(path);

        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        Assert.Equal(["dungeon", "forest"], reopened.TilesetNames);
        Assert.Empty(reopened.OpenTabs);
        reopened.OpenTileset("dungeon");
        Assert.Equal(3, reopened.Editor().Tiles.Count);
        reopened.OpenTileset("forest");
        Assert.Equal(16, reopened.Editor().Tiles.Count);
        Assert.Equal(2, ProjectFile.Load(path).Tilesets.Count);
    }

    [AvaloniaFact]
    public void TheExplorerListsTheTilesetsAndSelectingOneDoesNotOpenIt()
    {
        var viewModel = TwoTilesets();
        viewModel.CloseTab(viewModel.ActiveTab!);
        viewModel.CloseTab(viewModel.ActiveTab!);
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var tree = window.GetLogicalDescendants().OfType<TreeView>().Single(t => t.Name == "ExplorerTree");
        Assert.Equal(["Tilesets", "Maps"], viewModel.ExplorerRoots.Select(node => node.Name));
        Assert.Equal(["dungeon", "forest"], viewModel.TilesetsGroup.Children.Select(node => node.Name));

        tree.SelectedItem = viewModel.TilesetsGroup.Children[0];

        Assert.Equal("dungeon", viewModel.SelectedTilesetName);
        Assert.Empty(viewModel.OpenTabs);
        window.Close();
    }

    [AvaloniaFact]
    public void TheExplorerIsHiddenWithoutAnOpenProject()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var panel = window.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "ExplorerPanel");
        Assert.False(panel.IsVisible);

        viewModel.NewProject();

        Assert.True(panel.IsVisible);
        window.Close();
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
