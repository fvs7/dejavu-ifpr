using System.Diagnostics;

namespace ReaFreeze.Core.Services;

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public class LoggerService
{
    private const string EventSourceName = "REA Freeze";
    private const string EventLogName = "Application";
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LoggerService()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string dir = Path.Combine(programData, "ReaFreeze", "logs");
        Directory.CreateDirectory(dir);
        _logFilePath = Path.Combine(dir, "reafreeze.log");
    }

    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        string fullMessage = ex == null ? message : $"{message} | Exceção: {ex.GetType().Name}: {ex.Message}";
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string line = $"[{timestamp}] [{level}] {fullMessage}";

        // 1. Arquivo de log com rotação básica (max 5MB)
        lock (_lock)
        {
            try
            {
                RotateLogFileIfNeeded();
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch
            {
                // Fallback silencioso para não interromper CLI
            }
        }

        // 2. Windows Event Log
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!EventLog.SourceExists(EventSourceName))
                {
                    EventLog.CreateEventSource(EventSourceName, EventLogName);
                }

                EventLogEntryType type = level switch
                {
                    LogLevel.Warning => EventLogEntryType.Warning,
                    LogLevel.Error => EventLogEntryType.Error,
                    _ => EventLogEntryType.Information
                };

                EventLog.WriteEntry(EventSourceName, fullMessage, type, 100);
            }
        }
        catch
        {
            // Escrita no EventLog pode falhar se não tiver privilégio administrativo elevado
        }
    }

    private void RotateLogFileIfNeeded()
    {
        if (File.Exists(_logFilePath))
        {
            var fi = new FileInfo(_logFilePath);
            if (fi.Length > 5 * 1024 * 1024) // 5MB
            {
                string oldPath = _logFilePath + ".old";
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(_logFilePath, oldPath);
            }
        }
    }
}
