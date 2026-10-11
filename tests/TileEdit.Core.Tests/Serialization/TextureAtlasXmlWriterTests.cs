using System.Text;
using TileEdit.Core.Model;
using TileEdit.Core.Serialization;

namespace TileEdit.Core.Tests.Serialization;

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

    private static string WriteToString(Tileset tileset)
    {
        using var stream = new MemoryStream();
        TextureAtlasXmlWriter.Write(tileset, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
