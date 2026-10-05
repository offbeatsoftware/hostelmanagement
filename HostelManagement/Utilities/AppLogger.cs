using System.Text;

namespace HostelManagement.Utilities;

/// <summary>
/// Minimal file logger. Technical details go to Logs/app-yyyyMMdd.log and are
/// never shown to the admin.
/// </summary>
public static class AppLogger
{
    private static readonly object SyncRoot = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            string file = Path.Combine(AppPaths.LogsFolder, $"app-{DateTime.Now:yyyyMMdd}.log");
            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";

            lock (SyncRoot)
            {
                Directory.CreateDirectory(AppPaths.LogsFolder);
                File.AppendAllText(file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the application.
        }
    }
}
