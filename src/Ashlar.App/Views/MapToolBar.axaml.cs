using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MapToolBar : UserControl
{
    public MapToolBar()
    {
        InitializeComponent();
    }

    private void OnDeleteSelectedCellsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MapEditorViewModel editor)
            editor.DeleteSelectedCells();
    }
}
