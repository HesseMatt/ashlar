using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapRectangleSelector
{
    // The rectangle spans from one cell to another, both inclusive and in either
    // order, so a rectangle that never left its start cell still selects that cell.
    // Corners outside the map are allowed; only cells inside the map are returned.
    public static IReadOnlyList<(int X, int Y)> CellsIn(TileMap map, int startX, int startY, int endX, int endY)
    {
        var left = Math.Max(0, Math.Min(startX, endX));
        var right = Math.Min(map.Width - 1, Math.Max(startX, endX));
        var top = Math.Max(0, Math.Min(startY, endY));
        var bottom = Math.Min(map.Height - 1, Math.Max(startY, endY));

        var cells = new List<(int X, int Y)>();
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
                cells.Add((x, y));
        }

        return cells;
    }
}
