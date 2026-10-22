using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(TileEdit.App.Tests.TestAppBuilder))]

namespace TileEdit.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TileEdit.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
