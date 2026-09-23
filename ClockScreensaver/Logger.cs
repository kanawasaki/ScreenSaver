namespace ClockScreensaver;

public static class Logger
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockScreensaver");
    public static readonly string LogFile = Path.Combine(LogDir, "log.txt");
    private static readonly object _lock = new();
    private const long MaxBytes = 1_000_000;

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(LogDir);
                if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxBytes)
                    File.Delete(LogFile);
                File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch { }
    }
}
