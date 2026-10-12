using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TileEdit.Core.Model;

namespace TileEdit.App.Controls;

public class TileGridOverlay : Control
{
    private static readonly Pen GridPen = new(Brushes.Red);
    private static readonly Pen HighlightPen = new(Brushes.Yellow, 2);

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

    static TileGridOverlay()
    {
        AffectsRender<TileGridOverlay>(ImageWidthProperty, ImageHeightProperty, TileWidthProperty, TileHeightProperty, SelectedTilesProperty);
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
    }
}
