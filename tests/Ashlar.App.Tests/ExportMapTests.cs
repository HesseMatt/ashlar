using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core;
using Ashlar.Core.Model;

namespace Ashlar.App.Tests;

public class ExportMapTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void DialogOffersBothFilesAndNeedsAtLeastOne()
    {
        var dialog = new ExportMapDialog(ProjectWithMap().CreateExportMapDialogViewModel()!);
        dialog.Show();
        var viewModel = (ExportMapDialogViewModel)dialog.DataContext!;
        var ok = dialog.FindControl<Button>("OkButton")!;

        Assert.True(dialog.FindControl<CheckBox>("ExportXmlBox")!.IsChecked);
        Assert.True(dialog.FindControl<CheckBox>("ExportImageBox")!.IsChecked);
        Assert.True(ok.IsEnabled);

        dialog.FindControl<CheckBox>("ExportXmlBox")!.IsChecked = false;
        Assert.True(ok.IsEnabled);

        dialog.FindControl<CheckBox>("ExportImageBox")!.IsChecked = false;
        Assert.False(ok.IsEnabled);
        Assert.False(viewModel.CanExport);
    }

    [AvaloniaFact]
    public void DialogPreviewShowsTheTilemapXml()
    {
        var dialog = new ExportMapDialog(ProjectWithMap().CreateExportMapDialogViewModel()!);
        dialog.Show();

        var preview = dialog.FindControl<TextBox>("PreviewBox")!.Text;

        Assert.Contains("<Tilemap>", preview);
        Assert.Contains("tileWidth=\"16\"", preview);
    }

    [AvaloniaFact]
    public async Task OkReturnsTheChoiceAndCancelReturnsNothing()
    {
        var owner = new Window();
        owner.Show();
        var main = ProjectWithMap();

        var okDialog = new ExportMapDialog(main.CreateExportMapDialogViewModel()!);
        var okResult = okDialog.ShowDialog<ExportMapDialogViewModel?>(owner);
        okDialog.FindControl<CheckBox>("ExportImageBox")!.IsChecked = false;
        okDialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var choice = await okResult;
        Assert.NotNull(choice);
        Assert.True(choice.ExportXml);
        Assert.False(choice.ExportImage);

        var cancelDialog = new ExportMapDialog(main.CreateExportMapDialogViewModel()!);
        var cancelResult = cancelDialog.ShowDialog<ExportMapDialogViewModel?>(owner);
        cancelDialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(await cancelResult);
    }

    [AvaloniaFact]
    public void ExportWritesXmlWithTheStoredTexturePathAndAnImageOfTheRightSize()
    {
        var main = ProjectWithMap();
        main.TexturePath = "atlas/dungeon";
        var xmlPath = Path.Combine(_directory, "map.xml");
        var imagePath = Path.Combine(_directory, "map.png");

        main.ExportMap(xmlPath, imagePath);

        var xml = File.ReadAllText(xmlPath);
        Assert.Contains(">atlas/dungeon</Tileset>", xml);
        Assert.Contains("        0 1\n        -1 -1\n", xml);
        using var image = EditableTilesetImage.Load(imagePath);
        Assert.Equal(32, image.Width);
        Assert.Equal(32, image.Height);
        Assert.True(image.IsRegionEmpty(0, 16, 32, 16));
        Assert.False(image.IsRegionEmpty(0, 0, 16, 16));
    }

    [AvaloniaFact]
    public void ExportCanWriteOnlyOneOfTheFiles()
    {
        var main = ProjectWithMap();
        var xmlPath = Path.Combine(_directory, "map.xml");

        main.ExportMap(xmlPath, null);

        Assert.Single(Directory.GetFiles(_directory, "map.*"));
    }

    [AvaloniaFact]
    public void ExportUsesTheLiveEditedTilesetImageAndTileList()
    {
        var main = ProjectWithMap();
        // Unsaved edit: the first tile is repainted blue and the second tile is deleted.
        main.Editor.Image!.SetPixel(0, 0, new PixelColor(0, 0, 255, 255));
        main.Editor.HandleCanvasClick(20, 5, addToSelection: false);
        main.Editor.DeleteSelectedTiles();
        var xmlPath = Path.Combine(_directory, "map.xml");
        var imagePath = Path.Combine(_directory, "map.png");

        main.ExportMap(xmlPath, imagePath);

        Assert.Contains("        0 -1\n", File.ReadAllText(xmlPath));
        using var image = EditableTilesetImage.Load(imagePath);
        Assert.True(image.TryGetPixel(0, 0, out var pixel));
        Assert.Equal(new PixelColor(0, 0, 255, 255), pixel);
        Assert.True(image.IsRegionEmpty(16, 0, 16, 16));
    }

    [AvaloniaFact]
    public void ExportMapMenuItemIsEnabledOnlyOnTheMapsTabWithASelectedMap_AndTilesetExportsOnlyOnTheTilesetsTab()
    {
        var main = new MainViewModel();
        var window = new MainWindow { DataContext = main, Width = 800, Height = 600 };
        window.Show();
        var items = window.GetLogicalDescendants().OfType<MenuItem>().ToList();
        MenuItem Item(string header) => items.First(m => m.Header as string == header);

        main.LoadTileset(WriteImage("dungeon"), 16, 16);
        Assert.False(Item("Export map...").IsEnabled);
        Assert.True(Item("Export XML...").IsEnabled);
        Assert.True(Item("Save image...").IsEnabled);

        main.SelectedTabIndex = MainViewModel.MapsTabIndex;
        Assert.False(Item("Export map...").IsEnabled);
        Assert.False(Item("Export XML...").IsEnabled);
        Assert.False(Item("Save image...").IsEnabled);

        main.MapEditor.CreateMap("m", 2, 2, "dungeon");
        Assert.True(Item("Export map...").IsEnabled);

        main.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
        Assert.False(Item("Export map...").IsEnabled);
        window.Close();
    }

    private MainViewModel ProjectWithMap()
    {
        var main = new MainViewModel();
        main.LoadTileset(WriteImage("dungeon"), 16, 16);
        main.MapEditor.CreateMap("m", 2, 2, "dungeon");
        var map = main.MapEditor.SelectedMap!;
        map[0, 0] = main.Editor.Tiles[0].Name;
        map[1, 0] = main.Editor.Tiles[1].Name;
        return main;
    }

    private string WriteImage(string name)
    {
        var path = Path.Combine(_directory, name + ".png");
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
