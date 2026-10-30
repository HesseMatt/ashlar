using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Ashlar.App.Controls;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MapEditorView : UserControl
{
    private readonly CanvasViewport _viewport;
    private bool _scrollToStartPending;
    private string? _lastScrolledMapName;
    private MapEditorViewModel? _viewModel;
    private const double DragThreshold = 4;
    private bool _isDrawing;
    private bool _isMoveDragging;
    private bool _isRectangleDragging;
    private Point? _rectangleDragOrigin;
    private (int X, int Y) _rectangleDragStartCell;
    private bool _rectangleDragAddsToSelection;
    private IPointer? _dragPointer;

    public MapEditorView()
    {
        InitializeComponent();
        _viewport = new CanvasViewport(MapCanvasScrollViewer, MapCanvasPadding, MapCanvasContent);
        MapCanvasScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MapShown -= OnMapShown;
            _viewModel.CellsPainted -= OnCellsPainted;
        }

        _viewModel = DataContext as MapEditorViewModel;
        if (_viewModel is not { } viewModel)
            return;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.MapShown += OnMapShown;
        viewModel.CellsPainted += OnCellsPainted;
        UpdateInterpolation(viewModel);
        UpdateOverscrollPadding();
        // The tab content is created lazily, so a map selected before that was never scrolled to its start.
        // Coming back to the same map keeps the position the user panned to.
        _scrollToStartPending = viewModel.HasSelectedMap && viewModel.SelectedMap?.Name != _lastScrolledMapName;
        ScrollToMapStartIfPending();
    }

    // The canvas is still hidden or laid out for the previous map when the event is raised.
    private void OnMapShown(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ScrollToMapStart, DispatcherPriority.Loaded);

    private void OnCellsPainted(object? sender, EventArgs e) => MapCells.InvalidateVisual();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MapEditorViewModel viewModel)
            return;

        if (e.PropertyName == nameof(MapEditorViewModel.PreviewInterpolation))
            UpdateInterpolation(viewModel);

        if (e.PropertyName is nameof(MapEditorViewModel.ScaledMapWidth) or nameof(MapEditorViewModel.ScaledMapHeight)
            or nameof(MapEditorViewModel.TileWidth) or nameof(MapEditorViewModel.TileHeight))
        {
            UpdateOverscrollPadding();
        }
    }

    private void UpdateInterpolation(MapEditorViewModel viewModel)
    {
        RenderOptions.SetBitmapInterpolationMode(MapCells, viewModel.PreviewInterpolation);
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            UpdateOverscrollPadding();
            ScrollToMapStartIfPending();
        }

        if (e.Property == ScrollViewer.ViewportProperty || e.Property == ScrollViewer.OffsetProperty
            || e.Property == ScrollViewer.ExtentProperty)
        {
            UpdateVisibleArea();
        }
    }

    private void ScrollToMapStartIfPending()
    {
        if (_scrollToStartPending)
            Dispatcher.UIThread.Post(ScrollToMapStart, DispatcherPriority.Loaded);
    }

    // Only the cells in view are drawn, so large maps stay cheap to render.
    private void UpdateVisibleArea()
    {
        var viewport = MapCanvasScrollViewer.Viewport;
        var topLeft = MapCanvasScrollViewer.TranslatePoint(new Point(0, 0), MapCanvasContent);
        var bottomRight = MapCanvasScrollViewer.TranslatePoint(new Point(viewport.Width, viewport.Height), MapCanvasContent);
        MapCells.VisibleArea = topLeft is { } start && bottomRight is { } end ? new Rect(start, end) : null;
    }

    private void UpdateOverscrollPadding()
    {
        if (DataContext is not MapEditorViewModel viewModel || !viewModel.HasSelectedMap)
            return;

        _viewport.UpdateOverscrollPadding(viewModel.ScaledMapWidth, viewModel.ScaledMapHeight,
            viewModel.TileWidth * viewModel.ZoomLevel, viewModel.TileHeight * viewModel.ZoomLevel);
    }

    private void ScrollToMapStart()
    {
        if (DataContext is not MapEditorViewModel viewModel)
            return;

        // A hidden tab has no viewport yet; scrolling now would be lost, so retry once it is shown.
        _scrollToStartPending = !MapCanvasScrollViewer.IsEffectivelyVisible;
        if (_scrollToStartPending)
            return;

        _lastScrolledMapName = viewModel.SelectedMap?.Name;
        _viewport.ScrollToStart(viewModel.ScaledMapWidth, viewModel.ScaledMapHeight,
            viewModel.TileWidth * viewModel.ZoomLevel, viewModel.TileHeight * viewModel.ZoomLevel);
    }

    // Handles the editing shortcuts.
    public void HandleKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MapEditorViewModel viewModel)
            return;

        switch (e.Key)
        {
            case Key.Escape when _rectangleDragOrigin is not null:
                e.Handled = true;
                viewModel.CancelSelectionDrag();
                ResetDrag();
                break;
            case Key.Escape when _isMoveDragging:
                e.Handled = true;
                viewModel.EndMoveDrag();
                ResetDrag();
                break;
            // Only with the canvas focused, so Delete in the map list or name box never clears cells.
            case Key.Delete when e.Source == MapCanvasPadding && viewModel.HasSelectedCells:
                e.Handled = true;
                viewModel.DeleteSelectedCells();
                break;
        }
    }

    // A rectangle press is pending before the drag threshold is crossed, which
    // is why it counts as a drag here already.
    private bool IsPointerDragPending => _rectangleDragOrigin is not null || _isMoveDragging || _isDrawing;

    private void OnMapCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control canvas || IsPointerDragPending || _viewport.IsPanning)
            return;

        var properties = e.GetCurrentPoint(canvas).Properties;
        if (properties.IsRightButtonPressed)
        {
            _viewport.BeginPan(e, canvas);
        }
        else if (properties.IsLeftButtonPressed && DataContext is MapEditorViewModel viewModel)
        {
            canvas.Focus();
            var (cellX, cellY) = CellAt(e, viewModel);
            if (viewModel.IsMoveToolActive)
            {
                if (viewModel.BeginMoveDrag(cellX, cellY))
                {
                    _isMoveDragging = true;
                    _dragPointer = e.Pointer;
                    e.Pointer.Capture(canvas);
                }
            }
            else if (viewModel.IsRectangleToolActive)
            {
                // Whether this becomes a click or a drag is only known once the pointer
                // has moved (or has been released), so selection is deferred until then.
                _rectangleDragOrigin = e.GetPosition(canvas);
                _rectangleDragStartCell = (cellX, cellY);
                _rectangleDragAddsToSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                _dragPointer = e.Pointer;
                e.Pointer.Capture(canvas);
            }
            else if (viewModel.BeginStroke(cellX, cellY))
            {
                _isDrawing = true;
                _dragPointer = e.Pointer;
                e.Pointer.Capture(canvas);
            }
        }
    }

    private void OnMapCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel)
        {
            if (_isDrawing)
            {
                var (cellX, cellY) = CellAt(e, viewModel);
                viewModel.ContinueStroke(cellX, cellY);
                return;
            }

            if (_isMoveDragging)
            {
                var (cellX, cellY) = CellAt(e, viewModel);
                viewModel.UpdateMoveDrag(cellX, cellY);
                return;
            }

            if (_rectangleDragOrigin is { } origin && sender is Control canvas)
            {
                if (!_isRectangleDragging)
                {
                    var travelled = e.GetPosition(canvas) - origin;
                    if (Math.Abs(travelled.X) < DragThreshold && Math.Abs(travelled.Y) < DragThreshold)
                        return;

                    _isRectangleDragging = true;
                    viewModel.BeginSelectionDrag(_rectangleDragStartCell.X, _rectangleDragStartCell.Y, _rectangleDragAddsToSelection);
                }

                var (cellX, cellY) = CellAt(e, viewModel);
                viewModel.UpdateSelectionDrag(cellX, cellY);
                return;
            }
        }

        _viewport.Pan(e);
    }

    private void OnMapCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left && DataContext is MapEditorViewModel viewModel)
        {
            if (_isDrawing)
            {
                viewModel.EndStroke();
                ResetDrag();
                return;
            }

            if (_isMoveDragging)
            {
                var (cellX, cellY) = CellAt(e, viewModel);
                viewModel.UpdateMoveDrag(cellX, cellY);
                viewModel.CommitMoveDrag();
                ResetDrag();
                return;
            }

            if (_rectangleDragOrigin is not null)
            {
                if (_isRectangleDragging)
                    viewModel.EndSelectionDrag();
                else
                    viewModel.HandleCanvasClick(_rectangleDragStartCell.X, _rectangleDragStartCell.Y, _rectangleDragAddsToSelection);

                ResetDrag();
                return;
            }
        }

        if (_viewport.IsPanning)
            _viewport.EndPan(e.Pointer);
    }

    private void OnMapCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel)
        {
            if (_isDrawing)
                viewModel.EndStroke();
            if (_isMoveDragging)
                viewModel.EndMoveDrag();
            if (_isRectangleDragging)
                viewModel.EndSelectionDrag();
        }

        ClearDragState();
        _viewport.CancelPan();
    }

    private void ResetDrag()
    {
        var pointer = _dragPointer;
        ClearDragState();
        pointer?.Capture(null);
    }

    private void ClearDragState()
    {
        _isDrawing = false;
        _isMoveDragging = false;
        _isRectangleDragging = false;
        _rectangleDragOrigin = null;
        _dragPointer = null;
    }

    // Cells outside the map come out negative or too large; the tools ignore them.
    private (int X, int Y) CellAt(PointerEventArgs e, MapEditorViewModel viewModel)
    {
        var position = e.GetPosition(MapCanvasContent);
        return ((int)Math.Floor(position.X / viewModel.TileWidth), (int)Math.Floor(position.Y / viewModel.TileHeight));
    }

    private void OnMapCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MapEditorViewModel viewModel)
            return;

        var zoomIn = e.Delta.Y > 0;
        _viewport.AnchoredZoom(e.GetPosition(MapCanvasScrollViewer), () =>
        {
            if (zoomIn) viewModel.ZoomIn(); else viewModel.ZoomOut();
        });
    }

    private async void OnCreateMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapEditorViewModel viewModel || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var dialog = new CreateMapDialog(new CreateMapDialogViewModel(
            viewModel.CreateUniqueName(), viewModel.TilesetNames, viewModel.PreferredTilesetName ?? viewModel.TilesetNames[0], viewModel.Maps.Select(map => map.Name).ToList()));
        if (await dialog.ShowDialog<NewMapSettings?>(owner) is { } settings)
            viewModel.CreateMap(settings.Name, settings.Width, settings.Height, settings.TilesetName);
    }

    private void OnMapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel)
            MapNameBox.Text = viewModel.SelectedMap?.Name;
    }

    private void OnRenameMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel && viewModel.RenameSelectedMap(MapNameBox.Text ?? string.Empty))
            MapNameBox.Text = viewModel.SelectedMap?.Name;
    }

    private async void OnResizeMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapEditorViewModel { SelectedMap: { } map } viewModel || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var dialog = new ResizeMapDialog(new ResizeMapDialogViewModel(map.Width, map.Height));
        if (await dialog.ShowDialog<(int Width, int Height)?>(owner) is not { } size)
            return;

        var lostTiles = viewModel.CountTilesLostByResize(size.Width, size.Height);
        if (lostTiles > 0)
        {
            var confirmed = await new ConfirmDialog(
                "Resize map",
                $"Shrinking the map '{map.Name}' removes {lostTiles} {(lostTiles == 1 ? "tile" : "tiles")}. Resize anyway?",
                "Resize").ShowDialog<bool>(owner);
            if (!confirmed)
                return;
        }

        viewModel.ResizeSelectedMap(size.Width, size.Height);
    }

    private async void OnDeleteMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapEditorViewModel { SelectedMap: { } map } viewModel || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await new ConfirmDialog(
            "Delete map", $"Delete the map '{map.Name}' from the project?", "Delete").ShowDialog<bool>(owner);
        if (confirmed)
            viewModel.DeleteSelectedMap();
    }
}
