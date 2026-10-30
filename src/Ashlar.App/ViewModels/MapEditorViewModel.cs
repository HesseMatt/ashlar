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

// Owns the maps of the open project and what the selected map shows: the cells
// resolved against its tileset, and the palette of that tileset's tiles.
public partial class MapEditorViewModel : ViewModelBase
{
    private readonly Func<string, ProjectTileset?> _findTileset;

    public MapEditorViewModel(ObservableCollection<string> tilesetNames, Func<string, ProjectTileset?> findTileset)
    {
        TilesetNames = tilesetNames;
        _findTileset = findTileset;
        TilesetNames.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanCreateMap));
    }

    public ObservableCollection<TileMap> Maps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMap), nameof(SelectedMapSizeText), nameof(SelectedMapTilesetText))]
    private TileMap? _selectedMap;

    partial void OnSelectedMapChanged(TileMap? value)
    {
        EndStroke();
        RefreshTileset();
        UpdateHistoryState();
        if (_isReplacingMap)
            return;

        SelectedCells = [];
        EndSelectionDrag();
        EndMoveDrag();
        ZoomLevel = 1.0;
        MapShown?.Invoke(this, EventArgs.Empty);
    }

    // A renamed map is a new instance, but the user is still looking at the same map.
    private bool _isReplacingMap;

    // The tileset image and tile size the selected map is drawn with; null/0 without a map.
    [ObservableProperty]
    private Bitmap? _tilesetImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelWidth), nameof(ScaledMapWidth), nameof(SelectionOutlines))]
    private int _tileWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelHeight), nameof(ScaledMapHeight), nameof(SelectionOutlines))]
    private int _tileHeight;

    // Per cell of the selected map, row-major: the region to draw, or null for an empty cell.
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

    // The selected cells of the selected map, as (column, row).
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

    // Every map has its own history, found by the map's name (names are unique in
    // a project). Switching maps or tabs keeps it. The history of a map is dropped
    // when the map is deleted or closed with the project, and when tiles are
    // cleared from it because they were deleted from its tileset: those changes
    // cannot be undone, so the older steps no longer describe the map's cells.
    private readonly Dictionary<string, EditHistory<MapEditStep>> _histories = new();

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledMapWidth), nameof(ScaledMapHeight), nameof(PreviewInterpolation))]
    private double _zoomLevel = 1.0;

    public int MapPixelWidth => (SelectedMap?.Width ?? 0) * TileWidth;

    public int MapPixelHeight => (SelectedMap?.Height ?? 0) * TileHeight;

    public double ScaledMapWidth => MapPixelWidth * ZoomLevel;

    public double ScaledMapHeight => MapPixelHeight * ZoomLevel;

    public BitmapInterpolationMode PreviewInterpolation => TilesetEditorViewModel.InterpolationAt(ZoomLevel);

    // Raised when another map is selected, so the view can scroll to its start.
    public event EventHandler? MapShown;

    // Raised when painting changed cells in place, so the view redraws them.
    public event EventHandler? CellsPainted;

    public double ZoomIn() => ZoomLevel = TilesetEditorViewModel.NextZoomIn(ZoomLevel);

    public double ZoomOut() => ZoomLevel = TilesetEditorViewModel.NextZoomOut(ZoomLevel);

    public void ResetZoom() => ZoomLevel = 1.0;

    // Reads the map's tileset again; the tileset may have been edited since the map was last shown.
    public void RefreshTileset()
    {
        if (SelectedMap is not { } map || _findTileset(map.TilesetName) is not { } tileset)
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

    // The project's tileset names; maps can only be created once there is a tileset to draw from.
    public ObservableCollection<string> TilesetNames { get; }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // The tileset open in the Tilesets tab, offered as the default for a new map.
    public string? PreferredTilesetName { get; set; }

    public bool HasSelectedMap => SelectedMap is not null;

    public string SelectedMapSizeText => SelectedMap is { } map ? $"{map.Width} x {map.Height} cells" : string.Empty;

    public string SelectedMapTilesetText => SelectedMap is { } map ? $"Tileset: {map.TilesetName}" : string.Empty;

    public bool CanCreateMap => TilesetNames.Count > 0;

    // Starts a draw stroke at a cell. Returns false when there is nothing to paint with.
    public bool BeginStroke(int cellX, int cellY)
    {
        if (!IsDrawToolActive || SelectedMap is null || SelectedPaletteTile is null)
            return false;

        _strokeCell = (cellX, cellY);
        _stroke = new DrawStroke(SelectedMap, MapState.Capture(SelectedMap));
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
        if (SelectedMap is not { } map || SelectedPaletteTile is not { } tile)
            return;

        var changed = MapPainter.PaintLine(map, fromX, fromY, toX, toY, tile.Name);
        if (changed.Count == 0)
            return;

        _stroke!.PaintedAnything = true;
        foreach (var (x, y) in changed)
            CellRegions[y * map.Width + x] = tile.Region;
        CellsPainted?.Invoke(this, EventArgs.Empty);
        MapsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Whether a drag with the rectangle or move tool is in progress.
    public bool IsDragInProgress => _selectionDrag is not null || _moveDragStart is not null;

    public void HandleCanvasClick(int cellX, int cellY, bool addToSelection)
    {
        if (!IsRectangleToolActive || SelectedMap is not { } map)
            return;

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
        if (_selectionDrag is not { } drag || SelectedMap is not { } map)
            return;

        var touched = MapRectangleSelector.CellsIn(map, drag.StartX, drag.StartY, cellX, cellY);
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
        if (SelectedMap is not { } map || !HasSelectedCells || IsDragInProgress)
            return;

        var before = MapState.Capture(map);
        var selectionBefore = SelectedCells;
        var cleared = MapEraser.Clear(map, SelectedCells);
        SelectedCells = [];
        StatusMessage = $"Cleared {cleared.Count} {(cleared.Count == 1 ? "cell" : "cells")}.";
        if (cleared.Count == 0)
            return;

        RefreshCellRegions();
        RecordEdit(map, $"Delete {cleared.Count} {(cleared.Count == 1 ? "cell" : "cells")}", before, selectionBefore);
        MapsChanged?.Invoke(this, EventArgs.Empty);
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

        if (!isDragged || SelectedMap is not { } map)
            return;

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
            MapsChanged?.Invoke(this, EventArgs.Empty);
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
        if (SelectedMap is { } map && _findTileset(map.TilesetName) is { } tileset)
            CellRegions = map.ResolveRegions(tileset.Tiles);
    }

    // Raised for create, rename, delete and painting, so the project can be marked as changed.
    public event EventHandler? MapsChanged;

    public void Load(IEnumerable<TileMap> maps)
    {
        Clear();
        foreach (var map in maps)
            Maps.Add(map);
        SelectedMap = Maps.FirstOrDefault();
    }

    public void Clear()
    {
        Maps.Clear();
        SelectedMap = null;
        _histories.Clear();
        UpdateHistoryState();
        StatusMessage = string.Empty;
    }

    public bool CreateMap(string name, int width, int height, string tilesetName)
    {
        name = name.Trim();
        if (GetNameError(name, null) is { } error)
        {
            StatusMessage = error;
            return false;
        }

        var map = TileMap.CreateEmpty(name, tilesetName, width, height);
        Maps.Add(map);
        SelectedMap = map;
        StatusMessage = $"Created the map '{name}'.";
        MapsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool RenameSelectedMap(string newName)
    {
        if (SelectedMap is not { } map)
            return false;

        newName = newName.Trim();
        if (newName == map.Name)
            return true;

        if (GetNameError(newName, map) is { } error)
        {
            StatusMessage = error;
            return false;
        }

        if (_histories.Remove(map.Name, out var history))
            _histories[newName] = history;
        Replace(map, new TileMap(newName, map.TilesetName, map.Width, map.Height, map.Cells));
        StatusMessage = $"Renamed the map to '{newName}'.";
        MapsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public int CountTilesLostByResize(int width, int height) =>
        SelectedMap is { } map ? MapResizer.CountLostTiles(map, width, height) : 0;

    public void ResizeSelectedMap(int width, int height)
    {
        if (SelectedMap is not { } map || (width == map.Width && height == map.Height))
            return;

        // Replace() skips the cleanup of a map change, but a drag in progress refers to the old size.
        EndSelectionDrag();
        EndMoveDrag();
        var before = MapState.Capture(map);
        var selectionBefore = SelectedCells;
        var resized = MapResizer.Resize(map, width, height);
        Replace(map, resized);
        SelectedCells = selectionBefore.Where(cell => cell.X < width && cell.Y < height).ToList();
        RecordEdit(resized, "Resize map", before, selectionBefore);
        StatusMessage = $"Resized the map to {width} x {height} cells.";
        MapsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteSelectedMap()
    {
        if (SelectedMap is not { } map)
            return;

        var index = Maps.IndexOf(map);
        Maps.RemoveAt(index);
        SelectedMap = Maps.Count == 0 ? null : Maps[Math.Min(index, Maps.Count - 1)];
        // After the selection moved on, which ends a stroke that was still open on the deleted map.
        _histories.Remove(map.Name);
        StatusMessage = $"Deleted the map '{map.Name}'.";
        MapsChanged?.Invoke(this, EventArgs.Empty);
    }

    public int CountMapsUsing(string tilesetName) => Maps.Count(map => map.TilesetName == tilesetName);

    // Keeps every map pointing at an existing tileset when that tileset is renamed.
    public void RenameTileset(string oldName, string newName)
    {
        foreach (var map in Maps.Where(map => map.TilesetName == oldName).ToList())
            Replace(map, new TileMap(map.Name, newName, map.Width, map.Height, map.Cells));
    }

    // Cells whose tile was deleted from the tileset become empty. Undoing that
    // deletion in the tileset editor restores the tile but not these cells.
    public void ClearDeletedTiles(string tilesetName, IReadOnlySet<string> deletedTileNames)
    {
        var clearedCells = MapTileCleaner.ClearTiles(Maps, tilesetName, deletedTileNames);

        // Older and redo steps of any map of the tileset may still hold the deleted tiles, even if no current cell does.
        foreach (var map in Maps.Where(map => map.TilesetName == tilesetName))
            _histories.Remove(map.Name);
        UpdateHistoryState();

        if (clearedCells == 0)
            return;

        RefreshCellRegions();
        MapsChanged?.Invoke(this, EventArgs.Empty);
    }

    // Maps cannot outlive their tileset.
    public void DeleteMapsUsing(string tilesetName)
    {
        foreach (var map in Maps.Where(map => map.TilesetName == tilesetName).ToList())
        {
            if (SelectedMap == map)
                SelectedMap = null;
            _histories.Remove(map.Name);
            Maps.Remove(map);
        }

        SelectedMap ??= Maps.FirstOrDefault();
    }

    // Ignored mid-drag: the drag's own state would be stale after the cells changed underneath it.
    public void Undo()
    {
        if (SelectedMap is not { } map || !CanUndo || IsDragInProgress || _stroke is not null)
            return;

        var step = _histories[map.Name].Undo();
        Restore(map, step.Before, step.SelectionBefore);
        StatusMessage = $"Undid: {step.Description}.";
    }

    public void Redo()
    {
        if (SelectedMap is not { } map || !CanRedo || IsDragInProgress || _stroke is not null)
            return;

        var step = _histories[map.Name].Redo();
        Restore(map, step.After, step.SelectionAfter);
        StatusMessage = $"Redid: {step.Description}.";
    }

    // Like the tileset editor, undoing back to the saved state still counts as a
    // change, because MapsChanged marks the project as unsaved.
    private void Restore(TileMap map, MapState state, IReadOnlyList<(int X, int Y)> selection)
    {
        Replace(map, state.ToMap(map.Name, map.TilesetName));
        SelectedCells = selection.Where(cell => cell.X < state.Width && cell.Y < state.Height).ToList();
        UpdateHistoryState();
        MapsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RecordEdit(TileMap map, string description, MapState before, IReadOnlyList<(int X, int Y)> selectionBefore)
    {
        if (!_histories.TryGetValue(map.Name, out var history))
            _histories[map.Name] = history = new EditHistory<MapEditStep>();

        history.Record(new MapEditStep(description, before, MapState.Capture(map), selectionBefore, SelectedCells));
        UpdateHistoryState();
    }

    private void UpdateHistoryState()
    {
        var history = SelectedMap is { } map && _histories.TryGetValue(map.Name, out var found) ? found : null;
        CanUndo = history?.CanUndo ?? false;
        CanRedo = history?.CanRedo ?? false;
    }

    private void Replace(TileMap oldMap, TileMap newMap)
    {
        var wasSelected = SelectedMap == oldMap;
        // The map list briefly deselects the map while it is replaced, which resets the brush.
        var brushName = SelectedPaletteTile?.Name;
        _isReplacingMap = true;
        try
        {
            Maps[Maps.IndexOf(oldMap)] = newMap;
            if (wasSelected)
            {
                SelectedMap = newMap;
                SelectedPaletteTile = PaletteTiles.FirstOrDefault(tile => tile.Name == brushName);
            }
        }
        finally
        {
            _isReplacingMap = false;
        }
    }

    // Names are shown to the user and must stay unique within the project.
    private string? GetNameError(string name, TileMap? renamed)
    {
        if (name.Length == 0)
            return "A map name cannot be empty.";
        if (Maps.Any(map => map.Name == name && map != renamed))
            return $"A map named '{name}' already exists in this project.";
        return null;
    }

    public string CreateUniqueName()
    {
        for (var number = Maps.Count + 1; ; number++)
        {
            var name = $"Map {number}";
            if (Maps.All(map => map.Name != name))
                return name;
        }
    }
}
