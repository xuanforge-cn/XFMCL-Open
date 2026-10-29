/**
 * Program.cs - 程序入口
 * 玹砺科技 XUANFORGE
 *
 * 本项目以 MIT 协议开源。
 */

using Avalonia;

namespace XfmclOpen.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
