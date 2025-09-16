using System;
using System.IO;

namespace NugetDownloader
{
    public class AppLogger
    {
        private readonly FileInfo? _logFile;
        private readonly object _logLock = new object();

        public AppLogger(FileInfo? logFile)
        {
            _logFile = logFile;
            // Clear the log file on a new run if it's specified
            if (_logFile != null && File.Exists(_logFile.FullName))
            {
                File.Delete(_logFile.FullName);
            }
        }

        public void Log(string message, ConsoleColor? color = null)
        {
            lock (_logLock)
            {
                if (color.HasValue)
                {
                    Console.ForegroundColor = color.Value;
                    Console.WriteLine(message);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(message);
                }

                if (_logFile != null)
                {
                    string logMessage = "[" + DateTime.UtcNow.ToString("O") + "] " + message + "\n";
                    File.AppendAllText(_logFile.FullName, logMessage);
                }
            }
        }
    }
}
