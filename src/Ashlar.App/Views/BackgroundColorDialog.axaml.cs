using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Ashlar.App.Views;

public partial class BackgroundColorDialog : Window
{
    public BackgroundColorDialog()
    {
        InitializeComponent();
    }

    public BackgroundColorDialog(Color? current) : this()
    {
        Picker.Color = current ?? Colors.CornflowerBlue;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close((Color?)Picker.Color);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
