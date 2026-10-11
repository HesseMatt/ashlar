using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TileEdit.Core.Model;

namespace TileEdit.Core.Tests;

public class EditableTilesetImageTests
{
    private static readonly Rgba32 Red = new(255, 0, 0, 255);
    private static readonly Rgba32 Blue = new(0, 0, 255, 255);

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

            using var result = Image.Load<Rgba32>(image.EncodeToPng());

            Assert.Equal(default(Rgba32), result[0, 0]);
            Assert.Equal(default(Rgba32), result[1, 1]);
            Assert.Equal(Red, result[2, 2]);
            Assert.Equal(Red, result[3, 3]);
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

            using var result = Image.Load<Rgba32>(outputPath);

            Assert.Equal(default(Rgba32), result[0, 0]);
            Assert.Equal(Blue, result[1, 1]);
        }
        finally
        {
            File.Delete(sourcePath);
            File.Delete(outputPath);
        }
    }

    private static string CreateSampleImage(int width, int height, Rgba32 color)
    {
        var filePath = Path.GetTempFileName() + ".png";
        using var image = new Image<Rgba32>(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = color;
        image.SaveAsPng(filePath);
        return filePath;
    }
}
