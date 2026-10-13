using SkiaSharp;
using TileEdit.Core;
using TileEdit.Core.Model;

namespace TileEdit.Core.Tests;

public class TilesetGridBuilderTests
{
    [Fact]
    public void Build_EvenlyDivisibleImage_CoversWholeGridInRowMajorOrder()
    {
        var tiles = BuildFor("dungeon.png", 40, 40, 20, 20);

        Assert.Equal(
            [(0, 0), (20, 0), (0, 20), (20, 20)],
            tiles.Select(t => (t.X, t.Y)).ToList());
    }

    [Fact]
    public void Build_AssignsSequentialFourDigitNamesWithTilesetBaseName()
    {
        var tiles = BuildFor("dungeon.png", 40, 20, 20, 20);

        Assert.Equal(["dungeon_0001", "dungeon_0002"], tiles.Select(t => t.Name).ToList());
    }

    [Fact]
    public void Build_LeftoverPixelsThatDontFillAFullTile_AreNotTurnedIntoATile()
    {
        var tiles = BuildFor("dungeon.png", 45, 45, 20, 20);

        // Only full 20x20 cells fit twice in each direction (40 <= 45 < 60); the leftover
        // 5-pixel strip on the right and bottom is dropped rather than becoming a partial tile.
        Assert.Equal(4, tiles.Count);
        Assert.All(tiles, t => Assert.Equal((20, 20), (t.Width, t.Height)));
    }

    [Fact]
    public void Build_SanitizesTilesetBaseNameFromFileName()
    {
        var tiles = BuildFor("My Tileset!.png", 20, 20, 20, 20);

        Assert.Equal("My_Tileset_0001", tiles.Single().Name);
    }

    [Fact]
    public void Build_FullyTransparentCells_AreSkippedAndLeaveGapsInTheNumbering()
    {
        using var bitmap = NewTransparentBitmap(60, 20);
        bitmap.SetPixel(0, 0, SKColors.Red);
        bitmap.SetPixel(45, 10, SKColors.Blue);

        var tiles = Build(bitmap, "dungeon.png", 20, 20);

        Assert.Equal(["dungeon_0001", "dungeon_0003"], tiles.Select(t => t.Name).ToList());
        Assert.Equal([(0, 0), (40, 0)], tiles.Select(t => (t.X, t.Y)).ToList());
    }

    [Fact]
    public void Build_TransparentPixelsWithColor_AreStillConsideredEmpty()
    {
        using var bitmap = NewTransparentBitmap(20, 20);
        bitmap.SetPixel(5, 5, new SKColor(255, 0, 0, 0));

        Assert.Empty(Build(bitmap, "dungeon.png", 20, 20));
    }

    [Fact]
    public void Build_SingleNearlyTransparentPixel_MakesTheCellATile()
    {
        using var bitmap = NewTransparentBitmap(20, 20);
        bitmap.SetPixel(19, 19, new SKColor(0, 0, 0, 1));

        Assert.Single(Build(bitmap, "dungeon.png", 20, 20));
    }

    [Fact]
    public void Build_FullyTransparentImage_ResultsInNoTiles()
    {
        using var bitmap = NewTransparentBitmap(40, 40);

        Assert.Empty(Build(bitmap, "dungeon.png", 20, 20));
    }

    private static IReadOnlyList<TileRegion> BuildFor(
        string fileName, int imageWidth, int imageHeight, int tileWidth, int tileHeight)
    {
        using var bitmap = new SKBitmap(imageWidth, imageHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Red);
        return Build(bitmap, fileName, tileWidth, tileHeight);
    }

    private static IReadOnlyList<TileRegion> Build(
        SKBitmap bitmap, string fileName, int tileWidth, int tileHeight)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var image = EditableTilesetImage.Load(data.ToArray());
        return TilesetGridBuilder.Build(fileName, image, tileWidth, tileHeight);
    }

    private static SKBitmap NewTransparentBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        return bitmap;
    }
}
