using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace devotter.Models
{
    public class Logger : IDisposable
    {
        private static Logger? _instance;
        private static readonly object _instanceLock = new object();
        
        private readonly string _logFilePath;
        private readonly bool _enableFileLogging;
        private readonly ConcurrentQueue<LogEntry> _messageQueue = new ConcurrentQueue<LogEntry>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private Task? _processingTask;
        private bool _isDisposed;
        
        // Event to notify subscribers of new log entries
        private EventHandler<LogEntry>? _logEntryAdded;
        public event EventHandler<LogEntry> LogEntryAdded
        {
            add
            {
                lock (_instanceLock)
                {
                    _logEntryAdded += value;
                }
            }
            remove
            {
                lock (_instanceLock)
                {
                    _logEntryAdded -= value;
                }
            }
        }
        
        private Logger(string logFilePath, bool enableFileLogging)
        {
            _logFilePath = logFilePath;
            _enableFileLogging = enableFileLogging;
            
            // Ensure the log directory exists
            if (_enableFileLogging && !string.IsNullOrEmpty(_logFilePath))
            {
                var logDir = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
            }
            
            // Start the processing task
            _processingTask = Task.Run(ProcessLogQueue);
        }
        
        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        // Create paths step by step to avoid nesting issues
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string logDir = Path.Combine(appDataPath, "devotter", "logs");
                        string logFile = Path.Combine(logDir, $"devotter_log_{DateTime.Now:yyyy-MM-dd}.log");
                        
                        _instance ??= new Logger(logFile, true);
                    }
                }
                
                return _instance;
            }
        }
        
        public static void Initialize(string logFilePath, bool enableFileLogging)
        {
            lock (_instanceLock)
            {
                // Dispose old instance if it exists
                var oldInstance = _instance;
                _instance = null;  // Clear before disposal to avoid recursion
                
                if (oldInstance != null)
                {
                    try
                    {
                        oldInstance.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing logger instance: {ex.Message}");
                    }
                }
                
                // Validate log file path if logging is enabled
                if (enableFileLogging && string.IsNullOrEmpty(logFilePath))
                {
                    // Use default log file if none specified
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string logDir = Path.Combine(appDataPath, "devotter", "logs");
                    Directory.CreateDirectory(logDir);
                    string safeDate = DateTime.Now.ToString("yyyy-MM-dd");
                    logFilePath = Path.Combine(logDir, $"devotter_log_{safeDate}.log");
                }
                
                _instance = new Logger(logFilePath, enableFileLogging);
            }
        }
        
        public void Log(LogLevel level, string message)
        {
            if (_isDisposed)
            {
                System.Diagnostics.Debug.WriteLine("Warning: Attempted to log after logger was disposed");
                return;
            }
            
            LogEntry entry = new LogEntry(DateTime.Now, level, message);
            _messageQueue.Enqueue(entry);
            
            // Trigger event for UI subscribers (thread-safe copy of delegate)
            var handler = _logEntryAdded;
            handler?.Invoke(this, entry);
        }
        
        public void LogInfo(string message) => Log(LogLevel.Info, message);
        public void LogWarning(string message) => Log(LogLevel.Warning, message);
        public void LogError(string message) => Log(LogLevel.Error, message);
        public void LogDebug(string message) => Log(LogLevel.Debug, message);
        
        private async Task ProcessLogQueue()
        {
            try
            {
                while (!_cancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        // Process all current messages
                        int count = _messageQueue.Count;
                        if (count == 0)
                        {
                            // No messages to process, wait for more
                            try
                            {
                                await Task.Delay(100, _cancellation.Token);
                            }
                            catch (TaskCanceledException)
                            {
                                // Expected when cancellation is requested
                                break;
                            }
                            continue;
                        }
                            
                        List<LogEntry> entries = new List<LogEntry>();
                        
                        for (int i = 0; i < count; i++)
                        {
                            if (_messageQueue.TryDequeue(out LogEntry entry))
                            {
                                entries.Add(entry);
                            }
                        }
                        
                        // Write entries to file
                        if (_enableFileLogging && entries.Count > 0 && !string.IsNullOrEmpty(_logFilePath))
                        {
                            try
                            {
                                // Ensure directory exists before writing
                                string directory = Path.GetDirectoryName(_logFilePath);
                                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                                {
                                    Directory.CreateDirectory(directory);
                                }
                            
                                using (StreamWriter writer = new StreamWriter(_logFilePath, true))
                                {
                                    foreach (var entry in entries)
                                    {
                                        await writer.WriteLineAsync(entry.ToString());
                                    }
                                    await writer.FlushAsync();
                                }
                            }
                            catch (IOException ioEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error writing to log file: {ioEx.Message}");
                                // Don't throw to keep the loop running
                            }
                            catch (UnauthorizedAccessException uaEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Access denied to log file: {uaEx.Message}");
                                // Don't throw to keep the loop running
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Unexpected error writing to log file: {ex.Message}");
                                // Don't throw to keep the loop running
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch any other exceptions to keep the loop running
                        System.Diagnostics.Debug.WriteLine($"Error in log processing loop: {ex.Message}");
                        
                        // Brief pause to avoid tight loop in case of persistent errors
                        try
                        {
                            await Task.Delay(500, _cancellation.Token);
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Last-resort exception handler
                System.Diagnostics.Debug.WriteLine($"Fatal error in logger thread: {ex.Message}");
            }
        }
        
        public void Shutdown()
        {
            if (_isDisposed)
            {
                return;
            }

            Dispose(true);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                try
                {
                    // Cancel the token
                    if (!_cancellation.IsCancellationRequested)
                    {
                        _cancellation.Cancel();
                    }
                    
                    try
                    {
                        // Use a separate CancellationToken to avoid deadlocks
                        if (_processingTask != null && !_processingTask.Wait(2000, CancellationToken.None))
                        {
                            System.Diagnostics.Debug.WriteLine("Warning: Logger task didn't complete within timeout.");
                        }
                    }
                    catch (Exception ex) 
                    { 
                        System.Diagnostics.Debug.WriteLine($"Error waiting for logger task: {ex.Message}");
                    }
                    
                    // Process any remaining messages synchronously
                    int remainingCount = _messageQueue.Count;
                    if (remainingCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Processing {remainingCount} remaining log entries during shutdown");
                        
                        // Only process if logging is enabled and path is valid
                        if (_enableFileLogging && !string.IsNullOrEmpty(_logFilePath))
                        {
                            try
                            {
                                using (StreamWriter writer = new StreamWriter(_logFilePath, true))
                                {
                                    while (_messageQueue.TryDequeue(out LogEntry entry))
                                    {
                                        writer.WriteLine(entry.ToString());
                                    }
                                    writer.Flush();
                                }
                            }
                            catch (Exception ex) 
                            { 
                                System.Diagnostics.Debug.WriteLine($"Error writing remaining logs during shutdown: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Just clear the queue if we can't log
                            while (_messageQueue.TryDequeue(out _)) { }
                        }
                    }
                }
                finally
                {
                    // Dispose of the cancellation token source
                    try
                    {
                        _cancellation.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing CancellationTokenSource: {ex.Message}");
                    }
                    
                    _isDisposed = true;
                }
            }
        }
        
        ~Logger()
        {
            Dispose(false);
        }
    }
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
    
    public class LogEntry
    {
        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        
        public LogEntry(DateTime timestamp, LogLevel level, string message)
        {
            Timestamp = timestamp;
            Level = level;
            Message = message;
        }
        
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level.ToString().ToUpper()}] {Message}";
        }
    }
}