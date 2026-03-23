using DesktopAssistantLite.App.Shell;

namespace DesktopAssistantLite.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var app = new TrayApplicationContext();
        Application.Run(app);
    }
}
