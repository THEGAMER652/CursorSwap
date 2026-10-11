namespace CursorSwap;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "CursorSwap_SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("CursorSwap is already running (check the system tray).", "CursorSwap");
            return;
        }

        // Always give the user their normal cursor back, even if something crashes
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CursorManager.ForceRestore();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => CursorManager.ForceRestore();
        Application.ThreadException += (_, e) =>
        {
            CursorManager.ForceRestore();
            MessageBox.Show(e.Exception.Message, "CursorSwap error");
        };

        ApplicationConfiguration.Initialize();
        bool tray = args.Any(a => a.Equals("--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new MainForm(tray));
    }
}
