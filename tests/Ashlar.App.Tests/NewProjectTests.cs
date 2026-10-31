using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core.Serialization;

namespace Ashlar.App.Tests;

public class NewProjectTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void NoProjectIsOpenAtStartup()
    {
        var viewModel = new MainViewModel();

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.IsProjectEmpty);
    }

    [AvaloniaFact]
    public void NewProjectIsOpenAndEmpty()
    {
        var viewModel = new MainViewModel();

        viewModel.NewProject();

        Assert.True(viewModel.IsProjectOpen);
        Assert.True(viewModel.IsProjectEmpty);
        Assert.Empty(viewModel.OpenTabs);
        Assert.Null(viewModel.ProjectPath);
    }

    [AvaloniaFact]
    public void NewProjectDiscardsTheOpenTileset()
    {
        var viewModel = OpenImage();

        viewModel.NewProject();

        Assert.Empty(viewModel.OpenTabs);
        Assert.Empty(viewModel.TilesetNames);
        Assert.False(viewModel.HasUnsavedChanges);
        Assert.Null(viewModel.ProjectPath);
    }

    [AvaloniaFact]
    public void AnEmptyProjectCanBeSavedAndReopened()
    {
        var path = Path.Combine(_directory, "empty.ashlar");
        var viewModel = new MainViewModel();
        viewModel.NewProject();

        var saved = viewModel.SaveProject(path);

        Assert.True(saved);
        Assert.Equal(path, viewModel.ProjectPath);
        Assert.Empty(ProjectFile.Load(path).Tilesets);
        var reopened = new MainViewModel();
        reopened.OpenProject(path);
        Assert.True(reopened.IsProjectEmpty);
        Assert.Equal(path, reopened.ProjectPath);
    }

    [AvaloniaFact]
    public void OpeningAnEmptyProjectReplacesTheOpenTileset()
    {
        var path = Path.Combine(_directory, "empty.ashlar");
        var empty = new MainViewModel();
        empty.NewProject();
        empty.SaveProject(path);
        var viewModel = OpenImage();

        viewModel.OpenProject(path);

        Assert.True(viewModel.IsProjectEmpty);
        Assert.Empty(viewModel.OpenTabs);
    }

    [AvaloniaFact]
    public void ImportingIntoAnEmptyProjectKeepsItsPath()
    {
        var path = Path.Combine(_directory, "empty.ashlar");
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        viewModel.SaveProject(path);

        viewModel.LoadTileset(WriteImage(), 16, 16);

        Assert.Equal(path, viewModel.ProjectPath);
        Assert.False(viewModel.IsProjectEmpty);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    [AvaloniaFact]
    public void ClosingTheProjectLeavesNoProjectOpen()
    {
        var viewModel = new MainViewModel();
        viewModel.NewProject();

        viewModel.CloseProject();

        Assert.False(viewModel.IsProjectOpen);
        Assert.False(viewModel.IsProjectEmpty);
    }

    [AvaloniaFact]
    public void SavingWithoutAnOpenProjectDoesNothing()
    {
        var path = Path.Combine(_directory, "none.ashlar");

        Assert.False(new MainViewModel().SaveProject(path));
        Assert.False(File.Exists(path));
    }

    [AvaloniaFact]
    public void ExportAndImageCommandsDoNothingOnAnEmptyProject()
    {
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        var xmlPath = Path.Combine(_directory, "a.xml");
        var pngPath = Path.Combine(_directory, "a.png");

        viewModel.ExportXml(xmlPath);
        viewModel.SaveImage(pngPath);

        Assert.False(File.Exists(xmlPath));
        Assert.False(File.Exists(pngPath));
    }

    [AvaloniaFact]
    public void EmptyStateIsShownWhileNoTabIsOpen()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();
        // The logical tree can yield the same control twice.
        var panel = window.GetLogicalDescendants().OfType<Border>().Where(b => b.Name == "EmptyEditorPanel").Distinct().Single();
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No project open");

        viewModel.NewProject();
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "This project has no tileset yet");

        viewModel.LoadTileset(WriteImage(), 16, 16);
        Assert.False(panel.IsVisible);

        viewModel.CloseTab(viewModel.ActiveTab!);
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("Double-click") == true);

        viewModel.LoadTileset(WriteImage(), 16, 16);
        Assert.False(panel.IsVisible);

        viewModel.CloseProject();
        Assert.True(panel.IsVisible);
        Assert.Contains(panel.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "No project open");
        window.Close();
    }

    [AvaloniaFact]
    public void FileMenuOffersAllProjectCommandsAndTheyFollowTheProjectState()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();
        var items = window.GetLogicalDescendants().OfType<MenuItem>().ToList();
        var newProject = items.Single(m => m.Header as string == "New project");
        var openProject = items.Single(m => m.Header as string == "Open project...");
        var save = items.Single(m => m.Header as string == "Save project");
        var saveAs = items.Single(m => m.Header as string == "Save project as...");
        var close = items.Single(m => m.Header as string == "Close project");
        Assert.True(newProject.IsEnabled);
        Assert.True(openProject.IsEnabled);
        Assert.False(save.IsEnabled);
        Assert.False(saveAs.IsEnabled);
        Assert.False(close.IsEnabled);

        viewModel.NewProject();

        Assert.True(save.IsEnabled);
        Assert.True(saveAs.IsEnabled);
        Assert.True(close.IsEnabled);
        Assert.Equal("Ctrl+N", newProject.InputGesture!.ToString());
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlSSavesAnEmptyProjectToItsPath()
    {
        var path = Path.Combine(_directory, "empty.ashlar");
        var viewModel = new MainViewModel();
        viewModel.NewProject();
        viewModel.SaveProject(path);
        File.Delete(path);
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);

        Assert.True(File.Exists(path));
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlNOnAnUnsavedTilesetKeepsItBehindTheConfirmation()
    {
        var viewModel = OpenImage();
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);

        Assert.True(viewModel.Editor().HasImage);
        window.Close();
    }

    [AvaloniaFact]
    public void CtrlNOnASavedProjectStartsANewOne()
    {
        var viewModel = OpenImage();
        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        var window = new MainWindow { DataContext = viewModel, Width = 800, Height = 600 };
        window.Show();

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Control);

        Assert.True(viewModel.IsProjectEmpty);
        Assert.Null(viewModel.ProjectPath);
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
