namespace KaziWatts.Services;

/// <summary>Écrit toute erreur non gérée dans %TEMP%\kaziwatts_crash.log pour diagnostiquer un plantage au démarrage.</summary>
public static class CrashLog
{
    public static string PathFile => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kaziwatts_crash.log");

    public static void Write(string source, Exception? ex)
    {
        try
        {
            File.AppendAllText(PathFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { /* on ne plante jamais à cause du journal */ }
    }

    public static void Hook()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("AppDomain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => Write("Task", e.Exception);
    }
}
