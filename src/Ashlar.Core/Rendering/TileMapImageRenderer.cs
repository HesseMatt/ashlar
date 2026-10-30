using Ashlar.Core.Model;

namespace Ashlar.Core.Rendering;

public static class TileMapImageRenderer
{
    // Cells that are empty, or whose tile no longer exists in the tileset, stay transparent.
    public static EditableTilesetImage Render(TileMap map, IEnumerable<TileRegion> tiles, EditableTilesetImage tilesetImage, int tileWidth, int tileHeight)
    {
        var result = EditableTilesetImage.CreateBlank(map.Width * tileWidth, map.Height * tileHeight);
        var regions = map.ResolveRegions(tiles);

        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (regions[y * map.Width + x] is not { } region)
                    continue;

                var pixels = tilesetImage.CopyRegion(region.X, region.Y, region.Width, region.Height);
                result.PasteRegion(pixels, x * tileWidth, y * tileHeight, region.Width, region.Height);
            }
        }

        return result;
    }
}
