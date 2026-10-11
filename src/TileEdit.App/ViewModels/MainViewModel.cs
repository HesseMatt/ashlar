using System;
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

    private string? _sourceFileName;

    public bool HasImage => PreviewBitmap is not null;

    public void LoadTileset(string filePath)
    {
        TilesetImage image;
        try
        {
            image = TilesetImageLoader.Load(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        using var stream = new MemoryStream(image.Bytes);
        PreviewBitmap = new Bitmap(stream);
        ImageWidth = image.Width;
        ImageHeight = image.Height;

        _sourceFileName = Path.GetFileName(filePath);
        TexturePath = Path.GetFileNameWithoutExtension(filePath);

        var suggestion = TileSizeDetector.DetectCandidates(image.Width, image.Height).First();
        TileWidth = suggestion.TileWidth;
        TileHeight = suggestion.TileHeight;

        StatusMessage = $"{Path.GetFileName(filePath)} ({image.Width}x{image.Height}) - suggested tile size {TileWidth}x{TileHeight}.";
    }

    public void ExportXml(string outputPath)
    {
        if (_sourceFileName is null)
            return;

        var tiles = TilesetGridBuilder.Build(_sourceFileName, ImageWidth, ImageHeight, TileWidth, TileHeight);
        var tileset = new Tileset(TexturePath, TileWidth, TileHeight, tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            StatusMessage = $"Exported {tiles.Count} tiles to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }
}
