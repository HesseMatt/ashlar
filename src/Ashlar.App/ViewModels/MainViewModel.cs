using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core;
using Ashlar.Core.Model;
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

    public ObservableCollection<string> TilesetNames { get; } = new();

    public bool HasTilesets => _tilesets.Count > 0;

    public TilesetEditorViewModel Editor { get; } = new() { StatusMessage = InitialStatusMessage };

    public MapEditorViewModel MapEditor { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveToolBar))]
    private int _selectedTabIndex;

    // The editor whose tool bar is shown; the window picks the tool bar view by its type.
    public bool IsTilesetsTabActive => SelectedTabIndex == TilesetsTabIndex;

    public ViewModelBase ActiveToolBar => SelectedTabIndex == MapsTabIndex ? MapEditor : Editor;

    public bool IsProjectEmpty => IsProjectOpen && !Editor.HasImage;

    private readonly List<ProjectTileset> _tilesets = new();
    private int _selectedIndex = -1;

    public MainViewModel()
    {
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TilesetEditorViewModel.HasImage))
                OnPropertyChanged(nameof(IsProjectEmpty));
        };
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
            Editor.StatusMessage = $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}";
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
        Editor.StatusMessage = $"{fileName} ({image.Width}x{image.Height}) - tile size {Editor.TileSizeText}.";
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
            Editor.StatusMessage = "A tileset name cannot be empty.";
            return false;
        }

        var existingIndex = TilesetNames.IndexOf(newName);
        if (existingIndex >= 0 && existingIndex != _selectedIndex)
        {
            Editor.StatusMessage = $"A tileset named '{newName}' already exists in this project.";
            return false;
        }

        if (newName == TilesetNames[_selectedIndex])
            return true;

        _tilesets[_selectedIndex] = _tilesets[_selectedIndex] with { Name = newName };
        TilesetNames[_selectedIndex] = newName;
        SelectedTilesetName = newName;
        Editor.HasUnsavedChanges = true;
        Editor.StatusMessage = $"Renamed the tileset to '{newName}'.";
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
        Editor.StatusMessage = $"Removed the tileset '{removedName}'.";
    }

    public void NewProject()
    {
        ResetProject(isOpen: true);
        Editor.StatusMessage = "New project. Import a tileset to add the first one.";
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
            Editor.StatusMessage = $"Could not open project '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        ResetProject(isOpen: true);
        ProjectPath = filePath;
        foreach (var tileset in project.Tilesets)
        {
            _tilesets.Add(tileset);
            TilesetNames.Add(tileset.Name);
        }

        OnPropertyChanged(nameof(HasTilesets));
        if (_tilesets.Count == 0)
        {
            Editor.StatusMessage = $"Resumed project {Path.GetFileName(filePath)} (no tilesets).";
            return;
        }

        ActivateTileset(0);
        Editor.StatusMessage = $"Resumed project {Path.GetFileName(filePath)} ({_tilesets.Count} tilesets).";
    }

    public void CloseProject()
    {
        ResetProject(isOpen: false);
        Editor.StatusMessage = InitialStatusMessage;
    }

    private void ResetProject(bool isOpen)
    {
        Editor.Close();
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
            Editor.StatusMessage = $"Exported {Editor.Tiles.Count} tiles to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public void SaveImage(string outputPath)
    {
        if (Editor.Image is not { } image)
            return;

        try
        {
            image.SaveAsPng(outputPath);
            Editor.StatusMessage = $"Saved image to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not save image to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
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
            Editor.StatusMessage = $"Saved project to {Path.GetFileName(outputPath)}.";
            return true;
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}";
            return false;
        }
    }

    private Project BuildProject()
    {
        CaptureSelectedTileset();
        return new Project(_tilesets.ToList(), []);
    }

    // The editor holds the live state of the selected tileset; the list entry
    // is only brought up to date when it is needed.
    private void CaptureSelectedTileset()
    {
        if (_selectedIndex >= 0)
            _tilesets[_selectedIndex] = CreateSnapshot(_tilesets[_selectedIndex].Name);
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
        Editor.StatusMessage = $"{tileset.Name} ({Editor.Tiles.Count} tiles) - tile size {Editor.TileSizeText}.";
    }

    private string CreateUniqueName(string baseName)
    {
        var name = baseName;
        for (var suffix = 2; TilesetNames.Contains(name); suffix++)
            name = $"{baseName} {suffix}";
        return name;
    }
}
