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
    private TileRegion? _selectedTile;

    private string? _sourceFileName;
    private EditableTilesetImage? _editableImage;

    public bool HasImage => PreviewBitmap is not null;

    public bool HasSelectedTile => SelectedTile is not null;

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

    public void SelectTileAt(int pixelX, int pixelY)
    {
        SelectedTile = Tiles.FirstOrDefault(tile =>
            pixelX >= tile.X && pixelX < tile.X + tile.Width &&
            pixelY >= tile.Y && pixelY < tile.Y + tile.Height);

        StatusMessage = SelectedTile is null
            ? "No tile selected."
            : $"Selected {SelectedTile.Name}.";
    }

    public void DeleteSelectedTile()
    {
        if (SelectedTile is not { } tile || _editableImage is null)
            return;

        _editableImage.ClearRegion(tile);
        Tiles.Remove(tile);
        SelectedTile = null;
        RefreshPreviewBitmap();

        StatusMessage = $"Deleted {tile.Name}. Use \"Save image...\" to write this change to disk.";
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

    private void RebuildGrid()
    {
        if (_sourceFileName is null)
            return;

        Tiles = new ObservableCollection<TileRegion>(
            TilesetGridBuilder.Build(_sourceFileName, ImageWidth, ImageHeight, TileWidth, TileHeight));
        SelectedTile = null;
    }

    private void RefreshPreviewBitmap()
    {
        if (_editableImage is null)
            return;

        using var stream = new MemoryStream(_editableImage.EncodeToPng());
        PreviewBitmap = new Bitmap(stream);
    }
}
