using System;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core;
using Ashlar.Core.Detection;

namespace Ashlar.App.ViewModels;

public partial class TileSizeDialogViewModel : ObservableObject
{
    public const int PreviewCellCount = 5;

    private const int BytesPerPixel = 4;

    private readonly EditableTilesetImage _image;

    [ObservableProperty]
    private int _tileWidth;

    [ObservableProperty]
    private int _tileHeight;

    [ObservableProperty]
    private Bitmap[] _previewCells = Array.Empty<Bitmap>();

    // The image is only read from, and must stay alive while the dialog is open.
    public TileSizeDialogViewModel(EditableTilesetImage image, int tileWidth, int tileHeight)
    {
        _image = image;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        UpdatePreview();
    }

    public static TileSizeDialogViewModel ForImport(EditableTilesetImage image)
    {
        var suggestion = TileSizeDetector.DetectCandidates(image.Width, image.Height).First();
        return new TileSizeDialogViewModel(image, suggestion.TileWidth, suggestion.TileHeight);
    }

    partial void OnTileWidthChanged(int value) => UpdatePreview();

    partial void OnTileHeightChanged(int value) => UpdatePreview();

    private unsafe void UpdatePreview()
    {
        var size = new PixelSize(TileWidth, TileHeight);
        var positions = TilesetGridBuilder.FirstCellPositions(_image.Width, _image.Height, TileWidth, TileHeight, PreviewCellCount);

        PreviewCells = positions
            .Select(position =>
            {
                var pixels = _image.CopyRegion(position.X, position.Y, TileWidth, TileHeight);
                fixed (byte* address = pixels)
                {
                    return new Bitmap(PixelFormat.Rgba8888, AlphaFormat.Unpremul, (IntPtr)address, size,
                        new Vector(96, 96), TileWidth * BytesPerPixel);
                }
            })
            .ToArray();
    }
}
