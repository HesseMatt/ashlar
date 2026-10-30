using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapMoverTests
{
    [Fact]
    public void ShiftsTheSelectedCellsAndEmptiesTheVacatedOnes()
    {
        var map = new TileMap("m", "forest", 4, 1, ["a", "b", null, null]);

        var moved = MapMover.Move(map, [(0, 0), (1, 0)], 2, 0);

        Assert.Equal([(2, 0), (3, 0)], moved);
        Assert.Equal<string?[]>([null, null, "a", "b"], map.Cells);
    }

    [Fact]
    public void OverlappingMoveDoesNotDependOnTheOrderOfTheCells()
    {
        var map = new TileMap("m", "forest", 4, 1, ["a", "b", "c", null]);

        var moved = MapMover.Move(map, [(0, 0), (1, 0), (2, 0)], 1, 0);

        Assert.Equal([(1, 0), (2, 0), (3, 0)], moved);
        Assert.Equal<string?[]>([null, "a", "b", "c"], map.Cells);
    }

    [Fact]
    public void SelectedCellsOverwriteUnselectedOnesTheyLandOn()
    {
        var map = new TileMap("m", "forest", 3, 1, ["a", "x", "y"]);

        MapMover.Move(map, [(0, 0)], 1, 0);

        Assert.Equal<string?[]>([null, "a", "y"], map.Cells);
    }

    [Fact]
    public void EmptySelectedCellsClearWhatTheyLandOn()
    {
        var map = new TileMap("m", "forest", 3, 1, [null, "a", "x"]);

        MapMover.Move(map, [(0, 0), (1, 0)], 1, 0);

        Assert.Equal<string?[]>([null, null, "a"], map.Cells);
    }

    [Fact]
    public void CellsMovedOutsideTheMapAreDropped()
    {
        var map = new TileMap("m", "forest", 3, 2, ["a", "b", "c", "d", "e", "f"]);

        var moved = MapMover.Move(map, [(1, 0), (2, 0), (2, 1)], 1, 0);

        Assert.Equal([(2, 0)], moved);
        Assert.Equal<string?[]>(["a", null, "b", "d", "e", null], map.Cells);
        Assert.Equal(3, map.Width);
        Assert.Equal(2, map.Height);
    }

    [Fact]
    public void MovingEntirelyOutsideEmptiesTheSourceAndKeepsNothing()
    {
        var map = new TileMap("m", "forest", 2, 2, ["a", "b", "c", "d"]);

        var moved = MapMover.Move(map, [(0, 0)], -1, 0);

        Assert.Empty(moved);
        Assert.Equal<string?[]>([null, "b", "c", "d"], map.Cells);
    }
}
