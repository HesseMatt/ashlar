using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapResizerTests
{
    private static TileMap SampleMap() => new("m", "forest", 3, 2, ["a", "b", "c", "d", "e", "f"]);

    [Fact]
    public void GrowingAddsEmptyCellsOnTheRightAndBottomAndKeepsTheRest()
    {
        var resized = MapResizer.Resize(SampleMap(), 4, 3);

        Assert.Equal((4, 3), (resized.Width, resized.Height));
        Assert.Equal<string?[]>(
            ["a", "b", "c", null,
             "d", "e", "f", null,
             null, null, null, null],
            resized.Cells);
    }

    [Fact]
    public void ShrinkingCropsOnTheRightAndBottom()
    {
        var resized = MapResizer.Resize(SampleMap(), 2, 1);

        Assert.Equal<string?[]>(["a", "b"], resized.Cells);
    }

    [Fact]
    public void ResizingKeepsNameAndTilesetAndLeavesTheOriginalUntouched()
    {
        var map = SampleMap();

        var resized = MapResizer.Resize(map, 1, 1);

        Assert.Equal(("m", "forest"), (resized.Name, resized.TilesetName));
        Assert.Equal((3, 2), (map.Width, map.Height));
        Assert.Equal<string?[]>(["a", "b", "c", "d", "e", "f"], map.Cells);
    }

    [Fact]
    public void ResizingToAnInvalidSizeThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MapResizer.Resize(SampleMap(), 0, 2));
    }

    [Fact]
    public void CountsOnlyTilesThatShrinkingWouldCutOff()
    {
        var map = new TileMap("m", "forest", 3, 2, ["a", null, "c", "d", null, null]);

        Assert.Equal(0, MapResizer.CountLostTiles(map, 4, 4));
        Assert.Equal(0, MapResizer.CountLostTiles(map, 3, 2));
        Assert.Equal(1, MapResizer.CountLostTiles(map, 2, 2));
        Assert.Equal(1, MapResizer.CountLostTiles(map, 3, 1));
        Assert.Equal(2, MapResizer.CountLostTiles(map, 1, 1));
    }
}
