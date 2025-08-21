using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Manager for coordinating multiple memory-mapped files with efficient resource management.
/// </summary>
public class MemoryMappedFileManager : IDisposable
{
    private readonly MemoryMappedFileConfiguration _configuration;
    private readonly ConcurrentDictionary<string, IMemoryMappedFile> _files;
    private readonly Timer _maintenanceTimer;
    private readonly SemaphoreSlim _creationSemaphore;
    private volatile bool _isDisposed;

    public MemoryMappedFileManager(MemoryMappedFileConfiguration? configuration = null)
    {
        _configuration = configuration ?? new MemoryMappedFileConfiguration();
        _files = new ConcurrentDictionary<string, IMemoryMappedFile>();
        _creationSemaphore = new SemaphoreSlim(_configuration.MaxConcurrentViews);

        // Set up maintenance timer for cleanup and optimization
        _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Gets the number of managed files.
    /// </summary>
    public int ManagedFileCount => _files.Count;

    /// <summary>
    /// Gets or creates a memory-mapped file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="access">Access mode</param>
    /// <returns>Memory-mapped file instance</returns>
    public IMemoryMappedFile GetOrCreateFile(string filePath, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);
        
        return _files.GetOrAdd(normalizedPath, path =>
        {
            return new MemoryMappedFile(path, access, _configuration);
        });
    }

    /// <summary>
    /// Gets an existing memory-mapped file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Memory-mapped file instance, or null if not found</returns>
    public IMemoryMappedFile? GetFile(string filePath)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);
        return _files.TryGetValue(normalizedPath, out var file) ? file : null;
    }

    /// <summary>
    /// Removes and disposes a memory-mapped file.
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file was found and removed</returns>
    public bool RemoveFile(string filePath)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var normalizedPath = Path.GetFullPath(filePath);
        
        if (_files.TryRemove(normalizedPath, out var file))
        {
            file.Dispose();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all managed file paths.
    /// </summary>
    /// <returns>Collection of file paths</returns>
    public IEnumerable<string> GetManagedFilePaths()
    {
        ThrowIfDisposed();
        return _files.Keys.ToList();
    }

    /// <summary>
    /// Flushes all managed files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var flushTasks = _files.Values.Select(file => file.FlushAsync(cancellationToken));
        await Task.WhenAll(flushTasks);
    }

    /// <summary>
    /// Gets aggregate statistics for all managed files.
    /// </summary>
    /// <returns>Aggregate statistics</returns>
    public MemoryMappedFileAggregateStatistics GetAggregateStatistics()
    {
        ThrowIfDisposed();

        var statistics = _files.Values.Select(file => file.Statistics).ToList();
        
        return new MemoryMappedFileAggregateStatistics(
            statistics.Sum(s => s.TotalReadOperations),
            statistics.Sum(s => s.TotalWriteOperations),
            statistics.Sum(s => s.TotalBytesRead),
            statistics.Sum(s => s.TotalBytesWritten),
            statistics.Sum(s => s.ActiveViews),
            statistics.Sum(s => s.TotalViewsCreated),
            statistics.Sum(s => s.PageFaults),
            statistics.Count > 0 ? statistics.Average(s => s.AverageAccessTimeMicroseconds) : 0.0,
            statistics.Sum(s => s.MemoryUsage),
            _files.Count,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Gets performance metrics for all managed files.
    /// </summary>
    /// <returns>Performance metrics</returns>
    public MemoryMappedFileManagerPerformanceMetrics GetPerformanceMetrics()
    {
        ThrowIfDisposed();

        var stats = GetAggregateStatistics();
        var totalFiles = _files.Count;
        var totalViews = stats.ActiveViews;
        var totalMemory = stats.MemoryUsage;

        return new MemoryMappedFileManagerPerformanceMetrics(
            stats.TotalReadOperations + stats.TotalWriteOperations,
            stats.TotalBytesRead + stats.TotalBytesWritten,
            stats.AverageAccessTimeMicroseconds,
            stats.PageFaults,
            totalFiles,
            totalViews,
            totalMemory,
            CalculateMemoryEfficiency(totalMemory, totalViews),
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Performs maintenance operations on all managed files.
    /// </summary>
    /// <returns>Maintenance result</returns>
    public async Task<MemoryMappedFileMaintenanceResult> PerformMaintenanceAsync()
    {
        ThrowIfDisposed();

        var startTime = DateTime.UtcNow;
        var processedFiles = 0;
        var flushedFiles = 0;
        var errors = 0;

        foreach (var kvp in _files.ToList())
        {
            try
            {
                processedFiles++;
                
                var file = kvp.Value;
                
                // Flush file if it has pending changes
                if (!file.IsReadOnly)
                {
                    await file.FlushAsync();
                    flushedFiles++;
                }
            }
            catch
            {
                errors++;
            }
        }

        var duration = DateTime.UtcNow - startTime;
        
        return new MemoryMappedFileMaintenanceResult(
            processedFiles,
            flushedFiles,
            errors,
            duration
        );
    }

    private void PerformMaintenance(object? state)
    {
        try
        {
            _ = Task.Run(async () => await PerformMaintenanceAsync());
        }
        catch
        {
            // Ignore maintenance errors
        }
    }

    private static double CalculateMemoryEfficiency(long memoryUsage, int activeViews)
    {
        if (activeViews == 0) return 0.0;
        
        // Simple efficiency calculation based on memory per view
        var memoryPerView = (double)memoryUsage / activeViews;
        var idealMemoryPerView = 64 * 1024 * 1024; // 64MB ideal
        
        return Math.Min(1.0, idealMemoryPerView / Math.Max(memoryPerView, 1.0));
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MemoryMappedFileManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _maintenanceTimer.Dispose();
        _creationSemaphore.Dispose();

        // Dispose all managed files
        foreach (var file in _files.Values)
        {
            file.Dispose();
        }
        
        _files.Clear();
    }
}

/// <summary>
/// Aggregate statistics for multiple memory-mapped files.
/// </summary>
public class MemoryMappedFileAggregateStatistics
{
    public MemoryMappedFileAggregateStatistics(
        long totalReadOperations,
        long totalWriteOperations,
        long totalBytesRead,
        long totalBytesWritten,
        int activeViews,
        long totalViewsCreated,
        long pageFaults,
        double averageAccessTimeMicroseconds,
        long memoryUsage,
        int fileCount,
        DateTime timestamp)
    {
        TotalReadOperations = totalReadOperations;
        TotalWriteOperations = totalWriteOperations;
        TotalBytesRead = totalBytesRead;
        TotalBytesWritten = totalBytesWritten;
        ActiveViews = activeViews;
        TotalViewsCreated = totalViewsCreated;
        PageFaults = pageFaults;
        AverageAccessTimeMicroseconds = averageAccessTimeMicroseconds;
        MemoryUsage = memoryUsage;
        FileCount = fileCount;
        Timestamp = timestamp;
    }

    public long TotalReadOperations { get; }
    public long TotalWriteOperations { get; }
    public long TotalBytesRead { get; }
    public long TotalBytesWritten { get; }
    public int ActiveViews { get; }
    public long TotalViewsCreated { get; }
    public long PageFaults { get; }
    public double AverageAccessTimeMicroseconds { get; }
    public long MemoryUsage { get; }
    public int FileCount { get; }
    public DateTime Timestamp { get; }

    public long TotalOperations => TotalReadOperations + TotalWriteOperations;
    public long TotalBytes => TotalBytesRead + TotalBytesWritten;
    public double PageFaultRate => TotalOperations > 0 ? (double)PageFaults / TotalOperations : 0.0;
    public double AverageViewsPerFile => FileCount > 0 ? (double)ActiveViews / FileCount : 0.0;

    public override string ToString()
    {
        return $"MemoryMappedFile Aggregate [{Timestamp:HH:mm:ss}]: " +
               $"Files={FileCount}, Operations={TotalOperations:N0}, " +
               $"Bytes={TotalBytes:N0}, Views={ActiveViews} ({AverageViewsPerFile:F1}/file), " +
               $"PageFaults={PageFaults:N0} ({PageFaultRate:P2}), " +
               $"Memory={MemoryUsage:N0} bytes";
    }
}

/// <summary>
/// Performance metrics for memory-mapped file manager.
/// </summary>
public class MemoryMappedFileManagerPerformanceMetrics
{
    public MemoryMappedFileManagerPerformanceMetrics(
        long totalOperations,
        long totalBytes,
        double averageLatencyMicroseconds,
        long pageFaults,
        int fileCount,
        int activeViews,
        long memoryUsage,
        double memoryEfficiency,
        DateTime timestamp)
    {
        TotalOperations = totalOperations;
        TotalBytes = totalBytes;
        AverageLatencyMicroseconds = averageLatencyMicroseconds;
        PageFaults = pageFaults;
        FileCount = fileCount;
        ActiveViews = activeViews;
        MemoryUsage = memoryUsage;
        MemoryEfficiency = memoryEfficiency;
        Timestamp = timestamp;
    }

    public long TotalOperations { get; }
    public long TotalBytes { get; }
    public double AverageLatencyMicroseconds { get; }
    public long PageFaults { get; }
    public int FileCount { get; }
    public int ActiveViews { get; }
    public long MemoryUsage { get; }
    public double MemoryEfficiency { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        return $"MemoryMappedFileManager Performance [{Timestamp:HH:mm:ss}]: " +
               $"Files={FileCount}, Views={ActiveViews}, " +
               $"Operations={TotalOperations:N0}, Bytes={TotalBytes:N0}, " +
               $"Latency={AverageLatencyMicroseconds:F1}μs, " +
               $"Memory={MemoryUsage:N0} bytes (Eff={MemoryEfficiency:P1})";
    }
}

/// <summary>
/// Result of memory-mapped file maintenance operations.
/// </summary>
public class MemoryMappedFileMaintenanceResult
{
    public MemoryMappedFileMaintenanceResult(int processedFiles, int flushedFiles, int errors, TimeSpan duration)
    {
        ProcessedFiles = processedFiles;
        FlushedFiles = flushedFiles;
        Errors = errors;
        Duration = duration;
        Timestamp = DateTime.UtcNow;
    }

    public int ProcessedFiles { get; }
    public int FlushedFiles { get; }
    public int Errors { get; }
    public TimeSpan Duration { get; }
    public DateTime Timestamp { get; }

    public bool IsSuccessful => Errors == 0;
    public double SuccessRate => ProcessedFiles > 0 ? (double)(ProcessedFiles - Errors) / ProcessedFiles : 0.0;

    public override string ToString()
    {
        return $"MemoryMappedFile Maintenance [{Timestamp:HH:mm:ss}]: " +
               $"Processed={ProcessedFiles}, Flushed={FlushedFiles}, " +
               $"Errors={Errors}, Duration={Duration.TotalMilliseconds:F0}ms, " +
               $"Success={SuccessRate:P1}";
    }
}
