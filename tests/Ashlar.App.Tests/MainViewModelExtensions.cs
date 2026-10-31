using Ashlar.App.ViewModels;

namespace Ashlar.App.Tests;

internal static class MainViewModelExtensions
{
    // The editors live in tabs; these read the active one for tests that work on a single editor.
    public static TilesetEditorViewModel Editor(this MainViewModel viewModel) => viewModel.ActiveTileset!;

    public static MapEditorViewModel MapEditor(this MainViewModel viewModel) => viewModel.ActiveMap!;

    public static TilesetEditorViewModel ShowTileset(this MainViewModel viewModel, string name)
    {
        viewModel.OpenTileset(name);
        return viewModel.ActiveTileset!;
    }

    public static MapEditorViewModel ShowMap(this MainViewModel viewModel, string name)
    {
        viewModel.OpenMap(name);
        return viewModel.ActiveMap!;
    }

    // Opening a project opens no tabs; most tests edit its first tileset.
    public static void OpenFirstTileset(this MainViewModel viewModel) => viewModel.OpenTileset(viewModel.TilesetNames[0]);

    public static void SelectMap(this MainViewModel viewModel, string name) =>
        viewModel.ExplorerSelection = viewModel.MapsGroup.Children.Single(node => node.Name == name);
}
