using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapEraserTests
{
    [Fact]
    public void ClearsTheGivenCellsAndLeavesTheRest()
    {
        var map = new TileMap("m", "forest", 2, 2, ["a", "b", "c", "d"]);

        var cleared = MapEraser.Clear(map, [(0, 0), (1, 1)]);

        Assert.Equal([(0, 0), (1, 1)], cleared);
        Assert.Equal<string?[]>([null, "b", "c", null], map.Cells);
    }

    [Fact]
    public void AlreadyEmptyCellsAreNotReportedAsCleared()
    {
        var map = new TileMap("m", "forest", 2, 1, ["a", null]);

        var cleared = MapEraser.Clear(map, [(0, 0), (1, 0)]);

        Assert.Equal([(0, 0)], cleared);
    }

    [Fact]
    public void CellsOutsideTheMapAreIgnored()
    {
        var map = new TileMap("m", "forest", 2, 1, ["a", "b"]);

        var cleared = MapEraser.Clear(map, [(-1, 0), (2, 0), (0, 1)]);

        Assert.Empty(cleared);
        Assert.Equal<string?[]>(["a", "b"], map.Cells);
    }
}
