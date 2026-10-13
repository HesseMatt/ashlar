using TileEdit.Core.Model;

namespace TileEdit.Core.Editing;

public record TilePlacement(TileRegion Original, TileRegion Moved);

public record CompactionResult(IReadOnlyList<TilePlacement> Placements, int Width, int Height);

public static class TilesetCompactor
{
    // Keeps the current column count, so the canvas only shrinks in width when
    // there are fewer tiles than columns.
    public static CompactionResult Compact(IReadOnlyList<TileRegion> tiles, int tileWidth, int tileHeight, int imageWidth, string defaultGroupName)
    {
        var columns = Math.Max(1, imageWidth / tileWidth);
        var ordered = OrderForPacking(tiles, defaultGroupName);

        var placements = new List<TilePlacement>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var moved = ordered[i] with { X = i % columns * tileWidth, Y = i / columns * tileHeight };
            placements.Add(new TilePlacement(ordered[i], moved));
        }

        var usedColumns = Math.Min(ordered.Count, columns);
        var usedRows = (ordered.Count + columns - 1) / columns;
        return new CompactionResult(placements, usedColumns * tileWidth, usedRows * tileHeight);
    }

    // A group's members are emitted together at the point where the group is
    // first encountered; everything else keeps its row-by-row order.
    private static List<TileRegion> OrderForPacking(IReadOnlyList<TileRegion> tiles, string defaultGroupName)
    {
        var layoutOrder = tiles.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
        var membersByGroup = layoutOrder
            .Where(t => TileGroupAssigner.GetGroupName(t, defaultGroupName) is not null)
            .ToLookup(t => TileGroupAssigner.GetGroupName(t, defaultGroupName)!);
        var emittedGroups = new HashSet<string>();
        var result = new List<TileRegion>(tiles.Count);

        foreach (var tile in layoutOrder)
        {
            var group = TileGroupAssigner.GetGroupName(tile, defaultGroupName);
            if (group is null)
                result.Add(tile);
            else if (emittedGroups.Add(group))
                result.AddRange(membersByGroup[group]);
        }

        return result;
    }
}
