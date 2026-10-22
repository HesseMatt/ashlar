using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using TileEdit.Core;
using TileEdit.Core.Detection;
using TileEdit.Core.Editing;
using TileEdit.Core.Model;
using TileEdit.Core.Serialization;

namespace TileEdit.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private WriteableBitmap? _previewBitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageWidth))]
    private int _imageWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageHeight))]
    private int _imageHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaledImageWidth))]
    [NotifyPropertyChangedFor(nameof(ScaledImageHeight))]
    [NotifyPropertyChangedFor(nameof(ZoomPercentageText))]
    [NotifyPropertyChangedFor(nameof(PreviewInterpolation))]
    [NotifyPropertyChangedFor(nameof(ShowPixelGrid))]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private int _tileWidth = 16;

    [ObservableProperty]
    private int _tileHeight = 16;

    [ObservableProperty]
    private string _texturePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Open a tileset image to get started.";

    [ObservableProperty]
    private ObservableCollection<TileRegion> _tiles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedTiles))]
    [NotifyPropertyChangedFor(nameof(SelectedTileId))]
    private IReadOnlyList<TileRegion> _selectedTiles = Array.Empty<TileRegion>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditTileSize))]
    private bool _hasEdits;

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private ObservableCollection<string> _groupNames = new();

    [ObservableProperty]
    private string _groupNameInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRectangleToolActive))]
    [NotifyPropertyChangedFor(nameof(IsMoveToolActive))]
    [NotifyPropertyChangedFor(nameof(IsDrawToolActive))]
    private CanvasTool _activeTool;

    [ObservableProperty]
    private Color _drawColor = Colors.Black;

    [ObservableProperty]
    private Rect? _selectionRectangle;

    [ObservableProperty]
    private Vector? _moveOffset;

    public const double MinZoom = 0.25;
    public const double MaxZoom = 32.0;

    // From this zoom level on, source pixels are drawn as sharp squares; below
    // it the image is downscaled, where a smooth filter avoids aliasing.
    public const double CrispPixelsZoom = 1.0;

    public const double PixelGridZoom = 8.0;

    private const double ZoomStep = 0.1;
    private const int BytesPerPixel = 4;

    private string? _sourceFileName;
    private EditableTilesetImage? _editableImage;
    private bool _isConfirmingGroupNameInput;
    private SelectionDrag? _selectionDrag;
    private MoveDrag? _moveDrag;
    private DrawStroke? _drawStroke;
    private readonly EditHistory _history = new();
    private bool _startedWithEdits;

    private record SelectionDrag(int StartX, int StartY, IReadOnlyList<TileRegion> SelectionBefore, bool AddToSelection);

    private record MoveDrag(int StartCellX, int StartCellY);

    // Cells already checked for a missing tile are remembered so a stroke
    // doesn't search the tile list again for every pixel.
    private class DrawStroke(TilesetState before, IReadOnlyList<string> selectionBefore, int lastX, int lastY)
    {
        public TilesetState Before { get; } = before;
        public IReadOnlyList<string> SelectionBefore { get; } = selectionBefore;
        public int LastX { get; set; } = lastX;
        public int LastY { get; set; } = lastY;
        public bool PaintedAnything { get; set; }
        public HashSet<(int, int)> CheckedCells { get; } = new();
        public int CreatedTiles { get; set; }
    }

    public event EventHandler? TilesetLoaded;

    // The preview bitmap is updated in place, so its property doesn't change
    // and the views have to be told to draw it again.
    public event EventHandler? PreviewPixelsChanged;

    public bool HasImage => PreviewBitmap is not null;

    public double ScaledImageWidth => ImageWidth * ZoomLevel;

    public double ScaledImageHeight => ImageHeight * ZoomLevel;

    public BitmapInterpolationMode PreviewInterpolation =>
        ZoomLevel >= CrispPixelsZoom ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;

    public bool ShowPixelGrid => ZoomLevel >= PixelGridZoom;

    public string ZoomPercentageText => $"{ZoomLevel:P0}";

    public bool CanEditTileSize => !HasEdits;

    public bool HasSelectedTiles => SelectedTiles.Count > 0;

    public bool IsRectangleToolActive
    {
        get => ActiveTool == CanvasTool.Rectangle;
        set => SetToolActive(CanvasTool.Rectangle, value);
    }

    public bool IsMoveToolActive
    {
        get => ActiveTool == CanvasTool.Move;
        set => SetToolActive(CanvasTool.Move, value);
    }

    public bool IsDrawToolActive
    {
        get => ActiveTool == CanvasTool.Draw;
        set => SetToolActive(CanvasTool.Draw, value);
    }

    public string SelectedTileId => SelectedTiles.Count switch
    {
        0 => "No tile selected",
        1 => SelectedTiles[0].Name,
        _ => $"{SelectedTiles.Count} tiles selected",
    };

    private bool IsDragInProgress => _selectionDrag is not null || _moveDrag is not null || _drawStroke is not null;

    private string DefaultGroupName =>
        _sourceFileName is null ? string.Empty : TilesetGridBuilder.SanitizeBaseName(Path.GetFileNameWithoutExtension(_sourceFileName));

    partial void OnTileWidthChanged(int value) => RebuildGrid();

    partial void OnTileHeightChanged(int value) => RebuildGrid();

    public void LoadTileset(string filePath)
    {
        EditableTilesetImage image;
        try
        {
            image = EditableTilesetImage.Load(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        _editableImage?.Dispose();
        _editableImage = image;

        ImageWidth = image.Width;
        ImageHeight = image.Height;
        RefreshPreviewBitmap();

        _sourceFileName = Path.GetFileName(filePath);
        ResetHistory(startedWithEdits: false);
        SelectedTiles = Array.Empty<TileRegion>();
        EndSelectionDrag();
        EndMoveDrag();
        _drawStroke = null;
        ActiveTool = CanvasTool.Select;
        ZoomLevel = 1.0;
        TexturePath = Path.GetFileNameWithoutExtension(filePath);

        var suggestion = TileSizeDetector.DetectCandidates(image.Width, image.Height).First();
        TileWidth = suggestion.TileWidth;
        TileHeight = suggestion.TileHeight;
        RebuildGrid();

        StatusMessage = $"{_sourceFileName} ({image.Width}x{image.Height}) - suggested tile size {TileWidth}x{TileHeight}.";
        TilesetLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void OpenProject(string filePath)
    {
        TilesetProject project;
        try
        {
            project = TilesetProjectFile.Load(filePath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not open project '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        EditableTilesetImage image;
        try
        {
            image = EditableTilesetImage.Load(project.ImagePng);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load the image stored in '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        _editableImage?.Dispose();
        _editableImage = image;

        ImageWidth = image.Width;
        ImageHeight = image.Height;
        RefreshPreviewBitmap();

        _sourceFileName = project.ImageFileName;
        SelectedTiles = Array.Empty<TileRegion>();
        EndSelectionDrag();
        EndMoveDrag();
        _drawStroke = null;
        ActiveTool = CanvasTool.Select;
        ZoomLevel = 1.0;
        TexturePath = project.TexturePath;
        var projectColor = project.DrawColor ?? PixelColor.Black;
        DrawColor = Color.FromArgb(projectColor.A, projectColor.R, projectColor.G, projectColor.B);
        TileWidth = project.TileWidth;
        TileHeight = project.TileHeight;
        Tiles = new ObservableCollection<TileRegion>(project.Tiles);
        ResetHistory(startedWithEdits: !project.Tiles.SequenceEqual(
            TilesetGridBuilder.Build(project.ImageFileName, image, project.TileWidth, project.TileHeight)));
        RefreshGroupNames();

        StatusMessage = $"Resumed project {Path.GetFileName(filePath)} ({Tiles.Count} tiles).";
        TilesetLoaded?.Invoke(this, EventArgs.Empty);
    }

    public ExportDialogViewModel CreateExportDialogViewModel() =>
        new(TexturePath, TileWidth, TileHeight, Tiles);

    public void ExportXml(string outputPath)
    {
        if (_sourceFileName is null)
            return;

        var tileset = new Tileset(TexturePath, TileWidth, TileHeight, Tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            StatusMessage = $"Exported {Tiles.Count} tiles to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public double ZoomIn() => ZoomLevel = Math.Clamp(ZoomLevel + CurrentZoomStep(), MinZoom, MaxZoom);

    public double ZoomOut() => ZoomLevel = Math.Clamp(ZoomLevel - CurrentZoomStep(), MinZoom, MaxZoom);

    // A fixed step would take dozens of clicks to get from 100% to 3200%.
    private double CurrentZoomStep() => ZoomStep * Math.Max(1.0, ZoomLevel);

    public void ResetZoom() => ZoomLevel = 1.0;

    public void HandleCanvasClick(int pixelX, int pixelY, bool addToSelection)
    {
        SelectTileAt(pixelX, pixelY, addToSelection);
    }

    public void BeginSelectionDrag(int pixelX, int pixelY, bool addToSelection)
    {
        _selectionDrag = new SelectionDrag(pixelX, pixelY, SelectedTiles, addToSelection);
        UpdateSelectionDrag(pixelX, pixelY);
    }

    public void UpdateSelectionDrag(int pixelX, int pixelY)
    {
        if (_selectionDrag is not { } drag)
            return;

        var rectangle = new Rect(
            Math.Min(drag.StartX, pixelX), Math.Min(drag.StartY, pixelY),
            Math.Abs(pixelX - drag.StartX) + 1, Math.Abs(pixelY - drag.StartY) + 1);
        var visibleRectangle = rectangle.Intersect(new Rect(0, 0, ImageWidth, ImageHeight));
        if (SelectionRectangle == visibleRectangle)
            return;

        SelectionRectangle = visibleRectangle;

        var touched = TileRectangleSelector.TilesTouching(Tiles, drag.StartX, drag.StartY, pixelX, pixelY);
        var selection = drag.AddToSelection
            ? drag.SelectionBefore.Concat(touched.Except(drag.SelectionBefore)).ToList()
            : touched;
        if (selection.SequenceEqual(SelectedTiles))
            return;

        SelectedTiles = selection;
        UpdateSelectionStatus();
    }

    public void EndSelectionDrag()
    {
        _selectionDrag = null;
        SelectionRectangle = null;
    }

    public void CancelSelectionDrag()
    {
        if (_selectionDrag is not { } drag)
            return;

        SelectedTiles = drag.SelectionBefore;
        EndSelectionDrag();
        UpdateSelectionStatus();
    }

    public void DeleteSelectedTiles()
    {
        if (SelectedTiles.Count == 0 || _editableImage is null || _drawStroke is not null)
            return;

        EndSelectionDrag();
        EndMoveDrag();

        var before = CaptureState();
        var selectionBefore = SelectedNames();
        var deletedTiles = SelectedTiles;
        foreach (var tile in deletedTiles)
        {
            _editableImage.ClearRegion(tile);
            Tiles.Remove(tile);
        }

        SelectedTiles = Array.Empty<TileRegion>();
        RefreshPreviewBitmap(deletedTiles);
        RefreshGroupNames();
        RecordEdit($"Delete {deletedTiles.Count} {(deletedTiles.Count == 1 ? "tile" : "tiles")}", before, selectionBefore);

        StatusMessage = deletedTiles.Count == 1
            ? $"Deleted {deletedTiles[0].Name}. Use \"Save image...\" to write this change to disk."
            : $"Deleted {deletedTiles.Count} tiles. Use \"Save image...\" to write this change to disk.";
    }

    // Only a press on an already selected tile starts a move; anywhere else the
    // press is ignored so the selection stays as it is.
    public bool BeginMoveDrag(int pixelX, int pixelY)
    {
        if (!IsMoveToolActive || !SelectedTiles.Any(t => Contains(t, pixelX, pixelY)))
            return false;

        _moveDrag = new MoveDrag(FloorDiv(pixelX, TileWidth), FloorDiv(pixelY, TileHeight));
        return true;
    }

    // The offset is computed from cell indices in image-pixel space, so the
    // tiles snap to the grid at every zoom level.
    public void UpdateMoveDrag(int pixelX, int pixelY)
    {
        if (_moveDrag is not { } drag)
            return;

        var offsetX = (FloorDiv(pixelX, TileWidth) - drag.StartCellX) * TileWidth;
        var offsetY = (FloorDiv(pixelY, TileHeight) - drag.StartCellY) * TileHeight;
        MoveOffset = offsetX == 0 && offsetY == 0 ? null : new Vector(offsetX, offsetY);
    }

    public void CommitMoveDrag()
    {
        var offset = MoveOffset;
        EndMoveDrag();

        if (offset is not { } moveOffset || _editableImage is null)
            return;

        var offsetX = (int)moveOffset.X;
        var offsetY = (int)moveOffset.Y;
        var movingCount = SelectedTiles.Count;
        var before = CaptureState();
        var selectionBefore = SelectedNames();
        var result = TileMover.Move(Tiles, SelectedTiles, offsetX, offsetY, ImageWidth, ImageHeight);

        _editableImage.MoveRegions(SelectedTiles, offsetX, offsetY);
        Tiles = new ObservableCollection<TileRegion>(result.Tiles);
        SelectedTiles = result.Moved;
        RefreshPreviewBitmap();
        RefreshGroupNames();
        RecordEdit($"Move {movingCount} {(movingCount == 1 ? "tile" : "tiles")}", before, selectionBefore);

        var deletedOutside = movingCount - result.Moved.Count;
        StatusMessage = (result.Moved.Count, deletedOutside) switch
        {
            (0, _) => $"Moved {movingCount} {(movingCount == 1 ? "tile" : "tiles")} outside the tileset and deleted them.",
            (1, 0) => $"Moved {result.Moved[0].Name}.",
            (_, 0) => $"Moved {result.Moved.Count} tiles.",
            _ => $"Moved {result.Moved.Count} {(result.Moved.Count == 1 ? "tile" : "tiles")}; {deletedOutside} moved outside the tileset and {(deletedOutside == 1 ? "was" : "were")} deleted.",
        };
    }

    public void EndMoveDrag()
    {
        _moveDrag = null;
        MoveOffset = null;
    }

    public bool BeginStroke(int pixelX, int pixelY)
    {
        if (!IsDrawToolActive || _editableImage is null)
            return false;

        _drawStroke = new DrawStroke(CaptureState(), SelectedNames(), pixelX, pixelY);
        PaintPoints([(pixelX, pixelY)]);
        return true;
    }

    public void ContinueStroke(int pixelX, int pixelY)
    {
        if (_drawStroke is not { } stroke)
            return;

        // The first point is where the previous update stopped, already painted.
        PaintPoints(LineRasterizer.Line(stroke.LastX, stroke.LastY, pixelX, pixelY).Skip(1));
        stroke.LastX = pixelX;
        stroke.LastY = pixelY;
    }

    public void CommitStroke()
    {
        if (_drawStroke is not { } stroke)
            return;

        _drawStroke = null;
        if (!stroke.PaintedAnything)
            return;

        RecordEdit("Draw", stroke.Before, stroke.SelectionBefore);
        StatusMessage = stroke.CreatedTiles switch
        {
            0 => "Drew on the image. Use \"Save image...\" to write this change to disk.",
            1 => "Drew on the image and created 1 tile.",
            _ => $"Drew on the image and created {stroke.CreatedTiles} tiles.",
        };
    }

    public void CancelStroke()
    {
        if (_drawStroke is not { } stroke)
            return;

        _drawStroke = null;
        Restore(stroke.Before, stroke.SelectionBefore);
    }

    // Re-entrancy guard: replacing GroupNames below updates the group combo
    // box's ItemsSource, which Avalonia can answer by re-raising its
    // SelectionChanged event - without this guard that would call back into
    // this same method while it's still running.
    public void ConfirmGroupNameInput()
    {
        if (_isConfirmingGroupNameInput || SelectedTiles.Count == 0)
            return;

        _isConfirmingGroupNameInput = true;
        try
        {
            var targetGroup = TileGroupAssigner.ResolveTargetGroupName(GroupNameInput, DefaultGroupName);
            var selectedCount = SelectedTiles.Count;
            var selectedPositions = SelectedTiles.Select(t => (t.X, t.Y)).ToHashSet();
            var assigned = TileGroupAssigner.AssignGroup(Tiles, SelectedTiles, targetGroup);
            var changed = !assigned.SequenceEqual(Tiles);
            var before = changed ? CaptureState() : null;
            var selectionBefore = SelectedNames();

            Tiles = new ObservableCollection<TileRegion>(assigned);
            SelectedTiles = Tiles.Where(t => selectedPositions.Contains((t.X, t.Y))).ToList();
            RefreshGroupNames();

            var noun = selectedCount == 1 ? "tile" : "tiles";
            if (before is not null)
            {
                RecordEdit(
                    targetGroup == DefaultGroupName ? "Revert to default naming" : $"Assign to group \"{targetGroup}\"",
                    before, selectionBefore);
            }

            StatusMessage = targetGroup == DefaultGroupName
                ? $"Reverted {selectedCount} {noun} to the default naming scheme."
                : $"Assigned {selectedCount} {noun} to group \"{targetGroup}\".";
        }
        finally
        {
            _isConfirmingGroupNameInput = false;
        }
    }

    // Ignored mid-drag: the drag's own state (selection before, move origin)
    // would be stale after the document changed underneath it.
    public void Undo()
    {
        if (!_history.CanUndo || IsDragInProgress)
            return;

        var step = _history.Undo();
        Restore(step.Before, step.SelectionBefore);
        UpdateHistoryState();
        StatusMessage = $"Undid: {step.Description}.";
    }

    public void Redo()
    {
        if (!_history.CanRedo || IsDragInProgress)
            return;

        var step = _history.Redo();
        Restore(step.After, step.SelectionAfter);
        UpdateHistoryState();
        StatusMessage = $"Redid: {step.Description}.";
    }

    public void SaveImage(string outputPath)
    {
        if (_editableImage is null)
            return;

        try
        {
            _editableImage.SaveAsPng(outputPath);
            StatusMessage = $"Saved image to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save image to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public void SaveProject(string outputPath)
    {
        if (_sourceFileName is null || _editableImage is null)
            return;

        var project = new TilesetProject(_sourceFileName, _editableImage.EncodeToPng(), TexturePath, TileWidth, TileHeight, Tiles,
            new PixelColor(DrawColor.R, DrawColor.G, DrawColor.B, DrawColor.A));

        try
        {
            TilesetProjectFile.Save(project, outputPath);
            StatusMessage = $"Saved project to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    // Writes into the image and straight into the preview bitmap, so a stroke
    // never has to re-encode or re-copy the whole image.
    private unsafe void PaintPoints(IEnumerable<(int X, int Y)> points)
    {
        if (_drawStroke is not { } stroke || _editableImage is null || PreviewBitmap is null)
            return;

        var color = new PixelColor(DrawColor.R, DrawColor.G, DrawColor.B, DrawColor.A);
        using (var framebuffer = PreviewBitmap.Lock())
        {
            foreach (var (x, y) in points)
            {
                if (!_editableImage.SetPixel(x, y, color))
                    continue;

                stroke.PaintedAnything = true;
                CopyPixels(framebuffer, x, y, 1, 1);
                if (color.A > 0)
                    CreateTileAt(stroke, x, y);
            }
        }

        PreviewPixelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CreateTileAt(DrawStroke stroke, int pixelX, int pixelY)
    {
        if (!stroke.CheckedCells.Add((FloorDiv(pixelX, TileWidth), FloorDiv(pixelY, TileHeight))))
            return;

        var tile = DrawnTileCreator.CreateTile(Tiles, pixelX, pixelY, TileWidth, TileHeight, ImageWidth, ImageHeight, DefaultGroupName);
        if (tile is null)
            return;

        Tiles.Add(tile);
        stroke.CreatedTiles++;
    }

    private TilesetState CaptureState() => new(_editableImage!.EncodeToPng(), Tiles.ToList());

    private IReadOnlyList<string> SelectedNames() => SelectedTiles.Select(t => t.Name).ToList();

    private void RecordEdit(string description, TilesetState before, IReadOnlyList<string> selectionBefore)
    {
        _history.Record(new EditStep(description, before, CaptureState(), selectionBefore, SelectedNames()));
        UpdateHistoryState();
    }

    private void ResetHistory(bool startedWithEdits)
    {
        _startedWithEdits = startedWithEdits;
        _history.Clear();
        UpdateHistoryState();
    }

    // A tileset opened with edits stays locked even with every recorded step
    // undone, because its initial state is itself an edited one.
    private void UpdateHistoryState()
    {
        CanUndo = _history.CanUndo;
        CanRedo = _history.CanRedo;
        HasEdits = _startedWithEdits || _history.CanUndo;
    }

    private void Restore(TilesetState state, IReadOnlyList<string> selectedNames)
    {
        var image = EditableTilesetImage.Load(state.ImagePng);
        _editableImage?.Dispose();
        _editableImage = image;
        ImageWidth = image.Width;
        ImageHeight = image.Height;
        RefreshPreviewBitmap();

        // Same guard as in ConfirmGroupNameInput: restoring the selection and
        // group names must not be mistaken for the user picking a group.
        _isConfirmingGroupNameInput = true;
        try
        {
            Tiles = new ObservableCollection<TileRegion>(state.Tiles);
            SelectedTiles = Tiles.Where(t => selectedNames.Contains(t.Name)).ToList();
            RefreshGroupNames();
        }
        finally
        {
            _isConfirmingGroupNameInput = false;
        }
    }

    private void SelectTileAt(int pixelX, int pixelY, bool addToSelection)
    {
        var tile = Tiles.FirstOrDefault(t => Contains(t, pixelX, pixelY));

        if (!addToSelection)
        {
            SelectedTiles = tile is null ? Array.Empty<TileRegion>() : [tile];
        }
        else if (tile is not null)
        {
            SelectedTiles = SelectedTiles.Contains(tile)
                ? SelectedTiles.Where(t => t != tile).ToList()
                : SelectedTiles.Append(tile).ToList();
        }

        UpdateSelectionStatus();
    }

    private void UpdateSelectionStatus()
    {
        StatusMessage = SelectedTiles.Count switch
        {
            0 => "No tile selected.",
            1 => $"Selected {SelectedTiles[0].Name}.",
            _ => $"Selected {SelectedTiles.Count} tiles.",
        };
    }

    private void SetToolActive(CanvasTool tool, bool active)
    {
        if (active)
            ActiveTool = tool;
        else if (ActiveTool == tool)
            ActiveTool = CanvasTool.Select;
    }

    private static bool Contains(TileRegion tile, int pixelX, int pixelY) =>
        pixelX >= tile.X && pixelX < tile.X + tile.Width && pixelY >= tile.Y && pixelY < tile.Y + tile.Height;

    // Pixel coordinates can be slightly negative (the canvas is padded), where
    // plain integer division would round toward zero instead of down.
    private static int FloorDiv(int value, int divisor) => (int)Math.Floor((double)value / divisor);

    private void RebuildGrid()
    {
        if (_sourceFileName is null || _editableImage is null)
            return;

        Tiles = new ObservableCollection<TileRegion>(
            TilesetGridBuilder.Build(_sourceFileName, _editableImage, TileWidth, TileHeight));
        SelectedTiles = Array.Empty<TileRegion>();
        EndSelectionDrag();
        EndMoveDrag();
        RefreshGroupNames();
        _history.Clear();
        UpdateHistoryState();
    }

    private void RefreshGroupNames()
    {
        GroupNames = new ObservableCollection<string>(TileGroupAssigner.GetGroupNames(Tiles, DefaultGroupName));
    }

    partial void OnSelectedTilesChanged(IReadOnlyList<TileRegion> value)
    {
        if (value.Count == 0)
        {
            GroupNameInput = string.Empty;
            return;
        }

        var groupNames = value.Select(t => TileGroupAssigner.GetGroupName(t, DefaultGroupName)).Distinct().ToList();
        GroupNameInput = groupNames.Count == 1 ? groupNames[0] ?? string.Empty : string.Empty;
    }

    // Without changed regions the whole image is copied. A new bitmap is only
    // created when the image size differs from the current one.
    private void RefreshPreviewBitmap(IReadOnlyList<TileRegion>? changedRegions = null)
    {
        if (_editableImage is null)
            return;

        var size = new PixelSize(_editableImage.Width, _editableImage.Height);
        if (PreviewBitmap is null || PreviewBitmap.PixelSize != size)
        {
            PreviewBitmap = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            changedRegions = null;
        }

        using var framebuffer = PreviewBitmap.Lock();
        if (changedRegions is null)
        {
            CopyPixels(framebuffer, 0, 0, size.Width, size.Height);
        }
        else
        {
            foreach (var region in changedRegions)
                CopyPixels(framebuffer, region.X, region.Y, region.Width, region.Height);
        }

        PreviewPixelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private unsafe void CopyPixels(ILockedFramebuffer framebuffer, int x, int y, int width, int height)
    {
        var source = _editableImage!.GetPixels();
        var rowByteWidth = width * BytesPerPixel;

        for (var row = y; row < y + height; row++)
        {
            var sourceRow = source.Slice(row * _editableImage.RowBytes + x * BytesPerPixel, rowByteWidth);
            var targetRow = new Span<byte>((byte*)framebuffer.Address + row * framebuffer.RowBytes + x * BytesPerPixel, rowByteWidth);
            sourceRow.CopyTo(targetRow);
        }
    }
}
