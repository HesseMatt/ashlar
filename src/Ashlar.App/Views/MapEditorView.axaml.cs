using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Ashlar.App.Controls;
using Ashlar.App.ViewModels;

namespace Ashlar.App.Views;

public partial class MapEditorView : UserControl
{
    private readonly CanvasViewport _viewport;
    private bool _scrollToStartPending;
    private MapEditorViewModel? _viewModel;

    public MapEditorView()
    {
        InitializeComponent();
        _viewport = new CanvasViewport(MapCanvasScrollViewer, MapCanvasPadding, MapCanvasContent);
        MapCanvasScrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.MapShown -= OnMapShown;
        }

        _viewModel = DataContext as MapEditorViewModel;
        if (_viewModel is not { } viewModel)
            return;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.MapShown += OnMapShown;
        UpdateInterpolation(viewModel);
        UpdateOverscrollPadding();
        // The tab content is created lazily, so a map selected before that was never scrolled to its start.
        _scrollToStartPending = viewModel.HasSelectedMap;
        ScrollToMapStartIfPending();
    }

    // The canvas is still hidden or laid out for the previous map when the event is raised.
    private void OnMapShown(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ScrollToMapStart, DispatcherPriority.Loaded);

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MapEditorViewModel viewModel)
            return;

        if (e.PropertyName == nameof(MapEditorViewModel.PreviewInterpolation))
            UpdateInterpolation(viewModel);

        if (e.PropertyName is nameof(MapEditorViewModel.ScaledMapWidth) or nameof(MapEditorViewModel.ScaledMapHeight)
            or nameof(MapEditorViewModel.TileWidth) or nameof(MapEditorViewModel.TileHeight))
        {
            UpdateOverscrollPadding();
        }
    }

    private void UpdateInterpolation(MapEditorViewModel viewModel)
    {
        RenderOptions.SetBitmapInterpolationMode(MapCells, viewModel.PreviewInterpolation);
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            UpdateOverscrollPadding();
            ScrollToMapStartIfPending();
        }

        if (e.Property == ScrollViewer.ViewportProperty || e.Property == ScrollViewer.OffsetProperty
            || e.Property == ScrollViewer.ExtentProperty)
        {
            UpdateVisibleArea();
        }
    }

    private void ScrollToMapStartIfPending()
    {
        if (_scrollToStartPending)
            Dispatcher.UIThread.Post(ScrollToMapStart, DispatcherPriority.Loaded);
    }

    // Only the cells in view are drawn, so large maps stay cheap to render.
    private void UpdateVisibleArea()
    {
        var viewport = MapCanvasScrollViewer.Viewport;
        var topLeft = MapCanvasScrollViewer.TranslatePoint(new Point(0, 0), MapCanvasContent);
        var bottomRight = MapCanvasScrollViewer.TranslatePoint(new Point(viewport.Width, viewport.Height), MapCanvasContent);
        MapCells.VisibleArea = topLeft is { } start && bottomRight is { } end ? new Rect(start, end) : null;
    }

    private void UpdateOverscrollPadding()
    {
        if (DataContext is not MapEditorViewModel viewModel || !viewModel.HasSelectedMap)
            return;

        _viewport.UpdateOverscrollPadding(viewModel.ScaledMapWidth, viewModel.ScaledMapHeight,
            viewModel.TileWidth * viewModel.ZoomLevel, viewModel.TileHeight * viewModel.ZoomLevel);
    }

    private void ScrollToMapStart()
    {
        if (DataContext is not MapEditorViewModel viewModel)
            return;

        // A hidden tab has no viewport yet; scrolling now would be lost, so retry once it is shown.
        _scrollToStartPending = !MapCanvasScrollViewer.IsEffectivelyVisible;
        if (_scrollToStartPending)
            return;

        _viewport.ScrollToStart(viewModel.ScaledMapWidth, viewModel.ScaledMapHeight,
            viewModel.TileWidth * viewModel.ZoomLevel, viewModel.TileHeight * viewModel.ZoomLevel);
    }

    private void OnMapCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control canvas && e.GetCurrentPoint(canvas).Properties.IsRightButtonPressed)
            _viewport.BeginPan(e, canvas);
    }

    private void OnMapCanvasPointerMoved(object? sender, PointerEventArgs e) => _viewport.Pan(e);

    private void OnMapCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewport.IsPanning)
            _viewport.EndPan(e.Pointer);
    }

    private void OnMapCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => _viewport.CancelPan();

    private void OnMapCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MapEditorViewModel viewModel)
            return;

        var zoomIn = e.Delta.Y > 0;
        _viewport.AnchoredZoom(e.GetPosition(MapCanvasScrollViewer), () =>
        {
            if (zoomIn) viewModel.ZoomIn(); else viewModel.ZoomOut();
        });
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
