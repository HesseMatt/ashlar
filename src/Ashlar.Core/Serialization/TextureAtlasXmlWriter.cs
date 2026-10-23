using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ashlar.Core.Model;

namespace Ashlar.Core.Serialization;

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

    // With a limit, only the first regions are rendered and a comment tells how many were
    // left out. That comment is for previews only; Write never produces it.
    public static string ToXmlString(Tileset tileset, int? maxRegions = null)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, Settings))
            BuildDocument(tileset, maxRegions).Save(writer);

        return Settings.Encoding.GetString(stream.ToArray());
    }

    private static XDocument BuildDocument(Tileset tileset, int? maxRegions = null)
    {
        var shownTiles = maxRegions is { } limit ? tileset.Tiles.Take(limit) : tileset.Tiles;
        var regions = shownTiles.Select(tile => (XNode)new XElement("Region",
            new XAttribute("name", tile.Name),
            new XAttribute("x", tile.X),
            new XAttribute("y", tile.Y),
            new XAttribute("width", tile.Width),
            new XAttribute("height", tile.Height))).ToList();

        var omitted = tileset.Tiles.Count - regions.Count;
        if (omitted > 0)
            regions.Add(new XComment($" … {omitted} more regions "));

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("TextureAtlas",
                new XElement("Texture", tileset.TexturePath),
                new XElement("Regions", regions)));
    }
}
