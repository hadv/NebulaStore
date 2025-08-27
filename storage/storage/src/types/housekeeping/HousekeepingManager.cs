using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types.Housekeeping;

/// <summary>
/// Manages storage housekeeping operations following Eclipse Store patterns.
/// Provides time-budgeted garbage collection, file consolidation, and storage optimization.
/// </summary>
public class HousekeepingManager : IDisposable
{
    #region Private Fields

    private readonly string _storageDirectory;
    private readonly TimeSpan _housekeepingInterval;
    private readonly long _timeBudgetNanoseconds;
    private readonly Timer _housekeepingTimer;
    private readonly object _lock = new();

    private long _totalGarbageCollections;
    private long _totalFileConsolidations;
    private long _totalBytesReclaimed;
    private DateTime _lastHousekeepingRun;
    private bool _disposed;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the HousekeepingManager class.
    /// </summary>
    /// <param name="storageDirectory">The storage directory to manage.</param>
    /// <param name="housekeepingInterval">The interval between housekeeping runs.</param>
    /// <param name="timeBudgetNanoseconds">The time budget for each housekeeping run in nanoseconds.</param>
    public HousekeepingManager(string storageDirectory, TimeSpan housekeepingInterval, long timeBudgetNanoseconds)
    {
        _storageDirectory = storageDirectory ?? throw new ArgumentNullException(nameof(storageDirectory));
        _housekeepingInterval = housekeepingInterval;
        _timeBudgetNanoseconds = timeBudgetNanoseconds;
        _lastHousekeepingRun = DateTime.UtcNow;

        // Start housekeeping timer
        _housekeepingTimer = new Timer(PerformHousekeeping, null, _housekeepingInterval, _housekeepingInterval);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the total number of garbage collections performed.
    /// </summary>
    public long TotalGarbageCollections => Interlocked.Read(ref _totalGarbageCollections);

    /// <summary>
    /// Gets the total number of file consolidations performed.
    /// </summary>
    public long TotalFileConsolidations => Interlocked.Read(ref _totalFileConsolidations);

    /// <summary>
    /// Gets the total bytes reclaimed through housekeeping.
    /// </summary>
    public long TotalBytesReclaimed => Interlocked.Read(ref _totalBytesReclaimed);

    /// <summary>
    /// Gets the last housekeeping run time.
    /// </summary>
    public DateTime LastHousekeepingRun => _lastHousekeepingRun;

    /// <summary>
    /// Gets the housekeeping interval.
    /// </summary>
    public TimeSpan HousekeepingInterval => _housekeepingInterval;

    /// <summary>
    /// Gets the time budget in nanoseconds.
    /// </summary>
    public long TimeBudgetNanoseconds => _timeBudgetNanoseconds;

    #endregion

    #region Public Methods

    /// <summary>
    /// Performs a full housekeeping operation following Eclipse Store patterns.
    /// </summary>
    /// <returns>The housekeeping result.</returns>
    public HousekeepingResult PerformFullHousekeeping()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var result = new HousekeepingResult
            {
                StartTime = DateTime.UtcNow,
                TimeBudgetNanoseconds = long.MaxValue // No time limit for full housekeeping
            };

            try
            {
                // Step 1: Garbage collection
                var gcResult = PerformGarbageCollection(long.MaxValue);
                result.GarbageCollectionResult = gcResult;

                // Step 2: File consolidation
                var consolidationResult = PerformFileConsolidation(long.MaxValue);
                result.FileConsolidationResult = consolidationResult;

                // Step 3: Storage optimization
                var optimizationResult = PerformStorageOptimization(long.MaxValue);
                result.StorageOptimizationResult = optimizationResult;

                result.Status = HousekeepingStatus.Completed;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;

                _lastHousekeepingRun = result.EndTime;

                return result;
            }
            catch (Exception ex)
            {
                result.Status = HousekeepingStatus.Failed;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                return result;
            }
        }
    }

    /// <summary>
    /// Performs time-budgeted housekeeping following Eclipse Store patterns.
    /// </summary>
    /// <param name="timeBudgetNanoseconds">The time budget in nanoseconds.</param>
    /// <returns>The housekeeping result.</returns>
    public HousekeepingResult PerformTimeBudgetedHousekeeping(long timeBudgetNanoseconds)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var result = new HousekeepingResult
            {
                StartTime = DateTime.UtcNow,
                TimeBudgetNanoseconds = timeBudgetNanoseconds
            };

            var startTicks = DateTime.UtcNow.Ticks;
            var budgetTicks = timeBudgetNanoseconds / 100; // Convert nanoseconds to ticks (100ns per tick)

            try
            {
                // Step 1: Garbage collection (use 40% of budget)
                var gcBudget = (long)(budgetTicks * 0.4);
                var gcResult = PerformGarbageCollection(gcBudget * 100); // Convert back to nanoseconds
                result.GarbageCollectionResult = gcResult;

                var elapsedTicks = DateTime.UtcNow.Ticks - startTicks;
                if (elapsedTicks >= budgetTicks)
                {
                    result.Status = HousekeepingStatus.TimeBudgetExceeded;
                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;
                    return result;
                }

                // Step 2: File consolidation (use 40% of remaining budget)
                var remainingTicks = budgetTicks - elapsedTicks;
                var consolidationBudget = (long)(remainingTicks * 0.4);
                var consolidationResult = PerformFileConsolidation(consolidationBudget * 100);
                result.FileConsolidationResult = consolidationResult;

                elapsedTicks = DateTime.UtcNow.Ticks - startTicks;
                if (elapsedTicks >= budgetTicks)
                {
                    result.Status = HousekeepingStatus.TimeBudgetExceeded;
                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;
                    return result;
                }

                // Step 3: Storage optimization (use remaining budget)
                remainingTicks = budgetTicks - elapsedTicks;
                var optimizationResult = PerformStorageOptimization(remainingTicks * 100);
                result.StorageOptimizationResult = optimizationResult;

                result.Status = HousekeepingStatus.Completed;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;

                _lastHousekeepingRun = result.EndTime;

                return result;
            }
            catch (Exception ex)
            {
                result.Status = HousekeepingStatus.Failed;
                result.Exception = ex;
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                return result;
            }
        }
    }

    /// <summary>
    /// Gets housekeeping statistics following Eclipse Store patterns.
    /// </summary>
    /// <returns>The housekeeping statistics.</returns>
    public HousekeepingStatistics GetStatistics()
    {
        ThrowIfDisposed();

        return new HousekeepingStatistics
        {
            TotalGarbageCollections = TotalGarbageCollections,
            TotalFileConsolidations = TotalFileConsolidations,
            TotalBytesReclaimed = TotalBytesReclaimed,
            LastHousekeepingRun = LastHousekeepingRun,
            HousekeepingInterval = HousekeepingInterval,
            TimeBudgetNanoseconds = TimeBudgetNanoseconds
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Timer callback for periodic housekeeping.
    /// </summary>
    /// <param name="state">Timer state (unused).</param>
    private void PerformHousekeeping(object? state)
    {
        try
        {
            if (!_disposed)
            {
                PerformTimeBudgetedHousekeeping(_timeBudgetNanoseconds);
            }
        }
        catch
        {
            // Ignore housekeeping errors to prevent timer from stopping
        }
    }

    /// <summary>
    /// Performs garbage collection following Eclipse Store patterns.
    /// </summary>
    /// <param name="timeBudgetNanoseconds">The time budget in nanoseconds.</param>
    /// <returns>The garbage collection result.</returns>
    private GarbageCollectionResult PerformGarbageCollection(long timeBudgetNanoseconds)
    {
        var result = new GarbageCollectionResult
        {
            StartTime = DateTime.UtcNow,
            TimeBudgetNanoseconds = timeBudgetNanoseconds
        };

        var startTicks = DateTime.UtcNow.Ticks;
        var budgetTicks = timeBudgetNanoseconds / 100;

        try
        {
            // Find and clean up orphaned files
            var orphanedFiles = FindOrphanedFiles();
            var bytesReclaimed = 0L;

            foreach (var file in orphanedFiles)
            {
                var elapsedTicks = DateTime.UtcNow.Ticks - startTicks;
                if (elapsedTicks >= budgetTicks)
                {
                    result.Status = GarbageCollectionStatus.TimeBudgetExceeded;
                    break;
                }

                try
                {
                    var fileSize = new FileInfo(file).Length;
                    File.Delete(file);
                    bytesReclaimed += fileSize;
                    result.FilesDeleted++;
                }
                catch
                {
                    // Skip files that can't be deleted
                }
            }

            result.BytesReclaimed = bytesReclaimed;
            Interlocked.Add(ref _totalBytesReclaimed, bytesReclaimed);
            Interlocked.Increment(ref _totalGarbageCollections);

            if (result.Status == GarbageCollectionStatus.InProgress)
            {
                result.Status = GarbageCollectionStatus.Completed;
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }
        catch (Exception ex)
        {
            result.Status = GarbageCollectionStatus.Failed;
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            return result;
        }
    }

    /// <summary>
    /// Performs file consolidation following Eclipse Store patterns.
    /// </summary>
    /// <param name="timeBudgetNanoseconds">The time budget in nanoseconds.</param>
    /// <returns>The file consolidation result.</returns>
    private FileConsolidationResult PerformFileConsolidation(long timeBudgetNanoseconds)
    {
        var result = new FileConsolidationResult
        {
            StartTime = DateTime.UtcNow,
            TimeBudgetNanoseconds = timeBudgetNanoseconds
        };

        var startTicks = DateTime.UtcNow.Ticks;
        var budgetTicks = timeBudgetNanoseconds / 100;

        try
        {
            // Find small files that can be consolidated
            var smallFiles = FindSmallDataFiles();
            var consolidatedFiles = 0;
            var bytesConsolidated = 0L;

            // Group small files for consolidation
            var consolidationGroups = GroupFilesForConsolidation(smallFiles);

            foreach (var group in consolidationGroups)
            {
                var elapsedTicks = DateTime.UtcNow.Ticks - startTicks;
                if (elapsedTicks >= budgetTicks)
                {
                    result.Status = FileConsolidationStatus.TimeBudgetExceeded;
                    break;
                }

                try
                {
                    var consolidatedSize = ConsolidateFileGroup(group);
                    consolidatedFiles += group.Count;
                    bytesConsolidated += consolidatedSize;
                }
                catch
                {
                    // Skip groups that can't be consolidated
                }
            }

            result.FilesConsolidated = consolidatedFiles;
            result.BytesConsolidated = bytesConsolidated;
            Interlocked.Increment(ref _totalFileConsolidations);

            if (result.Status == FileConsolidationStatus.InProgress)
            {
                result.Status = FileConsolidationStatus.Completed;
            }

            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }
        catch (Exception ex)
        {
            result.Status = FileConsolidationStatus.Failed;
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            return result;
        }
    }

    /// <summary>
    /// Performs storage optimization following Eclipse Store patterns.
    /// </summary>
    /// <param name="timeBudgetNanoseconds">The time budget in nanoseconds.</param>
    /// <returns>The storage optimization result.</returns>
    private StorageOptimizationResult PerformStorageOptimization(long timeBudgetNanoseconds)
    {
        var result = new StorageOptimizationResult
        {
            StartTime = DateTime.UtcNow,
            TimeBudgetNanoseconds = timeBudgetNanoseconds
        };

        try
        {
            // For now, just perform basic optimization
            // In a full implementation, this would include:
            // - Defragmentation
            // - Index optimization
            // - Cache optimization

            result.OptimizationsPerformed = 1;
            result.Status = StorageOptimizationStatus.Completed;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;

            return result;
        }
        catch (Exception ex)
        {
            result.Status = StorageOptimizationStatus.Failed;
            result.Exception = ex;
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            return result;
        }
    }

    /// <summary>
    /// Finds orphaned files that can be garbage collected.
    /// </summary>
    /// <returns>List of orphaned file paths.</returns>
    private List<string> FindOrphanedFiles()
    {
        var orphanedFiles = new List<string>();

        if (!Directory.Exists(_storageDirectory))
            return orphanedFiles;

        // Look for temporary files and old backup files
        var tempFiles = Directory.GetFiles(_storageDirectory, "*.tmp");
        var oldBackupFiles = Directory.GetFiles(_storageDirectory, "*.bak");
        var corruptedFiles = Directory.GetFiles(_storageDirectory, "*.corrupted.*");

        orphanedFiles.AddRange(tempFiles);
        orphanedFiles.AddRange(oldBackupFiles);
        orphanedFiles.AddRange(corruptedFiles);

        return orphanedFiles;
    }

    /// <summary>
    /// Finds small data files that can be consolidated.
    /// </summary>
    /// <returns>List of small data file paths.</returns>
    private List<string> FindSmallDataFiles()
    {
        var smallFiles = new List<string>();

        if (!Directory.Exists(_storageDirectory))
            return smallFiles;

        var dataFiles = Directory.GetFiles(_storageDirectory, "channel_*_data_*.dat");
        const long smallFileThreshold = 1024 * 1024; // 1MB

        foreach (var file in dataFiles)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length < smallFileThreshold)
                {
                    smallFiles.Add(file);
                }
            }
            catch
            {
                // Skip files that can't be accessed
            }
        }

        return smallFiles;
    }

    /// <summary>
    /// Groups files for consolidation.
    /// </summary>
    /// <param name="files">The files to group.</param>
    /// <returns>Groups of files for consolidation.</returns>
    private List<List<string>> GroupFilesForConsolidation(List<string> files)
    {
        var groups = new List<List<string>>();
        const int maxGroupSize = 5;

        for (int i = 0; i < files.Count; i += maxGroupSize)
        {
            var group = files.Skip(i).Take(maxGroupSize).ToList();
            if (group.Count > 1) // Only consolidate if there are multiple files
            {
                groups.Add(group);
            }
        }

        return groups;
    }

    /// <summary>
    /// Consolidates a group of files.
    /// </summary>
    /// <param name="fileGroup">The group of files to consolidate.</param>
    /// <returns>The total size of consolidated data.</returns>
    private long ConsolidateFileGroup(List<string> fileGroup)
    {
        // For now, just return the total size without actual consolidation
        // In a full implementation, this would merge the files

        var totalSize = 0L;

        foreach (var file in fileGroup)
        {
            try
            {
                totalSize += new FileInfo(file).Length;
            }
            catch
            {
                // Skip files that can't be accessed
            }
        }

        return totalSize;
    }

    /// <summary>
    /// Throws if the manager is disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HousekeepingManager));
    }

    #endregion

    #region IDisposable Implementation

    /// <summary>
    /// Disposes the housekeeping manager and stops all operations.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            _housekeepingTimer?.Dispose();
        }
    }

    #endregion
}