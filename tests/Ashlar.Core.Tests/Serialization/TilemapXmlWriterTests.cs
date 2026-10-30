using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.Core.Tests.Serialization;

public class TilemapXmlWriterTests
{
    private static Tileset CreateTileset(int tileCount) =>
        new("images/atlas", 20, 20,
            Enumerable.Range(0, tileCount).Select(i => new TileRegion($"t_{i:0000}", i * 20, 0, 20, 20)).ToList());

    [Fact]
    public void ToXmlString_WritesPaddedIndicesAndMinusOneForEmptyCells()
    {
        var map = TileMap.CreateEmpty("m", "t", 4, 2);
        map[0, 0] = "t_0000";
        map[1, 0] = "t_0001";
        map[2, 0] = "t_0002";
        map[0, 1] = "t_0004";
        map[1, 1] = "t_0005";
        map[3, 1] = "t_0007";

        var xml = TilemapXmlWriter.ToXmlString(map, CreateTileset(11));

        Assert.Equal(
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<Tilemap>\n" +
            "    <Tileset tileWidth=\"20\" tileHeight=\"20\">images/atlas</Tileset>\n" +
            "    <Tiles>\n" +
            "        00 01 02 -1\n" +
            "        04 05 -1 07\n" +
            "    </Tiles>\n" +
            "</Tilemap>",
            xml);
    }

    [Fact]
    public void ToXmlString_PadsToTheDigitWidthOfTheLargestIndex()
    {
        var map = TileMap.CreateEmpty("m", "t", 3, 1);
        map[0, 0] = "t_0003";
        map[2, 0] = "t_0010";

        var xml = TilemapXmlWriter.ToXmlString(map, CreateTileset(11));

        Assert.Contains("\n        03 -1 10\n", xml);
    }

    [Fact]
    public void ToXmlString_WritesMinusOneForNamesMissingFromTheTileset()
    {
        var map = TileMap.CreateEmpty("m", "t", 2, 1);
        map[0, 0] = "deleted_0001";
        map[1, 0] = "t_0001";

        var xml = TilemapXmlWriter.ToXmlString(map, CreateTileset(2));

        Assert.Contains("\n        -1 1\n", xml);
    }

    [Fact]
    public void Write_ProducesTheSameTextAsToXmlString()
    {
        var map = TileMap.CreateEmpty("m", "t", 2, 2);
        map[1, 1] = "t_0001";
        var tileset = CreateTileset(2);

        using var stream = new MemoryStream();
        TilemapXmlWriter.Write(map, tileset, stream);

        Assert.Equal(TilemapXmlWriter.ToXmlString(map, tileset), System.Text.Encoding.UTF8.GetString(stream.ToArray()));
    }
}
