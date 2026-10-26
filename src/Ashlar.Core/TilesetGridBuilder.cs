using System.Text.RegularExpressions;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core;

public static class TilesetGridBuilder
{
    private static readonly Regex InvalidNameCharacters = new(@"[^a-zA-Z0-9_]");

    public static IReadOnlyList<TileRegion> Build(string textureFileName, EditableTilesetImage image, int tileWidth, int tileHeight)
    {
        var baseName = SanitizeBaseName(Path.GetFileNameWithoutExtension(textureFileName));

        var tiles = new List<TileRegion>();
        var index = 1;

        for (var y = 0; y + tileHeight <= image.Height; y += tileHeight)
        {
            for (var x = 0; x + tileWidth <= image.Width; x += tileWidth)
            {
                // The index counts every cell, so skipped cells leave gaps in the numbering.
                if (!image.IsRegionEmpty(x, y, tileWidth, tileHeight))
                {
                    var name = TileGroupAssigner.FormatName(baseName, index);
                    tiles.Add(new TileRegion(name, x, y, tileWidth, tileHeight));
                }

                index++;
            }
        }

        return tiles;
    }

    // The same cells Build walks, in the same order, but without skipping empty ones.
    public static IReadOnlyList<(int X, int Y)> FirstCellPositions(
        int imageWidth, int imageHeight, int tileWidth, int tileHeight, int count)
    {
        var positions = new List<(int X, int Y)>();

        for (var y = 0; y + tileHeight <= imageHeight; y += tileHeight)
        {
            for (var x = 0; x + tileWidth <= imageWidth; x += tileWidth)
            {
                if (positions.Count == count)
                    return positions;

                positions.Add((x, y));
            }
        }

        return positions;
    }

    public static string SanitizeBaseName(string name)
    {
        var withUnderscores = name.Replace(' ', '_');
        return InvalidNameCharacters.Replace(withUnderscores, string.Empty);
    }
}
