using Avalonia.Headless.XUnit;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.Core.Model;

namespace Ashlar.App.Tests;

public class ClearMapCellsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void DeletingATileEmptiesItsCellsOnlyInMapsOfThatTileset()
    {
        var viewModel = ProjectWithMaps();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.HasUnsavedChanges = false;

        viewModel.Editor().DeleteSelectedTiles();

        Assert.Equal<string?[]>([null, "forest_0002", null, null], Map(viewModel, "forestMap").Cells);
        Assert.Equal<string?[]>(["forest_0001", "forest_0002", null, null], Map(viewModel, "dungeonMap").Cells);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void MovingTilesOutsideTheCanvasEmptiesTheirCells()
    {
        var viewModel = ProjectWithMaps();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(1, -40);
        viewModel.Editor().CommitMoveDrag();

        Assert.Equal<string?[]>([null, "forest_0002", null, null], Map(viewModel, "forestMap").Cells);
    }

    [AvaloniaFact]
    public void MovingATileOntoAnotherOneEmptiesTheCellsOfTheOverwrittenTile()
    {
        var viewModel = ProjectWithMaps();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(17, 1);
        viewModel.Editor().CommitMoveDrag();

        Assert.Equal<string?[]>(["forest_0001", null, null, null], Map(viewModel, "forestMap").Cells);
    }

    [AvaloniaFact]
    public void MovingTilesInsideTheCanvasChangesNothingInMaps()
    {
        var viewModel = ProjectWithMaps();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(1, 33);
        viewModel.Editor().CommitMoveDrag();

        Assert.Equal<string?[]>(["forest_0001", "forest_0002", null, null], Map(viewModel, "forestMap").Cells);
    }

    [AvaloniaFact]
    public void ChangingTheTileSizeEmptiesCellsBecauseTheGridIsRebuilt()
    {
        var viewModel = ProjectWithMaps();
        var forestMap = Map(viewModel, "forestMap");
        forestMap.Cells[2] = "forest_0016";

        viewModel.Editor().ChangeTileSize(32, 32);

        Assert.All(forestMap.Cells, Assert.Null);
    }

    [AvaloniaFact]
    public void RedoingADeletionEmptiesTheCellsAgain()
    {
        var viewModel = ProjectWithMaps();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().Undo();
        Map(viewModel, "forestMap").Cells[0] = "forest_0001";

        viewModel.Editor().Redo();

        Assert.Null(Map(viewModel, "forestMap").Cells[0]);
    }

    private static TileMap Map(MainViewModel viewModel, string name) =>
        viewModel.GetMap(name)!;

    private MainViewModel ProjectWithMaps()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("dungeon"), 16, 16);
        viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        viewModel.CreateMap("dungeonMap", 2, 2, "dungeon");
        viewModel.CreateMap("forestMap", 2, 2, "forest");
        // The dungeon map deliberately holds names that exist in the forest tileset.
        PutNames(Map(viewModel, "dungeonMap"));
        PutNames(Map(viewModel, "forestMap"));
        viewModel.OpenTileset("forest");
        return viewModel;
    }

    private static void PutNames(TileMap map)
    {
        map.Cells[0] = "forest_0001";
        map.Cells[1] = "forest_0002";
    }

    private string WriteImage(string name)
    {
        var path = Path.Combine(_directory, name + ".png");
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
