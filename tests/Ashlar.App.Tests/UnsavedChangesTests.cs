using SkiaSharp;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

public class UnsavedChangesTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void NothingIsUnsavedBeforeAnythingIsOpened()
    {
        Assert.False(new MainViewModel().HasUnsavedChanges);
    }

    [Fact]
    public void FreshlyImportedTilesetCountsAsUnsaved()
    {
        Assert.True(OpenImage().HasUnsavedChanges);
    }

    [Fact]
    public void FreshlyOpenedProjectCountsAsSaved()
    {
        var projectPath = SaveProject(OpenImage());
        var viewModel = new MainViewModel();

        viewModel.OpenProject(projectPath);

        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SavingTheProjectMarksTheStateAsSaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        Assert.True(saved);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void FailedSaveKeepsTheStateUnsaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "missing", "a.ashlar"));

        Assert.False(saved);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void SavingImageOrExportingXmlDoesNotCountAsSaving()
    {
        var viewModel = OpenImage();
        var projectPath = SaveProject(viewModel);
        viewModel.OpenProject(projectPath);
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.SaveImage(Path.Combine(_directory, "out.png"));
        viewModel.ExportXml(Path.Combine(_directory, "out.xml"));

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void EditsMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void DrawingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.IsDrawToolActive = true;

        viewModel.BeginStroke(1, 1);
        viewModel.CommitStroke();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void MovingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.IsMoveToolActive = true;

        viewModel.BeginMoveDrag(1, 1);
        viewModel.UpdateMoveDrag(17, 1);
        viewModel.CommitMoveDrag();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void AssigningAGroupMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.GroupNameInput = "walls";

        viewModel.ConfirmGroupNameInput();

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void UndoAndRedoMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenImage();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Undo();
        Assert.True(viewModel.HasUnsavedChanges);

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Redo();
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void ChangingTheTileSizeMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.ChangeTileSize(32, 32);

        Assert.True(viewModel.HasUnsavedChanges);
    }

    [Fact]
    public void ClosingTheProjectReturnsToTheStartupState()
    {
        var viewModel = OpenImage();
        var initial = new MainViewModel();
        viewModel.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.DeleteSelectedTiles();
        viewModel.ZoomIn();

        viewModel.CloseProject();

        Assert.False(viewModel.HasImage);
        Assert.Null(viewModel.PreviewBitmap);
        Assert.Empty(viewModel.Tiles);
        Assert.Empty(viewModel.SelectedTiles);
        Assert.Empty(viewModel.GroupNames);
        Assert.False(viewModel.HasEdits);
        Assert.False(viewModel.CanUndo);
        Assert.False(viewModel.CanRedo);
        Assert.False(viewModel.HasUnsavedChanges);
        Assert.Equal(1.0, viewModel.ZoomLevel);
        Assert.Equal(initial.StatusMessage, viewModel.StatusMessage);
        Assert.Equal(initial.TileSizeText, viewModel.TileSizeText);
        Assert.Equal(string.Empty, viewModel.TexturePath);
    }

    [Fact]
    public void ATilesetCanBeImportedAgainAfterClosing()
    {
        var viewModel = OpenImage();
        viewModel.CloseProject();

        viewModel.LoadTileset(WriteImage("again.png"), 32, 32);

        Assert.True(viewModel.HasImage);
        Assert.Equal(4, viewModel.Tiles.Count);
    }

    private MainViewModel OpenSavedProject()
    {
        var viewModel = new MainViewModel();
        viewModel.OpenProject(SaveProject(OpenImage()));
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
