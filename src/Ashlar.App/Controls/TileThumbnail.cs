using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Ashlar.Core.Model;

namespace Ashlar.App.Controls;

// One tile of a tileset image, scaled to fit the control while keeping its aspect ratio.
public class TileThumbnail : Control
{
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<TileThumbnail, IImage?>(nameof(Image));

    public static readonly StyledProperty<TileRegion?> RegionProperty =
        AvaloniaProperty.Register<TileThumbnail, TileRegion?>(nameof(Region));

    static TileThumbnail()
    {
        AffectsRender<TileThumbnail>(ImageProperty, RegionProperty);
    }

    public IImage? Image
    {
        get => GetValue(ImageProperty);
        set => SetValue(ImageProperty, value);
    }

    public TileRegion? Region
    {
        get => GetValue(RegionProperty);
        set => SetValue(RegionProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Image is not { } image || Region is not { } region)
            return;

        var scale = Math.Min(Bounds.Width / region.Width, Bounds.Height / region.Height);
        var width = region.Width * scale;
        var height = region.Height * scale;
        var destination = new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
        context.DrawImage(image, new Rect(region.X, region.Y, region.Width, region.Height), destination);
    }
}
