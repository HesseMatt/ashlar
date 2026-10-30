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
    private string? _lastScrolledMapName;
    private MapEditorViewModel? _viewModel;
    private bool _isDrawing;

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
            _viewModel.CellsPainted -= OnCellsPainted;
        }

        _viewModel = DataContext as MapEditorViewModel;
        if (_viewModel is not { } viewModel)
            return;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.MapShown += OnMapShown;
        viewModel.CellsPainted += OnCellsPainted;
        UpdateInterpolation(viewModel);
        UpdateOverscrollPadding();
        // The tab content is created lazily, so a map selected before that was never scrolled to its start.
        // Coming back to the same map keeps the position the user panned to.
        _scrollToStartPending = viewModel.HasSelectedMap && viewModel.SelectedMap?.Name != _lastScrolledMapName;
        ScrollToMapStartIfPending();
    }

    // The canvas is still hidden or laid out for the previous map when the event is raised.
    private void OnMapShown(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ScrollToMapStart, DispatcherPriority.Loaded);

    private void OnCellsPainted(object? sender, EventArgs e) => MapCells.InvalidateVisual();

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

        _lastScrolledMapName = viewModel.SelectedMap?.Name;
        _viewport.ScrollToStart(viewModel.ScaledMapWidth, viewModel.ScaledMapHeight,
            viewModel.TileWidth * viewModel.ZoomLevel, viewModel.TileHeight * viewModel.ZoomLevel);
    }

    private void OnMapCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control canvas)
            return;

        var properties = e.GetCurrentPoint(canvas).Properties;
        if (properties.IsRightButtonPressed)
        {
            _viewport.BeginPan(e, canvas);
        }
        else if (properties.IsLeftButtonPressed && DataContext is MapEditorViewModel viewModel)
        {
            var (cellX, cellY) = CellAt(e, viewModel);
            if (viewModel.BeginStroke(cellX, cellY))
            {
                _isDrawing = true;
                e.Pointer.Capture(canvas);
            }
        }
    }

    private void OnMapCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isDrawing && DataContext is MapEditorViewModel viewModel)
        {
            var (cellX, cellY) = CellAt(e, viewModel);
            viewModel.ContinueStroke(cellX, cellY);
            return;
        }

        _viewport.Pan(e);
    }

    private void OnMapCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDrawing && e.InitialPressMouseButton == MouseButton.Left)
        {
            EndDrawing(e.Pointer);
            return;
        }

        if (_viewport.IsPanning)
            _viewport.EndPan(e.Pointer);
    }

    private void OnMapCanvasPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDrawing)
            EndDrawing(null);
        _viewport.CancelPan();
    }

    private void EndDrawing(IPointer? pointer)
    {
        _isDrawing = false;
        (DataContext as MapEditorViewModel)?.EndStroke();
        pointer?.Capture(null);
    }

    // Cells outside the map come out negative or too large; painting ignores them.
    private (int X, int Y) CellAt(PointerEventArgs e, MapEditorViewModel viewModel)
    {
        var position = e.GetPosition(MapCanvasContent);
        return ((int)Math.Floor(position.X / viewModel.TileWidth), (int)Math.Floor(position.Y / viewModel.TileHeight));
    }

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
