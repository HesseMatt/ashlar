using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class TileGroupAssignerTests
{
    [Fact]
    public void AssignGroup_NewGroup_StartsNumberingAtOne()
    {
        var tiles = new[]
        {
            new TileRegion("dungeon_0001", 0, 0, 16, 16),
            new TileRegion("dungeon_0002", 16, 0, 16, 16),
        };

        var result = TileGroupAssigner.AssignGroup(tiles, new HashSet<TileRegion> { tiles[0] }, "enemies");

        Assert.Equal("enemies_0001", result[0].Name);
        Assert.Equal("dungeon_0002", result[1].Name);
    }

    [Fact]
    public void AssignGroup_ExistingGroupWithAGapFromARemovedTile_ContinuesAfterTheHighestNumber()
    {
        var tiles = new[]
        {
            new TileRegion("enemies_0001", 0, 0, 16, 16),
            new TileRegion("enemies_0003", 16, 0, 16, 16),
            new TileRegion("dungeon_0004", 32, 0, 16, 16),
        };

        var result = TileGroupAssigner.AssignGroup(tiles, new HashSet<TileRegion> { tiles[2] }, "enemies");

        // Highest existing number in "enemies" is 3, so the new member becomes
        // 4 even though the group only has two other members.
        Assert.Equal("enemies_0004", result[2].Name);
    }

    [Fact]
    public void AssignGroup_MultipleSelectedTiles_AreNumberedSequentiallyInGridOrder()
    {
        var tiles = new[]
        {
            new TileRegion("dungeon_0001", 0, 0, 16, 16),
            new TileRegion("dungeon_0002", 16, 0, 16, 16),
            new TileRegion("dungeon_0003", 32, 0, 16, 16),
        };

        var result = TileGroupAssigner.AssignGroup(tiles, new HashSet<TileRegion> { tiles[0], tiles[2] }, "enemies");

        Assert.Equal("enemies_0001", result[0].Name);
        Assert.Equal("dungeon_0002", result[1].Name);
        Assert.Equal("enemies_0002", result[2].Name);
    }

    [Fact]
    public void AssignGroup_DoesNotRenameTilesOutsideTheSelection()
    {
        var tiles = new[]
        {
            new TileRegion("enemies_0001", 0, 0, 16, 16),
            new TileRegion("dungeon_0002", 16, 0, 16, 16),
        };

        var result = TileGroupAssigner.AssignGroup(tiles, new HashSet<TileRegion> { tiles[1] }, "enemies");

        Assert.Equal("enemies_0001", result[0].Name);
    }

    [Fact]
    public void AssignGroup_BackToDefaultPrefix_SkipsGapsLeftInTheDefaultSequence()
    {
        var tiles = new[]
        {
            new TileRegion("dungeon_0001", 0, 0, 16, 16),
            new TileRegion("dungeon_0003", 16, 0, 16, 16),
            new TileRegion("enemies_0001", 32, 0, 16, 16),
        };

        var result = TileGroupAssigner.AssignGroup(tiles, new HashSet<TileRegion> { tiles[2] }, "dungeon");

        Assert.Equal("dungeon_0004", result[2].Name);
    }

    [Fact]
    public void GetGroupNames_ReturnsDistinctNonDefaultPrefixesSortedAlphabetically()
    {
        var tiles = new[]
        {
            new TileRegion("dungeon_0001", 0, 0, 16, 16),
            new TileRegion("traps_0001", 16, 0, 16, 16),
            new TileRegion("enemies_0001", 32, 0, 16, 16),
            new TileRegion("enemies_0002", 48, 0, 16, 16),
        };

        var groupNames = TileGroupAssigner.GetGroupNames(tiles, "dungeon");

        Assert.Equal(["enemies", "traps"], groupNames);
    }

    [Fact]
    public void GetGroupName_ReturnsNullForTheDefaultPrefix()
    {
        var tile = new TileRegion("dungeon_0001", 0, 0, 16, 16);

        Assert.Null(TileGroupAssigner.GetGroupName(tile, "dungeon"));
    }

    [Fact]
    public void GetGroupName_ReturnsThePrefixForAGroupedTile()
    {
        var tile = new TileRegion("enemies_0001", 0, 0, 16, 16);

        Assert.Equal("enemies", TileGroupAssigner.GetGroupName(tile, "dungeon"));
    }

    [Fact]
    public void FormatName_PadsTheNumberToFourDigits()
    {
        Assert.Equal("enemies_0042", TileGroupAssigner.FormatName("enemies", 42));
    }

    [Fact]
    public void ResolveTargetGroupName_BlankInput_ReturnsTheDefaultGroupName()
    {
        Assert.Equal("dungeon", TileGroupAssigner.ResolveTargetGroupName("   ", "dungeon"));
    }

    [Fact]
    public void ResolveTargetGroupName_ValidInput_IsSanitizedLikeATilesetBaseName()
    {
        Assert.Equal("Boss_Enemies", TileGroupAssigner.ResolveTargetGroupName(" Boss Enemies! ", "dungeon"));
    }

    [Fact]
    public void ResolveTargetGroupName_InputThatSanitizesToNothing_FallsBackToTheDefaultGroupName()
    {
        Assert.Equal("dungeon", TileGroupAssigner.ResolveTargetGroupName("!!!", "dungeon"));
    }
}
