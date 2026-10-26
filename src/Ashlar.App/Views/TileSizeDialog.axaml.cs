using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class TileSizeDialog : Window
{
    public TileSizeDialog()
    {
        InitializeComponent();
    }

    public TileSizeDialog(TileSizeDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => TileWidthBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is TileSizeDialogViewModel viewModel)
            Close((viewModel.TileWidth, viewModel.TileHeight));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
