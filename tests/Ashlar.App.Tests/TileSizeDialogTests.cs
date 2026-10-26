using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core;

namespace Ashlar.App.Tests;

public class TileSizeDialogTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void ImportDialogIsPrefilledWithTheDetectedSuggestion()
    {
        using var image = LoadImage(64, 64);

        var viewModel = TileSizeDialogViewModel.ForImport(image);

        Assert.Equal((16, 16), (viewModel.TileWidth, viewModel.TileHeight));
    }

    [AvaloniaFact]
    public void PreviewShowsTheCellsOfTheTopLeftBlockAtTheTileSize()
    {
        using var image = LoadImage(64, 64);

        var viewModel = new TileSizeDialogViewModel(image, 16, 16);

        Assert.Equal(16, viewModel.PreviewCells.Length);
        Assert.All(viewModel.PreviewCells, cell => Assert.Equal((16, 16), (cell.PixelSize.Width, cell.PixelSize.Height)));
    }

    [AvaloniaFact]
    public void PreviewIsLimitedToFiveColumnsAndFiveRows()
    {
        using var image = LoadImage(160, 160);

        var viewModel = new TileSizeDialogViewModel(image, 16, 16);

        Assert.Equal(25, viewModel.PreviewCells.Length);
    }

    [AvaloniaFact]
    public void PreviewStartsWhereTheContentBeginsAndKeepsEmptyCellsInside()
    {
        // Empty first row and column, plus one empty cell inside the content.
        using var image = LoadImage(160, 160, isFilled: (x, y) => x >= 16 && y >= 16 && !(x >= 32 && x < 48 && y >= 32 && y < 48));

        var viewModel = new TileSizeDialogViewModel(image, 16, 16);

        Assert.Equal(25, viewModel.PreviewCells.Length);
    }

    [AvaloniaFact]
    public void PreviewUpdatesWhenTheTileSizeIsEdited()
    {
        using var image = LoadImage(64, 64);
        var viewModel = new TileSizeDialogViewModel(image, 16, 16);

        viewModel.TileWidth = 32;
        viewModel.TileHeight = 32;

        Assert.Equal(4, viewModel.PreviewCells.Length);
        Assert.Equal(32, viewModel.PreviewCells[0].PixelSize.Width);
    }

    [AvaloniaFact]
    public void PreviewDrawsPixelsWithoutSmoothing()
    {
        using var image = LoadImage(32, 16);
        var dialog = new TileSizeDialog(new TileSizeDialogViewModel(image, 16, 16));
        dialog.Show();

        var cells = dialog.FindControl<ItemsControl>("PreviewList")!.GetVisualDescendants()
            .OfType<Avalonia.Controls.Image>().ToList();

        Assert.Equal(2, cells.Count);
        Assert.All(cells, cell => Assert.Equal(
            Avalonia.Media.Imaging.BitmapInterpolationMode.None, Avalonia.Media.RenderOptions.GetBitmapInterpolationMode(cell)));
    }

    [AvaloniaFact]
    public async Task OkReturnsTheEnteredTileSize()
    {
        using var image = LoadImage(64, 64);
        var dialog = new TileSizeDialog(new TileSizeDialogViewModel(image, 16, 16));
        var owner = new Window();
        owner.Show();
        var result = dialog.ShowDialog<(int, int)?>(owner);
        dialog.FindControl<NumericUpDown>("TileWidthBox")!.Value = 32;

        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal((32, 16), await result);
    }

    [AvaloniaFact]
    public async Task CancelReturnsNothing()
    {
        using var image = LoadImage(64, 64);
        var dialog = new TileSizeDialog(new TileSizeDialogViewModel(image, 16, 16));
        var owner = new Window();
        owner.Show();
        var result = dialog.ShowDialog<(int, int)?>(owner);
        dialog.FindControl<NumericUpDown>("TileWidthBox")!.Value = 32;

        dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(await result);
    }

    private EditableTilesetImage LoadImage(int width, int height, Func<int, int, bool>? isFilled = null)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                bitmap.SetPixel(x, y, isFilled is null || isFilled(x, y) ? SKColors.Red : SKColors.Transparent);

        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        return EditableTilesetImage.Load(data.ToArray());
    }
}
