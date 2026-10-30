using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        _strokeCell = null;
        RefreshTileset();
        if (_isReplacingMap)
            return;

        ZoomLevel = 1.0;
        MapShown?.Invoke(this, EventArgs.Empty);
    }

    // A renamed map is a new instance, but the user is still looking at the same map.
    private bool _isReplacingMap;

    // The tileset image and tile size the selected map is drawn with; null/0 without a map.
    [ObservableProperty]
    private Bitmap? _tilesetImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelWidth), nameof(ScaledMapWidth))]
    private int _tileWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MapPixelHeight), nameof(ScaledMapHeight))]
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
    private bool _isDrawToolActive;

    partial void OnIsDrawToolActiveChanged(bool value) => _strokeCell = null;

    // The cell the current draw stroke reached last; null while no stroke is in progress.
    private (int X, int Y)? _strokeCell;

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

    public void EndStroke() => _strokeCell = null;

    private void Paint(int fromX, int fromY) => Paint(fromX, fromY, fromX, fromY);

    private void Paint(int fromX, int fromY, int toX, int toY)
    {
        if (SelectedMap is not { } map || SelectedPaletteTile is not { } tile)
            return;

        var changed = MapPainter.PaintLine(map, fromX, fromY, toX, toY, tile.Name);
        if (changed.Count == 0)
            return;

        foreach (var (x, y) in changed)
            CellRegions[y * map.Width + x] = tile.Region;
        CellsPainted?.Invoke(this, EventArgs.Empty);
        MapsChanged?.Invoke(this, EventArgs.Empty);
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
        _strokeCell = null;
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

        Replace(map, new TileMap(newName, map.TilesetName, map.Width, map.Height, map.Cells));
        StatusMessage = $"Renamed the map to '{newName}'.";
        MapsChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void DeleteSelectedMap()
    {
        if (SelectedMap is not { } map)
            return;

        var index = Maps.IndexOf(map);
        Maps.RemoveAt(index);
        SelectedMap = Maps.Count == 0 ? null : Maps[Math.Min(index, Maps.Count - 1)];
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

    // Maps cannot outlive their tileset.
    public void DeleteMapsUsing(string tilesetName)
    {
        foreach (var map in Maps.Where(map => map.TilesetName == tilesetName).ToList())
        {
            if (SelectedMap == map)
                SelectedMap = null;
            Maps.Remove(map);
        }

        SelectedMap ??= Maps.FirstOrDefault();
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
