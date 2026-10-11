using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TileEdit.App.ViewModels;

namespace TileEdit.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OnOpenTilesetClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open tileset image",
            AllowMultiple = false,
            FileTypeFilter = [FilePickerFileTypes.ImageAll],
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.LoadTileset(path);
    }

    private async void OnExportXmlClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export tileset XML",
            SuggestedFileName = "tileset",
            DefaultExtension = "xml",
            FileTypeChoices = [new FilePickerFileType("XML file") { Patterns = ["*.xml"] }],
        });

        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.ExportXml(path);
    }

    private void OnTileCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control canvas)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        var position = e.GetPosition(canvas);
        viewModel.HandleCanvasClick((int)position.X, (int)position.Y);
    }

    private void OnDeleteSelectedTileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.DeleteSelectedTile();
    }

    private void OnToggleMoveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.ToggleMove();
    }

    private async void OnSaveImageClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save tileset image",
            SuggestedFileName = "tileset",
            DefaultExtension = "png",
            FileTypeChoices = [new FilePickerFileType("PNG image") { Patterns = ["*.png"] }],
        });

        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.SaveImage(path);
    }
}
