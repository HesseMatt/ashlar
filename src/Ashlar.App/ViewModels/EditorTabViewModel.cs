using CommunityToolkit.Mvvm.ComponentModel;

namespace Ashlar.App.ViewModels;

// One open element of the project. Every tab owns its own editor state: zoom,
// selection, tool and undo history.
public abstract partial class EditorTabViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    protected EditorTabViewModel(string title)
    {
        _title = title;
    }

    public abstract void Undo();

    public abstract void Redo();

    public abstract double ZoomIn();

    public abstract double ZoomOut();

    public abstract void ResetZoom();
}
