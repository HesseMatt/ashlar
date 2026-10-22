using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.Core.Tests.Editing;

public class DrawnTileCreatorTests
{
    private static TileRegion? Create(IReadOnlyList<TileRegion> tiles, int pixelX, int pixelY) =>
        DrawnTileCreator.CreateTile(tiles, pixelX, pixelY, 16, 16, 64, 64, "dungeon");

    [Fact]
    public void CreateTile_CoversTheWholeGridAlignedCell()
    {
        var tile = Create([], 37, 20);

        Assert.Equal(new TileRegion("dungeon_0001", 32, 16, 16, 16), tile);
    }

    [Fact]
    public void CreateTile_UsesTheHighestNumberOfTheDefaultGroupPlusOne()
    {
        var tiles = new[]
        {
            new TileRegion("dungeon_0002", 0, 0, 16, 16),
            new TileRegion("dungeon_0007", 16, 0, 16, 16),
            new TileRegion("enemies_0009", 32, 0, 16, 16),
        };

        Assert.Equal("dungeon_0008", Create(tiles, 0, 40)!.Name);
    }

    [Fact]
    public void CreateTile_IsNullWhereATileAlreadyExists()
    {
        var tiles = new[] { new TileRegion("dungeon_0001", 16, 16, 16, 16) };

        Assert.Null(Create(tiles, 20, 31));
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, -1)]
    [InlineData(64, 5)]
    [InlineData(5, 64)]
    public void CreateTile_IsNullOutsideTheImage(int pixelX, int pixelY)
    {
        Assert.Null(Create([], pixelX, pixelY));
    }

    [Fact]
    public void CreateTile_IsNullForACellThatOnlyPartlyFitsIntoTheImage()
    {
        Assert.Null(DrawnTileCreator.CreateTile([], 66, 5, 16, 16, 70, 64, "dungeon"));
    }
}
