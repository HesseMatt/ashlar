using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TileEdit.Core.Model;

namespace TileEdit.Core;

public sealed class EditableTilesetImage : IDisposable
{
    private readonly Image<Rgba32> _image;

    private EditableTilesetImage(Image<Rgba32> image)
    {
        _image = image;
    }

    public int Width => _image.Width;

    public int Height => _image.Height;

    public static EditableTilesetImage Load(string filePath) => new(Image.Load<Rgba32>(filePath));

    public void ClearRegion(TileRegion region)
    {
        _image.ProcessPixelRows(accessor =>
        {
            for (var y = region.Y; y < region.Y + region.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = region.X; x < region.X + region.Width; x++)
                    row[x] = default;
            }
        });
    }

    public byte[] EncodeToPng()
    {
        using var stream = new MemoryStream();
        _image.SaveAsPng(stream);
        return stream.ToArray();
    }

    public void SaveAsPng(string filePath) => _image.SaveAsPng(filePath);

    public void Dispose() => _image.Dispose();
}
