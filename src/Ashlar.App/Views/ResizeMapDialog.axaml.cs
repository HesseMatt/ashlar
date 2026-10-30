using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class ResizeMapDialog : Window
{
    public ResizeMapDialog()
    {
        InitializeComponent();
    }

    public ResizeMapDialog(ResizeMapDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => WidthBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ResizeMapDialogViewModel { IsValid: true } viewModel)
            Close((viewModel.Width, viewModel.Height));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
