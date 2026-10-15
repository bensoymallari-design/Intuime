using Avalonia;
using Intuime.Core;

namespace Intuime;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Any(arg => arg == "--self-check"))
            return SelfCheck.Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
        if (Environment.GetEnvironmentVariable("INTUIME_SOFTWARE") == "1")
        {
            builder = builder.With(new X11PlatformOptions
            {
                RenderingMode = new[] { X11RenderingMode.Software }
            });
        }
        return builder;
    }
}
