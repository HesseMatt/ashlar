using System.Text;
using System.Xml;
using System.Xml.Linq;
using Ashlar.Core.Model;

namespace Ashlar.Core.Serialization;

public static class TilemapXmlWriter
{
    private static readonly XmlWriterSettings Settings = new()
    {
        Indent = true,
        IndentChars = "    ",
        NewLineChars = "\n",
        Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    public static void Write(TileMap map, Tileset tileset, string filePath)
    {
        using var stream = File.Create(filePath);
        Write(map, tileset, stream);
    }

    public static void Write(TileMap map, Tileset tileset, Stream output)
    {
        using var writer = XmlWriter.Create(output, Settings);
        BuildDocument(map, tileset).Save(writer);
    }

    public static string ToXmlString(TileMap map, Tileset tileset)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, Settings))
            BuildDocument(map, tileset).Save(writer);

        return Settings.Encoding.GetString(stream.ToArray());
    }

    private static XDocument BuildDocument(TileMap map, Tileset tileset) =>
        new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Tilemap",
                new XElement("Tileset",
                    new XAttribute("tileWidth", tileset.TileWidth),
                    new XAttribute("tileHeight", tileset.TileHeight),
                    tileset.TexturePath),
                new XElement("Tiles", BuildTilesText(map, tileset))));

    private static string BuildTilesText(TileMap map, Tileset tileset)
    {
        var indexByName = new Dictionary<string, int>();
        for (var i = 0; i < tileset.Tiles.Count; i++)
            indexByName[tileset.Tiles[i].Name] = i;

        var digits = Math.Max(tileset.Tiles.Count - 1, 0).ToString().Length;
        var text = new StringBuilder("\n");
        for (var y = 0; y < map.Height; y++)
        {
            var cells = Enumerable.Range(0, map.Width).Select(x =>
                map[x, y] is { } name && indexByName.TryGetValue(name, out var index)
                    ? index.ToString().PadLeft(digits, '0')
                    : "-1");
            text.Append("        ").Append(string.Join(' ', cells)).Append('\n');
        }
        // Closing indentation of the Tiles element, which the XML writer cannot add to text content.
        return text.Append("    ").ToString();
    }
}
