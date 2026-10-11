using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using TileEdit.Core;

namespace TileEdit.Core.Tests;

public class TilesetImageLoaderTests
{
    [Fact]
    public void Load_ReadsDimensionsAndRawBytesOfTheFile()
    {
        var filePath = Path.GetTempFileName() + ".png";
        try
        {
            using (var image = new Image<Rgba32>(4, 3))
                image.SaveAsPng(filePath);

            var result = TilesetImageLoader.Load(filePath);

            Assert.Equal(4, result.Width);
            Assert.Equal(3, result.Height);
            Assert.Equal(File.ReadAllBytes(filePath), result.Bytes);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
