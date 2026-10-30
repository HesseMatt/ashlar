using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class TilesetToolBar : UserControl
{
    public TilesetToolBar()
    {
        InitializeComponent();
    }

    private void OnDeleteSelectedTilesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TilesetEditorViewModel editor)
            editor.DeleteSelectedTiles();
    }
}
