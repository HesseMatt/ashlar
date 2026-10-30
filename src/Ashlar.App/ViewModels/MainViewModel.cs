using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Ashlar.Core;
using Ashlar.Core.Model;
using Ashlar.Core.Serialization;

namespace Ashlar.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private const string InitialStatusMessage = "Create a project or open a tileset image to get started.";

    [ObservableProperty]
    private string _texturePath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectEmpty))]
    private bool _isProjectOpen;

    public TilesetEditorViewModel Editor { get; } = new() { StatusMessage = InitialStatusMessage };

    public bool IsProjectEmpty => IsProjectOpen && !Editor.HasImage;

    public MainViewModel()
    {
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TilesetEditorViewModel.HasImage))
                OnPropertyChanged(nameof(IsProjectEmpty));
        };
    }

    // The file a plain Save writes to; null until the project was saved or opened.
    public string? ProjectPath { get; private set; }

    public string SuggestedProjectFileName =>
        Editor.SourceFileName is null ? "project" : Path.GetFileNameWithoutExtension(Editor.SourceFileName);

    // Reads an image for the tile size dialog without touching the open
    // tileset, so cancelling the import leaves everything as it was.
    public EditableTilesetImage? ReadImage(string filePath)
    {
        try
        {
            return EditableTilesetImage.Load(filePath);
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not open '{Path.GetFileName(filePath)}': {ex.Message}";
            return null;
        }
    }

    public void LoadTileset(string filePath, int tileWidth, int tileHeight)
    {
        var image = ReadImage(filePath);
        if (image is null)
            return;

        var fileName = Path.GetFileName(filePath);
        // Importing into an empty project adds its first tileset; otherwise it replaces the open one.
        if (Editor.HasImage)
            ProjectPath = null;
        Editor.LoadNewTileset(image, fileName, tileWidth, tileHeight);
        IsProjectOpen = true;
        TexturePath = Path.GetFileNameWithoutExtension(filePath);
        Editor.StatusMessage = $"{fileName} ({image.Width}x{image.Height}) - tile size {Editor.TileSizeText}.";
    }

    public void NewProject()
    {
        ResetProject(isOpen: true);
        Editor.StatusMessage = "New project. Import a tileset to add the first one.";
    }

    public void OpenProject(string filePath)
    {
        ProjectTileset? tileset;
        try
        {
            var project = ProjectFile.Load(filePath);
            tileset = project.Tilesets.Count switch
            {
                0 => null,
                1 => project.Tilesets[0],
                _ => throw new InvalidDataException($"Expected at most one tileset but found {project.Tilesets.Count}."),
            };
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not open project '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        if (tileset is null)
        {
            ResetProject(isOpen: true);
            ProjectPath = filePath;
            Editor.StatusMessage = $"Resumed project {Path.GetFileName(filePath)} (no tilesets).";
            return;
        }

        EditableTilesetImage image;
        try
        {
            image = EditableTilesetImage.Load(tileset.ImagePng);
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not load the image stored in '{Path.GetFileName(filePath)}': {ex.Message}";
            return;
        }

        Editor.LoadProject(tileset, image);
        IsProjectOpen = true;
        ProjectPath = filePath;
        TexturePath = tileset.TexturePath;
        Editor.StatusMessage = $"Resumed project {Path.GetFileName(filePath)} ({Editor.Tiles.Count} tiles).";
    }

    public void CloseProject()
    {
        ResetProject(isOpen: false);
        Editor.StatusMessage = InitialStatusMessage;
    }

    private void ResetProject(bool isOpen)
    {
        Editor.Close();
        ProjectPath = null;
        TexturePath = string.Empty;
        IsProjectOpen = isOpen;
    }

    public ExportDialogViewModel CreateExportDialogViewModel() =>
        new(TexturePath, Editor.TileWidth, Editor.TileHeight, Editor.Tiles);

    public void ExportXml(string outputPath)
    {
        if (Editor.SourceFileName is null)
            return;

        var tileset = new Tileset(TexturePath, Editor.TileWidth, Editor.TileHeight, Editor.Tiles);

        try
        {
            TextureAtlasXmlWriter.Write(tileset, outputPath);
            Editor.StatusMessage = $"Exported {Editor.Tiles.Count} tiles to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not export to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public void SaveImage(string outputPath)
    {
        if (Editor.Image is not { } image)
            return;

        try
        {
            image.SaveAsPng(outputPath);
            Editor.StatusMessage = $"Saved image to {Path.GetFileName(outputPath)}.";
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not save image to '{Path.GetFileName(outputPath)}': {ex.Message}";
        }
    }

    public bool SaveProject(string outputPath)
    {
        if (!IsProjectOpen)
            return false;

        try
        {
            ProjectFile.Save(BuildProject(), outputPath);
            ProjectPath = outputPath;
            Editor.HasUnsavedChanges = false;
            Editor.StatusMessage = $"Saved project to {Path.GetFileName(outputPath)}.";
            return true;
        }
        catch (Exception ex)
        {
            Editor.StatusMessage = $"Could not save project to '{Path.GetFileName(outputPath)}': {ex.Message}";
            return false;
        }
    }

    private Project BuildProject()
    {
        if (Editor.SourceFileName is not { } sourceFileName || Editor.Image is not { } image)
            return new Project([], []);

        var drawColor = Editor.DrawColor;
        var tileset = new ProjectTileset(Path.GetFileNameWithoutExtension(sourceFileName), sourceFileName, image.EncodeToPng(), TexturePath,
            Editor.TileWidth, Editor.TileHeight, Editor.Tiles, new PixelColor(drawColor.R, drawColor.G, drawColor.B, drawColor.A));
        return new Project([tileset], []);
    }
}
