using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using TileEdit.Core;
using TileEdit.Core.Detection;
using TileEdit.Core.Model;
using TileEdit.Core.Serialization;

namespace TileEdit.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private Bitmap? _previewBitmap;

    [ObservableProperty]
    private int _imageWidth;

    [ObservableProperty]
    private int _imageHeight;

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
    [NotifyPropertyChangedFor(nameof(HasSelectedTile))]
    [NotifyPropertyChangedFor(nameof(CanToggleMove))]
    private TileRegion? _selectedTile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MoveButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CanToggleMove))]
    private bool _isMovePending;

    private string? _sourceFileName;
    private EditableTilesetImage? _editableImage;

    public bool HasImage => PreviewBitmap is not null;

    public bool HasSelectedTile => SelectedTile is not null;

    public bool CanToggleMove => HasSelectedTile || IsMovePending;

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
        TexturePath = Path.GetFileNameWithoutExtension(filePath);

        var suggestion = TileSizeDetector.DetectCandidates(image.Width, image.Height).First();
        TileWidth = suggestion.TileWidth;
        TileHeight = suggestion.TileHeight;
        RebuildGrid();

        StatusMessage = $"{Path.GetFileName(filePath)} ({image.Width}x{image.Height}) - suggested tile size {TileWidth}x{TileHeight}.";
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

    public void HandleCanvasClick(int pixelX, int pixelY)
    {
        if (IsMovePending)
            CompleteMove(pixelX, pixelY);
        else
            SelectTileAt(pixelX, pixelY);
    }

    public void DeleteSelectedTile()
    {
        if (SelectedTile is not { } tile || _editableImage is null)
            return;

        IsMovePending = false;
        _editableImage.ClearRegion(tile);
        Tiles.Remove(tile);
        SelectedTile = null;
        RefreshPreviewBitmap();

        StatusMessage = $"Deleted {tile.Name}. Use \"Save image...\" to write this change to disk.";
    }

    public void ToggleMove()
    {
        if (IsMovePending)
        {
            IsMovePending = false;
            StatusMessage = "Move cancelled.";
            return;
        }

        if (SelectedTile is not { } tile)
            return;

        IsMovePending = true;
        StatusMessage = $"Click a destination cell to move {tile.Name}, or click \"Cancel move\" to abort.";
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

    private void SelectTileAt(int pixelX, int pixelY)
    {
        SelectedTile = Tiles.FirstOrDefault(tile =>
            pixelX >= tile.X && pixelX < tile.X + tile.Width &&
            pixelY >= tile.Y && pixelY < tile.Y + tile.Height);

        StatusMessage = SelectedTile is null
            ? "No tile selected."
            : $"Selected {SelectedTile.Name}.";
    }

    private void CompleteMove(int pixelX, int pixelY)
    {
        IsMovePending = false;

        if (SelectedTile is not { } tile || _editableImage is null)
            return;

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

        SelectedTile = movedTile;
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
        SelectedTile = null;
        IsMovePending = false;
    }

    private void RefreshPreviewBitmap()
    {
        if (_editableImage is null)
            return;

        using var stream = new MemoryStream(_editableImage.EncodeToPng());
        PreviewBitmap = new Bitmap(stream);
    }
}
