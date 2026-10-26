using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

public static class FloodFiller
{
    private const int BytesPerPixel = 4;

    // Replaces the 4-connected region of pixels that exactly match the start
    // pixel with the new color and returns where they sit, as y * width + x.
    // Empty when the start lies outside the image or already has that color.
    // Uses an explicit stack, so a large region cannot overflow the call stack.
    public static List<int> Fill(Span<byte> pixels, int rowBytes, int width, int height, int startX, int startY, PixelColor color)
    {
        var filled = new List<int>();
        if (startX < 0 || startY < 0 || startX >= width || startY >= height)
            return filled;

        var target = ReadPixel(pixels, rowBytes, startX, startY);
        if (target == color)
            return filled;

        var pending = new Stack<int>();
        WritePixel(pixels, rowBytes, startX, startY, color);
        pending.Push(startY * width + startX);

        while (pending.Count > 0)
        {
            var position = pending.Pop();
            filled.Add(position);
            var x = position % width;
            var y = position / width;

            // Painting a pixel when it is queued keeps it from being queued
            // again, since it no longer matches the target color.
            if (x > 0) TryQueue(pixels, rowBytes, x - 1, y, width, target, color, pending);
            if (x < width - 1) TryQueue(pixels, rowBytes, x + 1, y, width, target, color, pending);
            if (y > 0) TryQueue(pixels, rowBytes, x, y - 1, width, target, color, pending);
            if (y < height - 1) TryQueue(pixels, rowBytes, x, y + 1, width, target, color, pending);
        }

        return filled;
    }

    private static void TryQueue(
        Span<byte> pixels, int rowBytes, int x, int y, int width, PixelColor target, PixelColor color, Stack<int> pending)
    {
        var offset = y * rowBytes + x * BytesPerPixel;
        if (pixels[offset] != target.R || pixels[offset + 1] != target.G ||
            pixels[offset + 2] != target.B || pixels[offset + 3] != target.A)
            return;

        WritePixel(pixels, rowBytes, x, y, color);
        pending.Push(y * width + x);
    }

    private static PixelColor ReadPixel(ReadOnlySpan<byte> pixels, int rowBytes, int x, int y)
    {
        var offset = y * rowBytes + x * BytesPerPixel;
        return new PixelColor(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]);
    }

    private static void WritePixel(Span<byte> pixels, int rowBytes, int x, int y, PixelColor color)
    {
        var offset = y * rowBytes + x * BytesPerPixel;
        pixels[offset] = color.R;
        pixels[offset + 1] = color.G;
        pixels[offset + 2] = color.B;
        pixels[offset + 3] = color.A;
    }
}
