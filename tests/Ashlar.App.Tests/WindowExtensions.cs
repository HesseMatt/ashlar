using Avalonia.Controls;
using Ashlar.App.Views;

namespace Ashlar.App.Tests;

internal static class WindowExtensions
{
    // The editor controls live in the TilesetEditorView's own name scope.
    public static T? FindEditorControl<T>(this Window window, string name) where T : Control =>
        window.FindControl<TilesetEditorView>("EditorView")!.FindControl<T>(name);
}
