using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

// Tiles is the resulting tile list; Moved holds the moving tiles that
// survived, i.e. the ones that stayed inside the tileset.
public record MoveResult(IReadOnlyList<TileRegion> Tiles, IReadOnlyList<TileRegion> Moved);

public static class TileMover
{
    // All moving tiles shift by the same offset at once, so the outcome never
    // depends on processing order. Unselected tiles on a destination cell are
    // overwritten, and moving tiles that end up outside the image are deleted.
    public static MoveResult Move(
        IReadOnlyList<TileRegion> tiles, IReadOnlyList<TileRegion> moving,
        int offsetX, int offsetY, int imageWidth, int imageHeight)
    {
        var movingSet = moving.ToHashSet();
        var moved = moving
            .Select(t => t with { X = t.X + offsetX, Y = t.Y + offsetY })
            .Where(t => IsInside(t, imageWidth, imageHeight))
            .ToList();
        var movedByName = moved.ToDictionary(t => t.Name);
        var destinationCells = moved.Select(t => (t.X, t.Y)).ToHashSet();

        var result = new List<TileRegion>(tiles.Count);
        foreach (var tile in tiles)
        {
            if (movingSet.Contains(tile))
            {
                if (movedByName.TryGetValue(tile.Name, out var movedTile))
                    result.Add(movedTile);
            }
            else if (!destinationCells.Contains((tile.X, tile.Y)))
            {
                result.Add(tile);
            }
        }

        return new MoveResult(result, moved);
    }

    public static bool IsInside(TileRegion tile, int imageWidth, int imageHeight) =>
        tile.X >= 0 && tile.Y >= 0 && tile.X + tile.Width <= imageWidth && tile.Y + tile.Height <= imageHeight;
}
