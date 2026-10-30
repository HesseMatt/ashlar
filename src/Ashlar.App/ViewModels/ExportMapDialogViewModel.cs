using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.App.ViewModels;

public partial class ExportMapDialogViewModel : ObservableObject
{
    public ExportMapDialogViewModel(TileMap map, Tileset tileset)
    {
        MapName = map.Name;
        PreviewXml = TilemapXmlWriter.ToXmlString(map, tileset);
    }

    public string MapName { get; }

    public string PreviewXml { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _exportXml = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    private bool _exportImage = true;

    public bool CanExport => ExportXml || ExportImage;
}
