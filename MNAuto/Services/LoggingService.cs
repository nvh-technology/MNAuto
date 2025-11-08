using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace MNAuto.Services
{
    public class LoggingService
    {
        private readonly string _logFilePath;
        private readonly ConcurrentQueue<LogEntry> _logQueue;
        private readonly object _fileLock = new object();
        private bool _isWriting;

        public event Action<LogEntry>? NewLogEntry;

        public LoggingService()
        {
            // Lưu Logs cùng cấp với file thực thi: ./Logs/MNAuto_Log_yyyyMMdd.txt
            var baseDir = AppContext.BaseDirectory;
            var logsDir = Path.Combine(baseDir, "Logs");
            Directory.CreateDirectory(logsDir);
            
            var logFileName = $"MNAuto_Log_{DateTime.Now:yyyyMMdd}.txt";
            _logFilePath = Path.Combine(logsDir, logFileName);
            
            _logQueue = new ConcurrentQueue<LogEntry>();
            _isWriting = false;
            
            // Start background task to process log queue
            Task.Run(ProcessLogQueue);
        }

        public void LogInfo(string profileName, string message)
        {
            Log(profileName, LogLevel.Info, message);
        }

        public void LogWarning(string profileName, string message)
        {
            Log(profileName, LogLevel.Warning, message);
        }

        public void LogError(string profileName, string message)
        {
            Log(profileName, LogLevel.Error, message);
        }

        public void LogError(string profileName, string message, Exception ex)
        {
            Log(profileName, LogLevel.Error, $"{message}: {ex.Message}");
        }

        private void Log(string profileName, LogLevel level, string message)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.Now,
                ProfileName = profileName,
                Level = level,
                Message = message
            };

            _logQueue.Enqueue(logEntry);
            NewLogEntry?.Invoke(logEntry);
        }

        private async Task ProcessLogQueue()
        {
            while (true)
            {
                if (_logQueue.TryDequeue(out var logEntry))
                {
                    await WriteLogToFile(logEntry);
                }
                else
                {
                    await Task.Delay(100); // Wait a bit before checking again
                }
            }
        }

        private async Task WriteLogToFile(LogEntry logEntry)
        {
            if (_isWriting)
            {
                // If already writing, re-queue the entry
                _logQueue.Enqueue(logEntry);
                return;
            }

            lock (_fileLock)
            {
                if (_isWriting) return;
                _isWriting = true;
            }

            try
            {
                var logLine = FormatLogEntry(logEntry);
                File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // If we can't write to file, at least write to console
                Console.WriteLine($"Failed to write log to file: {ex.Message}");
                Console.WriteLine(FormatLogEntry(logEntry));
            }
            finally
            {
                lock (_fileLock)
                {
                    _isWriting = false;
                }
            }

            await Task.CompletedTask;
        }

        private string FormatLogEntry(LogEntry logEntry)
        {
            var levelString = logEntry.Level switch
            {
                LogLevel.Info => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                _ => "INFO"
            };

            return $"{logEntry.Timestamp:HH:mm:ss} [{logEntry.ProfileName}]: [{levelString}] {logEntry.Message}";
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string ProfileName { get; set; } = string.Empty;
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}