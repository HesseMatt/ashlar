using Avalonia.Headless.XUnit;
using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class UnsavedChangesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void NothingIsUnsavedBeforeAnythingIsOpened()
    {
        Assert.False(new MainViewModel().HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void FreshlyImportedTilesetCountsAsUnsaved()
    {
        Assert.True(OpenImage().HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void FreshlyOpenedProjectCountsAsSaved()
    {
        var projectPath = SaveProject(OpenImage());
        var viewModel = new MainViewModel();

        viewModel.OpenProject(projectPath);

        viewModel.OpenFirstTileset();

        Assert.False(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void SavingTheProjectMarksTheStateAsSaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        Assert.True(saved);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void FailedSaveKeepsTheStateUnsaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "missing", "a.ashlar"));

        Assert.False(saved);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void SavingImageOrExportingXmlDoesNotCountAsSaving()
    {
        var viewModel = OpenImage();
        var projectPath = SaveProject(viewModel);
        viewModel.OpenProject(projectPath);
        viewModel.OpenFirstTileset();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.SaveImage(Path.Combine(_directory, "out.png"));
        viewModel.ExportXml(Path.Combine(_directory, "out.xml"));

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void EditsMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void DrawingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor().IsDrawToolActive = true;

        viewModel.Editor().BeginStroke(1, 1);
        viewModel.Editor().CommitStroke();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void MovingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().IsMoveToolActive = true;

        viewModel.Editor().BeginMoveDrag(1, 1);
        viewModel.Editor().UpdateMoveDrag(17, 1);
        viewModel.Editor().CommitMoveDrag();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void AssigningAGroupMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().GroupNameInput = "walls";

        viewModel.Editor().ConfirmGroupNameInput();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void UndoAndRedoMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenImage();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Editor().Undo();
        Assert.True(viewModel.HasUnsavedChanges);

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Editor().Redo();
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ChangingTheTileSizeMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.Editor().ChangeTileSize(32, 32);

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ClosingTheProjectReturnsToTheStartupState()
    {
        var viewModel = OpenImage();
        var initial = new MainViewModel();
        viewModel.Editor().HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor().DeleteSelectedTiles();
        viewModel.Editor().ZoomIn();

        viewModel.CloseProject();

        Assert.Empty(viewModel.OpenTabs);
        Assert.Null(viewModel.ActiveTab);
        Assert.Empty(viewModel.TilesetNames);
        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.HasUnsavedChanges);
        Assert.Equal(initial.StatusText, viewModel.StatusText);
    }

    [AvaloniaFact]
    public void ATilesetCanBeImportedAgainAfterClosing()
    {
        var viewModel = OpenImage();
        viewModel.CloseProject();

        viewModel.LoadTileset(WriteImage("again.png"), 32, 32);

        Assert.True(viewModel.Editor().HasImage);
        Assert.Equal(4, viewModel.Editor().Tiles.Count);
    }

    private MainViewModel OpenSavedProject()
    {
        var viewModel = new MainViewModel();
        viewModel.OpenProject(SaveProject(OpenImage()));
        viewModel.OpenFirstTileset();
        return viewModel;
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("tiles.png"), 16, 16);
        return viewModel;
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
