using System.Linq;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.App.ViewModels;

public partial class MainViewModel
{
    // The tileset a new map is drawn from unless the user picks another one.
    public string? SuggestedTilesetName =>
        SelectedTilesetName
        ?? (SelectedMapName is { } mapName ? FindMap(mapName)?.TilesetName : null)
        ?? ActiveTileset?.Title
        ?? TilesetNames.FirstOrDefault();

    public TileMap? GetMap(string name) => FindMap(name);

    public string CreateUniqueMapName()
    {
        for (var number = MapsGroup.Children.Count + 1; ; number++)
        {
            var name = $"Map {number}";
            if (IndexOfMap(name) < 0)
                return name;
        }
    }

    public bool CreateMap(string name, int width, int height, string tilesetName)
    {
        name = name.Trim();
        if (GetMapNameError(name, null) is { } error)
        {
            ReportStatus(error);
            return false;
        }

        _maps.Add(TileMap.CreateEmpty(name, tilesetName, width, height));
        MapsGroup.Children.Add(new ExplorerNode(ExplorerNodeKind.Map, name));
        MarkChanged();
        OpenMap(name);
        ReportStatus($"Created the map '{name}'.");
        return true;
    }

    // Focuses the tab of the map if it is open already.
    public void OpenMap(string name)
    {
        if (FindTab<MapEditorViewModel>(name) is { } open)
        {
            ActiveTab = open;
            return;
        }

        if (_maps.FirstOrDefault(map => map.Name == name) is not { } map)
            return;

        Attach(new MapEditorViewModel(map, FindTileset));
    }

    public bool RenameMap(string name, string newName)
    {
        var index = IndexOfMap(name);
        if (index < 0)
            return false;

        newName = newName.Trim();
        if (newName == name)
            return true;

        if (GetMapNameError(newName, name) is { } error)
        {
            ReportStatus(error);
            return false;
        }

        var map = _maps[index];
        _maps[index] = new TileMap(newName, map.TilesetName, map.Width, map.Height, map.Cells);
        MapsGroup.Children[index].Name = newName;
        if (FindTab<MapEditorViewModel>(name) is { } editor)
            editor.Rename(newName);

        MarkChanged();
        ReportStatus($"Renamed the map to '{newName}'.");
        return true;
    }

    public int CountTilesLostByResize(string name, int width, int height) =>
        FindMap(name) is { } map ? MapResizer.CountLostTiles(map, width, height) : 0;

    public void ResizeMap(string name, int width, int height)
    {
        if (FindMap(name) is not { } map || (width == map.Width && height == map.Height))
            return;

        if (FindTab<MapEditorViewModel>(name) is { } editor)
        {
            editor.Resize(width, height);
            return;
        }

        _maps[IndexOfMap(name)] = MapResizer.Resize(map, width, height);
        MarkChanged();
        ReportStatus($"Resized the map to {width} x {height} cells.");
    }

    public void DeleteMap(string name)
    {
        if (IndexOfMap(name) < 0)
            return;

        RemoveMap(name);
        MarkChanged();
        ReportStatus($"Deleted the map '{name}'.");
    }

    private void RemoveMap(string name)
    {
        if (FindTab<MapEditorViewModel>(name) is { } editor)
            CloseTab(editor, keepChanges: false);

        var index = IndexOfMap(name);
        if (ExplorerSelection == MapsGroup.Children[index])
            ExplorerSelection = null;
        _maps.RemoveAt(index);
        MapsGroup.Children.RemoveAt(index);
    }

    // Names are shown to the user and must stay unique within the project.
    private string? GetMapNameError(string name, string? renamed)
    {
        if (name.Length == 0)
            return "A map name cannot be empty.";
        if (MapNames.Any(existing => existing == name && existing != renamed))
            return $"A map named '{name}' already exists in this project.";
        return null;
    }
}
