using System.Text.RegularExpressions;

namespace Ashlar.Core.Model;

public class TileMap
{
    public string Name { get; }

    // Fixed at creation: a map draws from exactly one tileset.
    public string TilesetName { get; }

    public int Width { get; }

    public int Height { get; }

    // Row-major; null is an empty cell. Tiles are referenced by their stable name.
    public string?[] Cells { get; }

    // "#RRGGBB" drawn behind the cells in the map editor; null shows the transparency checkerboard.
    // A view setting that is saved with the project; exports ignore it.
    public string? BackgroundColor { get; }

    public TileMap(string name, string tilesetName, int width, int height, string?[] cells, string? backgroundColor = null)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "A map must be at least 1x1 cells.");
        if (cells.Length != width * height)
            throw new ArgumentException("The cell count does not match the map size.", nameof(cells));
        if (backgroundColor is not null && !Regex.IsMatch(backgroundColor, "^#[0-9A-Fa-f]{6}$"))
            throw new ArgumentException("The background color must look like #RRGGBB.", nameof(backgroundColor));

        Name = name;
        TilesetName = tilesetName;
        Width = width;
        Height = height;
        Cells = cells;
        BackgroundColor = backgroundColor;
    }

    public static TileMap CreateEmpty(string name, string tilesetName, int width, int height) =>
        new(name, tilesetName, width, height, new string?[Math.Max(width, 0) * Math.Max(height, 0)]);

    public TileMap WithBackgroundColor(string? backgroundColor) =>
        new(Name, TilesetName, Width, Height, Cells, backgroundColor);

    public string? this[int x, int y]
    {
        get => Cells[y * Width + x];
        set => Cells[y * Width + x] = value;
    }

    // The source region of every cell, row-major. Cells that are empty or whose
    // tile no longer exists in the tileset resolve to null.
    public TileRegion?[] ResolveRegions(IEnumerable<TileRegion> tiles)
    {
        var regionsByName = tiles.ToDictionary(tile => tile.Name);
        return Cells
            .Select(name => name is not null && regionsByName.TryGetValue(name, out var region) ? region : null)
            .ToArray();
    }
}
