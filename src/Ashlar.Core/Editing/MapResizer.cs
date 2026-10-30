using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class MapResizer
{
    // Anchored at the top-left cell: growing adds empty cells on the right and
    // bottom, shrinking crops there. Returns a new map and leaves the original
    // untouched, so a resize can be recorded as a step with both maps.
    public static TileMap Resize(TileMap map, int newWidth, int newHeight)
    {
        var resized = TileMap.CreateEmpty(map.Name, map.TilesetName, newWidth, newHeight);
        for (var y = 0; y < Math.Min(map.Height, newHeight); y++)
            for (var x = 0; x < Math.Min(map.Width, newWidth); x++)
                resized[x, y] = map[x, y];

        return resized;
    }

    // How many tiles a resize to the given size would cut off.
    public static int CountLostTiles(TileMap map, int newWidth, int newHeight)
    {
        var lost = 0;
        for (var y = 0; y < map.Height; y++)
            for (var x = 0; x < map.Width; x++)
                if ((x >= newWidth || y >= newHeight) && map[x, y] is not null)
                    lost++;

        return lost;
    }
}
