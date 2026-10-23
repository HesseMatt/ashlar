using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class TileRectangleSelectorTests
{
    // Two rows of three 16x16 tiles; the bottom-right cell (32, 16) is empty.
    private static readonly TileRegion[] Tiles =
    [
        new("t_0001", 0, 0, 16, 16),
        new("t_0002", 16, 0, 16, 16),
        new("t_0003", 32, 0, 16, 16),
        new("t_0004", 0, 16, 16, 16),
        new("t_0005", 16, 16, 16, 16),
    ];

    private static string[] Names(IEnumerable<TileRegion> tiles) => tiles.Select(t => t.Name).ToArray();

    [Fact]
    public void PartialOverlap_SelectsEveryTouchedTile()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, 10, 10, 20, 20);

        Assert.Equal(["t_0001", "t_0002", "t_0004", "t_0005"], Names(result));
    }

    [Fact]
    public void ZeroSizeRectangle_SelectsTheTileUnderThatPixel()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, 17, 3, 17, 3);

        Assert.Equal(["t_0002"], Names(result));
    }

    [Fact]
    public void RectangleEndingOnATileEdge_IncludesThatTilesFirstPixelOnly()
    {
        var touchesNextTile = TileRectangleSelector.TilesTouching(Tiles, 0, 0, 16, 0);
        var staysInFirstTile = TileRectangleSelector.TilesTouching(Tiles, 0, 0, 15, 0);

        Assert.Equal(["t_0001", "t_0002"], Names(touchesNextTile));
        Assert.Equal(["t_0001"], Names(staysInFirstTile));
    }

    [Fact]
    public void FullContainment_SelectsAllTiles()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, 0, 0, 47, 31);

        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void RectangleInsideASingleTile_SelectsOnlyThatTile()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, 34, 2, 40, 9);

        Assert.Equal(["t_0003"], Names(result));
    }

    [Fact]
    public void EmptyArea_SelectsNothing()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, 33, 17, 40, 30);

        Assert.Empty(result);
    }

    [Fact]
    public void RectangleExtendingBeyondTheImage_ClampsImplicitly()
    {
        var result = TileRectangleSelector.TilesTouching(Tiles, -50, -50, 8, 8);

        Assert.Equal(["t_0001"], Names(result));
    }

    [Fact]
    public void CornersInReverseOrder_GiveTheSameResult()
    {
        var forward = TileRectangleSelector.TilesTouching(Tiles, 10, 10, 20, 20);
        var backward = TileRectangleSelector.TilesTouching(Tiles, 20, 20, 10, 10);

        Assert.Equal(Names(forward), Names(backward));
    }
}
