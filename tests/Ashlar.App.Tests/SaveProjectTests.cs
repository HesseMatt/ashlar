using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class SaveProjectTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void NoPathIsKnownForAFreshlyImportedTileset()
    {
        Assert.Null(OpenImage().ProjectPath);
    }

    [Fact]
    public void SuggestedProjectFileNameIsTheImportedImageName()
    {
        Assert.Equal("tiles", OpenImage().SuggestedProjectFileName);
    }

    [Fact]
    public void SuggestedProjectFileNameSurvivesReopeningAProject()
    {
        var path = Path.Combine(_directory, "a.ashlar");
        OpenImage().SaveProject(path);
        var viewModel = new MainViewModel();

        viewModel.OpenProject(path);

        Assert.Equal("tiles", viewModel.SuggestedProjectFileName);
    }

    [Fact]
    public void SavingRemembersThePath()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "a.ashlar");

        viewModel.SaveProject(path);

        Assert.Equal(path, viewModel.ProjectPath);
    }

    [Fact]
    public void OpeningAProjectRemembersItsPath()
    {
        var path = Path.Combine(_directory, "a.ashlar");
        OpenImage().SaveProject(path);
        var viewModel = new MainViewModel();

        viewModel.OpenProject(path);

        Assert.Equal(path, viewModel.ProjectPath);
    }

    [Fact]
    public void ImportingAnotherTilesetKeepsThePath()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);

        viewModel.LoadTileset(WriteImage(), 16, 16);

        Assert.Equal(path, viewModel.ProjectPath);
    }

    [Fact]
    public void ClosingTheProjectForgetsThePath()
    {
        var viewModel = OpenImage();
        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        viewModel.CloseProject();

        Assert.Null(viewModel.ProjectPath);
    }

    [Fact]
    public void AFailedSaveKeepsThePreviousPathAndTheUnsavedFlag()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);
        viewModel.Editor.ChangeTileSize(8, 8);

        var saved = viewModel.SaveProject(Path.Combine(_directory, "missing", "b.ashlar"));

        Assert.False(saved);
        Assert.Equal(path, viewModel.ProjectPath);
        Assert.True(viewModel.Editor.HasUnsavedChanges);
    }

    [Fact]
    public void AFailedFirstSaveLeavesNoPath()
    {
        var viewModel = OpenImage();

        viewModel.SaveProject(Path.Combine(_directory, "missing", "a.ashlar"));

        Assert.Null(viewModel.ProjectPath);
    }

    [AvaloniaFact]
    public void SaveMenuItemsFollowWhetherAnImageIsOpenAndUseTheirShortcuts()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();
        var items = window.GetLogicalDescendants().OfType<MenuItem>().ToList();
        var save = items.Single(m => m.Header as string == "Save project");
        var saveAs = items.Single(m => m.Header as string == "Save project as...");
        var saveImage = items.Single(m => m.Header as string == "Save image...");
        Assert.False(save.IsEnabled);
        Assert.False(saveAs.IsEnabled);

        viewModel.LoadTileset(WriteImage(), 16, 16);

        Assert.True(save.IsEnabled);
        Assert.True(saveAs.IsEnabled);
        Assert.Equal("Ctrl+S", save.InputGesture!.ToString());
        Assert.Equal("Ctrl+Shift+S", saveAs.InputGesture!.ToString());
        Assert.Null(saveImage.InputGesture);
        viewModel.CloseProject();
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlSWritesToTheRememberedPathWithoutADialog()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);
        File.Delete(path);
        viewModel.Editor.ChangeTileSize(8, 8);
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);

        Assert.True(File.Exists(path));
        Assert.False(viewModel.Editor.HasUnsavedChanges);
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlSWritesEvenWithoutUnsavedChanges()
    {
        var viewModel = OpenImage();
        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);
        File.Delete(path);
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);

        Assert.True(File.Exists(path));
        window.Close();
    }

    private MainViewModel OpenImage()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage(), 16, 16);
        return viewModel;
    }

    private string WriteImage()
    {
        var path = Path.Combine(_directory, "tiles.png");
        using var bitmap = new SKBitmap(64, 64);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
