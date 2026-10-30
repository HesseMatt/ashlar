using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapEraser
{
    // Empties the given cells. Returns the cells that held a tile before.
    public static IReadOnlyList<(int X, int Y)> Clear(TileMap map, IEnumerable<(int X, int Y)> cells)
    {
        var cleared = new List<(int X, int Y)>();
        foreach (var (x, y) in cells)
        {
            if (x < 0 || x >= map.Width || y < 0 || y >= map.Height || map[x, y] is null)
                continue;

            map[x, y] = null;
            cleared.Add((x, y));
        }

        return cleared;
    }
}
