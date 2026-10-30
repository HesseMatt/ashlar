using CommunityToolkit.Mvvm.ComponentModel;

namespace Ashlar.App.ViewModels;

public partial class ResizeMapDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private int _width;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error), nameof(IsValid))]
    private int _height;

    public ResizeMapDialogViewModel(int currentWidth, int currentHeight)
    {
        _width = currentWidth;
        _height = currentHeight;
    }

    public string? Error =>
        Width < 1 || Height < 1 || Width > CreateMapDialogViewModel.MaxMapSize || Height > CreateMapDialogViewModel.MaxMapSize
            ? $"The map size must be between 1 and {CreateMapDialogViewModel.MaxMapSize} cells."
            : null;

    public bool IsValid => Error is null;
}
