using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using System.Linq;
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

        if (_canCloseWindow || DataContext is not MainViewModel viewModel || !viewModel.HasUnsavedChanges)
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
        if (!viewModel.HasUnsavedChanges)
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

        viewModel.LoadTileset(path, tileWidth, tileHeight);
    }

    private async void OnRemoveSelectedTilesetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedTilesetName: { } name })
            await RemoveTilesetAsync(name);
    }

    private async Task RemoveTilesetAsync(string name)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var message = $"Remove the tileset '{name}' from the project? Its image and all its edits are lost once the project is saved.";
        var mapCount = viewModel.CountMapsUsing(name);
        if (mapCount > 0)
            message += $" The {mapCount} map(s) using it are deleted as well.";

        var confirmed = await new ConfirmDialog("Remove tileset", message, "Remove").ShowDialog<bool>(this);
        if (confirmed)
            viewModel.RemoveTileset(name);
    }

    private async void OnCreateMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { CanCreateMap: true, SuggestedTilesetName: { } tilesetName } viewModel)
            return;

        var dialog = new CreateMapDialog(new CreateMapDialogViewModel(
            viewModel.CreateUniqueMapName(), viewModel.TilesetNames, tilesetName, viewModel.MapNames));
        if (await dialog.ShowDialog<NewMapSettings?>(this) is { } settings)
            viewModel.CreateMap(settings.Name, settings.Width, settings.Height, settings.TilesetName);
    }

    private async void OnResizeSelectedMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedMapName: { } name })
            await ResizeMapAsync(name);
    }

    private async Task ResizeMapAsync(string name)
    {
        if (DataContext is not MainViewModel viewModel || viewModel.GetMap(name) is not { } map)
            return;

        var dialog = new ResizeMapDialog(new ResizeMapDialogViewModel(map.Width, map.Height));
        if (await dialog.ShowDialog<(int Width, int Height)?>(this) is not { } size)
            return;

        var lostTiles = viewModel.CountTilesLostByResize(name, size.Width, size.Height);
        if (lostTiles > 0)
        {
            var confirmed = await new ConfirmDialog(
                "Resize map",
                $"Shrinking the map '{name}' removes {lostTiles} {(lostTiles == 1 ? "tile" : "tiles")}. Resize anyway?",
                "Resize").ShowDialog<bool>(this);
            if (!confirmed)
                return;
        }

        viewModel.ResizeMap(name, size.Width, size.Height);
    }

    private async void OnDeleteSelectedMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { SelectedMapName: { } name })
            await DeleteMapAsync(name);
    }

    private async Task DeleteMapAsync(string name)
    {
        var confirmed = await new ConfirmDialog(
            "Delete map", $"Delete the map '{name}' from the project?", "Delete").ShowDialog<bool>(this);
        if (confirmed && DataContext is MainViewModel viewModel)
            viewModel.DeleteMap(name);
    }

    private void OnRenameSelectedTilesetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { ExplorerSelection: { IsTileset: true } node })
            node.BeginRename();
    }

    private void OnRenameSelectedMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel { ExplorerSelection: { IsMap: true } node })
            node.BeginRename();
    }

    // The entries get their menu when they are created, because the actions depend on the kind of entry.
    private void OnExplorerEntryDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is not Control entry || entry.DataContext is not ExplorerNode node)
            return;

        var menu = new ContextMenu();
        switch (node.Kind)
        {
            case ExplorerNodeKind.TilesetGroup:
                menu.Items.Add(CreateMenuItem("Add tileset...", () => OnOpenTilesetClick(this, new RoutedEventArgs())));
                break;
            case ExplorerNodeKind.MapGroup:
                var create = CreateMenuItem("Create map...", () => OnCreateMapClick(this, new RoutedEventArgs()));
                menu.Opening += (_, _) => create.IsEnabled = (DataContext as MainViewModel)?.CanCreateMap == true;
                menu.Items.Add(create);
                break;
            case ExplorerNodeKind.Tileset:
                menu.Items.Add(CreateMenuItem("Rename", node.BeginRename));
                menu.Items.Add(CreateMenuItem("Remove", () => _ = RemoveTilesetAsync(node.Name)));
                break;
            case ExplorerNodeKind.Map:
                menu.Items.Add(CreateMenuItem("Rename", node.BeginRename));
                menu.Items.Add(CreateMenuItem("Resize...", () => _ = ResizeMapAsync(node.Name)));
                menu.Items.Add(CreateMenuItem("Delete", () => _ = DeleteMapAsync(node.Name)));
                break;
        }

        entry.ContextMenu = menu;
    }

    private static MenuItem CreateMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }

    private static ExplorerNode? NodeOf(object? sender) => (sender as StyledElement)?.DataContext as ExplorerNode;

    private void OnExplorerDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainViewModel { ExplorerSelection: { } node } viewModel && e.Source is Visual source
            && source.FindAncestorOfType<TreeViewItem>()?.DataContext == node)
        {
            viewModel.OpenTab(node);
        }
    }

    private void OnExplorerKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel { ExplorerSelection: { } node } viewModel || e.Source is TextBox)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            viewModel.OpenTab(node);
        }
        else if (e.Key == Key.F2)
        {
            e.Handled = true;
            node.BeginRename();
        }
    }

    private void OnRenameBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (NodeOf(sender) is not { } node)
            return;

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitRename(node);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            node.IsRenaming = false;
        }
    }

    private void OnRenameBoxLostFocus(object? sender, RoutedEventArgs e)
    {
        if (NodeOf(sender) is { IsRenaming: true } node)
            CommitRename(node);
    }

    // A refused name leaves the old one in place; the reason is shown in the status bar.
    private void CommitRename(ExplorerNode node)
    {
        node.IsRenaming = false;
        if (DataContext is MainViewModel viewModel)
            viewModel.RenameNode(node, node.RenameText);
    }

    private void OnRenameBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IsVisibleProperty && e.NewValue is true && sender is TextBox box)
        {
            box.Focus();
            box.SelectAll();
        }
    }

    private void OnTabHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Middle && (sender as StyledElement)?.DataContext is EditorTabViewModel tab
            && DataContext is MainViewModel viewModel)
        {
            e.Handled = true;
            viewModel.CloseTab(tab);
        }
    }

    private void OnTabCloseClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as StyledElement)?.DataContext is EditorTabViewModel tab && DataContext is MainViewModel viewModel)
        {
            e.Handled = true;
            viewModel.CloseTab(tab);
        }
    }

    private void OnCloseTabClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.CloseActiveTab();
    }

    // The view of the active tab; every open tab keeps its own view in the editor host.
    private T? ActiveView<T>() where T : Control =>
        DataContext is MainViewModel { ActiveTab: { } tab }
            ? EditorHost.GetVisualDescendants().OfType<T>().FirstOrDefault(view => view.DataContext == tab)
            : null;

    private async void OnNewProjectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && await ConfirmDiscardingUnsavedChangesAsync(viewModel))
            viewModel.NewProject();
    }

    private async void OnCloseProjectClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && await ConfirmDiscardingUnsavedChangesAsync(viewModel))
            viewModel.CloseProject();
    }

    private async void OnChangeTileSizeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel { ActiveTileset: { } editor })
            return;

        var size = await new TileSizeDialog(editor.CreateChangeTileSizeDialogViewModel()).ShowDialog<(int, int)?>(this);
        if (size is not var (tileWidth, tileHeight))
            return;

        if (editor.NeedsTileSizeConfirmation(tileWidth, tileHeight))
        {
            var confirmed = await new ConfirmDialog(
                "Change tile size",
                "Changing the tile size rebuilds the grid. Tile names, group assignments, deleted tiles and the undo history "
                + "are lost; the image pixels are kept.",
                "Change tile size").ShowDialog<bool>(this);
            if (!confirmed)
                return;
        }

        editor.ChangeTileSize(tileWidth, tileHeight);
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
        if (DataContext is not MainViewModel { ActiveTileset: { } editor } viewModel)
            return;

        var texturePath = await new ExportDialog(viewModel.CreateExportDialogViewModel()).ShowDialog<string?>(this);
        if (texturePath is null)
            return;

        editor.TexturePath = texturePath;

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

    private async void OnExportMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        if (viewModel.CreateExportMapDialogViewModel() is not { } dialogViewModel)
            return;

        if (await new ExportMapDialog(dialogViewModel).ShowDialog<ExportMapDialogViewModel?>(this) is not { } choice)
            return;

        string? xmlPath = null;
        if (choice.ExportXml)
        {
            xmlPath = await PickSavePathAsync("Export map XML", choice.MapName, "xml", new FilePickerFileType("XML file") { Patterns = ["*.xml"] });
            if (xmlPath is null)
                return;
        }

        string? imagePath = null;
        if (choice.ExportImage)
        {
            imagePath = await PickSavePathAsync("Export map image", choice.MapName, "png", FilePickerFileTypes.ImagePng);
            if (imagePath is null)
                return;
        }

        viewModel.ExportMap(xmlPath, imagePath);
    }

    private async Task<string?> PickSavePathAsync(string title, string suggestedName, string extension, FilePickerFileType fileType)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = [fileType],
        });

        return file?.TryGetLocalPath();
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.N when ctrl:
                e.Handled = true;
                OnNewProjectClick(this, new RoutedEventArgs());
                break;
            case Key.O when ctrl && shift:
                e.Handled = true;
                OnOpenProjectClick(this, new RoutedEventArgs());
                break;
            case Key.O when ctrl:
                e.Handled = true;
                OnOpenTilesetClick(this, new RoutedEventArgs());
                break;
            case Key.W when ctrl && shift && viewModel.IsProjectOpen:
                e.Handled = true;
                OnCloseProjectClick(this, new RoutedEventArgs());
                break;
            case Key.W when ctrl && viewModel.HasOpenTabs:
                e.Handled = true;
                viewModel.CloseActiveTab();
                break;
            case Key.E when ctrl && viewModel.IsTilesetTabActive:
                e.Handled = true;
                OnExportXmlClick(this, new RoutedEventArgs());
                break;
            case Key.E when ctrl && viewModel.HasSelectedMap:
                e.Handled = true;
                OnExportMapClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && shift && viewModel.IsProjectOpen:
                e.Handled = true;
                OnSaveProjectAsClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && viewModel.IsProjectOpen:
                e.Handled = true;
                OnSaveProjectClick(this, new RoutedEventArgs());
                break;
            case Key.F2 when viewModel.ExplorerSelection is { IsGroup: false } node && e.Source is not TextBox:
                e.Handled = true;
                node.BeginRename();
                break;
            default:
                ActiveView<TilesetEditorView>()?.HandleKeyDown(e);
                ActiveView<MapEditorView>()?.HandleKeyDown(e);
                break;
        }
    }

    // Zooming the tileset view anchors on the viewport center, which only the view knows.
    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        if (ActiveView<TilesetEditorView>() is { } view)
            view.ZoomIn();
        else
            (DataContext as MainViewModel)?.ActiveTab?.ZoomIn();
    }

    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        if (ActiveView<TilesetEditorView>() is { } view)
            view.ZoomOut();
        else
            (DataContext as MainViewModel)?.ActiveTab?.ZoomOut();
    }

    private void OnResetZoomClick(object? sender, RoutedEventArgs e)
    {
        if (ActiveView<TilesetEditorView>() is { } view)
            view.ResetZoom();
        else
            (DataContext as MainViewModel)?.ActiveTab?.ResetZoom();
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Undo();
    }

    private void OnRedoClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Redo();
    }

    private void OnDeleteSelectedTilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.ActiveTileset?.DeleteSelectedTiles();
    }

    private void OnDeleteSelectedCellsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.ActiveMap?.DeleteSelectedCells();
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
