using Ashlar.Core.Editing;

namespace Ashlar.Core.Tests.Editing;

public class EditHistoryTests
{
    private static EditStep Step(string description) =>
        new(description, new TilesetState([], []), new TilesetState([], []), [], []);

    [Fact]
    public void NewHistoryCanNeitherUndoNorRedo()
    {
        var history = new EditHistory<EditStep>();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoWalksBackThroughStepsInReverseOrder()
    {
        var history = new EditHistory<EditStep>();
        history.Record(Step("first"));
        history.Record(Step("second"));

        Assert.Equal("second", history.Undo().Description);
        Assert.Equal("first", history.Undo().Description);
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void RedoReappliesUndoneStepsInOriginalOrder()
    {
        var history = new EditHistory<EditStep>();
        history.Record(Step("first"));
        history.Record(Step("second"));
        history.Undo();
        history.Undo();

        Assert.Equal("first", history.Redo().Description);
        Assert.Equal("second", history.Redo().Description);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void RecordingAfterUndoDiscardsTheUndoneSteps()
    {
        var history = new EditHistory<EditStep>();
        history.Record(Step("first"));
        history.Record(Step("second"));
        history.Undo();

        history.Record(Step("third"));

        Assert.False(history.CanRedo);
        Assert.Equal("third", history.Undo().Description);
        Assert.Equal("first", history.Undo().Description);
    }

    [Fact]
    public void ClearRemovesAppliedAndUndoneSteps()
    {
        var history = new EditHistory<EditStep>();
        history.Record(Step("first"));
        history.Record(Step("second"));
        history.Undo();

        history.Clear();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void UndoWithoutStepsThrows()
    {
        Assert.Throws<InvalidOperationException>(() => new EditHistory<EditStep>().Undo());
    }

    [Fact]
    public void RedoWithoutUndoneStepsThrows()
    {
        var history = new EditHistory<EditStep>();
        history.Record(Step("first"));

        Assert.Throws<InvalidOperationException>(() => history.Redo());
    }

    [Fact]
    public void HistoryWorksWithAnyStepType()
    {
        var history = new EditHistory<string>();
        history.Record("a");
        history.Record("b");

        Assert.Equal("b", history.Undo());
        Assert.Equal("b", history.Redo());
    }
}
