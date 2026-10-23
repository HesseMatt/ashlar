using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Ashlar.App.ViewModels;
using Ashlar.App.Views;
using Ashlar.Core.Model;

namespace Ashlar.App.Tests;

public class ExportDialogTests
{
    private static ExportDialog CreateDialog(int tileCount)
    {
        var tiles = Enumerable.Range(0, tileCount).Select(i => new TileRegion($"t_{i:0000}", i * 16, 0, 16, 16)).ToList();
        return new ExportDialog(new ExportDialogViewModel("dungeon", 16, 16, tiles));
    }

    [AvaloniaFact]
    public void PreviewShowsTheTexturePathAndTheFirstThreeTiles()
    {
        var dialog = CreateDialog(tileCount: 5);
        dialog.Show();

        var preview = dialog.FindControl<TextBox>("PreviewBox")!;

        Assert.True(preview.IsReadOnly);
        Assert.Contains("<Texture>dungeon</Texture>", preview.Text);
        Assert.Contains("t_0002", preview.Text);
        Assert.DoesNotContain("t_0003", preview.Text);
        Assert.Contains("2 more regions", preview.Text);
    }

    [AvaloniaFact]
    public void PreviewUpdatesWhileTheTexturePathIsEdited()
    {
        var dialog = CreateDialog(tileCount: 2);
        dialog.Show();

        dialog.FindControl<TextBox>("TexturePathBox")!.Text = "atlas/dungeon";

        Assert.Contains("<Texture>atlas/dungeon</Texture>", dialog.FindControl<TextBox>("PreviewBox")!.Text);
    }

    [AvaloniaFact]
    public async Task OkReturnsTheEnteredTexturePath()
    {
        var dialog = CreateDialog(tileCount: 2);
        var owner = new Window();
        owner.Show();
        var result = dialog.ShowDialog<string?>(owner);
        dialog.FindControl<TextBox>("TexturePathBox")!.Text = "atlas/dungeon";

        dialog.FindControl<Button>("OkButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("atlas/dungeon", await result);
    }

    [AvaloniaFact]
    public async Task CancelReturnsNothing()
    {
        var dialog = CreateDialog(tileCount: 2);
        var owner = new Window();
        owner.Show();
        var result = dialog.ShowDialog<string?>(owner);
        dialog.FindControl<TextBox>("TexturePathBox")!.Text = "changed";

        dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Null(await result);
    }
}
