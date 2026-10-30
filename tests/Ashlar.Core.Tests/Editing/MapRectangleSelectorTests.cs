using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapRectangleSelectorTests
{
    private static readonly TileMap Map = TileMap.CreateEmpty("m", "forest", 4, 3);

    [Fact]
    public void SelectsEveryCellInTheRectangle()
    {
        var cells = MapRectangleSelector.CellsIn(Map, 1, 0, 2, 1);

        Assert.Equal([(1, 0), (2, 0), (1, 1), (2, 1)], cells);
    }

    [Fact]
    public void CornersInEitherOrderGiveTheSameCells()
    {
        Assert.Equal(
            MapRectangleSelector.CellsIn(Map, 0, 0, 2, 2),
            MapRectangleSelector.CellsIn(Map, 2, 2, 0, 0));
    }

    [Fact]
    public void SingleCellRectangleSelectsThatCell()
    {
        Assert.Equal([(3, 2)], MapRectangleSelector.CellsIn(Map, 3, 2, 3, 2));
    }

    [Fact]
    public void CellsOutsideTheMapAreLeftOut()
    {
        var cells = MapRectangleSelector.CellsIn(Map, -5, 1, 1, 9);

        Assert.Equal([(0, 1), (1, 1), (0, 2), (1, 2)], cells);
    }

    [Fact]
    public void RectangleEntirelyOutsideTheMapSelectsNothing()
    {
        Assert.Empty(MapRectangleSelector.CellsIn(Map, 10, 10, 12, 12));
        Assert.Empty(MapRectangleSelector.CellsIn(Map, -3, 0, -1, 0));
    }
}
