using SkiaSharp;
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

    [Fact]
    public void MoveRegion_OntoAnEmptyCell_RelocatesThePixelsAndClearsTheSource()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using (var image = new SKBitmap(new SKImageInfo(4, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                for (var y = 0; y < 2; y++)
                    for (var x = 0; x < 2; x++)
                        image.SetPixel(x, y, Red);
                SavePng(image, filePath);
            }

            using var editable = EditableTilesetImage.Load(filePath);
            editable.MoveRegion(new TileRegion("tile_0001", 0, 0, 2, 2), 2, 0);

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
    public void MoveRegion_OntoAnOccupiedCell_SwapsBothTilesPixels()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using (var image = new SKBitmap(new SKImageInfo(4, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var x = 0; x < 2; x++)
                        image.SetPixel(x, y, Red);
                    for (var x = 2; x < 4; x++)
                        image.SetPixel(x, y, Blue);
                }
                SavePng(image, filePath);
            }

            using var editable = EditableTilesetImage.Load(filePath);
            editable.MoveRegion(new TileRegion("tile_0001", 0, 0, 2, 2), 2, 0);

            using var result = DecodeRgba(editable.EncodeToPng());

            Assert.Equal(Blue, result.GetPixel(0, 0));
            Assert.Equal(Blue, result.GetPixel(1, 1));
            Assert.Equal(Red, result.GetPixel(2, 0));
            Assert.Equal(Red, result.GetPixel(3, 1));
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
