using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class DrawnTileCreator
{
    // The tile a painted pixel brings into existence: a whole grid cell, named
    // after the highest number the default group has used so far. Null when a
    // tile already sits in that cell or the cell is not completely inside the
    // image (the grid builder skips those partial cells as well).
    public static TileRegion? CreateTile(
        IReadOnlyList<TileRegion> tiles, int pixelX, int pixelY,
        int tileWidth, int tileHeight, int imageWidth, int imageHeight, string defaultGroupName)
    {
        var x = (int)Math.Floor((double)pixelX / tileWidth) * tileWidth;
        var y = (int)Math.Floor((double)pixelY / tileHeight) * tileHeight;
        if (x < 0 || y < 0 || x + tileWidth > imageWidth || y + tileHeight > imageHeight)
            return null;

        if (tiles.Any(t => t.X == x && t.Y == y))
            return null;

        var number = TileGroupAssigner.HighestNumber(tiles, defaultGroupName) + 1;
        return new TileRegion(TileGroupAssigner.FormatName(defaultGroupName, number), x, y, tileWidth, tileHeight);
    }
}
