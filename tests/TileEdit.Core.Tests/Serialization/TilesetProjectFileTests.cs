using TileEdit.Core.Model;
using TileEdit.Core.Serialization;

namespace TileEdit.Core.Tests.Serialization;

public class TilesetProjectFileTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsTheProject()
    {
        var project = new TilesetProject("dungeon.png", [1, 2, 3, 4], "dungeon", 16, 16,
        [
            new TileRegion("dungeon_0001", 0, 0, 16, 16),
            new TileRegion("dungeon_0002", 16, 0, 16, 16),
        ]);

        var filePath = Path.GetTempFileName();
        try
        {
            TilesetProjectFile.Save(project, filePath);
            var loaded = TilesetProjectFile.Load(filePath);

            Assert.Equal(project.ImageFileName, loaded.ImageFileName);
            Assert.Equal(project.ImagePng, loaded.ImagePng);
            Assert.Equal(project.TexturePath, loaded.TexturePath);
            Assert.Equal(project.TileWidth, loaded.TileWidth);
            Assert.Equal(project.TileHeight, loaded.TileHeight);
            Assert.Equal(project.Tiles, loaded.Tiles);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
