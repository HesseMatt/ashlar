using Ashlar.Core.Model;
using Ashlar.Core.Rendering;

namespace Ashlar.Core.Tests.Rendering;

public class TileMapImageRendererTests
{
    private static readonly PixelColor Red = new(255, 0, 0, 255);
    private static readonly PixelColor Blue = new(0, 0, 255, 255);

    // Two 2x2 tiles side by side: "red" on the left, "blue" on the right.
    private static (EditableTilesetImage Image, List<TileRegion> Tiles) CreateTileset()
    {
        var image = EditableTilesetImage.CreateBlank(4, 2);
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                image.SetPixel(x, y, Red);
                image.SetPixel(x + 2, y, Blue);
            }
        }

        return (image, [new TileRegion("red", 0, 0, 2, 2), new TileRegion("blue", 2, 0, 2, 2)]);
    }

    private static PixelColor PixelAt(EditableTilesetImage image, int x, int y)
    {
        Assert.True(image.TryGetPixel(x, y, out var color));
        return color;
    }

    [Fact]
    public void Render_SizesTheImageByCellsTimesTileSize()
    {
        var (tilesetImage, tiles) = CreateTileset();
        using var _ = tilesetImage;
        var map = TileMap.CreateEmpty("m", "t", 3, 2);

        using var image = TileMapImageRenderer.Render(map, tiles, tilesetImage, 2, 2);

        Assert.Equal(6, image.Width);
        Assert.Equal(4, image.Height);
    }

    [Fact]
    public void Render_PlacesTilesAtTheirCellsAndLeavesEmptyCellsTransparent()
    {
        var (tilesetImage, tiles) = CreateTileset();
        using var _ = tilesetImage;
        var map = TileMap.CreateEmpty("m", "t", 2, 2);
        map[0, 0] = "blue";
        map[1, 1] = "red";

        using var image = TileMapImageRenderer.Render(map, tiles, tilesetImage, 2, 2);

        Assert.Equal(Blue, PixelAt(image, 1, 1));
        Assert.Equal(0, PixelAt(image, 2, 0).A);
        Assert.Equal(0, PixelAt(image, 1, 2).A);
        Assert.Equal(Red, PixelAt(image, 2, 2));
        Assert.Equal(Red, PixelAt(image, 3, 3));
    }

    [Fact]
    public void Render_LeavesCellsOfDeletedTilesTransparent()
    {
        var (tilesetImage, tiles) = CreateTileset();
        using var _ = tilesetImage;
        var map = TileMap.CreateEmpty("m", "t", 1, 1);
        map[0, 0] = "gone";

        using var image = TileMapImageRenderer.Render(map, tiles, tilesetImage, 2, 2);

        Assert.Equal(0, PixelAt(image, 0, 0).A);
    }
}
