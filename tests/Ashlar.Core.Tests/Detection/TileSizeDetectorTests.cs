using Ashlar.Core.Detection;

namespace Ashlar.Core.Tests.Detection;

public class TileSizeDetectorTests
{
    [Fact]
    public void DetectCandidates_SquareImageWithCommonDivisors_RanksTypicalSizesFirst()
    {
        var candidates = TileSizeDetector.DetectCandidates(320, 320);

        var topTwo = candidates.Take(2).Select(c => c.TileWidth).ToList();
        Assert.Equal([16, 32], topTwo);
    }

    [Fact]
    public void DetectCandidates_OnlyCommonDivisorIsSquare_ReturnsSquareCandidates()
    {
        var candidates = TileSizeDetector.DetectCandidates(320, 320);

        Assert.All(candidates, c => Assert.Equal(c.TileWidth, c.TileHeight));
    }

    [Fact]
    public void DetectCandidates_NoDivisorMatchesACommonSize_FallsBackToClosestToTypicalSize()
    {
        // gcd(111, 222) = 111, whose only divisors are 1, 3, 37 and 111 - none of them
        // a common tile size. 37 is the closest to the typical fallback size of 32.
        var candidates = TileSizeDetector.DetectCandidates(111, 222);

        Assert.Equal((37, 37), candidates[0]);
    }

    [Fact]
    public void DetectCandidates_DimensionsShareNoDivisorBeyondOne_StillReturnsACandidate()
    {
        var candidates = TileSizeDetector.DetectCandidates(7, 13);

        Assert.Equal((1, 1), candidates[0]);
    }
}
