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

    // The top-left-most column and row (as cell indices) that hold content, found independently,
    // so a tileset with an empty border can be previewed from where its content begins.
    public static (int Column, int Row) FirstContentCell(EditableTilesetImage image, int tileWidth, int tileHeight)
    {
        var firstColumn = int.MaxValue;
        var firstRow = int.MaxValue;

        for (var row = 0; (row + 1) * tileHeight <= image.Height; row++)
        {
            for (var column = 0; (column + 1) * tileWidth <= image.Width; column++)
            {
                if (image.IsRegionEmpty(column * tileWidth, row * tileHeight, tileWidth, tileHeight))
                    continue;

                firstColumn = Math.Min(firstColumn, column);
                firstRow = Math.Min(firstRow, row);
            }
        }

        return firstColumn == int.MaxValue ? (0, 0) : (firstColumn, firstRow);
    }

    // Cells of a block of the given size starting at a cell, row by row; cells outside the image are left out.
    public static IReadOnlyList<(int X, int Y)> BlockCellPositions(
        int imageWidth, int imageHeight, int tileWidth, int tileHeight,
        int firstColumn, int firstRow, int columns, int rows)
    {
        var positions = new List<(int X, int Y)>();

        for (var row = firstRow; row < firstRow + rows && (row + 1) * tileHeight <= imageHeight; row++)
        {
            for (var column = firstColumn; column < firstColumn + columns && (column + 1) * tileWidth <= imageWidth; column++)
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
