using System.IO;

namespace PrayingTime.Services;

public static class Logger
{
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "praying-time.log");
    private static readonly object _sync = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex == null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}";
        try
        {
            lock (_sync)
            {
                var info = new FileInfo(LogPath);
                if (info.Exists && info.Length > 1_000_000)
                    File.WriteAllText(LogPath, line);
                else
                    File.AppendAllText(LogPath, line);
            }
        }
        catch { }
    }
}
