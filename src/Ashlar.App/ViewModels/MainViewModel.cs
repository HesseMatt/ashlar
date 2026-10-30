using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core;
using Ashlar.Core.Model;
using Ashlar.Core.Rendering;
using Ashlar.Core.Serialization;

namespace Ashlar.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public const int TilesetsTabIndex = 0;
    public const int MapsTabIndex = 1;

    private const string InitialStatusMessage = "Create a project or open a tileset image to get started.";

    [ObservableProperty]
    private string _texturePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectEmpty))]
    private bool _isProjectOpen;

    [ObservableProperty]
    private string? _selectedTilesetName;

    partial void OnSelectedTilesetNameChanged(string? value) => MapEditor.PreferredTilesetName = value;

    public ObservableCollection<string> TilesetNames { get; } = new();

    public bool HasTilesets => _tilesets.Count > 0;

    public TilesetEditorViewModel Editor { get; } = new() { StatusMessage = InitialStatusMessage };

    public MapEditorViewModel MapEditor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveToolBar))]
    [NotifyPropertyChangedFor(nameof(IsTilesetsTabActive), nameof(CanUndo), nameof(CanRedo))]
    private int _selectedTabIndex;

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (value == MapsTabIndex)
            MapEditor.RefreshTileset();
    }

    public bool IsTilesetsTabActive => SelectedTabIndex == TilesetsTabIndex;

    // The editor whose tool bar is shown; the window picks the tool bar view by its type.
    public ViewModelBase ActiveToolBar => SelectedTabIndex == MapsTabIndex ? MapEditor : Editor;

    // Undo and redo act on the editor of the active tab.
    public bool CanUndo => IsTilesetsTabActive ? Editor.CanUndo : MapEditor.CanUndo;

    public bool CanRedo => IsTilesetsTabActive ? Editor.CanRedo : MapEditor.CanRedo;

    public void Undo()
    {
        if (IsTilesetsTabActive)
            Editor.Undo();
        else
            MapEditor.Undo();
    }

    public void Redo()
    {
        if (IsTilesetsTabActive)
            Editor.Redo();
        else
            MapEditor.Redo();
    }

    public bool IsProjectEmpty => IsProjectOpen && !Editor.HasImage;

    private readonly List<ProjectTileset> _tilesets = new();
    private int _selectedIndex = -1;

    public MainViewModel()
    {
        MapEditor = new MapEditorViewModel(TilesetNames, FindTileset);
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TilesetEditorViewModel.HasImage))
                OnPropertyChanged(nameof(IsProjectEmpty));
            else if (e.PropertyName == nameof(TilesetEditorViewModel.CanUndo))
                OnPropertyChanged(nameof(CanUndo));
            else if (e.PropertyName == nameof(TilesetEditorViewModel.CanRedo))
                OnPropertyChanged(nameof(CanRedo));
        };
        MapEditor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MapEditorViewModel.CanUndo))
                OnPropertyChanged(nameof(CanUndo));
            else if (e.PropertyName == nameof(MapEditorViewModel.CanRedo))
                OnPropertyChanged(nameof(CanRedo));
        };
        Editor.TilesDeleted += (_, names) =>
        {
            if (SelectedTilesetName is { } tilesetName)
                MapEditor.ClearDeletedTiles(tilesetName, names);
        };
        MapEditor.MapsChanged += (_, _) => Editor.HasUnsavedChanges = true;
    }

    // The file a plain Save writes to; null until the project was saved or opened.
    public string? ProjectPath { get; private set; }

    public string SuggestedProjectFileName =>
        Editor.SourceFileName is null ? "project" : Path.GetFileNameWithoutExtension(Editor.SourceFileName);

    // Reads an image for the tile size dialog without touching the open
    // tileset, so cancelling the import leaves everything as it was.
    public EditableTilesetImage? ReadImage(string filePath)
    {
        try
        {
            return EditableTilesetImage.Load(filePath);
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not open '{Path.GetFileName(filePath)}': {ex.Message}");
            return null;
        }
    }

    // Adds the image as a new tileset and selects it. The undo history is
    // per editor session, so it is reset whenever another tileset is selected.
    public void LoadTileset(string filePath, int tileWidth, int tileHeight)
    {
        var image = ReadImage(filePath);
        if (image is null)
            return;

        CaptureSelectedTileset();
        var fileName = Path.GetFileName(filePath);
        var name = CreateUniqueName(Path.GetFileNameWithoutExtension(filePath));
        // Opened first so the tileset list is already laid out when the canvas positions the new image.
        IsProjectOpen = true;
        Editor.LoadNewTileset(image, fileName, tileWidth, tileHeight);
        TexturePath = Path.GetFileNameWithoutExtension(filePath);
        _tilesets.Add(CreateSnapshot(name));
        TilesetNames.Add(name);
        _selectedIndex = _tilesets.Count - 1;
        SelectedTilesetName = name;
        OnPropertyChanged(nameof(HasTilesets));
        ReportStatus($"{fileName} ({image.Width}x{image.Height}) - tile size {Editor.TileSizeText}.");
    }

    public void SelectTileset(string name)
    {
        var index = TilesetNames.IndexOf(name);
        if (index < 0 || index == _selectedIndex)
            return;

        CaptureSelectedTileset();
        ActivateTileset(index);
    }

    // Names must stay unique because they identify tilesets in the project file.
    public bool RenameSelectedTileset(string newName)
    {
        if (_selectedIndex < 0)
            return false;

        newName = newName.Trim();
        if (newName.Length == 0)
        {
            ReportStatus("A tileset name cannot be empty.");
            return false;
        }

        var existingIndex = TilesetNames.IndexOf(newName);
        if (existingIndex >= 0 && existingIndex != _selectedIndex)
        {
            ReportStatus($"A tileset named '{newName}' already exists in this project.");
            return false;
        }

        if (newName == TilesetNames[_selectedIndex])
            return true;

        var oldName = TilesetNames[_selectedIndex];
        _tilesets[_selectedIndex] = _tilesets[_selectedIndex] with { Name = newName };
        // The names are updated first so the maps can find their renamed tileset.
        TilesetNames[_selectedIndex] = newName;
        MapEditor.RenameTileset(oldName, newName);
        SelectedTilesetName = newName;
        Editor.HasUnsavedChanges = true;
        ReportStatus($"Renamed the tileset to '{newName}'.");
        return true;
    }

    public void RemoveSelectedTileset()
    {
        if (_selectedIndex < 0)
            return;

        var removedName = TilesetNames[_selectedIndex];
        var removedIndex = _selectedIndex;
        _tilesets.RemoveAt(removedIndex);
        TilesetNames.RemoveAt(removedIndex);
        // The editor still holds the removed tileset; maps must not look it up as the live one.
        _selectedIndex = -1;
        MapEditor.DeleteMapsUsing(removedName);

        if (_tilesets.Count == 0)
        {
            Editor.Close();
            TexturePath = string.Empty;
            _selectedIndex = -1;
            SelectedTilesetName = null;
        }
        else
        {
            ActivateTileset(Math.Min(removedIndex, _tilesets.Count - 1));
        }

        OnPropertyChanged(nameof(HasTilesets));
        Editor.HasUnsavedChanges = true;
        ReportStatus($"Removed the tileset '{removedName}'.");
    }

    public void NewProject()
    {
        ResetProject(isOpen: true);
        ReportStatus("New project. Import a tileset to add the first one.");
    }

    public void OpenProject(string filePath)
    {
        Project project;
        try
        {
            project = ProjectFile.Load(filePath);
            foreach (var tileset in project.Tilesets)
                EditableTilesetImage.Load(tileset.ImagePng).Dispose();
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not open project '{Path.GetFileName(filePath)}': {ex.Message}");
            return;
        }

        ResetProject(isOpen: true);
        ProjectPath = filePath;
        foreach (var tileset in project.Tilesets)
        {
            _tilesets.Add(tileset);
            TilesetNames.Add(tileset.Name);
        }

        MapEditor.Load(project.Maps);

        OnPropertyChanged(nameof(HasTilesets));
        if (_tilesets.Count == 0)
        {
            ReportStatus($"Resumed project {Path.GetFileName(filePath)} (no tilesets).");
            return;
        }

        ActivateTileset(0);
        ReportStatus($"Resumed project {Path.GetFileName(filePath)} ({_tilesets.Count} tilesets).");
    }

    public void CloseProject()
    {
        ResetProject(isOpen: false);
        Editor.StatusMessage = InitialStatusMessage;
        MapEditor.StatusMessage = InitialStatusMessage;
    }

    private void ResetProject(bool isOpen)
    {
        Editor.Close();
        MapEditor.Clear();
        _tilesets.Clear();
        TilesetNames.Clear();
        _selectedIndex = -1;
        SelectedTilesetName = null;
        OnPropertyChanged(nameof(HasTilesets));
        ProjectPath = null;
        TexturePath = string.Empty;
        IsProjectOpen = isOpen;
    }

    public ExportDialogViewModel CreateExportDialogViewModel() =>
        new(TexturePath, Editor.TileWidth, Editor.TileHeight, Editor.Tiles);

    public void ExportXml(string outputPath)
    {
        if (Editor.SourceFileName is null)
            return;

        var tileset = new Tileset(TexturePath, Editor.TileWidth, Editor.TileHeight, Editor.Tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            ReportStatus($"Exported {Editor.Tiles.Count} tiles to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}");
        }
    }

    // Null when no map is selected or its tileset no longer exists.
    public ExportMapDialogViewModel? CreateExportMapDialogViewModel()
    {
        if (MapEditor.SelectedMap is not { } map || FindTileset(map.TilesetName) is not { } tileset)
            return null;

        return new ExportMapDialogViewModel(map, ToTileset(tileset));
    }

    // Either path may be null to skip that file. The tileset is read live, so edits not yet saved to the project are included.
    public void ExportMap(string? xmlPath, string? imagePath)
    {
        if (MapEditor.SelectedMap is not { } map || FindTileset(map.TilesetName) is not { } tileset)
            return;

        try
        {
            if (xmlPath is not null)
                TilemapXmlWriter.Write(map, ToTileset(tileset), xmlPath);

            if (imagePath is not null)
            {
                using var tilesetImage = EditableTilesetImage.Load(tileset.ImagePng);
                using var mapImage = TileMapImageRenderer.Render(map, tileset.Tiles, tilesetImage, tileset.TileWidth, tileset.TileHeight);
                mapImage.SaveAsPng(imagePath);
            }

            var fileNames = new[] { xmlPath, imagePath }.OfType<string>().Select(Path.GetFileName);
            ReportStatus($"Exported map '{map.Name}' to {string.Join(" and ", fileNames)}.");
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not export map '{map.Name}': {ex.Message}");
        }
    }

    private static Tileset ToTileset(ProjectTileset tileset) =>
        new(tileset.TexturePath, tileset.TileWidth, tileset.TileHeight, tileset.Tiles);

    public void SaveImage(string outputPath)
    {
        if (Editor.Image is not { } image)
            return;

        try
        {
            image.SaveAsPng(outputPath);
            ReportStatus($"Saved image to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not save image to '{Path.GetFileName(outputPath)}': {ex.Message}");
        }
    }

    // The status bar shows the active tab's message, so shell-level messages go to that editor.
    private void ReportStatus(string message)
    {
        if (IsTilesetsTabActive)
            Editor.StatusMessage = message;
        else
            MapEditor.StatusMessage = message;
    }

    public bool SaveProject(string outputPath)
    {
        if (!IsProjectOpen)
            return false;

        try
        {
            ProjectFile.Save(BuildProject(), outputPath);
            ProjectPath = outputPath;
            Editor.HasUnsavedChanges = false;
            ReportStatus($"Saved project to {Path.GetFileName(outputPath)}.");
            return true;
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}");
            return false;
        }
    }

    private Project BuildProject()
    {
        CaptureSelectedTileset();
        return new Project(_tilesets.ToList(), MapEditor.Maps.ToList());
    }

    // The editor holds the live state of the selected tileset; the list entry
    // is only brought up to date when it is needed.
    private void CaptureSelectedTileset()
    {
        if (_selectedIndex >= 0)
            _tilesets[_selectedIndex] = CreateSnapshot(_tilesets[_selectedIndex].Name);
    }

    // The selected tileset is edited live, so its list entry can be stale.
    private ProjectTileset? FindTileset(string name)
    {
        var index = TilesetNames.IndexOf(name);
        if (index < 0)
            return null;

        return index == _selectedIndex ? CreateSnapshot(name) : _tilesets[index];
    }

    private ProjectTileset CreateSnapshot(string name)
    {
        var drawColor = Editor.DrawColor;
        return new ProjectTileset(name, Editor.SourceFileName!, Editor.Image!.EncodeToPng(), TexturePath,
            Editor.TileWidth, Editor.TileHeight, Editor.Tiles.ToList(), new PixelColor(drawColor.R, drawColor.G, drawColor.B, drawColor.A));
    }

    private void ActivateTileset(int index)
    {
        var tileset = _tilesets[index];
        var hadUnsavedChanges = Editor.HasUnsavedChanges;
        Editor.LoadProject(tileset, EditableTilesetImage.Load(tileset.ImagePng));
        Editor.HasUnsavedChanges = hadUnsavedChanges;
        TexturePath = tileset.TexturePath;
        _selectedIndex = index;
        SelectedTilesetName = tileset.Name;
        ReportStatus($"{tileset.Name} ({Editor.Tiles.Count} tiles) - tile size {Editor.TileSizeText}.");
    }

    private string CreateUniqueName(string baseName)
    {
        var name = baseName;
        for (var suffix = 2; TilesetNames.Contains(name); suffix++)
            name = $"{baseName} {suffix}";
        return name;
    }
}
