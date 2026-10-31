using Avalonia;
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
        var map = viewModel.MapEditor().Map;
        map[0, 0] = "forest_0001";
        map[2, 1] = "forest_0006";

        viewModel.MapEditor().RefreshTileset();

        var regions = viewModel.MapEditor().CellRegions;
        Assert.Equal(6, regions.Length);
        Assert.Equal(("forest_0001", 0, 0), (regions[0]!.Name, regions[0]!.X, regions[0]!.Y));
        Assert.Equal(("forest_0006", 16, 16), (regions[5]!.Name, regions[5]!.X, regions[5]!.Y));
        Assert.Equal(4, regions.Count(region => region is null));
    }

    [AvaloniaFact]
    public void CellsWhoseTileWasDeletedFromTheTilesetShowAsEmpty()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 1);
        var mapEditor = viewModel.MapEditor();
        mapEditor.Map[0, 0] = "forest_0001";
        mapEditor.Map[1, 0] = "forest_0002";
        viewModel.OpenTileset("forest");
        viewModel.Editor().HandleCanvasClick(5, 5, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        var regions = mapEditor.CellRegions;
        Assert.Null(regions[0]);
        Assert.Equal("forest_0002", regions[1]!.Name);
    }

    [AvaloniaFact]
    public void EachMapTabHasThePaletteOfItsTileset()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        viewModel.CreateMap("cave", 2, 2, "dungeon");

        Assert.Equal(4, viewModel.MapEditor().PaletteTiles.Count);
        Assert.All(viewModel.MapEditor().PaletteTiles, tile => Assert.StartsWith("dungeon_", tile.Name));

        viewModel.OpenMap("level1");

        Assert.Equal(16, viewModel.MapEditor().PaletteTiles.Count);
        Assert.All(viewModel.MapEditor().PaletteTiles, tile => Assert.StartsWith("forest_", tile.Name));
    }

    [AvaloniaFact]
    public void ThePaletteTracksASingleSelectedTile()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var editor = viewModel.MapEditor();
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
        var palette = MapView(window).FindControl<ListBox>("PaletteList")!;

        Assert.Equal(16, palette.ItemCount);
        palette.SelectedIndex = 2;

        Assert.Equal("forest_0003", viewModel.MapEditor().SelectedPaletteTile!.Name);
        window.Close();
    }

    [AvaloniaFact]
    public void TheCanvasIsAsLargeAsTheMapInPixelsTimesZoom()
    {
        var viewModel = ProjectWithForestMap(width: 5, height: 3);
        var window = OpenMapsTab(viewModel);
        var content = MapView(window).FindControl<Panel>("MapCanvasContent")!;

        Assert.Equal((80, 48), (content.Bounds.Width, content.Bounds.Height));

        viewModel.MapEditor().ZoomLevel = 2;

        Assert.Equal((160, 96), (viewModel.MapEditor().ScaledMapWidth, viewModel.MapEditor().ScaledMapHeight));
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
        Assert.Equal(1600, viewModel.MapEditor().MapPixelWidth);
        // Only the visible cells are drawn.
        var visible = MapView(window).FindControl<TileMapCanvas>("MapCells")!.VisibleArea;
        Assert.NotNull(visible);
        Assert.True(visible!.Value.Width < 1600);
        window.Close();
    }

    [AvaloniaFact]
    public void TheVisibleAreaOfANewLargeMapMatchesTheViewport()
    {
        var viewModel = ProjectWithForestMap(width: 50, height: 50);
        var window = OpenMapsTab(viewModel);
        viewModel.CreateMap("big", 50, 50, "forest");
        Dispatcher.UIThread.RunJobs();

        AssertVisibleAreaMatchesViewport(window);
        window.Close();
    }

    [AvaloniaFact]
    public void TheVisibleAreaStaysCorrectAfterSwitchingMapsTabsZoomingResizingAndPanning()
    {
        var viewModel = ProjectWithForestMap(width: 50, height: 50);
        viewModel.CreateMap("other", 80, 80, "forest");
        var window = OpenMapsTab(viewModel);
        Dispatcher.UIThread.RunJobs();
        var scroll = MapView(window).FindControl<ScrollViewer>("MapCanvasScrollViewer")!;

        viewModel.OpenMap("level1");
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);

        viewModel.OpenTileset("forest");
        Dispatcher.UIThread.RunJobs();
        viewModel.OpenMap("level1");
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);

        viewModel.MapEditor().ZoomIn();
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);

        viewModel.MapEditor().ZoomOut();
        viewModel.MapEditor().ZoomOut();
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);

        viewModel.ResizeMap("level1", 70, 60);
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);

        scroll.Offset = new Vector(scroll.Offset.X + 300, scroll.Offset.Y + 200);
        Dispatcher.UIThread.RunJobs();
        AssertVisibleAreaMatchesViewport(window);
        window.Close();
    }

    [AvaloniaFact]
    public void TheVisibleAreaOfTheTilesetCanvasMatchesTheViewport()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("huge", 1600, 1600), 16, 16);
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var view = window.GetVisualDescendants().OfType<TilesetEditorView>().Single();
        var scroll = view.FindControl<ScrollViewer>("TileCanvasScrollViewer")!;
        var content = view.FindControl<Control>("TileCanvasContent")!;
        var expected = ViewportInContent(scroll, content);
        Assert.Equal(expected, view.FindControl<TileGridOverlay>("TileOverlay")!.VisibleArea);
        window.Close();
    }

    private static void AssertVisibleAreaMatchesViewport(Window window)
    {
        var view = MapView(window);
        var scroll = view.FindControl<ScrollViewer>("MapCanvasScrollViewer")!;
        var content = view.FindControl<Control>("MapCanvasContent")!;
        var visible = view.FindControl<TileMapCanvas>("MapCells")!.VisibleArea;
        Assert.Equal(ViewportInContent(scroll, content), visible);

        var center = scroll.TranslatePoint(new Point(scroll.Viewport.Width / 2, scroll.Viewport.Height / 2), content)!.Value;
        Assert.True(visible!.Value.Contains(center));
    }

    private static Rect? ViewportInContent(ScrollViewer scroll, Control content)
    {
        var topLeft = scroll.TranslatePoint(new Point(0, 0), content);
        var bottomRight = scroll.TranslatePoint(new Point(scroll.Viewport.Width, scroll.Viewport.Height), content);
        return new Rect(topLeft!.Value, bottomRight!.Value);
    }

    [AvaloniaFact]
    public void AMapTabOpenedWhileAnotherTabIsShownStartsAtTheTopLeftOnceShown()
    {
        var viewModel = ProjectWithForestMap(width: 200, height: 200);
        viewModel.CreateMap("small", 2, 2, "forest");
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.OpenMap("level1");
        Dispatcher.UIThread.RunJobs();

        var view = MapView(window);
        var scroll = view.FindControl<ScrollViewer>("MapCanvasScrollViewer")!;
        var padding = view.FindControl<Border>("MapCanvasPadding")!.Padding;
        Assert.True(padding.Left > 0);
        Assert.Equal((padding.Left, padding.Top), (scroll.Offset.X, scroll.Offset.Y));
        window.Close();
    }

    [AvaloniaFact]
    public void APannedMapKeepsItsOffsetWhileAnotherTabIsShown()
    {
        var viewModel = ProjectWithForestMap(width: 200, height: 200);
        var window = OpenMapsTab(viewModel);
        Dispatcher.UIThread.RunJobs();
        var scroll = MapView(window).FindControl<ScrollViewer>("MapCanvasScrollViewer")!;
        scroll.Offset = new Vector(scroll.Offset.X + 500, scroll.Offset.Y + 400);
        Dispatcher.UIThread.RunJobs();
        var panned = scroll.Offset;

        viewModel.OpenTileset("forest");
        Dispatcher.UIThread.RunJobs();
        viewModel.OpenMap("level1");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(panned, MapView(window).FindControl<ScrollViewer>("MapCanvasScrollViewer")!.Offset);
        window.Close();
    }

    [AvaloniaFact]
    public void RenamingTheSelectedMapKeepsThePaletteBrush()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var window = OpenMapsTab(viewModel);
        Dispatcher.UIThread.RunJobs();
        viewModel.MapEditor().SelectedPaletteTile = viewModel.MapEditor().PaletteTiles[5];

        viewModel.RenameMap("level1", "renamed");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("renamed", viewModel.MapEditor().Map.Name);
        Assert.Equal("forest_0006", viewModel.MapEditor().SelectedPaletteTile?.Name);
        window.Close();
    }

    [AvaloniaFact]
    public void ZoomingStaysWithinTheLimitsOfTheTilesetEditor()
    {
        var viewModel = ProjectWithForestMap(width: 2, height: 2);
        var editor = viewModel.MapEditor();

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

        viewModel.RenameTileset("forest", "woods");

        Assert.Equal("woods", viewModel.MapEditor().Map.TilesetName);
        Assert.Equal(16, viewModel.MapEditor().PaletteTiles.Count);
        Assert.Equal(4, viewModel.MapEditor().CellRegions.Length);
    }

    private static MapEditorView MapView(Window window) => window.GetVisualDescendants().OfType<MapEditorView>().Single(view => view.IsEffectivelyVisible);

    private static Window OpenMapsTab(MainViewModel viewModel)
    {
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        return window;
    }

    private MainViewModel ProjectWithForestMap(int width, int height)
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("dungeon", 32, 32), 16, 16);
        viewModel.LoadTileset(WriteImage("forest", 64, 64), 16, 16);
        viewModel.CreateMap("level1", width, height, "forest");
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
