using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapMover
{
    // The selected cells move as a block: they are read first and emptied, then
    // written at their new position, so the outcome never depends on the order
    // of the cells. Empty selected cells move too, which means they clear whatever
    // they land on. Cells that end up outside the map are dropped. Returns the
    // new positions of the cells that stayed inside.
    public static IReadOnlyList<(int X, int Y)> Move(
        TileMap map, IReadOnlyList<(int X, int Y)> cells, int offsetX, int offsetY)
    {
        var contents = cells.Select(cell => map[cell.X, cell.Y]).ToList();
        foreach (var (x, y) in cells)
            map[x, y] = null;

        var moved = new List<(int X, int Y)>();
        for (var i = 0; i < cells.Count; i++)
        {
            var x = cells[i].X + offsetX;
            var y = cells[i].Y + offsetY;
            if (x < 0 || x >= map.Width || y < 0 || y >= map.Height)
                continue;

            map[x, y] = contents[i];
            moved.Add((x, y));
        }

        return moved;
    }
}
