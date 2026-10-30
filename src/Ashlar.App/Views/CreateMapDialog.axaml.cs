using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class CreateMapDialog : Window
{
    public CreateMapDialog()
    {
        InitializeComponent();
    }

    public CreateMapDialog(CreateMapDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        Opened += (_, _) => NameBox.Focus();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CreateMapDialogViewModel { IsValid: true } viewModel)
            Close(viewModel.ToSettings());
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
