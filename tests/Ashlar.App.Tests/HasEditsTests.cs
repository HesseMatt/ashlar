using Avalonia.Headless.XUnit;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class HasEditsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void NoEditsAfterImportingAnImage()
    {
        var viewModel = OpenImage();

        Assert.False(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void DeletingATileCountsAsAnEdit()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);

        viewModel.Editor().DeleteSelectedTiles();

        Assert.True(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void MovingATileCountsAsAnEdit()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(17, 1);
        viewModel.Editor().CommitMoveDrag();

        Assert.True(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void MoveWithoutNetOffsetIsNotAnEdit()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(17, 1);
        viewModel.Editor().UpdateMoveDrag(5, 5);
        viewModel.Editor().CommitMoveDrag();

        Assert.False(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void TilesetStaysEditedAfterFurtherEdits()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.Editor().HandleCanvasClick(17, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        Assert.True(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ImportingANewImageClearsTheEdits()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.LoadTileset(WriteImage("other.png"), 16, 16);

        Assert.False(viewModel.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ProjectWithoutEditsOpensWithoutEdits()
    {
        var projectPath = SaveProject(OpenImage());

        var reopened = new MainViewModel();
        reopened.OpenProject(projectPath);
        reopened.OpenFirstTileset();

        Assert.False(reopened.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void UneditedProjectWithEmptyCellsOpensWithoutEdits()
    {
        var path = Path.Combine(_directory, "sparse.png");
        using (var bitmap = new SKBitmap(new SKImageInfo(64, 64, SKColorType.Rgba8888, SKAlphaType.Unpremul)))
        {
            bitmap.Erase(SKColors.Transparent);
            bitmap.SetPixel(0, 0, SKColors.Red);
            using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(path, data.ToArray());
        }

        var viewModel = new MainViewModel();
        viewModel.LoadTileset(path, 16, 16);
        var reopened = new MainViewModel();
        reopened.OpenProject(SaveProject(viewModel));
        reopened.OpenFirstTileset();

        Assert.Single(reopened.Editor().Tiles);
        Assert.False(reopened.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void ProjectWithEditsOpensWithEdits()
    {
        var viewModel = OpenImage();
        MoveFirstTileOneCellRight(viewModel);
        var projectPath = SaveProject(viewModel);

        var reopened = new MainViewModel();
        reopened.OpenProject(projectPath);
        reopened.OpenFirstTileset();

        Assert.True(reopened.Editor().HasEdits);
    }

    [AvaloniaFact]
    public void OpeningAnUneditedProjectClearsTheEdits()
    {
        var unedited = SaveProject(OpenImage());
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.OpenProject(unedited);

        viewModel.OpenFirstTileset();

        Assert.False(viewModel.Editor().HasEdits);
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("tiles.png"), 16, 16);
        return viewModel;
    }

    // A moved tile keeps its name, so unlike a deletion (which just leaves an
    // empty cell) the result differs from a grid freshly built from the image.
    private static void MoveFirstTileOneCellRight(MainViewModel viewModel)
    {
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;
        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(1 + viewModel.Editor().TileWidth, 1);
        viewModel.Editor().CommitMoveDrag();
    }

    private string SaveProject(MainViewModel viewModel)
    {
        var path = Path.Combine(_directory, $"{Guid.NewGuid():N}.ashlar");
        viewModel.SaveProject(path);
        return path;
    }

    private string WriteImage(string fileName)
    {
        var path = Path.Combine(_directory, fileName);
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
