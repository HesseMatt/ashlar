using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.Core.Tests.Editing;

public class TilesetCompactorTests
{
    private static TileRegion Tile(string name, int column, int row) => new(name, column * 16, row * 16, 16, 16);

    private static TileRegion[] Moved(CompactionResult result) => result.Placements.Select(p => p.Moved).ToArray();

    [Fact]
    public void Compact_RemovesGapsAndPacksRowByRow()
    {
        var tiles = new[]
        {
            Tile("dungeon_0001", 0, 0),
            Tile("dungeon_0003", 2, 0),
            Tile("dungeon_0006", 1, 1),
        };

        var result = TilesetCompactor.Compact(tiles, 16, 16, 48, "dungeon");

        Assert.Equal(
            new[] { Tile("dungeon_0001", 0, 0), Tile("dungeon_0003", 1, 0), Tile("dungeon_0006", 2, 0) },
            Moved(result));
    }

    [Fact]
    public void Compact_CropsCanvasToTheUsedColumnsAndRows()
    {
        var tiles = Enumerable.Range(0, 5).Select(i => Tile($"dungeon_{i + 1:D4}", i % 4, i / 4 + 2)).ToArray();

        var result = TilesetCompactor.Compact(tiles, 16, 16, 64, "dungeon");

        Assert.Equal(64, result.Width);
        Assert.Equal(32, result.Height);
    }

    [Fact]
    public void Compact_FewerTilesThanColumns_ShrinksWidth()
    {
        var tiles = new[] { Tile("dungeon_0001", 3, 3), Tile("dungeon_0002", 1, 2) };

        var result = TilesetCompactor.Compact(tiles, 16, 16, 64, "dungeon");

        Assert.Equal(32, result.Width);
        Assert.Equal(16, result.Height);
    }

    [Fact]
    public void Compact_KeepsGroupMembersTogetherAtTheirFirstAppearance()
    {
        var tiles = new[]
        {
            Tile("dungeon_0001", 0, 0),
            Tile("enemies_0001", 1, 0),
            Tile("dungeon_0003", 2, 0),
            Tile("enemies_0002", 3, 0),
            Tile("dungeon_0005", 0, 1),
        };

        var result = TilesetCompactor.Compact(tiles, 16, 16, 64, "dungeon");

        Assert.Equal(
            new[] { "dungeon_0001", "enemies_0001", "enemies_0002", "dungeon_0003", "dungeon_0005" },
            Moved(result).Select(t => t.Name));
    }

    [Fact]
    public void Compact_DoesNotChangeTileNames()
    {
        var tiles = new[] { Tile("enemies_0007", 2, 1), Tile("dungeon_0002", 0, 3) };

        var result = TilesetCompactor.Compact(tiles, 16, 16, 48, "dungeon");

        Assert.Equal(new[] { "dungeon_0002", "enemies_0007" }, Moved(result).Select(t => t.Name).Order());
    }
}
