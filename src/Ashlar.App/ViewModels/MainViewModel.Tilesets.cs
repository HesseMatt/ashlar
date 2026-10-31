using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ashlar.Core;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;

namespace Ashlar.App.ViewModels;

public partial class MainViewModel
{
    // Adds the image as a new tileset and opens it in a tab.
    public void LoadTileset(string filePath, int tileWidth, int tileHeight)
    {
        var image = ReadImage(filePath);
        if (image is null)
            return;

        var fileName = Path.GetFileName(filePath);
        var name = CreateUniqueTilesetName(Path.GetFileNameWithoutExtension(filePath));
        // Opened first so the editor area is already laid out when the canvas positions the new image.
        IsProjectOpen = true;
        var editor = new TilesetEditorViewModel(name);
        editor.LoadNewTileset(image, fileName, tileWidth, tileHeight);
        _tilesets.Add(editor.ToProjectTileset());
        TilesetsGroup.Children.Add(new ExplorerNode(ExplorerNodeKind.Tileset, name));
        MarkChanged();
        Attach(editor);
        ReportStatus($"{fileName} ({image.Width}x{image.Height}) - tile size {editor.TileSizeText}.");
    }

    // Focuses the tab of the tileset if it is open already.
    public void OpenTileset(string name)
    {
        if (FindTab<TilesetEditorViewModel>(name) is { } open)
        {
            ActiveTab = open;
            return;
        }

        if (_tilesets.FirstOrDefault(tileset => tileset.Name == name) is not { } tileset)
            return;

        var editor = new TilesetEditorViewModel(name);
        editor.LoadProject(tileset, EditableTilesetImage.Load(tileset.ImagePng));
        Attach(editor);
        ReportStatus($"{name} ({editor.Tiles.Count} tiles) - tile size {editor.TileSizeText}.");
    }

    // Names must stay unique because they identify tilesets in the project file.
    public bool RenameTileset(string name, string newName)
    {
        var index = IndexOfTileset(name);
        if (index < 0)
            return false;

        newName = newName.Trim();
        if (newName.Length == 0)
        {
            ReportStatus("A tileset name cannot be empty.");
            return false;
        }

        var existingIndex = IndexOfTileset(newName);
        if (existingIndex >= 0 && existingIndex != index)
        {
            ReportStatus($"A tileset named '{newName}' already exists in this project.");
            return false;
        }

        if (newName == name)
            return true;

        _tilesets[index] = _tilesets[index] with { Name = newName };
        TilesetsGroup.Children[index].Name = newName;
        if (FindTab<TilesetEditorViewModel>(name) is { } editor)
            editor.Title = newName;

        foreach (var mapName in MapNames.Where(mapName => FindMap(mapName)!.TilesetName == name))
        {
            if (FindTab<MapEditorViewModel>(mapName) is { } mapEditor)
                mapEditor.RenameTileset(newName);
            else
                _maps[IndexOfMap(mapName)] = RenamedTileset(_maps[IndexOfMap(mapName)], newName);
        }

        MarkChanged();
        ReportStatus($"Renamed the tileset to '{newName}'.");
        return true;
    }

    private static TileMap RenamedTileset(TileMap map, string tilesetName) =>
        new(map.Name, tilesetName, map.Width, map.Height, map.Cells);

    public int CountMapsUsing(string tilesetName) => MapNames.Count(mapName => FindMap(mapName)!.TilesetName == tilesetName);

    // Maps cannot outlive their tileset.
    public void RemoveTileset(string name)
    {
        var index = IndexOfTileset(name);
        if (index < 0)
            return;

        foreach (var mapName in MapNames.Where(mapName => FindMap(mapName)!.TilesetName == name))
            RemoveMap(mapName);

        if (FindTab<TilesetEditorViewModel>(name) is { } editor)
            CloseTab(editor, keepChanges: false);

        if (ExplorerSelection == TilesetsGroup.Children[index])
            ExplorerSelection = null;
        _tilesets.RemoveAt(index);
        TilesetsGroup.Children.RemoveAt(index);
        MarkChanged();
        ReportStatus($"Removed the tileset '{name}'.");
    }

    // Cells whose tile was deleted from the tileset become empty. Undoing that
    // deletion in the tileset editor restores the tile but not these cells.
    private void OnTilesDeleted(object? sender, IReadOnlySet<string> deletedTileNames)
    {
        var tilesetName = ((TilesetEditorViewModel)sender!).Title;
        var maps = MapNames.Select(name => FindMap(name)!).ToList();
        var clearedCells = MapTileCleaner.ClearTiles(maps, tilesetName, deletedTileNames);

        foreach (var editor in OpenTabs.OfType<MapEditorViewModel>().Where(editor => editor.Map.TilesetName == tilesetName))
            editor.TilesDeleted();

        if (clearedCells > 0)
            MarkChanged();
    }

    private string CreateUniqueTilesetName(string baseName)
    {
        var name = baseName;
        for (var suffix = 2; IndexOfTileset(name) >= 0; suffix++)
            name = $"{baseName} {suffix}";
        return name;
    }
}
