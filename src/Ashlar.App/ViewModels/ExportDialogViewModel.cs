using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.App.ViewModels;

public partial class ExportDialogViewModel : ObservableObject
{
    private const int PreviewRegionCount = 3;

    private readonly int _tileWidth;
    private readonly int _tileHeight;
    private readonly IReadOnlyList<TileRegion> _tiles;

    [ObservableProperty]
    private string _texturePath;

    public ExportDialogViewModel(string texturePath, int tileWidth, int tileHeight, IReadOnlyList<TileRegion> tiles)
    {
        _texturePath = texturePath;
        _tileWidth = tileWidth;
        _tileHeight = tileHeight;
        _tiles = tiles;
    }

    public string PreviewXml =>
        TextureAtlasXmlWriter.ToXmlString(new Tileset(TexturePath, _tileWidth, _tileHeight, _tiles), PreviewRegionCount);

    partial void OnTexturePathChanged(string value) => OnPropertyChanged(nameof(PreviewXml));
}
