using System;

namespace NebulaStore.Storage;

/// <summary>
/// Interface for storage configuration that defines all aspects of storage behavior.
/// This is the central configuration interface that coordinates all storage components.
/// </summary>
public interface IStorageConfiguration
{
    /// <summary>
    /// Gets the channel count provider that determines how many storage channels to use.
    /// </summary>
    IStorageChannelCountProvider ChannelCountProvider { get; }

    /// <summary>
    /// Gets the housekeeping controller that manages background maintenance operations.
    /// </summary>
    IStorageHousekeepingController HousekeepingController { get; }

    /// <summary>
    /// Gets the entity cache evaluator that determines cache behavior and cleanup policies.
    /// </summary>
    IStorageEntityCacheEvaluator EntityCacheEvaluator { get; }

    /// <summary>
    /// Gets the file provider that manages storage file locations and naming.
    /// </summary>
    IStorageLiveFileProvider FileProvider { get; }

    /// <summary>
    /// Gets the data file evaluator that determines file size limits and cleanup policies.
    /// </summary>
    IStorageDataFileEvaluator DataFileEvaluator { get; }

    /// <summary>
    /// Gets the backup setup configuration. May be null if backup is not configured.
    /// </summary>
    IStorageBackupSetup? BackupSetup { get; }
}

/// <summary>
/// Interface for storage channel count provider.
/// Determines the number of parallel storage channels to use.
/// </summary>
public interface IStorageChannelCountProvider
{
    /// <summary>
    /// Gets the number of storage channels to use.
    /// Must be a power of 2 (1, 2, 4, 8, 16, etc.).
    /// </summary>
    int ChannelCount { get; }
}

/// <summary>
/// Interface for storage housekeeping controller.
/// Manages background maintenance operations like garbage collection and file cleanup.
/// </summary>
public interface IStorageHousekeepingController
{
    /// <summary>
    /// Gets the interval in milliseconds between housekeeping operations.
    /// </summary>
    long HousekeepingIntervalMs { get; }

    /// <summary>
    /// Gets the time budget in nanoseconds for each housekeeping operation.
    /// </summary>
    long HousekeepingTimeBudgetNs { get; }

    /// <summary>
    /// Gets the time budget in nanoseconds for garbage collection operations.
    /// </summary>
    long GarbageCollectionTimeBudgetNs { get; }

    /// <summary>
    /// Gets the time budget in nanoseconds for file cleanup operations.
    /// </summary>
    long FileCheckTimeBudgetNs { get; }

    /// <summary>
    /// Gets the time budget in nanoseconds for entity cache check operations.
    /// </summary>
    long LiveCheckTimeBudgetNs { get; }
}

/// <summary>
/// Interface for storage entity cache evaluator.
/// Determines when entities should be removed from cache.
/// </summary>
public interface IStorageEntityCacheEvaluator
{
    /// <summary>
    /// Gets the timeout in milliseconds after which unused entities are eligible for cache removal.
    /// </summary>
    long TimeoutMs { get; }

    /// <summary>
    /// Gets the threshold value for cache evaluation algorithms.
    /// </summary>
    long Threshold { get; }

    /// <summary>
    /// Evaluates whether an entity should be removed from cache based on its age and size.
    /// </summary>
    /// <param name="entityAge">The age of the entity in milliseconds</param>
    /// <param name="entitySize">The size of the entity in bytes</param>
    /// <param name="currentCacheSize">The current total cache size in bytes</param>
    /// <returns>True if the entity should be removed from cache</returns>
    bool ShouldClearFromCache(long entityAge, long entitySize, long currentCacheSize);
}

/// <summary>
/// Interface for storage live file provider.
/// Manages storage file locations, naming, and directory structure.
/// </summary>
public interface IStorageLiveFileProvider
{
    /// <summary>
    /// Gets the base storage directory path.
    /// </summary>
    string StorageDirectory { get; }

    /// <summary>
    /// Gets the directory for data files.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Gets the directory for transaction files.
    /// </summary>
    string TransactionDirectory { get; }

    /// <summary>
    /// Gets the directory for type dictionary files.
    /// </summary>
    string TypeDictionaryDirectory { get; }

    /// <summary>
    /// Gets the file name pattern for data files.
    /// </summary>
    string DataFileNamePattern { get; }

    /// <summary>
    /// Gets the file name pattern for transaction files.
    /// </summary>
    string TransactionFileNamePattern { get; }

    /// <summary>
    /// Gets the file extension for data files.
    /// </summary>
    string DataFileExtension { get; }

    /// <summary>
    /// Gets the file extension for transaction files.
    /// </summary>
    string TransactionFileExtension { get; }
}

/// <summary>
/// Interface for storage data file evaluator.
/// Determines file size limits and when files should be cleaned up or consolidated.
/// </summary>
public interface IStorageDataFileEvaluator
{
    /// <summary>
    /// Gets the minimum file size in bytes. Files smaller than this may be consolidated.
    /// </summary>
    int FileMinimumSize { get; }

    /// <summary>
    /// Gets the maximum file size in bytes. Files larger than this will be split.
    /// </summary>
    int FileMaximumSize { get; }

    /// <summary>
    /// Gets the minimum use ratio (0.0 to 1.0) of non-gap data in a file.
    /// Files with lower ratios may be cleaned up.
    /// </summary>
    double MinimumUseRatio { get; }

    /// <summary>
    /// Gets whether the current head file should be subject to cleanup operations.
    /// </summary>
    bool CleanUpHeadFile { get; }

    /// <summary>
    /// Gets the maximum size for transaction files in bytes.
    /// </summary>
    int TransactionFileMaximumSize { get; }

    /// <summary>
    /// Evaluates whether a file should be cleaned up based on its characteristics.
    /// </summary>
    /// <param name="fileSize">The current file size in bytes</param>
    /// <param name="usedSize">The amount of used (non-gap) data in bytes</param>
    /// <param name="isHeadFile">Whether this is the current head file being written to</param>
    /// <returns>True if the file should be cleaned up</returns>
    bool ShouldCleanUpFile(long fileSize, long usedSize, bool isHeadFile);
}

/// <summary>
/// Interface for storage backup setup.
/// Configures backup operations and destinations.
/// </summary>
public interface IStorageBackupSetup
{
    /// <summary>
    /// Gets the backup file provider that manages backup file locations.
    /// </summary>
    IStorageBackupFileProvider BackupFileProvider { get; }

    /// <summary>
    /// Gets whether backup operations are enabled.
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Interface for storage backup file provider.
/// Manages backup file locations and naming.
/// </summary>
public interface IStorageBackupFileProvider
{
    /// <summary>
    /// Gets the backup directory path.
    /// </summary>
    string BackupDirectory { get; }

    /// <summary>
    /// Gets the file name pattern for backup files.
    /// </summary>
    string BackupFileNamePattern { get; }

    /// <summary>
    /// Gets the file extension for backup files.
    /// </summary>
    string BackupFileExtension { get; }
}
