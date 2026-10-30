using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MapEditorView : UserControl
{
    public MapEditorView()
    {
        InitializeComponent();
    }

    private async void OnCreateMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapEditorViewModel viewModel || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var dialog = new CreateMapDialog(new CreateMapDialogViewModel(
            viewModel.CreateUniqueName(), viewModel.TilesetNames, viewModel.PreferredTilesetName ?? viewModel.TilesetNames[0], viewModel.Maps.Select(map => map.Name).ToList()));
        if (await dialog.ShowDialog<NewMapSettings?>(owner) is { } settings)
            viewModel.CreateMap(settings.Name, settings.Width, settings.Height, settings.TilesetName);
    }

    private void OnMapSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel)
            MapNameBox.Text = viewModel.SelectedMap?.Name;
    }

    private void OnRenameMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MapEditorViewModel viewModel && viewModel.RenameSelectedMap(MapNameBox.Text ?? string.Empty))
            MapNameBox.Text = viewModel.SelectedMap?.Name;
    }

    private async void OnDeleteMapClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MapEditorViewModel { SelectedMap: { } map } viewModel || TopLevel.GetTopLevel(this) is not Window owner)
            return;

        var confirmed = await new ConfirmDialog(
            "Delete map", $"Delete the map '{map.Name}' from the project?", "Delete").ShowDialog<bool>(owner);
        if (confirmed)
            viewModel.DeleteSelectedMap();
    }
}
