using Ashlar.Core.Editing;

namespace Ashlar.Core.Tests.Editing;

public class LineRasterizerTests
{
    [Fact]
    public void Line_OfASinglePointYieldsThatPoint()
    {
        Assert.Equal([(3, 4)], LineRasterizer.Line(3, 4, 3, 4));
    }

    [Fact]
    public void Line_IncludesBothEndPoints()
    {
        var points = LineRasterizer.Line(0, 0, 5, 2).ToList();

        Assert.Equal((0, 0), points.First());
        Assert.Equal((5, 2), points.Last());
    }

    [Theory]
    [InlineData(0, 0, 10, 3)]
    [InlineData(10, 3, 0, 0)]
    [InlineData(2, 9, 4, -20)]
    [InlineData(-7, 5, 8, 5)]
    [InlineData(6, -3, 6, 12)]
    public void Line_NeverSkipsAPixel(int fromX, int fromY, int toX, int toY)
    {
        var points = LineRasterizer.Line(fromX, fromY, toX, toY).ToList();

        Assert.Equal(Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY)) + 1, points.Count);
        for (var i = 1; i < points.Count; i++)
        {
            Assert.True(Math.Abs(points[i].X - points[i - 1].X) <= 1);
            Assert.True(Math.Abs(points[i].Y - points[i - 1].Y) <= 1);
        }
    }

    [Fact]
    public void Line_FollowsADiagonalExactly()
    {
        Assert.Equal([(0, 0), (1, 1), (2, 2), (3, 3)], LineRasterizer.Line(0, 0, 3, 3));
    }
}
