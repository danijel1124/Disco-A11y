namespace ModDebugger;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // The mod passes its own game folder when it launches us (Ctrl+Y), so the tool never
        // asks a blind player to go hunting for a path they already gave the installer.
        var gamePath = args.Length > 0 && Directory.Exists(args[0]) ? args[0] : null;

        Application.Run(new MainForm(gamePath));
    }
}
