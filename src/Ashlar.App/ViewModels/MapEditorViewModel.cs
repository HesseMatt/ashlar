using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.App.ViewModels;

// One tile in the palette: its region plus the tileset image it is cut from.
public record PaletteTile(TileRegion Region, IImage Image)
{
    public string Name => Region.Name;
}

// Edits one map of the project in its own tab: the cells resolved against the
// map's tileset, the palette of that tileset's tiles, and the tab's own history.
public partial class MapEditorViewModel : EditorTabViewModel
{
    private readonly Func<string, ProjectTileset?> _findTileset;

    public MapEditorViewModel(TileMap map, Func<string, ProjectTileset?> findTileset) : base(map.Name)
    {
        _map = map;
        _findTileset = findTileset;
        RefreshTileset();
    }

    // The current instance: undo, redo, resize and rename replace it, so the project reads it back from here.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelWidth), nameof(MapPixelHeight), nameof(ScaledMapWidth), nameof(ScaledMapHeight),
        nameof(SelectedMapSizeText), nameof(SelectedMapTilesetText))]
    private TileMap _map;

    // The tileset image and tile size the map is drawn with; null/0 while its tileset is missing.
    [ObservableProperty]
    private Bitmap? _tilesetImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelWidth), nameof(ScaledMapWidth), nameof(SelectionOutlines))]
    private int _tileWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelHeight), nameof(ScaledMapHeight), nameof(SelectionOutlines))]
    private int _tileHeight;

    // Per cell of the map, row-major: the region to draw, or null for an empty cell.
    [ObservableProperty]
    private TileRegion?[] _cellRegions = [];

    [ObservableProperty]
    private IReadOnlyList<PaletteTile> _paletteTiles = [];

    // The single tile that becomes the draw brush.
    [ObservableProperty]
    private PaletteTile? _selectedPaletteTile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRectangleToolActive), nameof(IsMoveToolActive), nameof(IsDrawToolActive))]
    private CanvasTool _activeTool;

    partial void OnActiveToolChanged(CanvasTool value)
    {
        EndStroke();
        EndSelectionDrag();
        EndMoveDrag();
    }

    public bool IsRectangleToolActive
    {
        get => ActiveTool == CanvasTool.Rectangle;
        set => SetToolActive(CanvasTool.Rectangle, value);
    }

    public bool IsMoveToolActive
    {
        get => ActiveTool == CanvasTool.Move;
        set => SetToolActive(CanvasTool.Move, value);
    }

    public bool IsDrawToolActive
    {
        get => ActiveTool == CanvasTool.Draw;
        set => SetToolActive(CanvasTool.Draw, value);
    }

    private void SetToolActive(CanvasTool tool, bool active)
    {
        if (active)
            ActiveTool = tool;
        else if (ActiveTool == tool)
            ActiveTool = CanvasTool.Select;
    }

    // The selected cells of the map, as (column, row).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedCells), nameof(SelectionOutlines))]
    private IReadOnlyList<(int X, int Y)> _selectedCells = [];

    public bool HasSelectedCells => SelectedCells.Count > 0;

    // The selected cells as pixel rectangles on the map, for the overlay that highlights them.
    public IReadOnlyList<TileRegion> SelectionOutlines =>
        SelectedCells.Select(cell => new TileRegion(string.Empty, cell.X * TileWidth, cell.Y * TileHeight, TileWidth, TileHeight)).ToList();

    // The rectangle being dragged with the rectangle tool, in map pixels.
    [ObservableProperty]
    private Rect? _selectionRectangle;

    // How far the selection is dragged with the move tool, in map pixels; null while not dragging.
    [ObservableProperty]
    private Vector? _moveOffset;

    private record SelectionDrag(int StartX, int StartY, IReadOnlyList<(int X, int Y)> SelectionBefore, bool AddToSelection);

    private SelectionDrag? _selectionDrag;

    // The cell the move drag started on, and how many cells the selection is dragged by.
    private (int X, int Y)? _moveDragStart;
    private (int X, int Y) _moveDragCells;

    // The cell the current draw stroke reached last; null while no stroke is in progress.
    private (int X, int Y)? _strokeCell;

    private sealed class DrawStroke(TileMap map, MapState before)
    {
        public TileMap Map { get; } = map;

        public MapState Before { get; } = before;

        public bool PaintedAnything { get; set; }
    }

    private DrawStroke? _stroke;

    // Dropped when tiles are cleared from the map because they were deleted from
    // its tileset: that change cannot be undone, so the older steps no longer
    // describe the map's cells.
    private readonly EditHistory<MapEditStep> _history = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledMapWidth), nameof(ScaledMapHeight), nameof(PreviewInterpolation), nameof(ZoomPercentageText))]
    private double _zoomLevel = 1.0;

    public string ZoomPercentageText => $"{ZoomLevel:P0}";

    public int MapPixelWidth => Map.Width * TileWidth;

    public int MapPixelHeight => Map.Height * TileHeight;

    public double ScaledMapWidth => MapPixelWidth * ZoomLevel;

    public double ScaledMapHeight => MapPixelHeight * ZoomLevel;

    public BitmapInterpolationMode PreviewInterpolation => TilesetEditorViewModel.InterpolationAt(ZoomLevel);

    // Raised when painting changed cells in place, so the view redraws them.
    public event EventHandler? CellsPainted;

    public override double ZoomIn() => ZoomLevel = TilesetEditorViewModel.NextZoomIn(ZoomLevel);

    public override double ZoomOut() => ZoomLevel = TilesetEditorViewModel.NextZoomOut(ZoomLevel);

    public override void ResetZoom() => ZoomLevel = 1.0;

    // Reads the map's tileset again; the tileset may have been edited since the map was last shown.
    public void RefreshTileset()
    {
        var map = Map;
        if (_findTileset(map.TilesetName) is not { } tileset)
        {
            TilesetImage = null;
            TileWidth = 0;
            TileHeight = 0;
            CellRegions = [];
            PaletteTiles = [];
            SelectedPaletteTile = null;
            return;
        }

        var selectedName = SelectedPaletteTile?.Name;
        var image = new Bitmap(new MemoryStream(tileset.ImagePng));
        TilesetImage = image;
        TileWidth = tileset.TileWidth;
        TileHeight = tileset.TileHeight;
        CellRegions = map.ResolveRegions(tileset.Tiles);
        PaletteTiles = tileset.Tiles.Select(tile => new PaletteTile(tile, image)).ToList();
        SelectedPaletteTile = PaletteTiles.FirstOrDefault(tile => tile.Name == selectedName);
        OnPropertyChanged(nameof(MapPixelWidth));
        OnPropertyChanged(nameof(MapPixelHeight));
        OnPropertyChanged(nameof(ScaledMapWidth));
        OnPropertyChanged(nameof(ScaledMapHeight));
    }

    public string SelectedMapSizeText => $"{Map.Width} x {Map.Height} cells";

    public string SelectedMapTilesetText => $"Tileset: {Map.TilesetName}";

    // Starts a draw stroke at a cell. Returns false when there is nothing to paint with.
    public bool BeginStroke(int cellX, int cellY)
    {
        if (!IsDrawToolActive || SelectedPaletteTile is null)
            return false;

        _strokeCell = (cellX, cellY);
        _stroke = new DrawStroke(Map, MapState.Capture(Map));
        Paint(cellX, cellY);
        return true;
    }

    public void ContinueStroke(int cellX, int cellY)
    {
        if (_strokeCell is not { } last)
            return;

        _strokeCell = (cellX, cellY);
        Paint(last.X, last.Y, cellX, cellY);
    }

    // The whole stroke is one undo step.
    public void EndStroke()
    {
        _strokeCell = null;
        var stroke = _stroke;
        _stroke = null;
        if (stroke is { PaintedAnything: true })
            RecordEdit(stroke.Map, "Draw", stroke.Before, SelectedCells);
    }

    private void Paint(int fromX, int fromY) => Paint(fromX, fromY, fromX, fromY);

    private void Paint(int fromX, int fromY, int toX, int toY)
    {
        if (SelectedPaletteTile is not { } tile)
            return;

        var map = Map;
        var changed = MapPainter.PaintLine(map, fromX, fromY, toX, toY, tile.Name);
        if (changed.Count == 0)
            return;

        _stroke!.PaintedAnything = true;
        foreach (var (x, y) in changed)
            CellRegions[y * map.Width + x] = tile.Region;
        CellsPainted?.Invoke(this, EventArgs.Empty);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Whether a drag with the rectangle or move tool is in progress.
    public bool IsDragInProgress => _selectionDrag is not null || _moveDragStart is not null;

    public void HandleCanvasClick(int cellX, int cellY, bool addToSelection)
    {
        if (!IsRectangleToolActive)
            return;

        var map = Map;
        var cell = (cellX, cellY);
        var isInside = cellX >= 0 && cellX < map.Width && cellY >= 0 && cellY < map.Height;
        if (!addToSelection)
            SelectedCells = isInside ? [cell] : [];
        else if (isInside)
            SelectedCells = SelectedCells.Contains(cell)
                ? SelectedCells.Where(c => c != cell).ToList()
                : SelectedCells.Append(cell).ToList();

        UpdateSelectionStatus();
    }

    public void BeginSelectionDrag(int cellX, int cellY, bool addToSelection)
    {
        _selectionDrag = new SelectionDrag(cellX, cellY, SelectedCells, addToSelection);
        UpdateSelectionDrag(cellX, cellY);
    }

    public void UpdateSelectionDrag(int cellX, int cellY)
    {
        if (_selectionDrag is not { } drag)
            return;

        var touched = MapRectangleSelector.CellsIn(Map, drag.StartX, drag.StartY, cellX, cellY);
        SelectionRectangle = touched.Count == 0 ? null : new Rect(
            touched[0].X * TileWidth, touched[0].Y * TileHeight,
            (touched[^1].X - touched[0].X + 1) * TileWidth, (touched[^1].Y - touched[0].Y + 1) * TileHeight);

        var selection = drag.AddToSelection
            ? drag.SelectionBefore.Concat(touched.Except(drag.SelectionBefore)).ToList()
            : touched;
        if (selection.SequenceEqual(SelectedCells))
            return;

        SelectedCells = selection;
        UpdateSelectionStatus();
    }

    public void EndSelectionDrag()
    {
        _selectionDrag = null;
        SelectionRectangle = null;
    }

    public void CancelSelectionDrag()
    {
        if (_selectionDrag is not { } drag)
            return;

        SelectedCells = drag.SelectionBefore;
        EndSelectionDrag();
        UpdateSelectionStatus();
    }

    public void DeleteSelectedCells()
    {
        if (!HasSelectedCells || IsDragInProgress)
            return;

        var map = Map;
        var before = MapState.Capture(map);
        var selectionBefore = SelectedCells;
        var cleared = MapEraser.Clear(map, SelectedCells);
        SelectedCells = [];
        StatusMessage = $"Cleared {cleared.Count} {(cleared.Count == 1 ? "cell" : "cells")}.";
        if (cleared.Count == 0)
            return;

        RefreshCellRegions();
        RecordEdit(map, $"Delete {cleared.Count} {(cleared.Count == 1 ? "cell" : "cells")}", before, selectionBefore);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Only a press on an already selected cell starts a move; anywhere else the
    // press is ignored so the selection stays as it is.
    public bool BeginMoveDrag(int cellX, int cellY)
    {
        if (!IsMoveToolActive || !SelectedCells.Contains((cellX, cellY)))
            return false;

        _moveDragStart = (cellX, cellY);
        return true;
    }

    public void UpdateMoveDrag(int cellX, int cellY)
    {
        if (_moveDragStart is not { } start)
            return;

        _moveDragCells = (cellX - start.X, cellY - start.Y);
        MoveOffset = _moveDragCells == (0, 0) ? null : new Vector(_moveDragCells.X * TileWidth, _moveDragCells.Y * TileHeight);
    }

    public void CommitMoveDrag()
    {
        var (offsetX, offsetY) = _moveDragCells;
        var isDragged = MoveOffset is not null;
        EndMoveDrag();

        if (!isDragged)
            return;

        var map = Map;
        var count = SelectedCells.Count;
        var before = MapState.Capture(map);
        var selectionBefore = SelectedCells;
        var moved = MapMover.Move(map, SelectedCells, offsetX, offsetY);
        var dropped = count - moved.Count;
        SelectedCells = moved;
        if (!map.Cells.SequenceEqual(before.Cells))
        {
            RefreshCellRegions();
            RecordEdit(map, $"Move {count} {(count == 1 ? "cell" : "cells")}", before, selectionBefore);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        StatusMessage = dropped == 0
            ? $"Moved {count} {(count == 1 ? "cell" : "cells")}."
            : $"Moved {moved.Count} {(moved.Count == 1 ? "cell" : "cells")}; {dropped} moved outside the map and {(dropped == 1 ? "was" : "were")} dropped.";
    }

    public void EndMoveDrag()
    {
        _moveDragStart = null;
        _moveDragCells = (0, 0);
        MoveOffset = null;
    }

    private void UpdateSelectionStatus()
    {
        StatusMessage = SelectedCells.Count switch
        {
            0 => "Selection cleared.",
            1 => "Selected 1 cell.",
            _ => $"Selected {SelectedCells.Count} cells.",
        };
    }

    private void RefreshCellRegions()
    {
        if (_findTileset(Map.TilesetName) is { } tileset)
            CellRegions = Map.ResolveRegions(tileset.Tiles);
    }

    // Raised for painting, deleting, moving, undo, redo, resize and rename, so the project can be marked as changed.
    public event EventHandler? Changed;

    public void Rename(string newName)
    {
        Title = newName;
        Adopt(new TileMap(newName, Map.TilesetName, Map.Width, Map.Height, Map.Cells));
    }

    // Keeps the map pointing at an existing tileset when that tileset is renamed.
    public void RenameTileset(string newName) =>
        Adopt(new TileMap(Map.Name, newName, Map.Width, Map.Height, Map.Cells));

    public int CountTilesLostByResize(int width, int height) => MapResizer.CountLostTiles(Map, width, height);

    public void Resize(int width, int height)
    {
        var map = Map;
        if (width == map.Width && height == map.Height)
            return;

        // A drag in progress refers to the old size.
        EndSelectionDrag();
        EndMoveDrag();
        var before = MapState.Capture(map);
        var selectionBefore = SelectedCells;
        var resized = MapResizer.Resize(map, width, height);
        Adopt(resized);
        SelectedCells = selectionBefore.Where(cell => cell.X < width && cell.Y < height).ToList();
        RecordEdit(resized, "Resize map", before, selectionBefore);
        StatusMessage = $"Resized the map to {width} x {height} cells.";
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // Cells whose tile was deleted from the tileset were cleared in the map's cells by the project.
    // Older and redo steps may still hold the deleted tiles even if no current cell does.
    public void TilesDeleted()
    {
        _history.Clear();
        UpdateHistoryState();
        RefreshTileset();
    }

    // Ignored mid-drag: the drag's own state would be stale after the cells changed underneath it.
    public override void Undo()
    {
        if (!CanUndo || IsDragInProgress || _stroke is not null)
            return;

        var step = _history.Undo();
        Restore(step.Before, step.SelectionBefore);
        StatusMessage = $"Undid: {step.Description}.";
    }

    public override void Redo()
    {
        if (!CanRedo || IsDragInProgress || _stroke is not null)
            return;

        var step = _history.Redo();
        Restore(step.After, step.SelectionAfter);
        StatusMessage = $"Redid: {step.Description}.";
    }

    // Like the tileset editor, undoing back to the saved state still counts as a
    // change, because Changed marks the project as unsaved.
    private void Restore(MapState state, IReadOnlyList<(int X, int Y)> selection)
    {
        Adopt(state.ToMap(Map.Name, Map.TilesetName));
        SelectedCells = selection.Where(cell => cell.X < state.Width && cell.Y < state.Height).ToList();
        UpdateHistoryState();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RecordEdit(TileMap map, string description, MapState before, IReadOnlyList<(int X, int Y)> selectionBefore)
    {
        _history.Record(new MapEditStep(description, before, MapState.Capture(map), selectionBefore, SelectedCells));
        UpdateHistoryState();
    }

    private void UpdateHistoryState()
    {
        CanUndo = _history.CanUndo;
        CanRedo = _history.CanRedo;
    }

    // Swaps in a new instance of the same map; the tab keeps its zoom, selection and brush.
    private void Adopt(TileMap newMap)
    {
        Map = newMap;
        RefreshTileset();
    }
}
