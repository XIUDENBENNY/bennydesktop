using System.Threading;
using DesktopAssistantLite.App.Shell;

namespace DesktopAssistantLite.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "DesktopAssistantLite.App.Singleton", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        using var app = new TrayApplicationContext();
        Application.Run(app);
    }
}
