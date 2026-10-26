using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class FloodFillerTests
{
    private static readonly PixelColor Red = new(255, 0, 0, 255);
    private static readonly PixelColor Blue = new(0, 0, 255, 255);
    private static readonly PixelColor Clear = new(0, 0, 0, 0);

    // One character per pixel: '.' transparent, 'R' red, 'B' blue.
    private static byte[] Parse(params string[] rows)
    {
        var pixels = new List<byte>();
        foreach (var color in rows.SelectMany(r => r).Select(c => c switch { 'R' => Red, 'B' => Blue, _ => Clear }))
            pixels.AddRange([color.R, color.G, color.B, color.A]);
        return pixels.ToArray();
    }

    private static string[] Render(byte[] pixels, int width) =>
        pixels.Chunk(4)
            .Select(p => p[3] == 0 ? '.' : p[0] == 255 ? 'R' : 'B')
            .Chunk(width)
            .Select(row => new string(row))
            .ToArray();

    private static List<int> Fill(byte[] pixels, int width, int height, int x, int y, PixelColor color) =>
        FloodFiller.Fill(pixels, width * 4, width, height, x, y, color);

    [Fact]
    public void Fill_OfASinglePixelRegionChangesOnlyThatPixel()
    {
        var pixels = Parse("RRR", "RBR", "RRR");

        var filled = Fill(pixels, 3, 3, 1, 1, Red);

        Assert.Equal([4], filled);
        Assert.Equal(["RRR", "RRR", "RRR"], Render(pixels, 3));
    }

    [Fact]
    public void Fill_StopsAtPixelsOfAnotherColor()
    {
        var pixels = Parse("..B..", "..B..", "..B..");

        var filled = Fill(pixels, 5, 3, 0, 0, Red);

        Assert.Equal(6, filled.Count);
        Assert.Equal(["RRB..", "RRB..", "RRB.."], Render(pixels, 5));
    }

    [Fact]
    public void Fill_DoesNotLeakDiagonally()
    {
        var pixels = Parse(".B", "B.");

        Fill(pixels, 2, 2, 0, 0, Red);

        Assert.Equal(["RB", "B."], Render(pixels, 2));
    }

    [Fact]
    public void Fill_ReachesAllFourImageEdges()
    {
        var pixels = Parse("...", "...", "...");

        var filled = Fill(pixels, 3, 3, 1, 1, Red);

        Assert.Equal(9, filled.Count);
        Assert.Equal(["RRR", "RRR", "RRR"], Render(pixels, 3));
    }

    [Fact]
    public void Fill_WithTheSameColorChangesNothing()
    {
        var pixels = Parse("RR", "RR");

        var filled = Fill(pixels, 2, 2, 0, 0, Red);

        Assert.Empty(filled);
    }

    [Fact]
    public void Fill_ComparesAllChannelsIncludingAlpha()
    {
        var pixels = Parse("RR");
        pixels[7] = 254;

        var filled = Fill(pixels, 2, 1, 0, 0, Blue);

        Assert.Equal([0], filled);
        Assert.Equal([0, 0, 255, 255], pixels[..4]);
        Assert.Equal([255, 0, 0, 254], pixels[4..]);
    }

    [Fact]
    public void Fill_WritesTheColorWithoutBlending()
    {
        var pixels = Parse("R");

        Fill(pixels, 1, 1, 0, 0, new PixelColor(10, 20, 30, 128));

        Assert.Equal([10, 20, 30, 128], pixels);
    }

    [Fact]
    public void Fill_OutsideTheImageChangesNothing()
    {
        var pixels = Parse("..");

        Assert.Empty(Fill(pixels, 2, 1, 2, 0, Red));
        Assert.Empty(Fill(pixels, 2, 1, 0, -1, Red));
    }

    [Fact]
    public void Fill_SkipsRowPadding()
    {
        var pixels = new byte[2 * 12];
        FloodFiller.Fill(pixels, 12, 2, 2, 0, 0, Red);

        Assert.Equal([255, 0, 0, 255, 255, 0, 0, 255, 0, 0, 0, 0], pixels[..12]);
        Assert.Equal([255, 0, 0, 255, 255, 0, 0, 255, 0, 0, 0, 0], pixels[12..]);
    }

    [Fact]
    public void Fill_HandlesAHugeRegionWithoutOverflowingTheStack()
    {
        const int size = 2000;
        var pixels = new byte[size * size * 4];

        var filled = FloodFiller.Fill(pixels, size * 4, size, size, 0, 0, Red);

        Assert.Equal(size * size, filled.Count);
    }
}
