using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class EditorTabsTests : IDisposable
{
    private readonly string _imagePath = CreateSampleImage();
    private readonly MainViewModel _viewModel = new();
    private readonly MainWindow _window;

    public EditorTabsTests()
    {
        _window = new MainWindow { DataContext = _viewModel, Width = 800, Height = 600 };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        File.Delete(_imagePath);
    }

    private TabControl Tabs => _window.FindControl<TabControl>("EditorTabs")!;

    private bool IsTilesetToolBarShown => _window.GetVisualDescendants().OfType<TilesetToolBar>().Any();

    [AvaloniaFact]
    public void HasTilesetsAndMapsTabsWithTilesetsSelected()
    {
        var headers = Tabs.Items.OfType<TabItem>().Select(tab => tab.Header).ToArray();

        Assert.Equal(["Tilesets", "Maps"], headers);
        Assert.Equal(MainViewModel.TilesetsTabIndex, Tabs.SelectedIndex);
        Assert.True(IsTilesetToolBarShown);
    }

    [AvaloniaFact]
    public void ToolBarFollowsTheActiveTab()
    {
        _viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;

        Assert.Equal(MainViewModel.MapsTabIndex, Tabs.SelectedIndex);
        Assert.Same(_viewModel.MapEditor, _viewModel.ActiveToolBar);
        Assert.False(IsTilesetToolBarShown);
        Assert.Null(_window.FindEditorControl<ToggleButton>("DrawButton"));
        Assert.NotNull(_window.GetVisualDescendants().OfType<MapEditorView>().FirstOrDefault());

        Tabs.SelectedIndex = MainViewModel.TilesetsTabIndex;

        Assert.Same(_viewModel.Editor, _viewModel.ActiveToolBar);
        Assert.True(IsTilesetToolBarShown);
        Assert.NotNull(_window.FindEditorControl<ToggleButton>("DrawButton"));
    }

    [AvaloniaFact]
    public void SwitchingTabsKeepsTilesetEditorState()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        var editor = _viewModel.Editor;
        editor.HandleCanvasClick(5, 5, addToSelection: false);
        editor.DeleteSelectedTiles();
        editor.HandleCanvasClick(20, 5, addToSelection: false);
        editor.ZoomLevel = 3;
        editor.IsDrawToolActive = true;
        var selection = editor.SelectedTiles.ToArray();
        Assert.True(editor.CanUndo);

        _viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;
        _viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;

        Assert.Same(editor, _viewModel.Editor);
        Assert.Equal(3, editor.ZoomLevel);
        Assert.True(editor.IsDrawToolActive);
        Assert.Equal(selection, editor.SelectedTiles);
        Assert.True(editor.CanUndo);
        Assert.True(_window.FindEditorControl<ToggleButton>("DrawButton")!.IsChecked);
    }

    [AvaloniaFact]
    public void TilesetShortcutsAreIgnoredOnTheMapsTab()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _viewModel.Editor.HandleCanvasClick(5, 5, addToSelection: false);
        _viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;

        _window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        Assert.True(_viewModel.Editor.HasSelectedTiles);
    }

    [AvaloniaFact]
    public void EditAndViewMenuItemsAreEnabledOnlyOnTheTilesetsTab()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _viewModel.Editor.HandleCanvasClick(5, 5, addToSelection: false);
        _viewModel.Editor.DeleteSelectedTiles();
        _viewModel.Editor.HandleCanvasClick(20, 5, addToSelection: false);
        var headers = new[]
        {
            "Undo", "Change tile size...", "Delete", "Rectangle selection", "Move", "Draw", "Bucket", "Pipette",
            "Zoom In", "Zoom Out", "Reset Zoom",
        };
        var items = _window.GetLogicalDescendants().OfType<MenuItem>()
            .Where(m => m.Header is string header && headers.Contains(header))
            .DistinctBy(m => m.Header)
            .ToList();
        Assert.Equal(headers.Length, items.Count);
        Assert.All(items, item => Assert.True(item.IsEnabled, (string)item.Header!));

        _viewModel.SelectedTabIndex = MainViewModel.MapsTabIndex;

        Assert.All(items, item => Assert.False(item.IsEnabled, (string)item.Header!));

        _viewModel.SelectedTabIndex = MainViewModel.TilesetsTabIndex;

        Assert.All(items, item => Assert.True(item.IsEnabled, (string)item.Header!));
    }

    private static string CreateSampleImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ashlar-tabs-{Guid.NewGuid():N}.png");
        using var bitmap = new SKBitmap(32, 32);
        bitmap.Erase(SKColors.Red);
        using var stream = File.OpenWrite(path);
        bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
        return path;
    }
}
