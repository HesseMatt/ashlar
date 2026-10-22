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
        ], new PixelColor(10, 20, 30, 40));

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
            Assert.Equal(project.DrawColor, loaded.DrawColor);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_ReadsProjectFilesWithoutADrawColor()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath,
                """{"ImageFileName":"a.png","ImagePng":"AQID","TexturePath":"a","TileWidth":16,"TileHeight":16,"Tiles":[]}""");

            var loaded = TilesetProjectFile.Load(filePath);

            Assert.Null(loaded.DrawColor);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
