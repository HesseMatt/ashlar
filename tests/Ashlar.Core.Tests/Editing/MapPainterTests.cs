using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapPainterTests
{
    [Fact]
    public void SingleCellSetsOnlyThatCell()
    {
        var map = TileMap.CreateEmpty("m", "forest", 3, 3);

        var changed = MapPainter.PaintLine(map, 1, 2, 1, 2, "forest_0001");

        Assert.Equal([(1, 2)], changed);
        Assert.Equal("forest_0001", map[1, 2]);
        Assert.Equal(1, map.Cells.Count(cell => cell is not null));
    }

    [Fact]
    public void LineSetsEveryCellAlongTheStrokeWithoutGaps()
    {
        var map = TileMap.CreateEmpty("m", "forest", 10, 10);

        var changed = MapPainter.PaintLine(map, 0, 0, 9, 3, "forest_0002");

        Assert.Equal(10, changed.Count);
        Assert.Equal(10, map.Cells.Count(cell => cell == "forest_0002"));
        Assert.Equal("forest_0002", map[0, 0]);
        Assert.Equal("forest_0002", map[9, 3]);
    }

    [Fact]
    public void CellsOutsideTheMapAreIgnored()
    {
        var map = TileMap.CreateEmpty("m", "forest", 3, 3);

        var changed = MapPainter.PaintLine(map, -2, 1, 5, 1, "forest_0001");

        Assert.Equal([(0, 1), (1, 1), (2, 1)], changed);
        Assert.Equal(3, map.Cells.Count(cell => cell is not null));
    }

    [Fact]
    public void LineEntirelyOutsideTheMapChangesNothing()
    {
        var map = TileMap.CreateEmpty("m", "forest", 3, 3);

        Assert.Empty(MapPainter.PaintLine(map, -5, -5, -1, -1, "forest_0001"));
        Assert.All(map.Cells, Assert.Null);
    }

    [Fact]
    public void CellsAlreadyHoldingTheTileAreNotReportedAsChanged()
    {
        var map = TileMap.CreateEmpty("m", "forest", 3, 1);
        map[1, 0] = "forest_0001";

        var changed = MapPainter.PaintLine(map, 0, 0, 2, 0, "forest_0001");

        Assert.Equal([(0, 0), (2, 0)], changed);
    }

    [Fact]
    public void ExistingTilesAreOverwritten()
    {
        var map = TileMap.CreateEmpty("m", "forest", 2, 1);
        map[0, 0] = "forest_0009";

        MapPainter.PaintLine(map, 0, 0, 0, 0, "forest_0001");

        Assert.Equal("forest_0001", map[0, 0]);
    }
}
