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
        Assert.False(new MainViewModel().Editor.HasUnsavedChanges);
    }

    [Fact]
    public void FreshlyImportedTilesetCountsAsUnsaved()
    {
        Assert.True(OpenImage().Editor.HasUnsavedChanges);
    }

    [Fact]
    public void FreshlyOpenedProjectCountsAsSaved()
    {
        var projectPath = SaveProject(OpenImage());
        var viewModel = new MainViewModel();

        viewModel.OpenProject(projectPath);

        Assert.False(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void SavingTheProjectMarksTheStateAsSaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        Assert.True(saved);
        Assert.False(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void FailedSaveKeepsTheStateUnsaved()
    {
        var viewModel = OpenImage();

        var saved = viewModel.SaveProject(Path.Combine(_directory, "missing", "a.ashlar"));

        Assert.False(saved);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void SavingImageOrExportingXmlDoesNotCountAsSaving()
    {
        var viewModel = OpenImage();
        var projectPath = SaveProject(viewModel);
        viewModel.OpenProject(projectPath);
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.SaveImage(Path.Combine(_directory, "out.png"));
        viewModel.ExportXml(Path.Combine(_directory, "out.xml"));

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void EditsMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void DrawingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor.IsDrawToolActive = true;

        viewModel.Editor.BeginStroke(1, 1);
        viewModel.Editor.CommitStroke();

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void MovingMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.IsMoveToolActive = true;

        viewModel.Editor.BeginMoveDrag(1, 1);
        viewModel.Editor.UpdateMoveDrag(17, 1);
        viewModel.Editor.CommitMoveDrag();

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void AssigningAGroupMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.GroupNameInput = "walls";

        viewModel.Editor.ConfirmGroupNameInput();

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void UndoAndRedoMarkTheSavedStateAsUnsaved()
    {
        var viewModel = OpenImage();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Editor.Undo();
        Assert.True(viewModel.Editor.HasUnsavedChanges);

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        viewModel.Editor.Redo();
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void ChangingTheTileSizeMarksTheSavedStateAsUnsaved()
    {
        var viewModel = OpenSavedProject();

        viewModel.Editor.ChangeTileSize(32, 32);

        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void ClosingTheProjectReturnsToTheStartupState()
    {
        var viewModel = OpenImage();
        var initial = new MainViewModel();
        viewModel.Editor.HandleCanvasClick(1, 1, addToSelection: false);
        viewModel.Editor.DeleteSelectedTiles();
        viewModel.Editor.ZoomIn();

        viewModel.CloseProject();

        Assert.False(viewModel.Editor.HasImage);
        Assert.Null(viewModel.Editor.PreviewBitmap);
        Assert.Empty(viewModel.Editor.Tiles);
        Assert.Empty(viewModel.Editor.SelectedTiles);
        Assert.Empty(viewModel.Editor.GroupNames);
        Assert.False(viewModel.Editor.HasEdits);
        Assert.False(viewModel.Editor.CanUndo);
        Assert.False(viewModel.Editor.CanRedo);
        Assert.False(viewModel.Editor.HasUnsavedChanges);
        Assert.Equal(1.0, viewModel.Editor.ZoomLevel);
        Assert.Equal(initial.Editor.StatusMessage, viewModel.Editor.StatusMessage);
        Assert.Equal(initial.Editor.TileSizeText, viewModel.Editor.TileSizeText);
        Assert.Equal(string.Empty, viewModel.TexturePath);
    }

    [Fact]
    public void ATilesetCanBeImportedAgainAfterClosing()
    {
        var viewModel = OpenImage();
        viewModel.CloseProject();

        viewModel.LoadTileset(WriteImage("again.png"), 32, 32);

        Assert.True(viewModel.Editor.HasImage);
        Assert.Equal(4, viewModel.Editor.Tiles.Count);
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
