using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

// The size and cells of a map. The cells are copied, so a state stays valid
// while the map keeps being edited in place.
public record MapState(int Width, int Height, string?[] Cells)
{
    public static MapState Capture(TileMap map) => new(map.Width, map.Height, (string?[])map.Cells.Clone());

    // A new map every time, so restoring a state never shares cells with the history.
    public TileMap ToMap(string name, string tilesetName) =>
        new(name, tilesetName, Width, Height, (string?[])Cells.Clone());
}

// Selection is kept as cells because it is not part of the document.
public record MapEditStep(
    string Description,
    MapState Before,
    MapState After,
    IReadOnlyList<(int X, int Y)> SelectionBefore,
    IReadOnlyList<(int X, int Y)> SelectionAfter);
