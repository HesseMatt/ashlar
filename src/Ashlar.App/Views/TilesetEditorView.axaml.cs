using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class TilesetEditorView : UserControl
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

    // A rectangle press is pending before the drag threshold is crossed, which
    // the view model does not know about yet.
    private bool IsPointerDragPending => _rectangleDragOrigin is not null || _isMoveDragging || _isDrawing || _isPicking;

    // Screen distance the pointer has to travel before a press counts as a drag
    // rather than a click.
    private const double DragThreshold = 4;

    public TilesetEditorView()
    {
        InitializeComponent();

        TileCanvasScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not TilesetEditorViewModel editor)
            return;

        editor.PropertyChanged += OnViewModelPropertyChanged;
        UpdateInterpolation(editor);
        editor.TilesetLoaded += (_, _) => ScrollToTilesetStart();
        editor.PreviewPixelsChanged += (_, _) =>
        {
            TilesetImage.InvalidateVisual();
            TileOverlay.InvalidateVisual();
        };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TilesetEditorViewModel.PreviewInterpolation) && sender is TilesetEditorViewModel editor)
            UpdateInterpolation(editor);

        if (e.PropertyName is nameof(TilesetEditorViewModel.ZoomLevel) or nameof(TilesetEditorViewModel.TileWidth)
            or nameof(TilesetEditorViewModel.TileHeight) or nameof(TilesetEditorViewModel.ImageWidth)
            or nameof(TilesetEditorViewModel.ImageHeight) or nameof(TilesetEditorViewModel.HasImage))
        {
            UpdateOverscrollPadding();
        }
    }

    private void UpdateInterpolation(TilesetEditorViewModel editor)
    {
        RenderOptions.SetBitmapInterpolationMode(TilesetImage, editor.PreviewInterpolation);
        RenderOptions.SetBitmapInterpolationMode(TileOverlay, editor.PreviewInterpolation);
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
        if (DataContext is not TilesetEditorViewModel editor || !editor.HasImage)
            return;

        var viewport = TileCanvasScrollViewer.Viewport;
        var horizontal = OverscrollPadding(viewport.Width, editor.ScaledImageWidth, editor.TileWidth * editor.ZoomLevel);
        var vertical = OverscrollPadding(viewport.Height, editor.ScaledImageHeight, editor.TileHeight * editor.ZoomLevel);
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

    // Handles the editing shortcuts.
    public void HandleKeyDown(KeyEventArgs e)
    {
        if (DataContext is not TilesetEditorViewModel editor)
            return;

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        switch (e.Key)
        {
            case Key.R when ctrl && !shift && editor.HasImage:
                e.Handled = true;
                editor.IsRectangleToolActive = !editor.IsRectangleToolActive;
                break;
            case Key.M when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && editor.HasImage:
                e.Handled = true;
                editor.IsMoveToolActive = !editor.IsMoveToolActive;
                break;
            case Key.B when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && editor.HasImage:
                e.Handled = true;
                editor.IsDrawToolActive = !editor.IsDrawToolActive;
                break;
            case Key.G when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && editor.HasImage:
                e.Handled = true;
                editor.IsBucketToolActive = !editor.IsBucketToolActive;
                break;
            case Key.I when e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && editor.HasImage:
                e.Handled = true;
                editor.IsPipetteToolActive = !editor.IsPipetteToolActive;
                break;
            case Key.Z when ctrl && shift && e.Source is not TextBox && !IsPointerDragPending:
            case Key.Y when ctrl && e.Source is not TextBox && !IsPointerDragPending:
                e.Handled = true;
                editor.Redo();
                break;
            case Key.Z when ctrl && e.Source is not TextBox && !IsPointerDragPending:
                e.Handled = true;
                editor.Undo();
                break;
            case Key.Escape when _rectangleDragOrigin is not null:
                e.Handled = true;
                editor.CancelSelectionDrag();
                ResetRectangleDrag();
                break;
            case Key.Escape when _isMoveDragging:
                e.Handled = true;
                editor.EndMoveDrag();
                ResetMoveDrag();
                break;
            case Key.Escape when _isDrawing:
                e.Handled = true;
                editor.CancelStroke();
                ResetDraw();
                break;
            case Key.Delete when editor.HasSelectedTiles:
                e.Handled = true;
                editor.DeleteSelectedTiles();
                break;
            case Key.OemPlus when ctrl && editor.HasImage:
                e.Handled = true;
                ZoomIn();
                break;
            case Key.OemMinus when ctrl && editor.HasImage:
                e.Handled = true;
                ZoomOut();
                break;
            case Key.D0 when ctrl && editor.HasImage:
                e.Handled = true;
                ResetZoom();
                break;
        }
    }

    private void OnTileCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control canvas)
            return;

        if (DataContext is not TilesetEditorViewModel editor)
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

        if (editor.IsMoveToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (startX, startY) = ImagePixelAt(e);
                if (editor.BeginMoveDrag(startX, startY))
                {
                    _isMoveDragging = true;
                    _moveDragPointer = e.Pointer;
                    e.Pointer.Capture(canvas);
                }
            }

            return;
        }

        if (editor.IsDrawToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (startX, startY) = ImagePixelAt(e);
                if (editor.BeginStroke(startX, startY))
                {
                    _isDrawing = true;
                    _drawPointer = e.Pointer;
                    e.Pointer.Capture(canvas);
                }
            }

            return;
        }

        if (editor.IsBucketToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (fillX, fillY) = ImagePixelAt(e);
                editor.Fill(fillX, fillY);
            }

            return;
        }

        if (editor.IsPipetteToolActive)
        {
            if (e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
            {
                var (pickX, pickY) = ImagePixelAt(e);
                editor.PickColor(pickX, pickY);
                _isPicking = true;
                _pickPointer = e.Pointer;
                e.Pointer.Capture(canvas);
            }

            return;
        }

        if (editor.IsRectangleToolActive && e.GetCurrentPoint(canvas).Properties.IsLeftButtonPressed)
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
        editor.HandleCanvasClick(pixelX, pixelY, e.KeyModifiers.HasFlag(KeyModifiers.Control));
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
        if (_isPicking && DataContext is TilesetEditorViewModel pickViewModel)
        {
            var (pickPixelX, pickPixelY) = ImagePixelAt(e);
            pickViewModel.PickColor(pickPixelX, pickPixelY);
            return;
        }

        if (_isDrawing && DataContext is TilesetEditorViewModel drawViewModel)
        {
            var (drawPixelX, drawPixelY) = ImagePixelAt(e);
            drawViewModel.ContinueStroke(drawPixelX, drawPixelY);
            return;
        }

        if (_isMoveDragging && DataContext is TilesetEditorViewModel moveViewModel)
        {
            var (movePixelX, movePixelY) = ImagePixelAt(e);
            moveViewModel.UpdateMoveDrag(movePixelX, movePixelY);
            return;
        }

        if (_rectangleDragOrigin is { } dragOrigin && sender is Control canvas && DataContext is TilesetEditorViewModel editor)
        {
            if (!_isRectangleDragging)
            {
                var travelled = e.GetPosition(canvas) - dragOrigin;
                if (Math.Abs(travelled.X) < DragThreshold && Math.Abs(travelled.Y) < DragThreshold)
                    return;

                _isRectangleDragging = true;
                editor.BeginSelectionDrag(_rectangleDragStartPixel.X, _rectangleDragStartPixel.Y, _rectangleDragAddsToSelection);
            }

            var (pixelX, pixelY) = ImagePixelAt(e);
            editor.UpdateSelectionDrag(pixelX, pixelY);
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

        if (_isDrawing && e.InitialPressMouseButton == MouseButton.Left && DataContext is TilesetEditorViewModel drawViewModel)
        {
            drawViewModel.CommitStroke();
            ResetDraw();
            return;
        }

        if (_isMoveDragging && e.InitialPressMouseButton == MouseButton.Left && DataContext is TilesetEditorViewModel moveViewModel)
        {
            moveViewModel.CommitMoveDrag();
            ResetMoveDrag();
            return;
        }

        if (_rectangleDragOrigin is not null && e.InitialPressMouseButton == MouseButton.Left
            && DataContext is TilesetEditorViewModel editor)
        {
            if (_isRectangleDragging)
                editor.EndSelectionDrag();
            else
                editor.HandleCanvasClick(_rectangleDragStartPixel.X, _rectangleDragStartPixel.Y, _rectangleDragAddsToSelection);

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
        if (_isDrawing && DataContext is TilesetEditorViewModel drawViewModel)
            drawViewModel.CommitStroke();

        _isDrawing = false;
        _drawPointer = null;
        _isPicking = false;
        _pickPointer = null;

        if (_isRectangleDragging && DataContext is TilesetEditorViewModel editor)
            editor.EndSelectionDrag();

        if (_isMoveDragging && DataContext is TilesetEditorViewModel moveViewModel)
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

        if (DataContext is not TilesetEditorViewModel editor)
            return;

        var anchorPosition = e.GetPosition(TileCanvasScrollViewer);
        var zoomIn = e.Delta.Y > 0;
        AnchoredZoom(anchorPosition, () =>
        {
            if (zoomIn) editor.ZoomIn(); else editor.ZoomOut();
        });
    }

    public void ZoomIn()
    {
        if (DataContext is not TilesetEditorViewModel editor)
            return;

        AnchoredZoom(ViewportCenter(), () => editor.ZoomIn());
    }

    public void ZoomOut()
    {
        if (DataContext is not TilesetEditorViewModel editor)
            return;

        AnchoredZoom(ViewportCenter(), () => editor.ZoomOut());
    }

    public void ResetZoom()
    {
        if (DataContext is not TilesetEditorViewModel editor)
            return;

        editor.ResetZoom();
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

    private void OnDeleteSelectedTilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TilesetEditorViewModel editor)
            editor.DeleteSelectedTiles();
    }

    private void OnGroupNameInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        if (DataContext is TilesetEditorViewModel editor)
            editor.ConfirmGroupNameInput();
    }

    private void OnGroupNameSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is TilesetEditorViewModel editor)
            editor.ConfirmGroupNameInput();
    }
}
