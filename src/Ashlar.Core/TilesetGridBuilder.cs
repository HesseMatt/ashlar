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
    // Only the top-left block of the given number of columns and rows is listed.
    public static IReadOnlyList<(int X, int Y)> TopLeftCellPositions(
        int imageWidth, int imageHeight, int tileWidth, int tileHeight, int columns, int rows)
    {
        var positions = new List<(int X, int Y)>();

        for (var row = 0; row < rows && (row + 1) * tileHeight <= imageHeight; row++)
        {
            for (var column = 0; column < columns && (column + 1) * tileWidth <= imageWidth; column++)
                positions.Add((column * tileWidth, row * tileHeight));
        }

        return positions;
    }

    public static string SanitizeBaseName(string name)
    {
        var withUnderscores = name.Replace(' ', '_');
        return InvalidNameCharacters.Replace(withUnderscores, string.Empty);
    }
}
