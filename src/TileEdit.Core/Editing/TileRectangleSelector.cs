using TileEdit.Core.Model;

namespace TileEdit.Core.Editing;

public static class TileRectangleSelector
{
    // The rectangle spans from one image pixel to another, both inclusive and in
    // either order, so a rectangle that never left its start pixel still touches
    // exactly the tile under that pixel. The corners may lie outside the image.
    public static IReadOnlyList<TileRegion> TilesTouching(
        IEnumerable<TileRegion> tiles, int startX, int startY, int endX, int endY)
    {
        var left = Math.Min(startX, endX);
        var right = Math.Max(startX, endX);
        var top = Math.Min(startY, endY);
        var bottom = Math.Max(startY, endY);

        return tiles
            .Where(t => t.X <= right && t.X + t.Width > left && t.Y <= bottom && t.Y + t.Height > top)
            .ToList();
    }
}
