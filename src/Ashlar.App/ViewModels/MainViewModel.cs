using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core;
using Ashlar.Core.Editing;
using Ashlar.Core.Model;
using Ashlar.Core.Rendering;
using Ashlar.Core.Serialization;

namespace Ashlar.App.ViewModels;

// The open project: its tilesets and maps, the explorer that lists them and the tabs
// they are edited in. An element that is open in a tab is edited live there, so its
// entry in the project lists is only brought up to date when it is needed.
public partial class MainViewModel : ViewModelBase
{
    private const string InitialStatusMessage = "Create a project or open a tileset image to get started.";

    private readonly List<ProjectTileset> _tilesets = new();
    private readonly List<TileMap> _maps = new();

    public MainViewModel()
    {
        ExplorerRoots = [TilesetsGroup, MapsGroup];
        OpenTabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasOpenTabs));
        TilesetsGroup.Children.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTilesets));
            OnPropertyChanged(nameof(IsProjectEmpty));
            OnPropertyChanged(nameof(CanCreateMap));
            OnPropertyChanged(nameof(EmptyEditorText));
        };
        MapsGroup.Children.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMaps));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectEmpty), nameof(EmptyEditorText))]
    private bool _isProjectOpen;

    // Whether the project is open but still without a tileset.
    public bool IsProjectEmpty => IsProjectOpen && !HasTilesets;

    // Whether the project differs from what was last saved.
    [ObservableProperty]
    private bool _hasUnsavedChanges;

    // The file a plain Save writes to; null until the project was saved or opened.
    public string? ProjectPath { get; private set; }

    public string SuggestedProjectFileName =>
        _tilesets.FirstOrDefault() is { } first ? Path.GetFileNameWithoutExtension(first.ImageFileName) : "project";

    public ExplorerNode TilesetsGroup { get; } = new(ExplorerNodeKind.TilesetGroup, "Tilesets");

    public ExplorerNode MapsGroup { get; } = new(ExplorerNodeKind.MapGroup, "Maps");

    public IReadOnlyList<ExplorerNode> ExplorerRoots { get; }

    public bool HasTilesets => TilesetsGroup.Children.Count > 0;

    public bool HasMaps => MapsGroup.Children.Count > 0;

    public IReadOnlyList<string> TilesetNames => TilesetsGroup.Children.Select(node => node.Name).ToList();

    public IReadOnlyList<string> MapNames => MapsGroup.Children.Select(node => node.Name).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTilesetName), nameof(SelectedMapName), nameof(HasSelectedTileset), nameof(HasSelectedMap))]
    private ExplorerNode? _explorerSelection;

    public string? SelectedTilesetName => ExplorerSelection is { IsTileset: true } node ? node.Name : null;

    public string? SelectedMapName => ExplorerSelection is { IsMap: true } node ? node.Name : null;

    public bool HasSelectedTileset => SelectedTilesetName is not null;

    public bool HasSelectedMap => SelectedMapName is not null;

    public ObservableCollection<EditorTabViewModel> OpenTabs { get; } = new();

    public bool HasOpenTabs => OpenTabs.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveTileset), nameof(ActiveMap), nameof(IsTilesetTabActive), nameof(IsMapTabActive), nameof(StatusText))]
    private EditorTabViewModel? _activeTab;

    // The view model whose tool bar is shown; the window picks the tool bar view by its type.
    public TilesetEditorViewModel? ActiveTileset => ActiveTab as TilesetEditorViewModel;

    public MapEditorViewModel? ActiveMap => ActiveTab as MapEditorViewModel;

    public bool IsTilesetTabActive => ActiveTab is TilesetEditorViewModel;

    public bool IsMapTabActive => ActiveTab is MapEditorViewModel;

    // Maps can only be created once there is a tileset to draw from.
    public bool CanCreateMap => HasTilesets;

    public string StatusText => ActiveTab?.StatusMessage ?? _shellStatusMessage;

    public string EmptyEditorText =>
        !IsProjectOpen ? "No project open" : HasTilesets ? "Double-click a tileset or map in the project explorer to open it" : "This project has no tileset yet";

    private string _shellStatusMessage = InitialStatusMessage;

    partial void OnActiveTabChanged(EditorTabViewModel? oldValue, EditorTabViewModel? newValue)
    {
        if (oldValue is not null)
            oldValue.IsActive = false;
        if (newValue is null)
            return;

        newValue.IsActive = true;
        // The tileset may have been edited in another tab since this map was last shown.
        (newValue as MapEditorViewModel)?.RefreshTileset();
        ExplorerSelection = FindNode(newValue);
    }

    private ExplorerNode? FindNode(EditorTabViewModel tab)
    {
        var group = tab is TilesetEditorViewModel ? TilesetsGroup : MapsGroup;
        return group.Children.FirstOrDefault(node => node.Name == tab.Title);
    }

    // The status bar shows the active tab's message, so shell-level messages go to that tab.
    private void ReportStatus(string message)
    {
        if (ActiveTab is { } tab)
            tab.StatusMessage = message;
        else
        {
            _shellStatusMessage = message;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void MarkChanged() => HasUnsavedChanges = true;

    public void Undo() => ActiveTab?.Undo();

    public void Redo() => ActiveTab?.Redo();

    // Reads an image for the tile size dialog without touching the project,
    // so cancelling the import leaves everything as it was.
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
            TilesetsGroup.Children.Add(new ExplorerNode(ExplorerNodeKind.Tileset, tileset.Name));
        }

        foreach (var map in project.Maps)
        {
            _maps.Add(map);
            MapsGroup.Children.Add(new ExplorerNode(ExplorerNodeKind.Map, map.Name));
        }

        ReportStatus($"Resumed project {Path.GetFileName(filePath)} ({_tilesets.Count} tilesets, {_maps.Count} maps).");
    }

    public void CloseProject()
    {
        ResetProject(isOpen: false);
        ReportStatus(InitialStatusMessage);
    }

    private void ResetProject(bool isOpen)
    {
        foreach (var tab in OpenTabs.ToList())
            CloseTab(tab, keepChanges: false);

        _tilesets.Clear();
        _maps.Clear();
        TilesetsGroup.Children.Clear();
        MapsGroup.Children.Clear();
        ExplorerSelection = null;
        ProjectPath = null;
        HasUnsavedChanges = false;
        IsProjectOpen = isOpen;
    }

    public bool SaveProject(string outputPath)
    {
        if (!IsProjectOpen)
            return false;

        try
        {
            CaptureOpenTabs();
            ProjectFile.Save(new Project(_tilesets.ToList(), _maps.ToList()), outputPath);
            ProjectPath = outputPath;
            HasUnsavedChanges = false;
            ReportStatus($"Saved project to {Path.GetFileName(outputPath)}.");
            return true;
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}");
            return false;
        }
    }

    // Brings the project lists up to date with the elements that are being edited.
    private void CaptureOpenTabs()
    {
        foreach (var tab in OpenTabs)
            Capture(tab);
    }

    private void Capture(EditorTabViewModel tab)
    {
        switch (tab)
        {
            case TilesetEditorViewModel editor:
                _tilesets[IndexOfTileset(editor.Title)] = editor.ToProjectTileset();
                break;
            case MapEditorViewModel editor:
                _maps[IndexOfMap(editor.Title)] = editor.Map;
                break;
        }
    }

    public void OpenTab(ExplorerNode node)
    {
        if (node.IsTileset)
            OpenTileset(node.Name);
        else if (node.IsMap)
            OpenMap(node.Name);
    }

    public void CloseActiveTab()
    {
        if (ActiveTab is { } tab)
            CloseTab(tab);
    }

    // Closing never asks: the changes already live in the project. The tab's undo history is dropped with it.
    public void CloseTab(EditorTabViewModel tab) => CloseTab(tab, keepChanges: true);

    private void CloseTab(EditorTabViewModel tab, bool keepChanges)
    {
        var index = OpenTabs.IndexOf(tab);
        if (index < 0)
            return;

        if (keepChanges)
            Capture(tab);

        // The tab strip selects another tab on its own once the active one is removed, so the choice is made here.
        var wasActive = ActiveTab == tab;
        Detach(tab);
        OpenTabs.RemoveAt(index);
        if (wasActive)
            ActiveTab = OpenTabs.Count == 0 ? null : OpenTabs[Math.Max(index - 1, 0)];
    }

    private void Attach(EditorTabViewModel tab)
    {
        tab.PropertyChanged += OnTabPropertyChanged;
        switch (tab)
        {
            case TilesetEditorViewModel editor:
                editor.Changed += OnElementChanged;
                editor.TilesDeleted += OnTilesDeleted;
                break;
            case MapEditorViewModel editor:
                editor.Changed += OnElementChanged;
                break;
        }

        OpenTabs.Add(tab);
        ActiveTab = tab;
    }

    private void Detach(EditorTabViewModel tab)
    {
        tab.PropertyChanged -= OnTabPropertyChanged;
        switch (tab)
        {
            case TilesetEditorViewModel editor:
                editor.Changed -= OnElementChanged;
                editor.TilesDeleted -= OnTilesDeleted;
                editor.Close();
                break;
            case MapEditorViewModel editor:
                editor.Changed -= OnElementChanged;
                break;
        }

        tab.IsActive = false;
    }

    private void OnTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorTabViewModel.StatusMessage) && sender == ActiveTab)
            OnPropertyChanged(nameof(StatusText));
    }

    private void OnElementChanged(object? sender, EventArgs e) => MarkChanged();

    private T? FindTab<T>(string name) where T : EditorTabViewModel =>
        OpenTabs.OfType<T>().FirstOrDefault(tab => tab.Title == name);

    private int IndexOfTileset(string name) => TilesetsGroup.Children.ToList().FindIndex(node => node.Name == name);

    private int IndexOfMap(string name) => MapsGroup.Children.ToList().FindIndex(node => node.Name == name);

    // Open elements are read live, so edits not yet captured into the project are included.
    private ProjectTileset? FindTileset(string name)
    {
        if (FindTab<TilesetEditorViewModel>(name) is { } editor)
            return editor.ToProjectTileset();

        return _tilesets.FirstOrDefault(tileset => tileset.Name == name);
    }

    private TileMap? FindMap(string name) =>
        FindTab<MapEditorViewModel>(name)?.Map ?? _maps.FirstOrDefault(map => map.Name == name);

    public void RenameNode(ExplorerNode node, string newName)
    {
        if (node.IsTileset)
            RenameTileset(node.Name, newName);
        else if (node.IsMap)
            RenameMap(node.Name, newName);
    }

    public ExportDialogViewModel CreateExportDialogViewModel() =>
        new(ActiveTileset!.TexturePath, ActiveTileset.TileWidth, ActiveTileset.TileHeight, ActiveTileset.Tiles);

    public void ExportXml(string outputPath)
    {
        if (ActiveTileset is not { SourceFileName: not null } editor)
            return;

        var tileset = new Tileset(editor.TexturePath, editor.TileWidth, editor.TileHeight, editor.Tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            ReportStatus($"Exported {editor.Tiles.Count} tiles to {Path.GetFileName(outputPath)}.");
        }
        catch (Exception ex)
        {
            ReportStatus($"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}");
        }
    }

    public void SaveImage(string outputPath)
    {
        if (ActiveTileset?.Image is not { } image)
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

    // Null when no map is selected or its tileset no longer exists.
    public ExportMapDialogViewModel? CreateExportMapDialogViewModel()
    {
        if (SelectedMapName is not { } name || FindMap(name) is not { } map || FindTileset(map.TilesetName) is not { } tileset)
            return null;

        return new ExportMapDialogViewModel(map, ToTileset(tileset));
    }

    // Either path may be null to skip that file. The tileset is read live, so edits not yet saved to the project are included.
    public void ExportMap(string? xmlPath, string? imagePath)
    {
        if (SelectedMapName is not { } name || FindMap(name) is not { } map || FindTileset(map.TilesetName) is not { } tileset)
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
}
