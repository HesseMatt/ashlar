using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Ashlar.Core.Model;

namespace Ashlar.App.Controls;

// Draws the cells of a map that have a tile; empty cells are left transparent
// so the checkerboard underneath shows through.
public class TileMapCanvas : Control
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<TileMapCanvas, IImage?>(nameof(Image));

    public static readonly StyledProperty<TileRegion?[]> CellRegionsProperty =
        AvaloniaProperty.Register<TileMapCanvas, TileRegion?[]>(nameof(CellRegions), defaultValue: []);

    public static readonly StyledProperty<int> MapWidthProperty =
        AvaloniaProperty.Register<TileMapCanvas, int>(nameof(MapWidth));

    public static readonly StyledProperty<int> TileWidthProperty =
        AvaloniaProperty.Register<TileMapCanvas, int>(nameof(TileWidth));

    public static readonly StyledProperty<int> TileHeightProperty =
        AvaloniaProperty.Register<TileMapCanvas, int>(nameof(TileHeight));

    public static readonly StyledProperty<Rect?> VisibleAreaProperty =
        AvaloniaProperty.Register<TileMapCanvas, Rect?>(nameof(VisibleArea));

    static TileMapCanvas()
    {
        AffectsRender<TileMapCanvas>(
            ImageProperty, CellRegionsProperty, MapWidthProperty, TileWidthProperty, TileHeightProperty, VisibleAreaProperty);
    }

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    // Row-major, MapWidth cells per row; null is an empty cell.
    public TileRegion?[] CellRegions
    {
        get => GetValue(CellRegionsProperty);
        set => SetValue(CellRegionsProperty, value);
    }

    public int MapWidth
    {
        get => GetValue(MapWidthProperty);
        set => SetValue(MapWidthProperty, value);
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

    // The part of the map currently scrolled into view, in map pixels. Null means "everything".
    public Rect? VisibleArea
    {
        get => GetValue(VisibleAreaProperty);
        set => SetValue(VisibleAreaProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Image is not { } image || MapWidth <= 0 || TileWidth <= 0 || TileHeight <= 0)
            return;

        var rows = CellRegions.Length / MapWidth;
        var firstColumn = 0;
        var lastColumn = MapWidth - 1;
        var firstRow = 0;
        var lastRow = rows - 1;
        if (VisibleArea is { } visible)
        {
            firstColumn = Math.Max(0, (int)Math.Floor(visible.Left / TileWidth));
            lastColumn = Math.Min(lastColumn, (int)Math.Floor(visible.Right / TileWidth));
            firstRow = Math.Max(0, (int)Math.Floor(visible.Top / TileHeight));
            lastRow = Math.Min(lastRow, (int)Math.Floor(visible.Bottom / TileHeight));
        }

        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                if (CellRegions[row * MapWidth + column] is not { } region)
                    continue;

                var source = new Rect(region.X, region.Y, region.Width, region.Height);
                var destination = new Rect(column * TileWidth, row * TileHeight, region.Width, region.Height);
                context.DrawImage(image, source, destination);
            }
        }
    }
}
