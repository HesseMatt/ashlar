namespace TileEdit.Core.Model;

// Unpremultiplied RGBA, the same layout the image stores its pixels in.
public record PixelColor(byte R, byte G, byte B, byte A)
{
    public static readonly PixelColor Black = new(0, 0, 0, 255);
}
