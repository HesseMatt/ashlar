using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(ExportDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => TexturePathBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ExportDialogViewModel viewModel)
            Close(viewModel.TexturePath);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
