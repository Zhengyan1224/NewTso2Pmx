using System.Text;
using Avalonia;
using NewTso2Pmx.Core.Infrastructure;

namespace NewTso2Pmx.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        LegacyPaths.BaseDirectory = AppContext.BaseDirectory;

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
