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

    public static EditableTilesetImage Load(byte[] encodedImageBytes) => new(Image.Load<Rgba32>(encodedImageBytes));

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

    // Always swaps both blocks, never just overwrites: if the target cell is
    // empty, its pixels are already transparent (from an earlier delete), so
    // swapping them into the source cell has the same effect as clearing it.
    public void MoveRegion(TileRegion from, int targetX, int targetY)
    {
        var width = from.Width;
        var height = from.Height;
        var buffer = new Rgba32[width * height];

        _image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < height; y++)
            {
                var sourceRow = accessor.GetRowSpan(from.Y + y).Slice(from.X, width);
                sourceRow.CopyTo(buffer.AsSpan(y * width, width));
            }

            for (var y = 0; y < height; y++)
            {
                var sourceRow = accessor.GetRowSpan(from.Y + y).Slice(from.X, width);
                var targetRow = accessor.GetRowSpan(targetY + y).Slice(targetX, width);
                targetRow.CopyTo(sourceRow);
            }

            for (var y = 0; y < height; y++)
            {
                var targetRow = accessor.GetRowSpan(targetY + y).Slice(targetX, width);
                buffer.AsSpan(y * width, width).CopyTo(targetRow);
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
