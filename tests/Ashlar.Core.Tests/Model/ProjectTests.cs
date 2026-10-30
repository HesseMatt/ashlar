using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Model;

public class ProjectTests
{
    private static ProjectTileset CreateTileset(string name) => new(name, "a.png", [1], name, 16, 16, []);

    [Fact]
    public void Constructor_RejectsDuplicateTilesetNames()
    {
        Assert.Throws<ArgumentException>(() => new Project([CreateTileset("a"), CreateTileset("a")], []));
    }

    [Fact]
    public void Constructor_AcceptsDistinctTilesetNames()
    {
        var project = new Project([CreateTileset("a"), CreateTileset("b")], []);

        Assert.Equal(2, project.Tilesets.Count);
    }
}
