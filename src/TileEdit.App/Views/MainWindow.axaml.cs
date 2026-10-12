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

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open tileset project",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("TileEdit project") { Patterns = ["*.json"] }],
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.OpenProject(path);
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

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.O when ctrl && shift:
                e.Handled = true;
                OnOpenProjectClick(this, new RoutedEventArgs());
                break;
            case Key.O when ctrl:
                e.Handled = true;
                OnOpenTilesetClick(this, new RoutedEventArgs());
                break;
            case Key.E when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnExportXmlClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && shift && viewModel.HasImage:
                e.Handled = true;
                OnSaveProjectClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnSaveImageClick(this, new RoutedEventArgs());
                break;
            case Key.Delete when viewModel.HasSelectedTile:
                e.Handled = true;
                OnDeleteSelectedTileClick(this, new RoutedEventArgs());
                break;
        }
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

    private async void OnSaveProjectClick(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save tileset project",
            SuggestedFileName = "tileset",
            DefaultExtension = "tileproj.json",
            FileTypeChoices = [new FilePickerFileType("TileEdit project") { Patterns = ["*.tileproj.json"] }],
        });

        if (file is null)
            return;

        var path = file.TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is MainViewModel viewModel)
            viewModel.SaveProject(path);
    }
}
