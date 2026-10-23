using Ashlar.Core.Model;

namespace Ashlar.Core.Editing;

// Everything an edit can change: the pixels (the PNG also carries the canvas
// size) and the tile list. Selection is kept by tile name in EditStep because
// it is not part of the document.
public record TilesetState(byte[] ImagePng, IReadOnlyList<TileRegion> Tiles);

public record EditStep(
    string Description,
    TilesetState Before,
    TilesetState After,
    IReadOnlyList<string> SelectionBefore,
    IReadOnlyList<string> SelectionAfter);

// Linear history: recording a new step drops every step that was undone.
public class EditHistory
{
    private readonly List<EditStep> _steps = new();
    private int _appliedCount;

    public bool CanUndo => _appliedCount > 0;

    public bool CanRedo => _appliedCount < _steps.Count;

    public void Record(EditStep step)
    {
        _steps.RemoveRange(_appliedCount, _steps.Count - _appliedCount);
        _steps.Add(step);
        _appliedCount++;
    }

    public EditStep Undo()
    {
        if (!CanUndo)
            throw new InvalidOperationException("There is no step to undo.");

        _appliedCount--;
        return _steps[_appliedCount];
    }

    public EditStep Redo()
    {
        if (!CanRedo)
            throw new InvalidOperationException("There is no step to redo.");

        return _steps[_appliedCount++];
    }

    public void Clear()
    {
        _steps.Clear();
        _appliedCount = 0;
    }
}
