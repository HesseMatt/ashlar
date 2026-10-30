using Avalonia.Controls;
using Avalonia.VisualTree;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

internal static class WindowExtensions
{
    // The canvas lives in the TilesetEditorView and the tools in the TilesetToolBar, each with its own
    // name scope. Both are only in the tree while the Tilesets tab is active.
    public static T? FindEditorControl<T>(this Window window, string name) where T : Control =>
        window.FindControl<TilesetEditorView>("EditorView")?.FindControl<T>(name)
        ?? window.GetVisualDescendants().OfType<TilesetToolBar>().FirstOrDefault()?.FindControl<T>(name);
}
