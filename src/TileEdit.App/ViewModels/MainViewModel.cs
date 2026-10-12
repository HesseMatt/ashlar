using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using TileEdit.Core;
using TileEdit.Core.Detection;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;
using TileEdit.Core.Serialization;

namespace TileEdit.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageWidth))]
    private int _imageWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageHeight))]
    private int _imageHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageWidth))]
    [NotifyPropertyChangedFor(nameof(ScaledImageHeight))]
    [NotifyPropertyChangedFor(nameof(ZoomPercentageText))]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private int _tileWidth = 16;

    [ObservableProperty]
    private int _tileHeight = 16;

    [ObservableProperty]
    private string _texturePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Open a tileset image to get started.";

    [ObservableProperty]
    private ObservableCollection<TileRegion> _tiles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTiles))]
    [NotifyPropertyChangedFor(nameof(CanToggleMove))]
    [NotifyPropertyChangedFor(nameof(SelectedTileId))]
    private IReadOnlyList<TileRegion> _selectedTiles = Array.Empty<TileRegion>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CanToggleMove))]
    private bool _isMovePending;

    [ObservableProperty]
    private ObservableCollection<string> _groupNames = new();

    [ObservableProperty]
    private string _groupNameInput = string.Empty;

    private const double ZoomStep = 0.1;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 8.0;

    private string? _sourceFileName;
    private EditableTilesetImage? _editableImage;
    private bool _isConfirmingGroupNameInput;

    public bool HasImage => PreviewBitmap is not null;

    public double ScaledImageWidth => ImageWidth * ZoomLevel;

    public double ScaledImageHeight => ImageHeight * ZoomLevel;

    public string ZoomPercentageText => $"{ZoomLevel:P0}";

    public bool HasSelectedTiles => SelectedTiles.Count > 0;

    public bool CanToggleMove => SelectedTiles.Count == 1 || IsMovePending;

    public string SelectedTileId => SelectedTiles.Count switch
    {
        0 => "No tile selected",
        1 => SelectedTiles[0].Name,
        _ => $"{SelectedTiles.Count} tiles selected",
    };

    private string DefaultGroupName =>
        _sourceFileName is null ? string.Empty : TilesetGridBuilder.SanitizeBaseName(Path.GetFileNameWithoutExtension(_sourceFileName));

    public string MoveButtonLabel => IsMovePending ? "Cancel move" : "Start move";

    partial void OnTileWidthChanged(int value) => RebuildGrid();

    partial void OnTileHeightChanged(int value) => RebuildGrid();

    public void LoadTileset(string filePath)
    {
        EditableTilesetImage image;
        try
        {
            image = EditableTilesetImage.Load(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        _editableImage?.Dispose();
        _editableImage = image;

        ImageWidth = image.Width;
        ImageHeight = image.Height;
        RefreshPreviewBitmap();

        _sourceFileName = Path.GetFileName(filePath);
        SelectedTiles = Array.Empty<TileRegion>();
        IsMovePending = false;
        ZoomLevel = 1.0;
        TexturePath = Path.GetFileNameWithoutExtension(filePath);

        var suggestion = TileSizeDetector.DetectCandidates(image.Width, image.Height).First();
        TileWidth = suggestion.TileWidth;
        TileHeight = suggestion.TileHeight;
        RebuildGrid();

        StatusMessage = $"{_sourceFileName} ({image.Width}x{image.Height}) - suggested tile size {TileWidth}x{TileHeight}.";
    }

    public void OpenProject(string filePath)
    {
        TilesetProject project;
        try
        {
            project = TilesetProjectFile.Load(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open project '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        EditableTilesetImage image;
        try
        {
            image = EditableTilesetImage.Load(project.ImagePng);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load the image stored in '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        _editableImage?.Dispose();
        _editableImage = image;

        ImageWidth = image.Width;
        ImageHeight = image.Height;
        RefreshPreviewBitmap();

        _sourceFileName = project.ImageFileName;
        SelectedTiles = Array.Empty<TileRegion>();
        IsMovePending = false;
        ZoomLevel = 1.0;
        TexturePath = project.TexturePath;
        TileWidth = project.TileWidth;
        TileHeight = project.TileHeight;
        Tiles = new ObservableCollection<TileRegion>(project.Tiles);
        RefreshGroupNames();

        StatusMessage = $"Resumed project {Path.GetFileName(filePath)} ({Tiles.Count} tiles).";
    }

    public void ExportXml(string outputPath)
    {
        if (_sourceFileName is null)
            return;

        var tileset = new Tileset(TexturePath, TileWidth, TileHeight, Tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            StatusMessage = $"Exported {Tiles.Count} tiles to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public double ZoomIn() => ZoomLevel = Math.Clamp(ZoomLevel + ZoomStep, MinZoom, MaxZoom);

    public double ZoomOut() => ZoomLevel = Math.Clamp(ZoomLevel - ZoomStep, MinZoom, MaxZoom);

    public void ResetZoom() => ZoomLevel = 1.0;

    public void HandleCanvasClick(int pixelX, int pixelY, bool addToSelection)
    {
        if (IsMovePending)
        {
            CompleteMove(pixelX, pixelY);
            return;
        }

        SelectTileAt(pixelX, pixelY, addToSelection);
    }

    public void DeleteSelectedTiles()
    {
        if (SelectedTiles.Count == 0 || _editableImage is null)
            return;

        IsMovePending = false;

        var deletedTiles = SelectedTiles;
        foreach (var tile in deletedTiles)
        {
            _editableImage.ClearRegion(tile);
            Tiles.Remove(tile);
        }

        SelectedTiles = Array.Empty<TileRegion>();
        RefreshPreviewBitmap();
        RefreshGroupNames();

        StatusMessage = deletedTiles.Count == 1
            ? $"Deleted {deletedTiles[0].Name}. Use \"Save image...\" to write this change to disk."
            : $"Deleted {deletedTiles.Count} tiles. Use \"Save image...\" to write this change to disk.";
    }

    public void ToggleMove()
    {
        if (IsMovePending)
        {
            IsMovePending = false;
            StatusMessage = "Move cancelled.";
            return;
        }

        if (SelectedTiles.Count != 1)
            return;

        IsMovePending = true;
        StatusMessage = $"Click a destination cell to move {SelectedTiles[0].Name}, or click \"Cancel move\" to abort.";
    }

    // Re-entrancy guard: replacing GroupNames below updates the group combo
    // box's ItemsSource, which Avalonia can answer by re-raising its
    // SelectionChanged event - without this guard that would call back into
    // this same method while it's still running.
    public void ConfirmGroupNameInput()
    {
        if (_isConfirmingGroupNameInput || SelectedTiles.Count == 0)
            return;

        _isConfirmingGroupNameInput = true;
        try
        {
            var targetGroup = TileGroupAssigner.ResolveTargetGroupName(GroupNameInput, DefaultGroupName);
            var selectedCount = SelectedTiles.Count;
            var selectedPositions = SelectedTiles.Select(t => (t.X, t.Y)).ToHashSet();

            Tiles = new ObservableCollection<TileRegion>(TileGroupAssigner.AssignGroup(Tiles, SelectedTiles, targetGroup));
            SelectedTiles = Tiles.Where(t => selectedPositions.Contains((t.X, t.Y))).ToList();
            RefreshGroupNames();

            var noun = selectedCount == 1 ? "tile" : "tiles";
            StatusMessage = targetGroup == DefaultGroupName
                ? $"Reverted {selectedCount} {noun} to the default naming scheme."
                : $"Assigned {selectedCount} {noun} to group \"{targetGroup}\".";
        }
        finally
        {
            _isConfirmingGroupNameInput = false;
        }
    }

    public void SaveImage(string outputPath)
    {
        if (_editableImage is null)
            return;

        try
        {
            _editableImage.SaveAsPng(outputPath);
            StatusMessage = $"Saved image to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save image to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public void SaveProject(string outputPath)
    {
        if (_sourceFileName is null || _editableImage is null)
            return;

        var project = new TilesetProject(_sourceFileName, _editableImage.EncodeToPng(), TexturePath, TileWidth, TileHeight, Tiles);

        try
        {
            TilesetProjectFile.Save(project, outputPath);
            StatusMessage = $"Saved project to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    private void SelectTileAt(int pixelX, int pixelY, bool addToSelection)
    {
        var tile = Tiles.FirstOrDefault(t =>
            pixelX >= t.X && pixelX < t.X + t.Width &&
            pixelY >= t.Y && pixelY < t.Y + t.Height);

        if (!addToSelection)
        {
            SelectedTiles = tile is null ? Array.Empty<TileRegion>() : [tile];
        }
        else if (tile is not null)
        {
            SelectedTiles = SelectedTiles.Contains(tile)
                ? SelectedTiles.Where(t => t != tile).ToList()
                : SelectedTiles.Append(tile).ToList();
        }

        StatusMessage = SelectedTiles.Count switch
        {
            0 => "No tile selected.",
            1 => $"Selected {SelectedTiles[0].Name}.",
            _ => $"Selected {SelectedTiles.Count} tiles.",
        };
    }

    private void CompleteMove(int pixelX, int pixelY)
    {
        IsMovePending = false;

        if (SelectedTiles.Count != 1 || _editableImage is null)
            return;

        var tile = SelectedTiles[0];

        if (pixelX < 0 || pixelY < 0 || pixelX >= ImageWidth || pixelY >= ImageHeight)
        {
            StatusMessage = "Move cancelled: destination is outside the image.";
            return;
        }

        var targetX = pixelX / TileWidth * TileWidth;
        var targetY = pixelY / TileHeight * TileHeight;

        if (targetX + TileWidth > ImageWidth || targetY + TileHeight > ImageHeight)
        {
            StatusMessage = "Move cancelled: destination is outside the tile grid.";
            return;
        }

        if (targetX == tile.X && targetY == tile.Y)
        {
            StatusMessage = "Move cancelled: that's the tile's current cell.";
            return;
        }

        _editableImage.MoveRegion(tile, targetX, targetY);

        var occupant = Tiles.FirstOrDefault(t => t.X == targetX && t.Y == targetY);
        var movedTile = tile with { X = targetX, Y = targetY };
        ReplaceTile(tile, movedTile);

        if (occupant is { } occupiedTile)
        {
            var displacedTile = occupiedTile with { X = tile.X, Y = tile.Y };
            ReplaceTile(occupiedTile, displacedTile);
            StatusMessage = $"Swapped {movedTile.Name} and {displacedTile.Name}.";
        }
        else
        {
            StatusMessage = $"Moved {movedTile.Name} to ({targetX}, {targetY}).";
        }

        SelectedTiles = [movedTile];
        RefreshPreviewBitmap();
    }

    private void ReplaceTile(TileRegion oldTile, TileRegion newTile)
    {
        var index = Tiles.IndexOf(oldTile);
        if (index >= 0)
            Tiles[index] = newTile;
    }

    private void RebuildGrid()
    {
        if (_sourceFileName is null)
            return;

        Tiles = new ObservableCollection<TileRegion>(
            TilesetGridBuilder.Build(_sourceFileName, ImageWidth, ImageHeight, TileWidth, TileHeight));
        SelectedTiles = Array.Empty<TileRegion>();
        IsMovePending = false;
        RefreshGroupNames();
    }

    private void RefreshGroupNames()
    {
        GroupNames = new ObservableCollection<string>(TileGroupAssigner.GetGroupNames(Tiles, DefaultGroupName));
    }

    partial void OnSelectedTilesChanged(IReadOnlyList<TileRegion> value)
    {
        if (value.Count == 0)
        {
            GroupNameInput = string.Empty;
            return;
        }

        var groupNames = value.Select(t => TileGroupAssigner.GetGroupName(t, DefaultGroupName)).Distinct().ToList();
        GroupNameInput = groupNames.Count == 1 ? groupNames[0] ?? string.Empty : string.Empty;
    }

    private void RefreshPreviewBitmap()
    {
        if (_editableImage is null)
            return;

        using var stream = new MemoryStream(_editableImage.EncodeToPng());
        PreviewBitmap = new Bitmap(stream);
    }
}
