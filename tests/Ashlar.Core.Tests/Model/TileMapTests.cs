using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Model;

public class TileMapTests
{
    [Fact]
    public void CreateEmpty_FillsEveryCellWithNoTile()
    {
        var map = TileMap.CreateEmpty("level1", "dungeon", 3, 2);

        Assert.Equal("level1", map.Name);
        Assert.Equal("dungeon", map.TilesetName);
        Assert.Equal(3, map.Width);
        Assert.Equal(2, map.Height);
        Assert.Equal(6, map.Cells.Length);
        Assert.All(map.Cells, Assert.Null);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 5)]
    public void CreateEmpty_RejectsSizeBelowOneByOne(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TileMap.CreateEmpty("m", "t", width, height));
    }

    [Fact]
    public void Indexer_ReadsAndWritesByColumnAndRow()
    {
        var map = TileMap.CreateEmpty("m", "t", 3, 2);

        map[2, 1] = "t_0001";

        Assert.Equal("t_0001", map[2, 1]);
        Assert.Equal("t_0001", map.Cells[5]);
        Assert.Null(map[1, 1]);
    }

    [Fact]
    public void ResolveRegions_MapsCellsToTheirTilesRegions()
    {
        var floor = new TileRegion("t_0001", 0, 0, 16, 16);
        var wall = new TileRegion("t_0002", 16, 0, 16, 16);
        var map = new TileMap("m", "t", 2, 2, ["t_0002", null, "t_0001", "t_0002"]);

        var regions = map.ResolveRegions([floor, wall]);

        Assert.Equal(new TileRegion?[] { wall, null, floor, wall }, regions);
    }

    [Fact]
    public void ResolveRegions_TreatsCellsWithADeletedTileAsEmpty()
    {
        var floor = new TileRegion("t_0001", 0, 0, 16, 16);
        var map = new TileMap("m", "t", 2, 1, ["t_0001", "t_0002"]);

        var regions = map.ResolveRegions([floor]);

        Assert.Equal(new TileRegion?[] { floor, null }, regions);
    }
}
