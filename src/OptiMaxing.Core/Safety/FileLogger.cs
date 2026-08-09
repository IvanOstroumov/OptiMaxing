namespace OptiMaxing.Core.Safety;

public enum LogLevel { Info, Warning, Error }

public interface IAppLogger
{
    void Write(LogLevel level, string message, Exception? exception = null);
}

public sealed class FileLogger : IAppLogger
{
    private readonly object _gate = new();

    public void Write(LogLevel level, string message, Exception? exception = null)
    {
        var path = Path.Combine(AppPaths.Logs, $"{DateTime.Now:yyyy-MM-dd}.log");
        var line = $"{DateTimeOffset.Now:HH:mm:ss.fff} [{level}] {message}";
        if (exception is not null)
            line += Environment.NewLine + exception;

        lock (_gate)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never take the application down.
            }
        }
    }
}
