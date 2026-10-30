using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapTileCleaner
{
    // Empties every cell that references one of the removed tiles, in the maps
    // of the given tileset only. Returns the number of cells that were cleared.
    public static int ClearTiles(IEnumerable<TileMap> maps, string tilesetName, IReadOnlySet<string> removedTileNames)
    {
        var cleared = 0;
        foreach (var map in maps.Where(map => map.TilesetName == tilesetName))
        {
            for (var i = 0; i < map.Cells.Length; i++)
            {
                if (map.Cells[i] is { } name && removedTileNames.Contains(name))
                {
                    map.Cells[i] = null;
                    cleared++;
                }
            }
        }

        return cleared;
    }
}
