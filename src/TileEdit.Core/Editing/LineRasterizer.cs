namespace TileEdit.Core.Editing;

public static class LineRasterizer
{
    // Bresenham's line algorithm. Both end points are included, and
    // consecutive points are always neighbours, so a stroke has no gaps.
    public static IEnumerable<(int X, int Y)> Line(int fromX, int fromY, int toX, int toY)
    {
        var deltaX = Math.Abs(toX - fromX);
        var deltaY = -Math.Abs(toY - fromY);
        var stepX = fromX < toX ? 1 : -1;
        var stepY = fromY < toY ? 1 : -1;
        var error = deltaX + deltaY;

        var x = fromX;
        var y = fromY;
        while (true)
        {
            yield return (x, y);
            if (x == toX && y == toY)
                yield break;

            var doubledError = 2 * error;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }
            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
        }
    }
}
