using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Ashlar.App.Tests.TestAppBuilder))]

namespace Ashlar.App.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Ashlar.App.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
