using SixLabors.ImageSharp;

namespace TileEdit.Core;

public sealed record TilesetImage(int Width, int Height, byte[] Bytes);

public static class TilesetImageLoader
{
    public static TilesetImage Load(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        using var image = Image.Load(bytes);
        return new TilesetImage(image.Width, image.Height, bytes);
    }
}
