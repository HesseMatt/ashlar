using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TileEdit.Core.Model;

namespace TileEdit.App.Controls;

public class TileGridOverlay : Control
{
    public static readonly StyledProperty<int> ImageWidthProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(ImageWidth));

    public static readonly StyledProperty<int> ImageHeightProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(ImageHeight));

    public static readonly StyledProperty<int> TileWidthProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(TileWidth));

    public static readonly StyledProperty<int> TileHeightProperty =
        AvaloniaProperty.Register<TileGridOverlay, int>(nameof(TileHeight));

    public static readonly StyledProperty<TileRegion?> SelectedTileProperty =
        AvaloniaProperty.Register<TileGridOverlay, TileRegion?>(nameof(SelectedTile));

    static TileGridOverlay()
    {
        AffectsRender<TileGridOverlay>(ImageWidthProperty, ImageHeightProperty, TileWidthProperty, TileHeightProperty, SelectedTileProperty);
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

    public TileRegion? SelectedTile
    {
        get => GetValue(SelectedTileProperty);
        set => SetValue(SelectedTileProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (TileWidth <= 0 || TileHeight <= 0)
            return;

        var pen = new Pen(Brushes.Red);

        for (var x = TileWidth; x < ImageWidth; x += TileWidth)
            context.DrawLine(pen, new Point(x, 0), new Point(x, ImageHeight));

        for (var y = TileHeight; y < ImageHeight; y += TileHeight)
            context.DrawLine(pen, new Point(0, y), new Point(ImageWidth, y));

        if (SelectedTile is { } selected)
        {
            var highlightPen = new Pen(Brushes.Yellow, 2);
            context.DrawRectangle(highlightPen, new Rect(selected.X, selected.Y, selected.Width, selected.Height));
        }
    }
}
