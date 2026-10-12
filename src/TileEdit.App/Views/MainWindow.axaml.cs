using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using TileEdit.App.ViewModels;

namespace TileEdit.App.Views;

public partial class MainWindow : Window
{
    private Point? _panStartViewportPosition;
    private Vector _panStartOffset;

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
            case Key.Delete when viewModel.HasSelectedTiles:
                e.Handled = true;
                OnDeleteSelectedTilesClick(this, new RoutedEventArgs());
                break;
            case Key.OemPlus when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnZoomInClick(this, new RoutedEventArgs());
                break;
            case Key.OemMinus when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnZoomOutClick(this, new RoutedEventArgs());
                break;
            case Key.D0 when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnResetZoomClick(this, new RoutedEventArgs());
                break;
        }
    }

    private void OnTileCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control canvas)
            return;

        if (DataContext is not MainViewModel viewModel)
            return;

        if (e.GetCurrentPoint(canvas).Properties.IsRightButtonPressed)
        {
            _panStartViewportPosition = e.GetPosition(TileCanvasScrollViewer);
            _panStartOffset = TileCanvasScrollViewer.Offset;
            e.Pointer.Capture(canvas);
            e.Handled = true;
            return;
        }

        canvas.Focus();

        var position = e.GetPosition(TileCanvasContent);
        var addToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        viewModel.HandleCanvasClick((int)position.X, (int)position.Y, addToSelection);
    }

    private void OnTileCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_panStartViewportPosition is not { } startPosition)
            return;

        var currentPosition = e.GetPosition(TileCanvasScrollViewer);
        TileCanvasScrollViewer.Offset = ClampOffset(_panStartOffset + (startPosition - currentPosition));
    }

    private void OnTileCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_panStartViewportPosition is null)
            return;

        _panStartViewportPosition = null;
        e.Pointer.Capture(null);
    }

    private void OnTileCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _panStartViewportPosition = null;
    }

    private void OnTileCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainViewModel viewModel)
            return;

        var anchorPosition = e.GetPosition(TileCanvasScrollViewer);
        var zoomIn = e.Delta.Y > 0;
        AnchoredZoom(anchorPosition, () =>
        {
            if (zoomIn) viewModel.ZoomIn(); else viewModel.ZoomOut();
        });
    }

    private void OnZoomInClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        AnchoredZoom(ViewportCenter(), () => viewModel.ZoomIn());
    }

    private void OnZoomOutClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        AnchoredZoom(ViewportCenter(), () => viewModel.ZoomOut());
    }

    private void OnResetZoomClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        AnchoredZoom(ViewportCenter(), () => viewModel.ResetZoom());
    }

    private Point ViewportCenter() =>
        new(TileCanvasScrollViewer.Viewport.Width / 2, TileCanvasScrollViewer.Viewport.Height / 2);

    // Keeps the image pixel under anchorViewportPosition at that same screen
    // position across a zoom change. This goes through Avalonia's own
    // coordinate-transform APIs (TranslatePoint) rather than reconstructing
    // the Offset/RenderTransform math by hand, because the canvas is
    // center-aligned by the ScrollViewer whenever it's smaller than the
    // viewport (i.e. at low zoom) - a hand-rolled "offset + position" formula
    // silently ignores that alignment and anchors to the wrong image pixel.
    private void AnchoredZoom(Point anchorViewportPosition, Action applyZoomChange)
    {
        var imagePoint = TileCanvasScrollViewer.TranslatePoint(anchorViewportPosition, TileCanvasContent);

        applyZoomChange();
        TileCanvasScrollViewer.GetLayoutManager()?.ExecuteLayoutPass();

        if (imagePoint is not { } anchorImagePoint)
            return;

        var renderedAt = TileCanvasContent.TranslatePoint(anchorImagePoint, TileCanvasScrollViewer);
        if (renderedAt is not { } actualPosition)
            return;

        var drift = anchorViewportPosition - actualPosition;
        TileCanvasScrollViewer.Offset = ClampOffset(TileCanvasScrollViewer.Offset - drift);
    }

    private Vector ClampOffset(Vector offset)
    {
        var maxX = Math.Max(0, TileCanvasScrollViewer.Extent.Width - TileCanvasScrollViewer.Viewport.Width);
        var maxY = Math.Max(0, TileCanvasScrollViewer.Extent.Height - TileCanvasScrollViewer.Viewport.Height);
        return new Vector(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }

    private void OnDeleteSelectedTilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.DeleteSelectedTiles();
    }

    private void OnToggleMoveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.ToggleMove();
    }

    private void OnGroupNameInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        if (DataContext is MainViewModel viewModel)
            viewModel.ConfirmGroupNameInput();
    }

    private void OnGroupNameSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.ConfirmGroupNameInput();
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
