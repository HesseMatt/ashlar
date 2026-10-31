using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ashlar.App.ViewModels;

public enum ExplorerNodeKind
{
    TilesetGroup,
    MapGroup,
    Tileset,
    Map,
}

// One line of the project explorer: a group header or an element of the project.
public partial class ExplorerNode : ViewModelBase
{
    public ExplorerNode(ExplorerNodeKind kind, string name)
    {
        Kind = kind;
        _name = name;
    }

    public ExplorerNodeKind Kind { get; }

    public bool IsGroup => Kind is ExplorerNodeKind.TilesetGroup or ExplorerNodeKind.MapGroup;

    public bool IsTileset => Kind == ExplorerNodeKind.Tileset;

    public bool IsMap => Kind == ExplorerNodeKind.Map;

    public bool IsTilesetGroup => Kind == ExplorerNodeKind.TilesetGroup;

    public bool IsMapGroup => Kind == ExplorerNodeKind.MapGroup;

    public ObservableCollection<ExplorerNode> Children { get; } = new();

    [ObservableProperty]
    private string _name;

    // The text being typed while the entry is renamed inline.
    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private bool _isRenaming;

    public void BeginRename()
    {
        if (IsGroup)
            return;

        RenameText = Name;
        IsRenaming = true;
    }
}
