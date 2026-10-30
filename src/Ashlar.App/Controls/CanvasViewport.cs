using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Ashlar.App.Controls;

// Scrolling, panning and cursor-anchored zooming of a canvas that sits in a
// ScrollViewer. Shared by the tileset and the map editor so both behave the same.
public class CanvasViewport
{
    private readonly ScrollViewer _scrollViewer;
    private readonly Border _padding;
    private readonly Control _content;
    private Point? _panStartViewportPosition;
    private Vector _panStartOffset;

    // scrollViewer holds paddingBorder, which wraps the zoomed content. The border's
    // padding is the overscroll margin; content is the unscaled, transformed canvas.
    public CanvasViewport(ScrollViewer scrollViewer, Border paddingBorder, Control content)
    {
        _scrollViewer = scrollViewer;
        _padding = paddingBorder;
        _content = content;
    }

    public bool IsPanning => _panStartViewportPosition is not null;

    // Lets the view be panned past the canvas edges, but never so far that the
    // canvas leaves the viewport: at maximum pan, exactly one tile row/column
    // stays visible at the opposite edge. Only applies on an axis where the
    // zoomed canvas overflows the viewport; otherwise it stays centered.
    public void UpdateOverscrollPadding(double scaledWidth, double scaledHeight, double scaledTileWidth, double scaledTileHeight)
    {
        var viewport = _scrollViewer.Viewport;
        var horizontal = OverscrollPadding(viewport.Width, scaledWidth, scaledTileWidth);
        var vertical = OverscrollPadding(viewport.Height, scaledHeight, scaledTileHeight);
        _padding.Padding = new Thickness(horizontal, vertical);
    }

    private static double OverscrollPadding(double viewportSize, double scaledSize, double scaledTileSize) =>
        scaledSize > viewportSize ? Math.Max(0, viewportSize - scaledTileSize) : 0;

    // Scrolls so the canvas's top-left corner sits at the viewport's top-left
    // corner, which is where the overscroll padding ends. An axis that fits into
    // the viewport has no padding and no scroll range, so it stays centered.
    public void ScrollToStart(double scaledWidth, double scaledHeight, double scaledTileWidth, double scaledTileHeight)
    {
        UpdateOverscrollPadding(scaledWidth, scaledHeight, scaledTileWidth, scaledTileHeight);
        _scrollViewer.GetLayoutManager()?.ExecuteLayoutPass();

        var padding = _padding.Padding;
        _scrollViewer.Offset = ClampOffset(new Vector(padding.Left, padding.Top));
    }

    public void BeginPan(PointerPressedEventArgs e, Control capturingControl)
    {
        _panStartViewportPosition = e.GetPosition(_scrollViewer);
        _panStartOffset = _scrollViewer.Offset;
        e.Pointer.Capture(capturingControl);
        e.Handled = true;
    }

    public void Pan(PointerEventArgs e)
    {
        if (_panStartViewportPosition is not { } startPosition)
            return;

        var currentPosition = e.GetPosition(_scrollViewer);
        _scrollViewer.Offset = ClampOffset(_panStartOffset + (startPosition - currentPosition));
    }

    public void EndPan(IPointer pointer)
    {
        _panStartViewportPosition = null;
        pointer.Capture(null);
    }

    // The pointer capture is already gone when this is called.
    public void CancelPan() => _panStartViewportPosition = null;

    public Point ViewportCenter() => new(_scrollViewer.Viewport.Width / 2, _scrollViewer.Viewport.Height / 2);

    // Keeps the canvas point under anchorViewportPosition at that same screen
    // position across a zoom change. This goes through Avalonia's own
    // coordinate-transform APIs (TranslatePoint) rather than reconstructing
    // the Offset/RenderTransform math by hand, because the canvas is
    // center-aligned by the ScrollViewer whenever it's smaller than the
    // viewport (i.e. at low zoom) - a hand-rolled "offset + position" formula
    // silently ignores that alignment and anchors to the wrong canvas point.
    public void AnchoredZoom(Point anchorViewportPosition, Action applyZoomChange)
    {
        var contentPoint = _scrollViewer.TranslatePoint(anchorViewportPosition, _content);

        applyZoomChange();
        _scrollViewer.GetLayoutManager()?.ExecuteLayoutPass();

        if (contentPoint is not { } anchorContentPoint)
            return;

        var renderedAt = _content.TranslatePoint(anchorContentPoint, _scrollViewer);
        if (renderedAt is not { } actualPosition)
            return;

        var drift = anchorViewportPosition - actualPosition;
        _scrollViewer.Offset = ClampOffset(_scrollViewer.Offset - drift);
    }

    private Vector ClampOffset(Vector offset)
    {
        var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        return new Vector(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }
}
