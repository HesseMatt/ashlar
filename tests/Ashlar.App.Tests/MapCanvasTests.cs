using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.Controls;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class MapCanvasTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void CellsAreResolvedToTheirTilesRegionsAndEmptyCellsHaveNone()
    {
        var viewModel = ProjectWithForestMap(width: 3, height: 2);
        var map = viewModel.MapEditor.SelectedMap!;
        map[0, 0] = "forest_0001";
        map[2, 1] = "forest_0006";

        viewModel.MapEditor.RefreshTileset();

        var regions = viewModel.MapEditor.CellRegions;
        Assert.Equal(6, regions.Length);
        Assert.Equal(("forest_0001", 0, 0), (regions[0]!.Name, regions[0]!.X, regions[0]!.Y));
        Assert.Equal(("forest_0006", 16, 16), (regions[5]!.Name, regions[5]!.X, regions[5]!.Y));
        Assert.Equal(4, regions.Count(region => region is null));
    }

    [AvaloniaFact]
    public void CellsWhoseTileWasDeletedFromTheTilesetShowAsEmpty()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 1);
        viewModel.MapEditor.SelectedMap![0, 0] = "forest_0001";
        viewModel.MapEditor.SelectedMap![1, 0] = "forest_0002";
        viewModel.Editor.HandleCanvasClick(5, 5, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;

        var regions = viewModel.MapEditor.CellRegions;
        Assert.Null(regions[0]);
        Assert.Equal("forest_0002", regions[1]!.Name);
    }

    [AvaloniaFact]
    public void ThePaletteListsTheTilesOfTheMapsTilesetAndFollowsTheSelectedMap()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        viewModel.MapEditor.CreateMap("cave", 2, 2, "dungeon");

        Assert.Equal(4, viewModel.MapEditor.PaletteTiles.Count);
        Assert.All(viewModel.MapEditor.PaletteTiles, tile => Assert.StartsWith("dungeon_", tile.Name));

        viewModel.MapEditor.SelectedMap = viewModel.MapEditor.Maps[0];

        Assert.Equal(16, viewModel.MapEditor.PaletteTiles.Count);
        Assert.All(viewModel.MapEditor.PaletteTiles, tile => Assert.StartsWith("forest_", tile.Name));
    }

    [AvaloniaFact]
    public void ThePaletteTracksASingleSelectedTile()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var editor = viewModel.MapEditor;
        Assert.Null(editor.SelectedPaletteTile);

        editor.SelectedPaletteTile = editor.PaletteTiles[3];
        editor.SelectedPaletteTile = editor.PaletteTiles[5];

        Assert.Equal("forest_0006", editor.SelectedPaletteTile!.Name);

        editor.RefreshTileset();

        Assert.Equal("forest_0006", editor.SelectedPaletteTile!.Name);
    }

    [AvaloniaFact]
    public void SelectingAPaletteItemInTheViewSetsTheSelectedTile()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var window = OpenMapsTab(viewModel);
        var palette = window.GetVisualDescendants().OfType<MapEditorView>().Single().FindControl<ListBox>("PaletteList")!;

        Assert.Equal(16, palette.ItemCount);
        palette.SelectedIndex = 2;

        Assert.Equal("forest_0003", viewModel.MapEditor.SelectedPaletteTile!.Name);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCanvasIsAsLargeAsTheMapInPixelsTimesZoom()
    {
        var viewModel = ProjectWithForestMap(width: 5, height: 3);
        var window = OpenMapsTab(viewModel);
        var content = MapView(window).FindControl<Panel>("MapCanvasContent")!;

        Assert.Equal((80, 48), (content.Bounds.Width, content.Bounds.Height));

        viewModel.MapEditor.ZoomLevel = 2;

        Assert.Equal((160, 96), (viewModel.MapEditor.ScaledMapWidth, viewModel.MapEditor.ScaledMapHeight));
        Assert.Equal((80, 48), (content.Bounds.Width, content.Bounds.Height));
        window.Close();
    }

    [AvaloniaFact]
    public void AMapLargerThanTheViewportCanBeScrolled()
    {
        var viewModel = ProjectWithForestMap(width: 100, height: 100);
        var window = OpenMapsTab(viewModel);
        var scroll = MapView(window).FindControl<ScrollViewer>("MapCanvasScrollViewer")!;

        Assert.True(scroll.Extent.Width > scroll.Viewport.Width);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        Assert.Equal(1600, viewModel.MapEditor.MapPixelWidth);
        // Only the visible cells are drawn.
        var visible = MapView(window).FindControl<TileMapCanvas>("MapCells")!.VisibleArea;
        Assert.NotNull(visible);
        Assert.True(visible!.Value.Width < 1600);
        window.Close();
    }

    [AvaloniaFact]
    public void AMapSelectedWhileTheMapsTabIsHiddenStartsAtTheTopLeftOnceShown()
    {
        var viewModel = ProjectWithForestMap(width: 200, height: 200);
        viewModel.MapEditor.CreateMap("small", 2, 2, "forest");
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        viewModel.MapEditor.SelectedMap = viewModel.MapEditor.Maps.Single(map => map.Name == "level1");
        Dispatcher.UIThread.RunJobs();

        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        Dispatcher.UIThread.RunJobs();

        var view = MapView(window);
        var scroll = view.FindControl<ScrollViewer>("MapCanvasScrollViewer")!;
        var padding = view.FindControl<Border>("MapCanvasPadding")!.Padding;
        Assert.True(padding.Left > 0);
        Assert.Equal((padding.Left, padding.Top), (scroll.Offset.X, scroll.Offset.Y));
        window.Close();
    }

    [AvaloniaFact]
    public void AMapSelectedWhileAwayStartsAtTheTopLeftWhenTheMapsTabIsShownAgain()
    {
        var viewModel = ProjectWithForestMap(width: 200, height: 200);
        viewModel.MapEditor.CreateMap("small", 2, 2, "forest");
        var window = OpenMapsTab(viewModel);
        Dispatcher.UIThread.RunJobs();

        viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
        viewModel.MapEditor.SelectedMap = viewModel.MapEditor.Maps.Single(map => map.Name == "level1");
        Dispatcher.UIThread.RunJobs();
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        Dispatcher.UIThread.RunJobs();

        var view = MapView(window);
        var scroll = view.FindControl<ScrollViewer>("MapCanvasScrollViewer")!;
        var padding = view.FindControl<Border>("MapCanvasPadding")!.Padding;
        Assert.True(padding.Left > 0);
        Assert.Equal((padding.Left, padding.Top), (scroll.Offset.X, scroll.Offset.Y));
        window.Close();
    }

    [AvaloniaFact]
    public void VisitingTheMapsTabSeveralTimesLeavesOneHandlerOnTheViewModel()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var window = OpenMapsTab(viewModel);
        for (var visit = 0; visit < 3; visit++)
        {
            viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;
            Dispatcher.UIThread.RunJobs();
            viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
            Dispatcher.UIThread.RunJobs();
        }

        var field = typeof(MapEditorViewModel).GetField(nameof(MapEditorViewModel.MapShown),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var handlers = ((Delegate?)field.GetValue(viewModel.MapEditor))!.GetInvocationList();
        Assert.Single(handlers);
        window.Close();
    }

    [AvaloniaFact]
    public void RenamingTheSelectedMapKeepsThePaletteBrush()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var window = OpenMapsTab(viewModel);
        Dispatcher.UIThread.RunJobs();
        viewModel.MapEditor.SelectedPaletteTile = viewModel.MapEditor.PaletteTiles[5];

        viewModel.MapEditor.RenameSelectedMap("renamed");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("renamed", viewModel.MapEditor.SelectedMap!.Name);
        Assert.Equal("forest_0006", viewModel.MapEditor.SelectedPaletteTile?.Name);
        window.Close();
    }

    [AvaloniaFact]
    public void ZoomingStaysWithinTheLimitsOfTheTilesetEditor()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var editor = viewModel.MapEditor;

        for (var i = 0; i < 500; i++)
            editor.ZoomIn();
        Assert.Equal(TilesetEditorViewModel.MaxZoom, editor.ZoomLevel);

        for (var i = 0; i < 500; i++)
            editor.ZoomOut();
        Assert.Equal(TilesetEditorViewModel.MinZoom, editor.ZoomLevel);

        editor.ResetZoom();
        Assert.Equal(1.0, editor.ZoomLevel);
    }

    [AvaloniaFact]
    public void RenamingTheTilesetKeepsTheMapShown()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);

        viewModel.RenameSelectedTileset("woods");

        Assert.Equal("woods", viewModel.MapEditor.SelectedMap!.TilesetName);
        Assert.Equal(16, viewModel.MapEditor.PaletteTiles.Count);
        Assert.Equal(4, viewModel.MapEditor.CellRegions.Length);
    }

    private static MapEditorView MapView(Window window) => window.GetVisualDescendants().OfType<MapEditorView>().Single();

    private static Window OpenMapsTab(MainViewModel viewModel)
    {
        viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        return window;
    }

    private MainViewModel ProjectWithForestMap(int width, int height)
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);
        viewModel.LoadTileset(WriteImage("forest", 64, 64), 16, 16);
        viewModel.MapEditor.CreateMap("level1", width, height, "forest");
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
