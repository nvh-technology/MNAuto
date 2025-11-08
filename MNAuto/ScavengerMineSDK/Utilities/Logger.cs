using System;
using System.IO;
using System.Threading;

namespace ScavengerMineSDK.Utilities
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public static class Logger
    {
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "scavenger_mine.log");
        private static readonly object LockObject = new object();
        
        static Logger()
        {
            var logDir = Path.GetDirectoryName(LogFilePath);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }
        }

        public static void Debug(string message, string workerId = "")
        {
            Log(LogLevel.Debug, message, workerId);
        }

        public static void Info(string message, string workerId = "")
        {
            Log(LogLevel.Info, message, workerId);
        }

        public static void Warning(string message, string workerId = "")
        {
            Log(LogLevel.Warning, message, workerId);
        }

        public static void Error(string message, Exception? exception = null, string workerId = "")
        {
            var fullMessage = exception != null ? $"{message}: {exception.Message}" : message;
            Log(LogLevel.Error, fullMessage, workerId);
        }

        private static void Log(LogLevel level, string message, string workerId)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var workerPrefix = string.IsNullOrEmpty(workerId) ? "" : $"[{workerId}] ";
            var logEntry = $"[{timestamp}] [{level}] {workerPrefix}{message}";

            lock (LockObject)
            {
                try
                {
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Ignore logging errors
                }
            }

            // Also output to console for debugging
            Console.WriteLine(logEntry);
        }
    }
}