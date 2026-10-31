using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class CloseProjectTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly MainViewModel _viewModel = new();
    private readonly MainWindow _window;

    public CloseProjectTests()
    {
        _window = new MainWindow { DataContext = _viewModel, Width = 800, Height = 600 };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        Directory.Delete(_directory, recursive: true);
    }

    [AvaloniaFact]
    public void CloseProjectMenuItemFollowsWhetherAnImageIsOpen()
    {
        var item = _window.GetLogicalDescendants().OfType<MenuItem>().Single(m => m.Header as string == "Close project");
        Assert.False(item.IsEnabled);

        _viewModel.LoadTileset(WriteImage(), 16, 16);

        Assert.True(item.IsEnabled);
        Assert.Equal("Ctrl+Shift+W", item.InputGesture!.ToString());
    }

    [AvaloniaFact]
    public void ClosingTheWindowWithUnsavedChangesKeepsItOpen()
    {
        _viewModel.LoadTileset(WriteImage(), 16, 16);

        _window.Close();

        Assert.True(_window.IsVisible);
    }

    [AvaloniaFact]
    public void ClosingTheWindowWithoutUnsavedChangesClosesIt()
    {
        _viewModel.LoadTileset(WriteImage(), 16, 16);
        _viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        _window.Close();

        Assert.False(_window.IsVisible);
    }

    [AvaloniaFact]
    public async Task DialogButtonsReturnTheirChoice()
    {
        foreach (var (buttonText, expected) in new[]
        {
            ("Save", UnsavedChangesChoice.Save),
            ("Discard", UnsavedChangesChoice.Discard),
            ("Cancel", UnsavedChangesChoice.Cancel),
        })
        {
            var dialog = new UnsavedChangesDialog();
            var result = dialog.ShowDialog<UnsavedChangesChoice>(_window);

            dialog.GetLogicalDescendants().OfType<Button>().Single(b => b.Content as string == buttonText)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(expected, await result);
        }
    }

    [AvaloniaFact]
    public async Task ClosingTheDialogWithoutAChoiceCountsAsCancel()
    {
        var dialog = new UnsavedChangesDialog();
        var result = dialog.ShowDialog<UnsavedChangesChoice>(_window);

        dialog.Close();

        Assert.Equal(UnsavedChangesChoice.Cancel, await result);
    }

    private string WriteImage()
    {
        var path = Path.Combine(_directory, "tiles.png");
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
