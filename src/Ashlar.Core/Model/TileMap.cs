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

    public TileMap(string name, string tilesetName, int width, int height, string?[] cells)
    {
        if (width < 1 || height < 1)
            throw new ArgumentOutOfRangeException(nameof(width), "A map must be at least 1x1 cells.");
        if (cells.Length != width * height)
            throw new ArgumentException("The cell count does not match the map size.", nameof(cells));

        Name = name;
        TilesetName = tilesetName;
        Width = width;
        Height = height;
        Cells = cells;
    }

    public static TileMap CreateEmpty(string name, string tilesetName, int width, int height) =>
        new(name, tilesetName, width, height, new string?[Math.Max(width, 0) * Math.Max(height, 0)]);

    public string? this[int x, int y]
    {
        get => Cells[y * Width + x];
        set => Cells[y * Width + x] = value;
    }
}
