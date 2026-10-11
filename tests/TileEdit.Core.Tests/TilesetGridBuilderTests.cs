using TileEdit.Core;

namespace TileEdit.Core.Tests;

public class TilesetGridBuilderTests
{
    [Fact]
    public void Build_EvenlyDivisibleImage_CoversWholeGridInRowMajorOrder()
    {
        var tiles = TilesetGridBuilder.Build("dungeon.png", imageWidth: 40, imageHeight: 40, tileWidth: 20, tileHeight: 20);

        Assert.Equal(
            [(0, 0), (20, 0), (0, 20), (20, 20)],
            tiles.Select(t => (t.X, t.Y)).ToList());
    }

    [Fact]
    public void Build_AssignsSequentialFourDigitNamesWithTilesetBaseName()
    {
        var tiles = TilesetGridBuilder.Build("dungeon.png", imageWidth: 40, imageHeight: 20, tileWidth: 20, tileHeight: 20);

        Assert.Equal(["dungeon_0001", "dungeon_0002"], tiles.Select(t => t.Name).ToList());
    }

    [Fact]
    public void Build_LeftoverPixelsThatDontFillAFullTile_AreNotTurnedIntoATile()
    {
        var tiles = TilesetGridBuilder.Build("dungeon.png", imageWidth: 45, imageHeight: 45, tileWidth: 20, tileHeight: 20);

        // Only full 20x20 cells fit twice in each direction (40 <= 45 < 60); the leftover
        // 5-pixel strip on the right and bottom is dropped rather than becoming a partial tile.
        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, t => Assert.Equal((20, 20), (t.Width, t.Height)));
    }

    [Fact]
    public void Build_SanitizesTilesetBaseNameFromFileName()
    {
        var tiles = TilesetGridBuilder.Build("My Tileset!.png", imageWidth: 20, imageHeight: 20, tileWidth: 20, tileHeight: 20);

        Assert.Equal("My_Tileset_0001", tiles.Single().Name);
    }
}
