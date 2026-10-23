using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class TileMoverTests
{
    // A 3x1 strip of 16px tiles in a 48x16 image.
    private static readonly TileRegion A = new("a", 0, 0, 16, 16);
    private static readonly TileRegion B = new("b", 16, 0, 16, 16);
    private static readonly TileRegion C = new("c", 32, 0, 16, 16);

    [Fact]
    public void Move_ToAnEmptyCell_ChangesOnlyThePositionAndKeepsTheName()
    {
        var result = TileMover.Move([A, C], [A], 16, 0, 48, 16);

        Assert.Equal([A with { X = 16 }, C], result.Tiles);
        Assert.Equal([A with { X = 16 }], result.Moved);
    }

    [Fact]
    public void Move_OntoAnUnselectedTile_OverwritesItInsteadOfSwapping()
    {
        var result = TileMover.Move([A, B, C], [A], 16, 0, 48, 16);

        Assert.Equal(["a", "c"], result.Tiles.Select(t => t.Name));
        Assert.Equal((16, 0), (result.Tiles[0].X, result.Tiles[0].Y));
    }

    [Fact]
    public void Move_OverlappingSelection_ShiftsTheWholeBlockWithoutLosingTiles()
    {
        var result = TileMover.Move([A, B, C], [A, B], 16, 0, 48, 16);

        Assert.Equal(["a", "b"], result.Tiles.Select(t => t.Name));
        Assert.Equal([16, 32], result.Tiles.Select(t => t.X));
    }

    [Fact]
    public void Move_DestinationOutsideTheImage_RemovesThatTile()
    {
        var result = TileMover.Move([A, B, C], [B, C], 16, 0, 48, 16);

        Assert.Equal(["a", "b"], result.Tiles.Select(t => t.Name));
        Assert.Equal(32, result.Tiles[1].X);
        Assert.Equal(["b"], result.Moved.Select(t => t.Name));
    }

    [Fact]
    public void Move_WithNegativeOffsetPastTheOrigin_RemovesTheTile()
    {
        var result = TileMover.Move([A, B], [A, B], -16, 0, 48, 16);

        Assert.Equal(["b"], result.Tiles.Select(t => t.Name));
        Assert.Equal(0, result.Tiles[0].X);
    }

    [Fact]
    public void Move_KeepsTheOrderOfTheTileList()
    {
        var result = TileMover.Move([A, B, C], [B], 0, 0, 48, 16);

        Assert.Equal(["a", "b", "c"], result.Tiles.Select(t => t.Name));
    }
}
