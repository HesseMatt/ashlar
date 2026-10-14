using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TileEdit.App.Controls;

public class TransparencyCheckerboard : Control
{
    public static readonly StyledProperty<IBrush?> LightBrushProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, IBrush?>(nameof(LightBrush));

    public static readonly StyledProperty<IBrush?> DarkBrushProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, IBrush?>(nameof(DarkBrush));

    public static readonly StyledProperty<int> ImageWidthProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, int>(nameof(ImageWidth));

    public static readonly StyledProperty<int> ImageHeightProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, int>(nameof(ImageHeight));

    public static readonly StyledProperty<int> TileWidthProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, int>(nameof(TileWidth));

    public static readonly StyledProperty<int> TileHeightProperty =
        AvaloniaProperty.Register<TransparencyCheckerboard, int>(nameof(TileHeight));

    static TransparencyCheckerboard()
    {
        AffectsRender<TransparencyCheckerboard>(
            ImageWidthProperty, ImageHeightProperty, TileWidthProperty, TileHeightProperty,
            LightBrushProperty, DarkBrushProperty);
    }

    public int ImageWidth
    {
        get => GetValue(ImageWidthProperty);
        set => SetValue(ImageWidthProperty, value);
    }

    public int ImageHeight
    {
        get => GetValue(ImageHeightProperty);
        set => SetValue(ImageHeightProperty, value);
    }

    public int TileWidth
    {
        get => GetValue(TileWidthProperty);
        set => SetValue(TileWidthProperty, value);
    }

    public int TileHeight
    {
        get => GetValue(TileHeightProperty);
        set => SetValue(TileHeightProperty, value);
    }

    public IBrush? LightBrush
    {
        get => GetValue(LightBrushProperty);
        set => SetValue(LightBrushProperty, value);
    }

    public IBrush? DarkBrush
    {
        get => GetValue(DarkBrushProperty);
        set => SetValue(DarkBrushProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (TileWidth <= 0 || TileHeight <= 0)
            return;

        context.DrawRectangle(LightBrush, null, new Rect(0, 0, ImageWidth, ImageHeight));

        for (var row = 0; row * TileHeight < ImageHeight; row++)
        {
            for (var column = 0; column * TileWidth < ImageWidth; column++)
            {
                if ((row + column) % 2 == 0)
                    continue;

                var x = column * TileWidth;
                var y = row * TileHeight;
                var width = Math.Min(TileWidth, ImageWidth - x);
                var height = Math.Min(TileHeight, ImageHeight - y);
                context.DrawRectangle(DarkBrush, null, new Rect(x, y, width, height));
            }
        }
    }
}
