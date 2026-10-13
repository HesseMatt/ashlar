using SkiaSharp;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.Core;

public sealed class EditableTilesetImage : IDisposable
{
    private const int BytesPerPixel = 4;

    private SKBitmap _bitmap;

    private EditableTilesetImage(SKBitmap bitmap)
    {
        _bitmap = bitmap;
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public static EditableTilesetImage Load(string filePath) => new(Decode(File.ReadAllBytes(filePath)));

    public static EditableTilesetImage Load(byte[] encodedImageBytes) => new(Decode(encodedImageBytes));

    // Pixel edits below work on raw, unpremultiplied bytes, so the bitmap is
    // decoded straight into that format rather than whatever the codec's
    // default happens to be.
    private static SKBitmap Decode(byte[] encodedImageBytes)
    {
        var bounds = SKBitmap.DecodeBounds(encodedImageBytes);
        var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        return SKBitmap.Decode(encodedImageBytes, info) ?? throw new InvalidDataException("Could not decode image.");
    }

    public void ClearRegion(TileRegion region)
    {
        var pixels = _bitmap.GetPixelSpan();
        var rowBytes = _bitmap.RowBytes;

        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            var row = pixels.Slice(y * rowBytes, rowBytes);
            row.Slice(region.X * BytesPerPixel, region.Width * BytesPerPixel).Clear();
        }
    }

    // Always swaps both blocks, never just overwrites: if the target cell is
    // empty, its pixels are already transparent (from an earlier delete), so
    // swapping them into the source cell has the same effect as clearing it.
    public void MoveRegion(TileRegion from, int targetX, int targetY)
    {
        var width = from.Width;
        var height = from.Height;
        var rowByteWidth = width * BytesPerPixel;
        var buffer = new byte[height * rowByteWidth];

        var pixels = _bitmap.GetPixelSpan();
        var rowBytes = _bitmap.RowBytes;

        for (var y = 0; y < height; y++)
        {
            var sourceRow = pixels.Slice((from.Y + y) * rowBytes + from.X * BytesPerPixel, rowByteWidth);
            sourceRow.CopyTo(buffer.AsSpan(y * rowByteWidth, rowByteWidth));
        }

        for (var y = 0; y < height; y++)
        {
            var sourceRow = pixels.Slice((from.Y + y) * rowBytes + from.X * BytesPerPixel, rowByteWidth);
            var targetRow = pixels.Slice((targetY + y) * rowBytes + targetX * BytesPerPixel, rowByteWidth);
            targetRow.CopyTo(sourceRow);
        }

        for (var y = 0; y < height; y++)
        {
            var targetRow = pixels.Slice((targetY + y) * rowBytes + targetX * BytesPerPixel, rowByteWidth);
            buffer.AsSpan(y * rowByteWidth, rowByteWidth).CopyTo(targetRow);
        }
    }

    // Builds a fresh canvas instead of moving blocks in place: the new size
    // differs, and source and target cells may overlap in the old layout.
    public void Repack(IReadOnlyList<TilePlacement> placements, int width, int height)
    {
        var repacked = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        repacked.Erase(SKColors.Transparent);

        var source = _bitmap.GetPixelSpan();
        var target = repacked.GetPixelSpan();

        foreach (var (original, moved) in placements)
        {
            var rowByteWidth = original.Width * BytesPerPixel;
            for (var y = 0; y < original.Height; y++)
            {
                var sourceRow = source.Slice((original.Y + y) * _bitmap.RowBytes + original.X * BytesPerPixel, rowByteWidth);
                var targetRow = target.Slice((moved.Y + y) * repacked.RowBytes + moved.X * BytesPerPixel, rowByteWidth);
                sourceRow.CopyTo(targetRow);
            }
        }

        _bitmap.Dispose();
        _bitmap = repacked;
    }

    public byte[] EncodeToPng()
    {
        using var data = _bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public void SaveAsPng(string filePath)
    {
        using var stream = File.Create(filePath);
        using var data = _bitmap.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }

    public void Dispose() => _bitmap.Dispose();
}
