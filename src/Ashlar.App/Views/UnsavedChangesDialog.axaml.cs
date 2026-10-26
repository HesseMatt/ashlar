using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Ashlar.App.Views;

// Cancel comes first because closing the dialog via its title bar returns
// the default value, which must not mean "save" or "discard".
public enum UnsavedChangesChoice
{
    Cancel,
    Save,
    Discard,
}

public partial class UnsavedChangesDialog : Window
{
    public UnsavedChangesDialog()
    {
        InitializeComponent();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Save);

    private void OnDiscardClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Discard);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(UnsavedChangesChoice.Cancel);
}
