using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ashlar.App.ViewModels;

public record NewMapSettings(string Name, int Width, int Height, string TilesetName);

public partial class CreateMapDialogViewModel : ObservableObject
{
    // Keeps a typo from allocating an absurdly large map.
    public const int MaxMapSize = 1000;

    private readonly IReadOnlyCollection<string> _existingMapNames;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private int _width = 20;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private int _height = 15;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private string? _selectedTileset;

    public IReadOnlyList<string> TilesetNames { get; }

    public CreateMapDialogViewModel(string suggestedName, IReadOnlyList<string> tilesetNames,
        string? initialTileset, IReadOnlyCollection<string> existingMapNames)
    {
        _name = suggestedName;
        _selectedTileset = initialTileset;
        _existingMapNames = existingMapNames;
        TilesetNames = tilesetNames;
    }

    public string? Error
    {
        get
        {
            var name = Name.Trim();
            if (name.Length == 0)
                return "Enter a map name.";
            if (_existingMapNames.Contains(name))
                return $"A map named '{name}' already exists.";
            if (Width < 1 || Height < 1 || Width > MaxMapSize || Height > MaxMapSize)
                return $"The map size must be between 1 and {MaxMapSize} cells.";
            if (SelectedTileset is null)
                return "Choose a tileset.";
            return null;
        }
    }

    public bool IsValid => Error is null;

    public NewMapSettings ToSettings() => new(Name.Trim(), Width, Height, SelectedTileset!);
}
