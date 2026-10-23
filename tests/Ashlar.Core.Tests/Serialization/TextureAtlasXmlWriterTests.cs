using System.Text;
using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.Core.Tests.Serialization;

public class TextureAtlasXmlWriterTests
{
    // Matches example.xml exactly, including indentation and the lack of a trailing newline.
    private const string ExpectedXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<TextureAtlas>\n" +
        "    <Texture>images/atlas</Texture>\n" +
        "    <Regions>\n" +
        "        <Region name=\"slime\" x=\"0\" y=\"0\" width=\"20\" height=\"20\" />\n" +
        "        <Region name=\"bat\" x=\"20\" y=\"0\" width=\"20\" height=\"20\" />\n" +
        "    </Regions>\n" +
        "</TextureAtlas>";

    [Fact]
    public void Write_MatchesTheExpectedTextureAtlasShape()
    {
        var tileset = new Tileset("images/atlas", TileWidth: 20, TileHeight: 20,
        [
            new TileRegion("slime", 0, 0, 20, 20),
            new TileRegion("bat", 20, 0, 20, 20),
        ]);

        Assert.Equal(ExpectedXml, WriteToString(tileset));
    }

    [Fact]
    public void Write_NoRegions_StillProducesAnEmptyRegionsElement()
    {
        var tileset = new Tileset("images/atlas", TileWidth: 20, TileHeight: 20, Tiles: []);

        Assert.Contains("<Regions />", WriteToString(tileset));
    }

    [Fact]
    public void ToXmlString_WithoutLimit_MatchesWrite()
    {
        var tileset = new Tileset("images/atlas", TileWidth: 20, TileHeight: 20,
        [
            new TileRegion("slime", 0, 0, 20, 20),
            new TileRegion("bat", 20, 0, 20, 20),
        ]);

        Assert.Equal(ExpectedXml, TextureAtlasXmlWriter.ToXmlString(tileset));
    }

    [Fact]
    public void ToXmlString_WithLimit_ShowsTheFirstRegionsAndCountsTheRest()
    {
        var tileset = new Tileset("images/atlas", TileWidth: 20, TileHeight: 20,
        [
            new TileRegion("a", 0, 0, 20, 20),
            new TileRegion("b", 20, 0, 20, 20),
            new TileRegion("c", 40, 0, 20, 20),
            new TileRegion("d", 60, 0, 20, 20),
        ]);

        var xml = TextureAtlasXmlWriter.ToXmlString(tileset, maxRegions: 2);

        Assert.Contains("name=\"b\"", xml);
        Assert.DoesNotContain("name=\"c\"", xml);
        Assert.Contains("<!-- … 2 more regions -->", xml);
    }

    [Fact]
    public void ToXmlString_LimitNotExceeded_HasNoComment()
    {
        var tileset = new Tileset("images/atlas", TileWidth: 20, TileHeight: 20, [new TileRegion("a", 0, 0, 20, 20)]);

        Assert.DoesNotContain("<!--", TextureAtlasXmlWriter.ToXmlString(tileset, maxRegions: 3));
    }

    [Fact]
    public void Write_NeverContainsThePreviewComment()
    {
        var tiles = Enumerable.Range(0, 5).Select(i => new TileRegion($"t{i}", i * 20, 0, 20, 20)).ToList();

        Assert.DoesNotContain("<!--", WriteToString(new Tileset("images/atlas", 20, 20, tiles)));
    }

    private static string WriteToString(Tileset tileset)
    {
        using var stream = new MemoryStream();
        TextureAtlasXmlWriter.Write(tileset, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
