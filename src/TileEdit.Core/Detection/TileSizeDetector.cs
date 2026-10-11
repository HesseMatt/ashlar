namespace TileEdit.Core.Detection;

public static class TileSizeDetector
{
    // Ordered by how often each size is actually used for tiles in 2D games,
    // not by numeric value - 16 and 32 dominate in practice, the rest trail off.
    private static readonly int[] CommonSizesByLikelihood = [16, 32, 8, 24, 48, 64, 96, 128];

    private const int TypicalFallbackSize = 32;

    public static IReadOnlyList<(int TileWidth, int TileHeight)> DetectCandidates(int imageWidth, int imageHeight)
    {
        var maxTileSize = Gcd(imageWidth, imageHeight);

        return DivisorsOf(maxTileSize)
            .OrderBy(RankOf)
            .Select(size => (size, size))
            .ToList();
    }

    // Common sizes are ranked by their position in CommonSizesByLikelihood. Anything else
    // is ranked below all of them, closest-to-32 first, since that's the most plausible
    // guess when no divisor matches a known tile size.
    private static int RankOf(int size)
    {
        var index = Array.IndexOf(CommonSizesByLikelihood, size);
        return index >= 0 ? index : CommonSizesByLikelihood.Length + Math.Abs(size - TypicalFallbackSize);
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    private static List<int> DivisorsOf(int n)
    {
        var divisors = new List<int>();
        for (var candidate = 1; candidate <= n; candidate++)
        {
            if (n % candidate == 0)
                divisors.Add(candidate);
        }
        return divisors;
    }
}
