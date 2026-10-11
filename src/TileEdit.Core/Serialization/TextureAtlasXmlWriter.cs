using System.Text;
using System.Xml;
using System.Xml.Linq;
using TileEdit.Core.Model;

namespace TileEdit.Core.Serialization;

public static class TextureAtlasXmlWriter
{
    // Fixed indentation, line endings and encoding so the output is identical across
    // platforms, matching the hand-written shape of example.xml.
    private static readonly XmlWriterSettings Settings = new()
    {
        Indent = true,
        IndentChars = "    ",
        NewLineChars = "\n",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    public static void Write(Tileset tileset, string filePath)
    {
        using var stream = File.Create(filePath);
        Write(tileset, stream);
    }

    public static void Write(Tileset tileset, Stream output)
    {
        using var writer = XmlWriter.Create(output, Settings);
        BuildDocument(tileset).Save(writer);
    }

    private static XDocument BuildDocument(Tileset tileset)
    {
        var regions = tileset.Tiles.Select(tile => new XElement("Region",
            new XAttribute("name", tile.Name),
            new XAttribute("x", tile.X),
            new XAttribute("y", tile.Y),
            new XAttribute("width", tile.Width),
            new XAttribute("height", tile.Height)));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("TextureAtlas",
                new XElement("Texture", tileset.TexturePath),
                new XElement("Regions", regions)));
    }
}
