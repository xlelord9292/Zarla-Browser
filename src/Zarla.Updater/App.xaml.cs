using System.Windows;

namespace Zarla.Updater;

public partial class App : Application
{
    public static string? InstallerPath { get; private set; }
    public static int? BrowserProcessId { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Parse command line arguments
        // Usage: ZarlaUpdater.exe "path\to\installer.exe" [browser_pid]
        if (e.Args.Length >= 1)
        {
            InstallerPath = e.Args[0];
        }

        if (e.Args.Length >= 2 && int.TryParse(e.Args[1], out var pid))
        {
            BrowserProcessId = pid;
        }
    }
}
