using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Rendering;
using Avalonia.VisualTree;
using SkiaSharp;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

public class ProjectExplorerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory().FullName;
    private readonly MainViewModel _viewModel = new();
    private readonly MainWindow _window;

    public ProjectExplorerTests()
    {
        _window = new MainWindow { DataContext = _viewModel, Width = 1000, Height = 600 };
        _window.Show();
        _viewModel.LoadTileset(WriteImage("forest"), 16, 16);
        _viewModel.CreateMap("level1", 4, 4, "forest");
        while (_viewModel.ActiveTab is not null)
            _viewModel.CloseActiveTab();
        _window.GetLayoutManager()?.ExecuteLayoutPass();
    }

    public void Dispose()
    {
        _window.Close();
        Directory.Delete(_directory, recursive: true);
    }

    private TreeView Tree => _window.FindControl<TreeView>("ExplorerTree")!;

    private ExplorerNode Tileset => _viewModel.TilesetsGroup.Children.Single();

    private ExplorerNode Map => _viewModel.MapsGroup.Children.Single();

    private void PressKey(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [AvaloniaFact]
    public void ClickingSelectsAnEntryAndEnterOpensItsTab()
    {
        Tree.SelectedItem = Map;
        Assert.Empty(_viewModel.OpenTabs);

        PressKey(Tree, Key.Enter);

        Assert.Equal(["level1"], _viewModel.OpenTabs.Select(tab => tab.Title));

        Tree.SelectedItem = Tileset;
        PressKey(Tree, Key.Enter);
        Tree.SelectedItem = Map;
        PressKey(Tree, Key.Enter);

        Assert.Equal(["level1", "forest"], _viewModel.OpenTabs.Select(tab => tab.Title));
        Assert.Equal("level1", _viewModel.ActiveTab!.Title);
    }

    [AvaloniaFact]
    public void F2StartsRenamingAndEnterCommitsTheNewName()
    {
        Tree.SelectedItem = Tileset;

        PressKey(Tree, Key.F2);
        _window.GetLayoutManager()?.ExecuteLayoutPass();

        Assert.True(Tileset.IsRenaming);
        var box = Tree.GetVisualDescendants().OfType<TextBox>().Single(b => b.DataContext == Tileset);
        Assert.True(box.IsVisible);
        box.Text = "woods";
        PressKey(box, Key.Enter);

        Assert.False(Tileset.IsRenaming);
        Assert.Equal(["woods"], _viewModel.TilesetNames);
        Assert.Equal("woods", _viewModel.GetMap("level1")!.TilesetName);
    }

    [AvaloniaFact]
    public void EscapeCancelsRenamingAndARefusedNameKeepsTheOldOne()
    {
        Map.BeginRename();
        _window.GetLayoutManager()?.ExecuteLayoutPass();
        var box = Tree.GetVisualDescendants().OfType<TextBox>().Single(b => b.DataContext == Map);
        box.Text = "other";
        PressKey(box, Key.Escape);

        Assert.False(Map.IsRenaming);
        Assert.Equal(["level1"], _viewModel.MapNames);

        _viewModel.CreateMap("level2", 2, 2, "forest");
        var level2 = _viewModel.MapsGroup.Children[1];
        level2.BeginRename();
        _window.GetLayoutManager()?.ExecuteLayoutPass();
        box = Tree.GetVisualDescendants().OfType<TextBox>().Single(b => b.DataContext == level2);
        box.Text = "level1";
        PressKey(box, Key.Enter);

        Assert.Equal(["level1", "level2"], _viewModel.MapNames);
        Assert.Contains("already exists", _viewModel.StatusText);
    }

    [AvaloniaFact]
    public void GroupHeadersCannotBeRenamed()
    {
        _viewModel.TilesetsGroup.BeginRename();

        Assert.False(_viewModel.TilesetsGroup.IsRenaming);
    }

    [AvaloniaFact]
    public void ContextMenusOfferTheActionsOfTheirEntryKind()
    {
        string[] VisibleItems(ExplorerNode node)
        {
            var panel = Tree.GetVisualDescendants().OfType<Panel>().First(p => p.DataContext == node && p.ContextMenu is not null);
            return panel.ContextMenu!.Items.OfType<MenuItem>().Where(item => item.IsVisible).Select(item => (string)item.Header!).ToArray();
        }

        Assert.Equal(["Add tileset..."], VisibleItems(_viewModel.TilesetsGroup));
        Assert.Equal(["Rename", "Remove"], VisibleItems(Tileset));
        Assert.Equal(["Create map..."], VisibleItems(_viewModel.MapsGroup));
        Assert.Equal(["Rename", "Resize...", "Delete"], VisibleItems(Map));
    }

    [AvaloniaFact]
    public void TheMenusActOnTheExplorerSelection()
    {
        IEnumerable<MenuItem> Items(string header) => _window.GetLogicalDescendants().OfType<Menu>().Single().GetLogicalDescendants().OfType<MenuItem>().Where(m => m.Header as string == header);
        Tree.SelectedItem = null;
        Assert.All(Items("Rename"), item => Assert.False(item.IsEnabled));

        Tree.SelectedItem = Map;
        Assert.Contains(Items("Resize..."), item => item.IsEnabled);
        Assert.Contains(Items("Export map..."), item => item.IsEnabled);
        Assert.Single(Items("Rename"), item => item.IsEnabled);

        Tree.SelectedItem = Tileset;
        Assert.Single(Items("Rename"), item => item.IsEnabled);
        Assert.All(Items("Resize..."), item => Assert.False(item.IsEnabled));
        Assert.All(Items("Change tile size..."), item => Assert.False(item.IsEnabled));
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
