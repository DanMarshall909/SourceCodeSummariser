using System.Collections.Concurrent;

namespace SourceCodeSummariser
{
    /// <summary>
    /// Service that monitors a directory for C# file changes and automatically processes them.
    /// Implements debouncing to avoid processing the same file multiple times during rapid saves.
    /// </summary>
    public class FileWatcherService : IDisposable
    {
        private readonly FileSystemWatcher _watcher;
        private readonly FileProcessorService _fileProcessorService;
        private readonly AppSettings _settings;
        private readonly string _rootPath;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, Timer> _debounceTimers;
        private readonly TimeSpan _debounceDelay;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the FileWatcherService.
        /// </summary>
        /// <param name="rootPath">The root directory to monitor</param>
        /// <param name="fileProcessorService">Service to process changed files</param>
        /// <param name="settings">Application settings</param>
        /// <param name="logger">Logger for output messages</param>
        /// <param name="debounceDelayMs">Debounce delay in milliseconds (default: 500ms)</param>
        public FileWatcherService(
            string rootPath,
            FileProcessorService fileProcessorService,
            AppSettings settings,
            ILogger logger,
            int debounceDelayMs = 500)
        {
            _rootPath = rootPath;
            _fileProcessorService = fileProcessorService;
            _settings = settings;
            _logger = logger;
            _debounceTimers = new ConcurrentDictionary<string, Timer>();
            _debounceDelay = TimeSpan.FromMilliseconds(debounceDelayMs);

            _watcher = new FileSystemWatcher(rootPath)
            {
                Filter = _settings.Processing.FilePattern,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = true,
                EnableRaisingEvents = false
            };

            // Subscribe to file system events
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Deleted += OnFileDeleted;
        }

        /// <summary>
        /// Starts monitoring the directory for changes.
        /// </summary>
        public void Start()
        {
            _watcher.EnableRaisingEvents = true;
            _logger.WriteLine($"\n👁️  Watching for changes in: {_rootPath}");
            _logger.WriteLine("Press Ctrl+C to stop monitoring...\n");
        }

        /// <summary>
        /// Stops monitoring the directory.
        /// </summary>
        public void Stop()
        {
            _watcher.EnableRaisingEvents = false;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessFile(e.FullPath))
            {
                DebounceFileChange(e.FullPath, e.ChangeType);
            }
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (ShouldProcessFile(e.FullPath))
            {
                _logger.WriteLine($"[{DateTime.Now:HH:mm:ss}] File renamed: {e.OldName} → {e.Name}");
                DebounceFileChange(e.FullPath, WatcherChangeTypes.Renamed);
            }
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (ShouldProcessFile(e.FullPath))
            {
                _logger.WriteLine($"[{DateTime.Now:HH:mm:ss}] File deleted: {e.Name}");
                // Note: We don't process deleted files, just notify the user
            }
        }

        /// <summary>
        /// Implements debouncing to avoid processing the same file multiple times.
        /// When a file changes, we wait for the debounce delay before processing.
        /// If the file changes again during the delay, we reset the timer.
        /// </summary>
        private void DebounceFileChange(string filePath, WatcherChangeTypes changeType)
        {
            // Cancel existing timer for this file if it exists
            if (_debounceTimers.TryRemove(filePath, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            // Create a new timer that will fire after the debounce delay
            var timer = new Timer(
                async _ => await ProcessFileAsync(filePath, changeType),
                null,
                _debounceDelay,
                Timeout.InfiniteTimeSpan);

            _debounceTimers.TryAdd(filePath, timer);
        }

        /// <summary>
        /// Processes a changed file asynchronously.
        /// </summary>
        private async Task ProcessFileAsync(string filePath, WatcherChangeTypes changeType)
        {
            // Remove the timer for this file
            if (_debounceTimers.TryRemove(filePath, out var timer))
            {
                timer.Dispose();
            }

            try
            {
                // Check if file still exists (it might have been deleted after the change event)
                if (!File.Exists(filePath))
                {
                    return;
                }

                var fileName = Path.GetFileName(filePath);
                var changeTypeStr = changeType switch
                {
                    WatcherChangeTypes.Changed => "MODIFIED",
                    WatcherChangeTypes.Created => "CREATED",
                    WatcherChangeTypes.Renamed => "RENAMED",
                    _ => "CHANGED"
                };

                _logger.Write($"[{DateTime.Now:HH:mm:ss}] [{changeTypeStr}] {fileName}... ");

                var changes = await _fileProcessorService.ProcessFile(filePath);

                if (changes.Any())
                {
                    _logger.WriteLine($"✓ ({changes.Count} changes detected)");

                    // Display the changes
                    foreach (var (methodSignature, oldSummary, newSummary) in changes)
                    {
                        _logger.WriteLine($"    • {methodSignature}");
                        if (!string.IsNullOrEmpty(oldSummary))
                        {
                            _logger.WriteLine($"      Old: {oldSummary}");
                        }
                        _logger.WriteLine($"      New: {newSummary}");
                    }
                }
                else
                {
                    _logger.WriteLine("✓ (no changes)");
                }
            }
            catch (Exception ex)
            {
                _logger.WriteLine($"✗ ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if a file should be processed based on exclusion rules.
        /// </summary>
        private bool ShouldProcessFile(string filePath)
        {
            // Check if file is in an excluded folder
            string normalizedPath = Path.GetFullPath(filePath).ToLower();
            string normalizedRootPath = Path.GetFullPath(_rootPath).ToLower();

            foreach (var excludedFolder in _settings.Processing.ExcludedFolders)
            {
                string excludedPath = Path.Combine(normalizedRootPath, excludedFolder).ToLower();
                if (normalizedPath.Contains(excludedPath))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Disposes resources used by the FileWatcherService.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _watcher?.Dispose();

            // Dispose all debounce timers
            foreach (var timer in _debounceTimers.Values)
            {
                timer?.Dispose();
            }
            _debounceTimers.Clear();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
