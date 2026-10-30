using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapPainter
{
    // Sets every cell on the line between the two cells (both included) to the
    // tile. Cells outside the map are skipped. Returns the cells whose content
    // actually changed.
    public static IReadOnlyList<(int X, int Y)> PaintLine(
        TileMap map, int fromX, int fromY, int toX, int toY, string tileName)
    {
        var changed = new List<(int X, int Y)>();
        foreach (var (x, y) in LineRasterizer.Line(fromX, fromY, toX, toY))
        {
            if (x < 0 || x >= map.Width || y < 0 || y >= map.Height || map[x, y] == tileName)
                continue;

            map[x, y] = tileName;
            changed.Add((x, y));
        }

        return changed;
    }
}
