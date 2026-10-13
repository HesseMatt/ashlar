using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.App.Controls;

public class TileGridOverlay : Control
{
    private static readonly Pen GridPen = new(Brushes.Black, 0.5);
    private static readonly Pen HighlightPen = new(Brushes.Yellow, 2);
    private static readonly Pen SelectionPen = new(Brushes.DeepSkyBlue, 1, DashStyle.Dash);
    private static readonly Pen MovePen = new(Brushes.LimeGreen, 2);
    private static readonly Pen MoveOutsidePen = new(Brushes.Red, 2);
    private static readonly IBrush MoveOutsideFill = new SolidColorBrush(Colors.Red, 0.35);
    private static readonly IBrush SelectionFill = new SolidColorBrush(Colors.DeepSkyBlue, 0.2);

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

    static TileGridOverlay()
    {
        AffectsRender<TileGridOverlay>(
            ImageWidthProperty, ImageHeightProperty, TileWidthProperty, TileHeightProperty, SelectedTilesProperty,
            SelectionRectangleProperty, MoveOffsetProperty, ImageProperty);
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

    public override void Render(DrawingContext context)
    {
        if (TileWidth <= 0 || TileHeight <= 0)
            return;

        for (var x = TileWidth; x < ImageWidth; x += TileWidth)
            context.DrawLine(GridPen, new Point(x, 0), new Point(x, ImageHeight));

        for (var y = TileHeight; y < ImageHeight; y += TileHeight)
            context.DrawLine(GridPen, new Point(0, y), new Point(ImageWidth, y));

        foreach (var selected in SelectedTiles)
            context.DrawRectangle(HighlightPen, new Rect(selected.X, selected.Y, selected.Width, selected.Height));

        if (MoveOffset is { } offset)
            RenderMovePreview(context, offset);

        if (SelectionRectangle is { } rectangle)
            context.DrawRectangle(SelectionFill, SelectionPen, rectangle);
    }

    private void RenderMovePreview(DrawingContext context, Vector offset)
    {
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

                context.DrawRectangle(MovePen, destination);
            }
            else
            {
                context.DrawRectangle(MoveOutsideFill, MoveOutsidePen, destination);
            }
        }
    }
}
