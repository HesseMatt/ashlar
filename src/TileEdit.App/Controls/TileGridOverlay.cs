using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.App.Controls;

public class TileGridOverlay : Control
{
    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<TileGridOverlay, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<TileGridOverlay, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentFillBrushProperty =
        AvaloniaProperty.Register<TileGridOverlay, IBrush?>(nameof(AccentFillBrush));

    public static readonly StyledProperty<int> ImageWidthProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(ImageWidth));

    public static readonly StyledProperty<int> ImageHeightProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(ImageHeight));

    public static readonly StyledProperty<int> TileWidthProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(TileWidth));

    public static readonly StyledProperty<int> TileHeightProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(TileHeight));

    public static readonly StyledProperty<IReadOnlyList<TileRegion>> SelectedTilesProperty =
        AvaloniaProperty.Register<TileGridOverlay, IReadOnlyList<TileRegion>>(nameof(SelectedTiles), defaultValue: []);

    public static readonly StyledProperty<Rect?> SelectionRectangleProperty =
        AvaloniaProperty.Register<TileGridOverlay, Rect?>(nameof(SelectionRectangle));

    public static readonly StyledProperty<Vector?> MoveOffsetProperty =
        AvaloniaProperty.Register<TileGridOverlay, Vector?>(nameof(MoveOffset));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<TileGridOverlay, IImage?>(nameof(Image));

    public static readonly StyledProperty<IBrush?> PixelGridBrushProperty =
        AvaloniaProperty.Register<TileGridOverlay, IBrush?>(nameof(PixelGridBrush));

    public static readonly StyledProperty<bool> ShowPixelGridProperty =
        AvaloniaProperty.Register<TileGridOverlay, bool>(nameof(ShowPixelGrid));

    public static readonly StyledProperty<double> ZoomLevelProperty =
        AvaloniaProperty.Register<TileGridOverlay, double>(nameof(ZoomLevel), defaultValue: 1.0);

    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<TileGridOverlay, Rect?>(nameof(VisibleArea));

    static TileGridOverlay()
    {
        AffectsRender<TileGridOverlay>(
            ImageWidthProperty, ImageHeightProperty, TileWidthProperty, TileHeightProperty, SelectedTilesProperty,
            SelectionRectangleProperty, MoveOffsetProperty, ImageProperty,
            GridBrushProperty, AccentBrushProperty, AccentFillBrushProperty,
            PixelGridBrushProperty, ShowPixelGridProperty, ZoomLevelProperty, VisibleAreaProperty);
    }

    public int ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public int ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public int TileWidth
    {
        get => GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    public int TileHeight
    {
        get => GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    public IReadOnlyList<TileRegion> SelectedTiles
    {
        get => GetValue(SelectedTilesProperty);
        set => SetValue(SelectedTilesProperty, value);
    }

    public Rect? SelectionRectangle
    {
        get => GetValue(SelectionRectangleProperty);
        set => SetValue(SelectionRectangleProperty, value);
    }

    public Vector? MoveOffset
    {
        get => GetValue(MoveOffsetProperty);
        set => SetValue(MoveOffsetProperty, value);
    }

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? PixelGridBrush
    {
        get => GetValue(PixelGridBrushProperty);
        set => SetValue(PixelGridBrushProperty, value);
    }

    public bool ShowPixelGrid
    {
        get => GetValue(ShowPixelGridProperty);
        set => SetValue(ShowPixelGridProperty, value);
    }

    public double ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    // The part of the image currently scrolled into view, in image pixels. Null
    // means "everything".
    public Rect? VisibleArea
    {
        get => GetValue(VisibleAreaProperty);
        set => SetValue(VisibleAreaProperty, value);
    }

    public IBrush? AccentBrush
    {
        get => GetValue(AccentBrushProperty);
        set => SetValue(AccentBrushProperty, value);
    }

    public IBrush? AccentFillBrush
    {
        get => GetValue(AccentFillBrushProperty);
        set => SetValue(AccentFillBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (TileWidth <= 0 || TileHeight <= 0)
            return;

        if (ShowPixelGrid)
            RenderPixelGrid(context);

        var gridPen = new Pen(GridBrush, LineWidth(0.5));
        var highlightPen = new Pen(AccentBrush, LineWidth(1));

        for (var x = TileWidth; x < ImageWidth; x += TileWidth)
            context.DrawLine(gridPen, new Point(x, 0), new Point(x, ImageHeight));

        for (var y = TileHeight; y < ImageHeight; y += TileHeight)
            context.DrawLine(gridPen, new Point(0, y), new Point(ImageWidth, y));

        foreach (var selected in SelectedTiles)
            context.DrawRectangle(highlightPen, new Rect(selected.X, selected.Y, selected.Width, selected.Height));

        if (MoveOffset is { } offset)
            RenderMovePreview(context, offset);

        if (SelectionRectangle is { } rectangle)
            context.DrawRectangle(AccentFillBrush, new Pen(AccentBrush, LineWidth(1), DashStyle.Dash), rectangle);
    }

    // The overlay is scaled with the image, so at 32x a 2-unit line would be 64
    // screen pixels wide. Widths are given in image units and capped at 2 screen pixels.
    private double LineWidth(double imageUnits) => Math.Min(imageUnits, 2 / ZoomLevel);

    // Only the lines inside the visible area are drawn, so the cost depends on
    // the viewport and not on the image size.
    private void RenderPixelGrid(DrawingContext context)
    {
        var imageBounds = new Rect(0, 0, ImageWidth, ImageHeight);
        var area = VisibleArea is { } visible ? visible.Intersect(imageBounds) : imageBounds;
        var pen = new Pen(PixelGridBrush, 1 / ZoomLevel);

        for (var x = (int)Math.Ceiling(area.Left); x <= area.Right; x++)
            context.DrawLine(pen, new Point(x, area.Top), new Point(x, area.Bottom));

        for (var y = (int)Math.Ceiling(area.Top); y <= area.Bottom; y++)
            context.DrawLine(pen, new Point(area.Left, y), new Point(area.Right, y));
    }

    private void RenderMovePreview(DrawingContext context, Vector offset)
    {
        var movePen = new Pen(AccentBrush, LineWidth(2));
        // Dashed outline plus a cross keeps "deleted" distinct from a normal move without another hue.
        var outsidePen = new Pen(AccentBrush, LineWidth(2), DashStyle.Dash);

        foreach (var tile in SelectedTiles)
        {
            var source = new Rect(tile.X, tile.Y, tile.Width, tile.Height);
            var destination = source.Translate(offset);
            var destinationTile = tile with { X = tile.X + (int)offset.X, Y = tile.Y + (int)offset.Y };

            if (TileMover.IsInside(destinationTile, ImageWidth, ImageHeight))
            {
                if (Image is { } image)
                {
                    using (context.PushOpacity(0.6))
                        context.DrawImage(image, source, destination);
                }

                context.DrawRectangle(movePen, destination);
            }
            else
            {
                using (context.PushOpacity(0.5))
                    context.DrawRectangle(AccentFillBrush, outsidePen, destination);

                context.DrawLine(outsidePen, destination.TopLeft, destination.BottomRight);
                context.DrawLine(outsidePen, destination.TopRight, destination.BottomLeft);
            }
        }
    }
}
