using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core.Model;

namespace Ashlar.App.ViewModels;

// Owns the maps of the open project. There is no map canvas yet, so the
// selected map is only shown as a placeholder.
public partial class MapEditorViewModel : ViewModelBase
{
    public MapEditorViewModel(ObservableCollection<string> tilesetNames)
    {
        TilesetNames = tilesetNames;
        TilesetNames.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanCreateMap));
    }

    public ObservableCollection<TileMap> Maps { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedMap), nameof(SelectedMapSizeText), nameof(SelectedMapTilesetText))]
    private TileMap? _selectedMap;

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

    // Raised for create, rename and delete, so the project can be marked as changed.
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
        Maps[Maps.IndexOf(oldMap)] = newMap;
        if (wasSelected)
            SelectedMap = newMap;
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
