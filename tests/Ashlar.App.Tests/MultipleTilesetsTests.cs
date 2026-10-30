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

    [Fact]
    public void ImportingAddsTilesetsNamedAfterTheirFilesAndSelectsTheLatest()
    {
        var viewModel = TwoTilesets();

        Assert.Equal(["dungeon", "forest"], viewModel.TilesetNames);
        Assert.Equal("forest", viewModel.SelectedTilesetName);
        Assert.Equal(64, viewModel.Editor.ImageWidth);
        Assert.StartsWith("forest_", viewModel.Editor.Tiles[0].Name);
    }

    [Fact]
    public void ImportingTheSameFileNameTwiceKeepsNamesUnique()
    {
        var viewModel = new MainViewModel();
        var path = WriteImage("dungeon", 64, 64);

        viewModel.LoadTileset(path, 16, 16);
        viewModel.LoadTileset(path, 16, 16);

        Assert.Equal(["dungeon", "dungeon 2"], viewModel.TilesetNames);
    }

    [Fact]
    public void SelectingATilesetShowsItAndKeepsItsEditsWhileAwayFromIt()
    {
        var viewModel = TwoTilesets();
        viewModel.SelectTileset("dungeon");
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();
        var tileCount = viewModel.Editor.Tiles.Count;

        viewModel.SelectTileset("forest");
        Assert.Equal(64, viewModel.Editor.ImageWidth);
        Assert.Equal(16, viewModel.Editor.Tiles.Count);

        viewModel.SelectTileset("dungeon");
        Assert.Equal(32, viewModel.Editor.ImageWidth);
        Assert.Equal(tileCount, viewModel.Editor.Tiles.Count);
        Assert.Equal(3, tileCount);
    }

    [Fact]
    public void SelectingATilesetResetsTheUndoHistoryButKeepsUnsavedChanges()
    {
        var viewModel = TwoTilesets();
        viewModel.SelectTileset("dungeon");
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.SelectTileset("forest");

        Assert.False(viewModel.Editor.CanUndo);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void RenamingChangesTheNameButNotTheTileNames()
    {
        var viewModel = TwoTilesets();
        var firstTileName = viewModel.Editor.Tiles[0].Name;

        var renamed = viewModel.RenameSelectedTileset("  woods ");

        Assert.True(renamed);
        Assert.Equal(["dungeon", "woods"], viewModel.TilesetNames);
        Assert.Equal("woods", viewModel.SelectedTilesetName);
        Assert.Equal(firstTileName, viewModel.Editor.Tiles[0].Name);
    }

    [Fact]
    public void RenamingToAnExistingNameIsRefusedWithAMessage()
    {
        var viewModel = TwoTilesets();

        var renamed = viewModel.RenameSelectedTileset("dungeon");

        Assert.False(renamed);
        Assert.Equal(["dungeon", "forest"], viewModel.TilesetNames);
        Assert.Contains("already exists", viewModel.Editor.StatusMessage);
    }

    [Fact]
    public void RenamingToAnEmptyNameIsRefused()
    {
        var viewModel = TwoTilesets();

        Assert.False(viewModel.RenameSelectedTileset("   "));
        Assert.Equal("forest", viewModel.SelectedTilesetName);
    }

    [Fact]
    public void RemovingSelectsANeighbourAndRemovingTheLastLeavesAnEmptyProject()
    {
        var viewModel = TwoTilesets();

        viewModel.RemoveSelectedTileset();
        Assert.Equal(["dungeon"], viewModel.TilesetNames);
        Assert.Equal("dungeon", viewModel.SelectedTilesetName);
        Assert.Equal(32, viewModel.Editor.ImageWidth);

        viewModel.RemoveSelectedTileset();
        Assert.Empty(viewModel.TilesetNames);
        Assert.True(viewModel.IsProjectEmpty);
        Assert.Null(viewModel.SelectedTilesetName);
    }

    [Fact]
    public void ExportsActOnTheSelectedTileset()
    {
        var viewModel = TwoTilesets();
        viewModel.SelectTileset("dungeon");
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

    [Fact]
    public void SavingAndReopeningRestoresAllTilesetsWithTheirEdits()
    {
        var path = Path.Combine(_directory, "p.ashlar");
        var viewModel = TwoTilesets();
        viewModel.SelectTileset("dungeon");
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();
        viewModel.SaveProject(path);

        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        Assert.Equal(["dungeon", "forest"], reopened.TilesetNames);
        Assert.Equal("dungeon", reopened.SelectedTilesetName);
        Assert.Equal(3, reopened.Editor.Tiles.Count);
        reopened.SelectTileset("forest");
        Assert.Equal(16, reopened.Editor.Tiles.Count);
        Assert.Equal(2, ProjectFile.Load(path).Tilesets.Count);
    }

    [AvaloniaFact]
    public void SelectingInTheListSwitchesTheEditedTileset()
    {
        var viewModel = TwoTilesets();
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var list = window.GetLogicalDescendants().OfType<ListBox>().Single(l => l.Name == "TilesetList");
        Assert.Equal(["dungeon", "forest"], list.Items.Cast<string>());
        Assert.Equal("forest", list.SelectedItem);

        list.SelectedItem = "dungeon";

        Assert.Equal("dungeon", viewModel.SelectedTilesetName);
        Assert.Equal(32, viewModel.Editor.ImageWidth);
        window.Close();
    }

    [AvaloniaFact]
    public void TheListIsHiddenWithoutAnOpenProject()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var panel = window.GetLogicalDescendants().OfType<Border>().Single(b => b.Name == "TilesetListPanel");
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
