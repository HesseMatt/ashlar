using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.Core.Tests.Editing;

public class MapStateTests
{
    [Fact]
    public void CaptureCopiesTheCellsSoLaterEditsDoNotChangeIt()
    {
        var map = TileMap.CreateEmpty("level", "forest", 2, 2);
        map[0, 0] = "forest_0001";

        var state = MapState.Capture(map);
        map[0, 0] = "forest_0002";

        Assert.Equal("forest_0001", state.Cells[0]);
        Assert.Equal((2, 2), (state.Width, state.Height));
    }

    [Fact]
    public void ToMapBuildsAnIndependentMap()
    {
        var state = new MapState(2, 1, ["forest_0001", null]);

        var map = state.ToMap("level", "forest");
        map[1, 0] = "forest_0003";

        Assert.Equal(("level", "forest"), (map.Name, map.TilesetName));
        Assert.Equal("forest_0001", map[0, 0]);
        Assert.Null(state.Cells[1]);
    }
}
