using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class ExportMapDialog : Window
{
    public ExportMapDialog()
    {
        InitializeComponent();
    }

    public ExportMapDialog(ExportMapDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExportMapDialogViewModel viewModel && viewModel.CanExport)
            Close(viewModel);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
