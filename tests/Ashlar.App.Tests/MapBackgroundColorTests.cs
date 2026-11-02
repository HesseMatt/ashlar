using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.Controls;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class MapBackgroundColorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [AvaloniaFact]
    public void MenuItemsAreDisabledWithoutAMapTabAndCheckerboardOnlyEnabledWithAColor()
    {
        var viewModel = ProjectWithMap();
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var backgroundItem = window.FindControl<MenuItem>("BackgroundColorMenuItem")!;
        var checkerboardItem = window.FindControl<MenuItem>("UseCheckerboardMenuItem")!;

        Assert.True(backgroundItem.IsEnabled);
        Assert.False(checkerboardItem.IsEnabled);

        viewModel.MapEditor().SetBackgroundColor("#87CEEB");
        Assert.True(checkerboardItem.IsEnabled);

        viewModel.MapEditor().SetBackgroundColor(null);
        Assert.False(checkerboardItem.IsEnabled);

        viewModel.OpenTileset("forest");
        Assert.False(backgroundItem.IsEnabled);
        Assert.False(checkerboardItem.IsEnabled);
    }

    [AvaloniaFact]
    public void TheRenderedBackgroundSwitchesBetweenCheckerboardAndColor()
    {
        var viewModel = ProjectWithMap();
        var window = new MainWindow { DataContext = viewModel, Width = 1000, Height = 600 };
        window.Show();
        var view = window.GetVisualDescendants().OfType<MapEditorView>().Single(v => v.IsEffectivelyVisible);
        var background = view.FindControl<Border>("MapBackground")!;
        var checkerboard = view.FindControl<TransparencyCheckerboard>("MapCheckerboard")!;

        Assert.True(checkerboard.IsVisible);
        Assert.False(background.IsVisible);

        viewModel.MapEditor().SetBackgroundColor("#87CEEB");
        Assert.False(checkerboard.IsVisible);
        Assert.True(background.IsVisible);
        Assert.Equal(Color.Parse("#87CEEB"), ((ISolidColorBrush)background.Background!).Color);

        viewModel.MapEditor().SetBackgroundColor(null);
        Assert.True(checkerboard.IsVisible);
        Assert.False(background.IsVisible);
    }

    [AvaloniaFact]
    public void TheGridSwitchesToALightLineOnDarkColors()
    {
        var editor = ProjectWithMap().MapEditor();

        editor.SetBackgroundColor("#101820");
        Assert.True(editor.IsBackgroundDark);

        editor.SetBackgroundColor("#87CEEB");
        Assert.False(editor.IsBackgroundDark);
    }

    [AvaloniaFact]
    public void ChangingTheColorMarksTheProjectChangedWithoutAnUndoStep()
    {
        var viewModel = ProjectWithMap();
        var editor = viewModel.MapEditor();
        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));

        editor.SetBackgroundColor("#87CEEB");

        Assert.True(viewModel.HasUnsavedChanges);
        Assert.False(editor.CanUndo);

        viewModel.SaveProject(Path.Combine(_directory, "a.ashlar"));
        editor.SetBackgroundColor(null);
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.False(editor.CanUndo);
    }

    [AvaloniaFact]
    public void EachMapKeepsItsColorAcrossTabsEditsAndSaveAndReopen()
    {
        var viewModel = ProjectWithMap();
        viewModel.MapEditor().SetBackgroundColor("#87CEEB");
        viewModel.CreateMap("level2", 2, 2, "forest");
        viewModel.MapEditor().SetBackgroundColor("#101820");
        viewModel.ShowMap("level1").Resize(3, 3);
        viewModel.RenameMap("level1", "renamed");
        Assert.Equal("#87CEEB", viewModel.ShowMap("renamed").Map.BackgroundColor);

        var path = Path.Combine(_directory, "a.ashlar");
        viewModel.SaveProject(path);
        var reopened = new MainViewModel();
        reopened.OpenProject(path);

        Assert.Equal("#87CEEB", reopened.GetMap("renamed")!.BackgroundColor);
        Assert.Equal("#101820", reopened.GetMap("level2")!.BackgroundColor);
    }

    [AvaloniaFact]
    public void UndoAndRedoKeepTheCurrentColor()
    {
        var editor = ProjectWithMap().MapEditor();
        editor.Resize(3, 3);

        editor.SetBackgroundColor("#87CEEB");
        editor.Undo();

        Assert.Equal("#87CEEB", editor.Map.BackgroundColor);
    }

    private MainViewModel ProjectWithMap()
    {
        var viewModel = new MainViewModel();
        viewModel.LoadTileset(WriteImage("forest", 64, 64), 16, 16);
        viewModel.CreateMap("level1", 2, 2, "forest");
        return viewModel;
    }

    private string WriteImage(string name, int width, int height)
    {
        var path = Path.Combine(_directory, name + ".png");
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var data = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
