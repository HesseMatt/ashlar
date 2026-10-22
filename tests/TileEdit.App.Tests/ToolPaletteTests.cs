using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SkiaSharp;
using TileEdit.App.ViewModels;
using TileEdit.App.Views;

namespace TileEdit.App.Tests;

public class ToolPaletteTests : IDisposable
{
    private readonly string _imagePath = CreateSampleImage();
    private readonly MainViewModel _viewModel = new();
    private readonly MainWindow _window;

    public ToolPaletteTests()
    {
        _window = new MainWindow { DataContext = _viewModel, Width = 800, Height = 600 };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        File.Delete(_imagePath);
    }

    private ToggleButton RectangleSelectButton => _window.FindControl<ToggleButton>("RectangleSelectButton")!;

    private ToggleButton MoveButton => _window.FindControl<ToggleButton>("MoveButton")!;

    private ToggleButton DrawButton => _window.FindControl<ToggleButton>("DrawButton")!;

    private Button DeleteButton => _window.FindControl<Button>("DeleteButton")!;

    [AvaloniaFact]
    public void AllButtonsAreDisabledWithoutAnImage()
    {
        Assert.False(RectangleSelectButton.IsEnabled);
        Assert.False(MoveButton.IsEnabled);
        Assert.False(DrawButton.IsEnabled);
        Assert.False(DeleteButton.IsEnabled);
    }

    [AvaloniaFact]
    public void ToolTogglesAreEnabledOnceAnImageIsLoadedAndDeleteWaitsForASelection()
    {
        _viewModel.LoadTileset(_imagePath);

        Assert.True(RectangleSelectButton.IsEnabled);
        Assert.True(MoveButton.IsEnabled);
        Assert.True(DrawButton.IsEnabled);
        Assert.False(DeleteButton.IsEnabled);

        _viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        Assert.True(DeleteButton.IsEnabled);
    }

    [AvaloniaFact]
    public void TogglesFollowTheActiveToolAndAreMutuallyExclusive()
    {
        _viewModel.LoadTileset(_imagePath);

        _viewModel.IsRectangleToolActive = true;
        Assert.True(RectangleSelectButton.IsChecked);
        Assert.False(MoveButton.IsChecked);

        _viewModel.IsMoveToolActive = true;
        Assert.False(RectangleSelectButton.IsChecked);
        Assert.True(MoveButton.IsChecked);

        _viewModel.IsMoveToolActive = false;
        Assert.False(RectangleSelectButton.IsChecked);
        Assert.False(MoveButton.IsChecked);
        Assert.Equal(CanvasTool.Select, _viewModel.ActiveTool);
    }

    [AvaloniaFact]
    public void CheckingATogglePicksTheToolAndUncheckingReturnsToClickSelection()
    {
        _viewModel.LoadTileset(_imagePath);

        RectangleSelectButton.IsChecked = true;
        Assert.Equal(CanvasTool.Rectangle, _viewModel.ActiveTool);

        MoveButton.IsChecked = true;
        Assert.Equal(CanvasTool.Move, _viewModel.ActiveTool);
        Assert.False(RectangleSelectButton.IsChecked);

        MoveButton.IsChecked = false;
        Assert.Equal(CanvasTool.Select, _viewModel.ActiveTool);
    }

    [AvaloniaFact]
    public void DrawToggleActivatesTheDrawTool()
    {
        _viewModel.LoadTileset(_imagePath);

        DrawButton.IsChecked = true;
        Assert.Equal(CanvasTool.Draw, _viewModel.ActiveTool);

        _viewModel.IsMoveToolActive = true;
        Assert.False(DrawButton.IsChecked);

        _viewModel.IsDrawToolActive = true;
        _viewModel.IsDrawToolActive = false;
        Assert.Equal(CanvasTool.Select, _viewModel.ActiveTool);
    }

    [AvaloniaFact]
    public void AllButtonsHaveTheSameSize()
    {
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.Equal(DeleteButton.Bounds.Size, RectangleSelectButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, MoveButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, DrawButton.Bounds.Size);
    }

    [AvaloniaFact]
    public void IconsFollowTheButtonsStateColors()
    {
        _viewModel.LoadTileset(_imagePath);
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        var icon = (Avalonia.Controls.Shapes.Path)MoveButton.Content!;
        var presenter = icon.FindAncestorOfType<ContentPresenter>()!;
        Assert.Equal(presenter.Foreground, icon.Stroke);

        _viewModel.IsMoveToolActive = true;
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.Equal(presenter.Foreground, icon.Stroke);
    }

    [AvaloniaFact]
    public void DeleteButtonDeletesTheSelectedTiles()
    {
        _viewModel.LoadTileset(_imagePath);
        _viewModel.HandleCanvasClick(1, 1, addToSelection: false);

        var tileCount = _viewModel.Tiles.Count;

        DeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(_viewModel.SelectedTiles);
        Assert.False(DeleteButton.IsEnabled);
        Assert.Equal(tileCount - 1, _viewModel.Tiles.Count);
    }

    private static string CreateSampleImage()
    {
        var filePath = Path.GetTempFileName() + ".png";
        using var bitmap = new SKBitmap(new SKImageInfo(128, 128, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(filePath);
        data.SaveTo(stream);
        return filePath;
    }
}
