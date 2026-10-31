using Avalonia.Controls;
using Avalonia.VisualTree;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

internal static class WindowExtensions
{
    // The canvas lives in the TilesetEditorView of the active tab and the tools in the TilesetToolBar,
    // each with its own name scope. The toolbar is only in the tree while a tileset tab is active.
    public static T? FindEditorControl<T>(this Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<TilesetEditorView>().FirstOrDefault(view => view.DataContext is TilesetEditorViewModel { IsActive: true })?.FindControl<T>(name)
        ?? window.GetVisualDescendants().OfType<TilesetToolBar>().FirstOrDefault()?.FindControl<T>(name);
}
