using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MainWindow : Window
{
    private Point? _panStartViewportPosition;
    private Vector _panStartOffset;
    private Point? _rectangleDragOrigin;
    private (int X, int Y) _rectangleDragStartPixel;
    private bool _rectangleDragAddsToSelection;
    private bool _isRectangleDragging;
    private IPointer? _rectangleDragPointer;
    private bool _isMoveDragging;
    private IPointer? _moveDragPointer;
    private bool _isDrawing;
    private IPointer? _drawPointer;
    private bool _isPicking;
    private IPointer? _pickPointer;

    // Set once the user has settled the unsaved-changes question, so the
    // close that follows does not ask again.
    private bool _canCloseWindow;

    // A rectangle press is pending before the drag threshold is crossed, which
    // the view model does not know about yet.
    private bool IsPointerDragPending => _rectangleDragOrigin is not null || _isMoveDragging || _isDrawing || _isPicking;

    // Screen distance the pointer has to travel before a press counts as a drag
    // rather than a click.
    private const double DragThreshold = 4;

    public MainWindow()
    {
        InitializeComponent();

        TileCanvasScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        DataContextChanged += OnDataContextChanged;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_canCloseWindow || DataContext is not MainViewModel viewModel || !viewModel.HasImage || !viewModel.HasUnsavedChanges)
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
        if (!viewModel.HasImage || !viewModel.HasUnsavedChanges)
            return true;

        return await new UnsavedChangesDialog().ShowDialog<UnsavedChangesChoice>(this) switch
        {
            UnsavedChangesChoice.Save => await SaveProjectAsync(viewModel),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateInterpolation(viewModel);
        viewModel.TilesetLoaded += (_, _) => ScrollToTilesetStart();
        viewModel.PreviewPixelsChanged += (_, _) =>
        {
            TilesetImage.InvalidateVisual();
            TileOverlay.InvalidateVisual();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.PreviewInterpolation) && sender is MainViewModel viewModel)
            UpdateInterpolation(viewModel);

        if (e.PropertyName is nameof(MainViewModel.ZoomLevel) or nameof(MainViewModel.TileWidth)
            or nameof(MainViewModel.TileHeight) or nameof(MainViewModel.ImageWidth)
            or nameof(MainViewModel.ImageHeight) or nameof(MainViewModel.HasImage))
        {
            UpdateOverscrollPadding();
        }
    }

    private void UpdateInterpolation(MainViewModel viewModel)
    {
        RenderOptions.SetBitmapInterpolationMode(TilesetImage, viewModel.PreviewInterpolation);
        RenderOptions.SetBitmapInterpolationMode(TileOverlay, viewModel.PreviewInterpolation);
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
            UpdateOverscrollPadding();

        if (e.Property == ScrollViewer.ViewportProperty || e.Property == ScrollViewer.OffsetProperty
            || e.Property == ScrollViewer.ExtentProperty)
        {
            UpdateVisibleArea();
        }
    }

    private void UpdateVisibleArea()
    {
        var viewport = TileCanvasScrollViewer.Viewport;
        var topLeft = TileCanvasScrollViewer.TranslatePoint(new Point(0, 0), TileCanvasContent);
        var bottomRight = TileCanvasScrollViewer.TranslatePoint(new Point(viewport.Width, viewport.Height), TileCanvasContent);
        TileOverlay.VisibleArea = topLeft is { } start && bottomRight is { } end ? new Rect(start, end) : null;
    }

    // Lets the view be panned past the tileset's edges, but never so far that the
    // tileset leaves the viewport: at maximum pan, exactly one tile row/column
    // stays visible at the opposite edge. Only applies on an axis where the
    // zoomed tileset overflows the viewport; otherwise it stays centered.
    private void UpdateOverscrollPadding()
    {
        if (DataContext is not MainViewModel viewModel || !viewModel.HasImage)
            return;

        var viewport = TileCanvasScrollViewer.Viewport;
        var horizontal = OverscrollPadding(viewport.Width, viewModel.ScaledImageWidth, viewModel.TileWidth * viewModel.ZoomLevel);
        var vertical = OverscrollPadding(viewport.Height, viewModel.ScaledImageHeight, viewModel.TileHeight * viewModel.ZoomLevel);
        TileCanvasPadding.Padding = new Thickness(horizontal, vertical);
    }

    // Scrolls so the tileset's top-left corner sits at the viewport's top-left
    // corner, which is where the overscroll padding ends. An axis that fits into
    // the viewport has no padding and no scroll range, so it stays centered.
    private void ScrollToTilesetStart()
    {
        UpdateOverscrollPadding();
        TileCanvasScrollViewer.GetLayoutManager()?.ExecuteLayoutPass();

        var padding = TileCanvasPadding.Padding;
        TileCanvasScrollViewer.Offset = ClampOffset(new Vector(padding.Left, padding.Top));
    }

    private static double OverscrollPadding(double viewportSize, double scaledImageSize, double scaledTileSize) =>
        scaledImageSize > viewportSize ? Math.Max(0, viewportSize - scaledTileSize) : 0;

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

        var size = await new TileSizeDialog(viewModel.CreateChangeTileSizeDialogViewModel()).ShowDialog<(int, int)?>(this);
        if (size is not var (tileWidth, tileHeight))
            return;

        if (viewModel.NeedsTileSizeConfirmation(tileWidth, tileHeight))
        {
            var confirmed = await new ConfirmDialog(
                "Change tile size",
                "Changing the tile size rebuilds the grid. Tile names, group assignments, deleted tiles and the undo history "
                + "are lost; the image pixels are kept.",
                "Change tile size").ShowDialog<bool>(this);
            if (!confirmed)
                return;
        }

        viewModel.ChangeTileSize(tileWidth, tileHeight);
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
            case Key.W when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnCloseProjectClick(this, new RoutedEventArgs());
                break;
            case Key.E when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnExportXmlClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && shift && viewModel.HasImage:
                e.Handled = true;
                OnSaveProjectAsClick(this, new RoutedEventArgs());
                break;
            case Key.S when ctrl && viewModel.HasImage:
                e.Handled = true;
                OnSaveProjectClick(this, new RoutedEventArgs());
                break;
            case Key.R when ctrl && !shift && viewModel.HasImage:
                e.Handled = true;
                viewModel.IsRectangleToolActive = !viewModel.IsRectangleToolActive;
                break;
            case Key.M when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && viewModel.HasImage:
                e.Handled = true;
                viewModel.IsMoveToolActive = !viewModel.IsMoveToolActive;
                break;
            case Key.B when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && viewModel.HasImage:
                e.Handled = true;
                viewModel.IsDrawToolActive = !viewModel.IsDrawToolActive;
                break;
            case Key.I when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && viewModel.HasImage:
                e.Handled = true;
                viewModel.IsPipetteToolActive = !viewModel.IsPipetteToolActive;
                break;
            case Key.Z when ctrl && shift && e.Source is not TextBox && !IsPointerDragPending:
            case Key.Y when ctrl && e.Source is not TextBox && !IsPointerDragPending:
                e.Handled = true;
                viewModel.Redo();
                break;
            case Key.Z when ctrl && e.Source is not TextBox && !IsPointerDragPending:
                e.Handled = true;
                viewModel.Undo();
                break;
            case Key.Escape when _rectangleDragOrigin is not null:
                e.Handled = true;
                viewModel.CancelSelectionDrag();
                ResetRectangleDrag();
                break;
            case Key.Escape when _isMoveDragging:
                e.Handled = true;
                viewModel.EndMoveDrag();
                ResetMoveDrag();
                break;
            case Key.Escape when _isDrawing:
                e.Handled = true;
                viewModel.CancelStroke();
                ResetDraw();
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

        if (IsPointerDragPending)
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

        if (viewModel.IsMoveToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (startX, startY) = ImagePixelAt(e);
                if (viewModel.BeginMoveDrag(startX, startY))
                {
                    _isMoveDragging = true;
                    _moveDragPointer = e.Pointer;
                    e.Pointer.Capture(canvas);
                }
            }

            return;
        }

        if (viewModel.IsDrawToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (startX, startY) = ImagePixelAt(e);
                if (viewModel.BeginStroke(startX, startY))
                {
                    _isDrawing = true;
                    _drawPointer = e.Pointer;
                    e.Pointer.Capture(canvas);
                }
            }

            return;
        }

        if (viewModel.IsPipetteToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (pickX, pickY) = ImagePixelAt(e);
                viewModel.PickColor(pickX, pickY);
                _isPicking = true;
                _pickPointer = e.Pointer;
                e.Pointer.Capture(canvas);
            }

            return;
        }

        if (viewModel.IsRectangleToolActive && e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
        {
            // Whether this becomes a click or a drag is only known once the pointer
            // has moved (or has been released), so selection is deferred until then.
            _rectangleDragOrigin = e.GetPosition(canvas);
            _rectangleDragStartPixel = ImagePixelAt(e);
            _rectangleDragAddsToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            _rectangleDragPointer = e.Pointer;
            e.Pointer.Capture(canvas);
            return;
        }

        var (pixelX, pixelY) = ImagePixelAt(e);
        viewModel.HandleCanvasClick(pixelX, pixelY, e.KeyModifiers.HasFlag(KeyModifiers.Control));
    }

    // Floor, not truncate: the padding makes coordinates in (-1, 0) reachable.
    private (int X, int Y) ImagePixelAt(PointerEventArgs e)
    {
        var position = e.GetPosition(TileCanvasContent);
        return ((int)Math.Floor(position.X), (int)Math.Floor(position.Y));
    }

    private void ResetRectangleDrag()
    {
        var pointer = _rectangleDragPointer;
        _rectangleDragOrigin = null;
        _isRectangleDragging = false;
        _rectangleDragPointer = null;
        pointer?.Capture(null);
    }

    private void ResetDraw()
    {
        var pointer = _drawPointer;
        _isDrawing = false;
        _drawPointer = null;
        pointer?.Capture(null);
    }

    private void ResetPick()
    {
        var pointer = _pickPointer;
        _isPicking = false;
        _pickPointer = null;
        pointer?.Capture(null);
    }

    private void ResetMoveDrag()
    {
        var pointer = _moveDragPointer;
        _isMoveDragging = false;
        _moveDragPointer = null;
        pointer?.Capture(null);
    }

    private void OnTileCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPicking && DataContext is MainViewModel pickViewModel)
        {
            var (pickPixelX, pickPixelY) = ImagePixelAt(e);
            pickViewModel.PickColor(pickPixelX, pickPixelY);
            return;
        }

        if (_isDrawing && DataContext is MainViewModel drawViewModel)
        {
            var (drawPixelX, drawPixelY) = ImagePixelAt(e);
            drawViewModel.ContinueStroke(drawPixelX, drawPixelY);
            return;
        }

        if (_isMoveDragging && DataContext is MainViewModel moveViewModel)
        {
            var (movePixelX, movePixelY) = ImagePixelAt(e);
            moveViewModel.UpdateMoveDrag(movePixelX, movePixelY);
            return;
        }

        if (_rectangleDragOrigin is { } dragOrigin && sender is Control canvas && DataContext is MainViewModel viewModel)
        {
            if (!_isRectangleDragging)
            {
                var travelled = e.GetPosition(canvas) - dragOrigin;
                if (Math.Abs(travelled.X) < DragThreshold && Math.Abs(travelled.Y) < DragThreshold)
                    return;

                _isRectangleDragging = true;
                viewModel.BeginSelectionDrag(_rectangleDragStartPixel.X, _rectangleDragStartPixel.Y, _rectangleDragAddsToSelection);
            }

            var (pixelX, pixelY) = ImagePixelAt(e);
            viewModel.UpdateSelectionDrag(pixelX, pixelY);
            return;
        }

        if (_panStartViewportPosition is not { } startPosition)
            return;

        var currentPosition = e.GetPosition(TileCanvasScrollViewer);
        TileCanvasScrollViewer.Offset = ClampOffset(_panStartOffset + (startPosition - currentPosition));
    }

    private void OnTileCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPicking && e.InitialPressMouseButton == MouseButton.Left)
        {
            ResetPick();
            return;
        }

        if (_isDrawing && e.InitialPressMouseButton == MouseButton.Left && DataContext is MainViewModel drawViewModel)
        {
            drawViewModel.CommitStroke();
            ResetDraw();
            return;
        }

        if (_isMoveDragging && e.InitialPressMouseButton == MouseButton.Left && DataContext is MainViewModel moveViewModel)
        {
            moveViewModel.CommitMoveDrag();
            ResetMoveDrag();
            return;
        }

        if (_rectangleDragOrigin is not null && e.InitialPressMouseButton == MouseButton.Left
            && DataContext is MainViewModel viewModel)
        {
            if (_isRectangleDragging)
                viewModel.EndSelectionDrag();
            else
                viewModel.HandleCanvasClick(_rectangleDragStartPixel.X, _rectangleDragStartPixel.Y, _rectangleDragAddsToSelection);

            ResetRectangleDrag();
            return;
        }

        if (_panStartViewportPosition is null)
            return;

        _panStartViewportPosition = null;
        e.Pointer.Capture(null);
    }

    private void OnTileCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDrawing && DataContext is MainViewModel drawViewModel)
            drawViewModel.CommitStroke();

        _isDrawing = false;
        _drawPointer = null;
        _isPicking = false;
        _pickPointer = null;

        if (_isRectangleDragging && DataContext is MainViewModel viewModel)
            viewModel.EndSelectionDrag();

        if (_isMoveDragging && DataContext is MainViewModel moveViewModel)
            moveViewModel.EndMoveDrag();

        _isMoveDragging = false;
        _moveDragPointer = null;

        _rectangleDragOrigin = null;
        _isRectangleDragging = false;
        _rectangleDragPointer = null;
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

        viewModel.ResetZoom();
        ScrollToTilesetStart();
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
            viewModel.DeleteSelectedTiles();
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
            SuggestedFileName = "tileset",
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
