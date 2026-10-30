using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.Core.Tests.Serialization;

public class ProjectFileTests
{
    private static ProjectTileset CreateTileset(string name, PixelColor? drawColor = null) =>
        new(name, $"{name}.png", [1, 2, 3, 4], name, 16, 16,
        [
            new TileRegion($"{name}_0001", 0, 0, 16, 16),
            new TileRegion($"{name}_0002", 16, 0, 16, 16),
        ], drawColor);

    [Fact]
    public void SaveThenLoad_RoundTripsAllTilesets()
    {
        var map = TileMap.CreateEmpty("level1", "dungeon", 3, 2);
        map[1, 0] = "dungeon_0002";
        var project = new Project(
            [CreateTileset("dungeon", new PixelColor(10, 20, 30, 40)), CreateTileset("forest")],
            [map]);

        var filePath = Path.GetTempFileName();
        try
        {
            ProjectFile.Save(project, filePath);
            var loaded = ProjectFile.Load(filePath);

            Assert.Equal(2, loaded.Tilesets.Count);
            for (var i = 0; i < project.Tilesets.Count; i++)
            {
                var expected = project.Tilesets[i];
                var actual = loaded.Tilesets[i];
                Assert.Equal(expected.Name, actual.Name);
                Assert.Equal(expected.ImageFileName, actual.ImageFileName);
                Assert.Equal(expected.ImagePng, actual.ImagePng);
                Assert.Equal(expected.TexturePath, actual.TexturePath);
                Assert.Equal(expected.TileWidth, actual.TileWidth);
                Assert.Equal(expected.TileHeight, actual.TileHeight);
                Assert.Equal(expected.Tiles, actual.Tiles);
                Assert.Equal(expected.DrawColor, actual.DrawColor);
            }

            var loadedMap = Assert.Single(loaded.Maps);
            Assert.Equal("level1", loadedMap.Name);
            Assert.Equal("dungeon", loadedMap.TilesetName);
            Assert.Equal(3, loadedMap.Width);
            Assert.Equal(2, loadedMap.Height);
            Assert.Equal(map.Cells, loadedMap.Cells);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_RejectsDuplicateTilesetNames()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath,
                """
                {"Tilesets":[
                  {"Name":"a","ImageFileName":"a.png","ImagePng":"AQID","TexturePath":"a","TileWidth":16,"TileHeight":16,"Tiles":[]},
                  {"Name":"a","ImageFileName":"b.png","ImagePng":"AQID","TexturePath":"b","TileWidth":16,"TileHeight":16,"Tiles":[]}
                ],"Maps":[]}
                """);

            Assert.Throws<ArgumentException>(() => ProjectFile.Load(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Load_RejectsDuplicateMapNames()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath,
                """
                {"Tilesets":[],"Maps":[
                  {"Name":"m","TilesetName":"a","Width":1,"Height":1,"Cells":[null]},
                  {"Name":"m","TilesetName":"a","Width":1,"Height":1,"Cells":[null]}
                ]}
                """);

            Assert.Throws<ArgumentException>(() => ProjectFile.Load(filePath));
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
