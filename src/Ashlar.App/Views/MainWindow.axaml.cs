using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MainWindow : Window
{
    // Set once the user has settled the unsaved-changes question, so the
    // close that follows does not ask again.
    private bool _canCloseWindow;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_canCloseWindow || DataContext is not MainViewModel viewModel || !viewModel.Editor.HasImage || !viewModel.Editor.HasUnsavedChanges)
            return;

        e.Cancel = true;
        if (await ConfirmDiscardingUnsavedChangesAsync(viewModel))
        {
            _canCloseWindow = true;
            Close();
        }
    }

    // Returns whether the caller may go on and replace or close the open
    // tileset. Saving counts as settling the question only if it succeeded.
    private async Task<bool> ConfirmDiscardingUnsavedChangesAsync(MainViewModel viewModel)
    {
        if (!viewModel.Editor.HasImage || !viewModel.Editor.HasUnsavedChanges)
            return true;

        return await new UnsavedChangesDialog().ShowDialog<UnsavedChangesChoice>(this) switch
        {
            UnsavedChangesChoice.Save => await SaveProjectAsync(viewModel),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
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

        if (DataContext is not MainViewModel viewModel)
            return;

        using var image = viewModel.ReadImage(path);
        if (image is null)
            return;

        var size = await new TileSizeDialog(TileSizeDialogViewModel.ForImport(image)).ShowDialog<(int, int)?>(this);
        if (size is not var (tileWidth, tileHeight))
            return;

        if (!await ConfirmDiscardingUnsavedChangesAsync(viewModel))
            return;

        viewModel.LoadTileset(path, tileWidth, tileHeight);
    }

    private async void OnCloseProjectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && await ConfirmDiscardingUnsavedChangesAsync(viewModel))
            viewModel.CloseProject();
    }

    private async void OnChangeTileSizeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var size = await new TileSizeDialog(viewModel.Editor.CreateChangeTileSizeDialogViewModel()).ShowDialog<(int, int)?>(this);
        if (size is not var (tileWidth, tileHeight))
            return;

        if (viewModel.Editor.NeedsTileSizeConfirmation(tileWidth, tileHeight))
        {
            var confirmed = await new ConfirmDialog(
                "Change tile size",
                "Changing the tile size rebuilds the grid. Tile names, group assignments, deleted tiles and the undo history "
                + "are lost; the image pixels are kept.",
                "Change tile size").ShowDialog<bool>(this);
            if (!confirmed)
                return;
        }

        viewModel.Editor.ChangeTileSize(tileWidth, tileHeight);
    }

    private async void OnOpenProjectClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open tileset project",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Ashlar project") { Patterns = ["*.ashlar"] }],
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();
        if (path is null)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        if (await ConfirmDiscardingUnsavedChangesAsync(viewModel))
            viewModel.OpenProject(path);
    }

    private async void OnExportXmlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var texturePath = await new ExportDialog(viewModel.CreateExportDialogViewModel()).ShowDialog<string?>(this);
        if (texturePath is null)
            return;

        viewModel.TexturePath = texturePath;

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
            case Key.W when ctrl && viewModel.Editor.HasImage:
                e.Handled = true;
                OnCloseProjectClick(this, new RoutedEventArgs());
                break;
            case Key.E when ctrl && viewModel.Editor.HasImage:
                e.Handled = true;
                OnExportXmlClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && shift && viewModel.Editor.HasImage:
                e.Handled = true;
                OnSaveProjectAsClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && viewModel.Editor.HasImage:
                e.Handled = true;
                OnSaveProjectClick(this, new RoutedEventArgs());
                break;
            default:
                EditorView.HandleKeyDown(e);
                break;
        }
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e) => EditorView.ZoomIn();

    private void OnZoomOutClick(object? sender, RoutedEventArgs e) => EditorView.ZoomOut();

    private void OnResetZoomClick(object? sender, RoutedEventArgs e) => EditorView.ResetZoom();

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Editor.Undo();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Editor.Redo();
    }

    private void OnDeleteSelectedTilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Editor.DeleteSelectedTiles();
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
        if (DataContext is MainViewModel viewModel)
            await SaveProjectAsync(viewModel);
    }

    private async void OnSaveProjectAsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            await SaveProjectAsAsync(viewModel);
    }

    private async Task<bool> SaveProjectAsync(MainViewModel viewModel) =>
        viewModel.ProjectPath is { } path ? viewModel.SaveProject(path) : await SaveProjectAsAsync(viewModel);

    private async Task<bool> SaveProjectAsAsync(MainViewModel viewModel)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save tileset project",
            SuggestedFileName = viewModel.SuggestedProjectFileName,
            DefaultExtension = "ashlar",
            FileTypeChoices = [new FilePickerFileType("Ashlar project") { Patterns = ["*.ashlar"] }],
        });

        if (file is null)
            return false;

        var path = file.TryGetLocalPath();
        if (path is null)
            return false;

        // Not every platform picker applies DefaultExtension.
        if (!Path.HasExtension(path))
            path += ".ashlar";

        return viewModel.SaveProject(path);
    }
}
