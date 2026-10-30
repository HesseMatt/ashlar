using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

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

    private ToggleButton RectangleSelectButton => _window.FindEditorControl<ToggleButton>("RectangleSelectButton")!;

    private ToggleButton MoveButton => _window.FindEditorControl<ToggleButton>("MoveButton")!;

    private ToggleButton DrawButton => _window.FindEditorControl<ToggleButton>("DrawButton")!;

    private ToggleButton BucketButton => _window.FindEditorControl<ToggleButton>("BucketButton")!;

    private ToggleButton PipetteButton => _window.FindEditorControl<ToggleButton>("PipetteButton")!;

    private Button DeleteButton => _window.FindEditorControl<Button>("DeleteButton")!;

    [AvaloniaFact]
    public void AllButtonsAreDisabledWithoutAnImage()
    {
        Assert.False(RectangleSelectButton.IsEnabled);
        Assert.False(MoveButton.IsEnabled);
        Assert.False(DrawButton.IsEnabled);
        Assert.False(BucketButton.IsEnabled);
        Assert.False(PipetteButton.IsEnabled);
        Assert.False(DeleteButton.IsEnabled);
    }

    [AvaloniaFact]
    public void ToolTogglesAreEnabledOnceAnImageIsLoadedAndDeleteWaitsForASelection()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        Assert.True(RectangleSelectButton.IsEnabled);
        Assert.True(MoveButton.IsEnabled);
        Assert.True(DrawButton.IsEnabled);
        Assert.True(BucketButton.IsEnabled);
        Assert.True(PipetteButton.IsEnabled);
        Assert.False(DeleteButton.IsEnabled);

        _viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);

        Assert.True(DeleteButton.IsEnabled);
    }

    [AvaloniaFact]
    public void TogglesFollowTheActiveToolAndAreMutuallyExclusive()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        _viewModel.Editor.IsRectangleToolActive = true;
        Assert.True(RectangleSelectButton.IsChecked);
        Assert.False(MoveButton.IsChecked);

        _viewModel.Editor.IsMoveToolActive = true;
        Assert.False(RectangleSelectButton.IsChecked);
        Assert.True(MoveButton.IsChecked);

        _viewModel.Editor.IsMoveToolActive = false;
        Assert.False(RectangleSelectButton.IsChecked);
        Assert.False(MoveButton.IsChecked);
        Assert.Equal(CanvasTool.Select, _viewModel.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void CheckingATogglePicksTheToolAndUncheckingReturnsToClickSelection()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        RectangleSelectButton.IsChecked = true;
        Assert.Equal(CanvasTool.Rectangle, _viewModel.Editor.ActiveTool);

        MoveButton.IsChecked = true;
        Assert.Equal(CanvasTool.Move, _viewModel.Editor.ActiveTool);
        Assert.False(RectangleSelectButton.IsChecked);

        MoveButton.IsChecked = false;
        Assert.Equal(CanvasTool.Select, _viewModel.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void DrawToggleActivatesTheDrawTool()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        DrawButton.IsChecked = true;
        Assert.Equal(CanvasTool.Draw, _viewModel.Editor.ActiveTool);

        _viewModel.Editor.IsMoveToolActive = true;
        Assert.False(DrawButton.IsChecked);

        _viewModel.Editor.IsDrawToolActive = true;
        _viewModel.Editor.IsDrawToolActive = false;
        Assert.Equal(CanvasTool.Select, _viewModel.Editor.ActiveTool);
    }

    [AvaloniaFact]
    public void BucketToggleActivatesTheBucketToolAndSitsBetweenDrawAndPipette()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        BucketButton.IsChecked = true;
        Assert.Equal(CanvasTool.Bucket, _viewModel.Editor.ActiveTool);
        Assert.Equal("Bucket (G)", ToolTip.GetTip(BucketButton));
        Assert.True(BucketButton.Bounds.Top > DrawButton.Bounds.Bottom);
        Assert.True(BucketButton.Bounds.Bottom < PipetteButton.Bounds.Top);

        _viewModel.Editor.IsDrawToolActive = true;
        Assert.False(BucketButton.IsChecked);
    }

    [AvaloniaFact]
    public void PipetteToggleActivatesThePipetteTool()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        PipetteButton.IsChecked = true;
        Assert.Equal(CanvasTool.Pipette, _viewModel.Editor.ActiveTool);

        _viewModel.Editor.IsDrawToolActive = true;
        Assert.False(PipetteButton.IsChecked);
    }

    private Button DrawColorButton => _window.FindEditorControl<Button>("DrawColorButton")!;

    [AvaloniaFact]
    public void DrawColorSwatchFollowsTheDrawColor()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        var swatch = _window.FindEditorControl<Border>("DrawColorSwatch")!;
        Assert.Equal(Colors.Black, ((ISolidColorBrush)swatch.Background!).Color);

        _viewModel.Editor.DrawColor = Color.FromArgb(128, 1, 2, 3);
        Assert.Equal(Color.FromArgb(128, 1, 2, 3), ((ISolidColorBrush)swatch.Background!).Color);
    }

    [AvaloniaFact]
    public void DrawColorFlyoutEditsTheDrawColor()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);

        var flyout = (Flyout)DrawColorButton.Flyout!;
        flyout.ShowAt(DrawColorButton);
        var colorView = (ColorView)flyout.Content!;
        Assert.True(colorView.IsAlphaEnabled);
        Assert.Equal(_viewModel.Editor.DrawColor, colorView.Color);

        colorView.Color = Color.FromArgb(128, 4, 5, 6);
        Assert.Equal(Color.FromArgb(128, 4, 5, 6), _viewModel.Editor.DrawColor);
    }

    [AvaloniaFact]
    public void DrawColorButtonSitsBelowDeleteWithATooltip()
    {
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.False(DrawColorButton.IsEnabled);
        Assert.Equal("Draw color", ToolTip.GetTip(DrawColorButton));
        Assert.Equal(DeleteButton.Bounds.Size, DrawColorButton.Bounds.Size);
        Assert.True(DrawColorButton.Bounds.Top > DeleteButton.Bounds.Bottom);
        Assert.Same(DeleteButton.Parent, DrawColorButton.Parent);
    }

    [AvaloniaFact]
    public void AllButtonsHaveTheSameSize()
    {
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.Equal(DeleteButton.Bounds.Size, RectangleSelectButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, MoveButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, DrawButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, BucketButton.Bounds.Size);
        Assert.Equal(DeleteButton.Bounds.Size, PipetteButton.Bounds.Size);
    }

    [AvaloniaFact]
    public void IconsFollowTheButtonsStateColors()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        var icon = (Avalonia.Controls.Shapes.Path)MoveButton.Content!;
        var presenter = icon.FindAncestorOfType<ContentPresenter>()!;
        Assert.Equal(presenter.Foreground, icon.Stroke);

        _viewModel.Editor.IsMoveToolActive = true;
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.Equal(presenter.Foreground, icon.Stroke);
    }

    [AvaloniaFact]
    public void DeleteButtonDeletesTheSelectedTiles()
    {
        _viewModel.LoadTileset(_imagePath, 16, 16);
        _viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);

        var tileCount = _viewModel.Editor.Tiles.Count;

        DeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Empty(_viewModel.Editor.SelectedTiles);
        Assert.False(DeleteButton.IsEnabled);
        Assert.Equal(tileCount - 1, _viewModel.Editor.Tiles.Count);
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
