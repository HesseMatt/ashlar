using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapTileCleanerTests
{
    [Fact]
    public void ClearsExactlyTheCellsReferencingRemovedTiles()
    {
        var map = new TileMap("m", "forest", 2, 2, ["a", "b", "a", null]);

        var cleared = MapTileCleaner.ClearTiles([map], "forest", new HashSet<string> { "a" });

        Assert.Equal(2, cleared);
        Assert.Equal<string?[]>([null, "b", null, null], map.Cells);
    }

    [Fact]
    public void LeavesMapsOfOtherTilesetsUntouched()
    {
        var forestMap = new TileMap("m1", "forest", 1, 1, ["a"]);
        var caveMap = new TileMap("m2", "cave", 1, 1, ["a"]);

        var cleared = MapTileCleaner.ClearTiles([forestMap, caveMap], "forest", new HashSet<string> { "a" });

        Assert.Equal(1, cleared);
        Assert.Null(forestMap.Cells[0]);
        Assert.Equal("a", caveMap.Cells[0]);
    }

    [Fact]
    public void ClearsNothingWhenNoCellReferencesTheTiles()
    {
        var map = new TileMap("m", "forest", 2, 1, ["a", null]);

        var cleared = MapTileCleaner.ClearTiles([map], "forest", new HashSet<string> { "z" });

        Assert.Equal(0, cleared);
        Assert.Equal<string?[]>(["a", null], map.Cells);
    }
}
