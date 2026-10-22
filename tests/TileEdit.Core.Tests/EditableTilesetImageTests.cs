using SkiaSharp;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;

namespace TileEdit.Core.Tests;

public class EditableTilesetImageTests
{
    private static readonly SKColor Red = new(255, 0, 0, 255);
    private static readonly SKColor Blue = new(0, 0, 255, 255);
    private static readonly SKColor Transparent = new(0, 0, 0, 0);

    [Fact]
    public void Load_ReadsDimensionsOfTheFile()
    {
        var filePath = CreateSampleImage(4, 3, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);

            Assert.Equal(4, image.Width);
            Assert.Equal(3, image.Height);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetPixels_ExposesUnpremultipliedRgbaBytesRowByRow()
    {
        var filePath = CreateSampleImage(3, 2, new SKColor(200, 100, 50, 128));
        try
        {
            using var image = EditableTilesetImage.Load(filePath);

            var pixels = image.GetPixels();

            Assert.True(image.RowBytes >= 3 * 4);
            Assert.Equal(image.RowBytes * 2, pixels.Length);
            Assert.Equal([200, 100, 50, 128], pixels.Slice(image.RowBytes + 4, 4).ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void GetPixels_ReflectsEditsMadeAfterwards()
    {
        var filePath = CreateSampleImage(4, 4, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);
            image.ClearRegion(new TileRegion("tile_0001", 0, 0, 2, 2));

            var pixels = image.GetPixels();

            Assert.Equal([0, 0, 0, 0], pixels.Slice(0, 4).ToArray());
            Assert.Equal([255, 0, 0, 255], pixels.Slice(2 * 4, 4).ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void ClearRegion_MakesThePixelsInThatRegionTransparent()
    {
        var filePath = CreateSampleImage(4, 4, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);
            image.ClearRegion(new TileRegion("tile_0001", 0, 0, 2, 2));

            using var result = DecodeRgba(image.EncodeToPng());

            Assert.Equal(Transparent, result.GetPixel(0, 0));
            Assert.Equal(Transparent, result.GetPixel(1, 1));
            Assert.Equal(Red, result.GetPixel(2, 2));
            Assert.Equal(Red, result.GetPixel(3, 3));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    // A 6x2 strip of 2x2 tiles: red, blue, green.
    private static EditableTilesetImage LoadStrip(string filePath)
    {
        var green = new SKColor(0, 255, 0, 255);
        using (var image = new SKBitmap(new SKImageInfo(6, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
        {
            for (var y = 0; y < 2; y++)
            {
                for (var x = 0; x < 2; x++)
                    image.SetPixel(x, y, Red);
                for (var x = 2; x < 4; x++)
                    image.SetPixel(x, y, Blue);
                for (var x = 4; x < 6; x++)
                    image.SetPixel(x, y, green);
            }
            SavePng(image, filePath);
        }

        return EditableTilesetImage.Load(filePath);
    }

    [Fact]
    public void MoveRegions_OntoAnEmptyCell_RelocatesThePixelsAndClearsTheSource()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using var editable = LoadStrip(filePath);
            editable.ClearRegion(new TileRegion("blue", 2, 0, 2, 2));

            editable.MoveRegions([new TileRegion("red", 0, 0, 2, 2)], 2, 0);

            using var result = DecodeRgba(editable.EncodeToPng());
            Assert.Equal(Transparent, result.GetPixel(0, 0));
            Assert.Equal(Transparent, result.GetPixel(1, 1));
            Assert.Equal(Red, result.GetPixel(2, 0));
            Assert.Equal(Red, result.GetPixel(3, 1));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MoveRegions_OntoAnOccupiedCell_OverwritesItWithoutSwapping()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using var editable = LoadStrip(filePath);

            editable.MoveRegions([new TileRegion("red", 0, 0, 2, 2)], 2, 0);

            using var result = DecodeRgba(editable.EncodeToPng());
            Assert.Equal(Transparent, result.GetPixel(0, 0));
            Assert.Equal(Transparent, result.GetPixel(1, 1));
            Assert.Equal(Red, result.GetPixel(2, 0));
            Assert.Equal(Red, result.GetPixel(3, 1));
            Assert.Equal(new SKColor(0, 255, 0, 255), result.GetPixel(4, 0));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MoveRegions_OverlappingSourcesAndDestinations_ShiftsTheBlockAsOne()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using var editable = LoadStrip(filePath);

            editable.MoveRegions([new TileRegion("red", 0, 0, 2, 2), new TileRegion("blue", 2, 0, 2, 2)], 2, 0);

            using var result = DecodeRgba(editable.EncodeToPng());
            Assert.Equal(Transparent, result.GetPixel(0, 0));
            Assert.Equal(Transparent, result.GetPixel(1, 1));
            Assert.Equal(Red, result.GetPixel(2, 0));
            Assert.Equal(Blue, result.GetPixel(4, 0));
            Assert.Equal(Blue, result.GetPixel(5, 1));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void MoveRegions_DestinationOutsideTheImage_ClearsTheSourceAndWritesNothing()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using var editable = LoadStrip(filePath);

            editable.MoveRegions([new TileRegion("green", 4, 0, 2, 2)], 2, 0);

            using var result = DecodeRgba(editable.EncodeToPng());
            Assert.Equal(6, editable.Width);
            Assert.Equal(Transparent, result.GetPixel(4, 0));
            Assert.Equal(Transparent, result.GetPixel(5, 1));
            Assert.Equal(Blue, result.GetPixel(3, 0));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void SaveAsPng_WritesTheCurrentPixelsToDisk()
    {
        var sourcePath = CreateSampleImage(2, 2, Blue);
        var outputPath = Path.GetTempFileName() + ".png";
        try
        {
            using var image = EditableTilesetImage.Load(sourcePath);
            image.ClearRegion(new TileRegion("tile_0001", 0, 0, 1, 1));
            image.SaveAsPng(outputPath);

            using var result = DecodeRgba(File.ReadAllBytes(outputPath));

            Assert.Equal(Transparent, result.GetPixel(0, 0));
            Assert.Equal(Blue, result.GetPixel(1, 1));
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void SetPixel_ReplacesTheValueWithoutBlending()
    {
        var filePath = CreateSampleImage(3, 3, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);

            var painted = image.SetPixel(1, 2, new PixelColor(10, 20, 30, 0));

            Assert.True(painted);
            var pixels = image.GetPixels();
            Assert.Equal([10, 20, 30, 0], pixels.Slice(2 * image.RowBytes + 4, 4).ToArray());
            Assert.Equal([255, 0, 0, 255], pixels.Slice(2 * image.RowBytes, 4).ToArray());
            Assert.Equal([255, 0, 0, 255], pixels.Slice(2 * image.RowBytes + 8, 4).ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void TryGetPixel_ReturnsTheExactRgbaValue()
    {
        var filePath = CreateSampleImage(3, 3, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);
            image.SetPixel(1, 2, new PixelColor(10, 20, 30, 0));

            Assert.True(image.TryGetPixel(1, 2, out var color));
            Assert.Equal(new PixelColor(10, 20, 30, 0), color);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void TryGetPixel_OutsideTheImage_ReturnsFalse(int x, int y)
    {
        var filePath = CreateSampleImage(3, 3, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);

            Assert.False(image.TryGetPixel(x, y, out _));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(3, 0)]
    [InlineData(0, 3)]
    public void SetPixel_ClipsPositionsOutsideTheImage(int x, int y)
    {
        var filePath = CreateSampleImage(3, 3, Red);
        try
        {
            using var image = EditableTilesetImage.Load(filePath);
            var before = image.GetPixels().ToArray();

            var painted = image.SetPixel(x, y, PixelColor.Black);

            Assert.False(painted);
            Assert.Equal(before, image.GetPixels().ToArray());
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static string CreateSampleImage(int width, int height, SKColor color)
    {
        var filePath = Path.GetTempFileName() + ".png";
        using var image = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image.SetPixel(x, y, color);
        SavePng(image, filePath);
        return filePath;
    }

    private static void SavePng(SKBitmap image, string filePath)
    {
        using var stream = File.Create(filePath);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        data.SaveTo(stream);
    }

    private static SKBitmap DecodeRgba(byte[] encodedImageBytes)
    {
        var bounds = SKBitmap.DecodeBounds(encodedImageBytes);
        var info = new SKImageInfo(bounds.Width, bounds.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        return SKBitmap.Decode(encodedImageBytes, info)!;
    }
}
