using SkiaSharp;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core;

public sealed class EditableTilesetImage : IDisposable
{
    private const int BytesPerPixel = 4;

    private readonly SKBitmap _bitmap;

    private EditableTilesetImage(SKBitmap bitmap)
    {
        _bitmap = bitmap;
    }

    public int Width => _bitmap.Width;

    public int Height => _bitmap.Height;

    public int RowBytes => _bitmap.RowBytes;

    // Unpremultiplied RGBA bytes, RowBytes apart. Only valid until the image is disposed.
    public ReadOnlySpan<byte> GetPixels() => _bitmap.GetPixelSpan();

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

    // Tightly packed unpremultiplied RGBA bytes of one region, without the row padding of the image itself.
    public byte[] CopyRegion(int x, int y, int width, int height)
    {
        var pixels = _bitmap.GetPixelSpan();
        var rowByteWidth = width * BytesPerPixel;
        var copy = new byte[height * rowByteWidth];

        for (var row = 0; row < height; row++)
        {
            var sourceRow = pixels.Slice((y + row) * _bitmap.RowBytes + x * BytesPerPixel, rowByteWidth);
            sourceRow.CopyTo(copy.AsSpan(row * rowByteWidth, rowByteWidth));
        }

        return copy;
    }

    // Pixels are RGBA, so every fourth byte (starting at offset 3) is an alpha value.
    public bool IsRegionEmpty(int x, int y, int width, int height)
    {
        var pixels = _bitmap.GetPixelSpan();

        for (var row = y; row < y + height; row++)
        {
            var rowStart = row * _bitmap.RowBytes + x * BytesPerPixel;
            for (var offset = 3; offset < width * BytesPerPixel; offset += BytesPerPixel)
            {
                if (pixels[rowStart + offset] != 0)
                    return false;
            }
        }

        return true;
    }

    // Replaces the pixel rather than blending, so a color with alpha 0 erases
    // it. Returns false for a position outside the image, which is skipped.
    public bool SetPixel(int x, int y, PixelColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return false;

        var offset = y * _bitmap.RowBytes + x * BytesPerPixel;
        var pixels = _bitmap.GetPixelSpan();
        pixels[offset] = color.R;
        pixels[offset + 1] = color.G;
        pixels[offset + 2] = color.B;
        pixels[offset + 3] = color.A;
        return true;
    }

    public bool TryGetPixel(int x, int y, out PixelColor color)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            color = default!;
            return false;
        }

        var offset = y * _bitmap.RowBytes + x * BytesPerPixel;
        var pixels = _bitmap.GetPixelSpan();
        color = new PixelColor(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
        return true;
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

    // Snapshots every block before touching the image, then clears all sources
    // and pastes the copies, so overlapping sources and destinations behave as
    // one simultaneous move. Blocks landing outside the image are dropped.
    public void MoveRegions(IReadOnlyList<TileRegion> regions, int offsetX, int offsetY)
    {
        var pixels = _bitmap.GetPixelSpan();
        var rowBytes = _bitmap.RowBytes;

        var snapshots = new List<byte[]>(regions.Count);
        foreach (var region in regions)
        {
            var rowByteWidth = region.Width * BytesPerPixel;
            var snapshot = new byte[region.Height * rowByteWidth];
            for (var y = 0; y < region.Height; y++)
            {
                var sourceRow = pixels.Slice((region.Y + y) * rowBytes + region.X * BytesPerPixel, rowByteWidth);
                sourceRow.CopyTo(snapshot.AsSpan(y * rowByteWidth, rowByteWidth));
            }
            snapshots.Add(snapshot);
        }

        foreach (var region in regions)
            ClearRegion(region);

        for (var i = 0; i < regions.Count; i++)
        {
            var destination = regions[i] with { X = regions[i].X + offsetX, Y = regions[i].Y + offsetY };
            if (!TileMover.IsInside(destination, Width, Height))
                continue;

            var rowByteWidth = destination.Width * BytesPerPixel;
            for (var y = 0; y < destination.Height; y++)
            {
                var targetRow = pixels.Slice((destination.Y + y) * rowBytes + destination.X * BytesPerPixel, rowByteWidth);
                snapshots[i].AsSpan(y * rowByteWidth, rowByteWidth).CopyTo(targetRow);
            }
        }
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
