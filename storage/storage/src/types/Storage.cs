using System;
using System.IO;

namespace NebulaStore.Storage;

/// <summary>
/// Static utility class containing factory methods for creating storage components.
/// Provides convenient access to default implementations and configurations.
/// </summary>
public static class Storage
{
    #region Constants

    /// <summary>
    /// Conversion factor from milliseconds to nanoseconds.
    /// </summary>
    private const long MillisecondsToNanoseconds = 1_000_000L;

    /// <summary>
    /// Default storage directory name.
    /// </summary>
    public const string DefaultStorageDirectoryName = "storage";

    #endregion

    #region Time Utilities

    /// <summary>
    /// Converts milliseconds to nanoseconds.
    /// </summary>
    public static long MillisecondsToNanoseconds(long milliseconds) => milliseconds * MillisecondsToNanoseconds;

    /// <summary>
    /// Converts nanoseconds to milliseconds.
    /// </summary>
    public static long NanosecondsToMilliseconds(long nanoseconds) => nanoseconds / MillisecondsToNanoseconds;

    #endregion

    #region Storage Configuration Factory Methods

    /// <summary>
    /// Creates a new storage configuration with default settings.
    /// </summary>
    public static StorageConfiguration Configuration() => StorageConfiguration.New();

    /// <summary>
    /// Creates a new storage configuration with the specified file provider.
    /// </summary>
    public static StorageConfiguration Configuration(IStorageLiveFileProvider fileProvider) =>
        StorageConfiguration.New(fileProvider);

    /// <summary>
    /// Creates a new storage configuration builder.
    /// </summary>
    public static StorageConfigurationBuilder ConfigurationBuilder() => StorageConfiguration.Builder();

    #endregion

    #region File Provider Factory Methods

    /// <summary>
    /// Creates a new storage file provider with default settings.
    /// </summary>
    public static StorageLiveFileProvider FileProvider() => StorageLiveFileProvider.New();

    /// <summary>
    /// Creates a new storage file provider with the specified storage directory.
    /// </summary>
    public static StorageLiveFileProvider FileProvider(string storageDirectory) =>
        StorageLiveFileProvider.New(storageDirectory);

    /// <summary>
    /// Gets the default storage directory path.
    /// </summary>
    public static string DefaultStorageDirectory() =>
        Path.Combine(Environment.CurrentDirectory, DefaultStorageDirectoryName);

    #endregion

    #region Backup Provider Factory Methods

    /// <summary>
    /// Creates a new backup file provider with default settings.
    /// </summary>
    public static StorageBackupFileProvider BackupFileProvider() =>
        StorageBackupFileProvider.New("backup");

    /// <summary>
    /// Creates a new backup file provider with the specified backup directory.
    /// </summary>
    public static StorageBackupFileProvider BackupFileProvider(string backupDirectory) =>
        StorageBackupFileProvider.New(backupDirectory);

    /// <summary>
    /// Creates a new backup setup with the specified backup directory.
    /// </summary>
    public static StorageBackupSetup BackupSetup(string backupDirectory) =>
        StorageBackupSetup.New(backupDirectory);

    /// <summary>
    /// Creates a new backup setup with the specified backup file provider.
    /// </summary>
    public static StorageBackupSetup BackupSetup(IStorageBackupFileProvider backupFileProvider) =>
        StorageBackupSetup.New(backupFileProvider);

    #endregion

    #region Housekeeping Controller Factory Methods

    /// <summary>
    /// Creates a new housekeeping controller with default settings.
    /// </summary>
    public static StorageHousekeepingController HousekeepingController() =>
        StorageHousekeepingController.New();

    /// <summary>
    /// Creates a new housekeeping controller with the specified interval and time budget.
    /// </summary>
    public static StorageHousekeepingController HousekeepingController(
        long housekeepingIntervalMs,
        long housekeepingTimeBudgetNs) =>
        StorageHousekeepingController.New(housekeepingIntervalMs, housekeepingTimeBudgetNs);

    #endregion

    #region Entity Cache Evaluator Factory Methods

    /// <summary>
    /// Creates a new entity cache evaluator with default settings.
    /// </summary>
    public static StorageEntityCacheEvaluator EntityCacheEvaluator() =>
        StorageEntityCacheEvaluator.New();

    /// <summary>
    /// Creates a new entity cache evaluator with the specified timeout.
    /// </summary>
    public static StorageEntityCacheEvaluator EntityCacheEvaluator(long timeoutMs) =>
        StorageEntityCacheEvaluator.New(timeoutMs);

    /// <summary>
    /// Creates a new entity cache evaluator with the specified timeout and threshold.
    /// </summary>
    public static StorageEntityCacheEvaluator EntityCacheEvaluator(long timeoutMs, long threshold) =>
        StorageEntityCacheEvaluator.New(timeoutMs, threshold);

    #endregion

    #region Channel Count Provider Factory Methods

    /// <summary>
    /// Creates a new channel count provider with default settings (processor count).
    /// </summary>
    public static StorageChannelCountProvider ChannelCountProvider() =>
        StorageChannelCountProvider.New();

    /// <summary>
    /// Creates a new channel count provider with the specified channel count.
    /// </summary>
    public static StorageChannelCountProvider ChannelCountProvider(int channelCount) =>
        StorageChannelCountProvider.New(channelCount);

    #endregion

    #region Data File Evaluator Factory Methods

    /// <summary>
    /// Creates a new data file evaluator with default settings.
    /// </summary>
    public static StorageDataFileEvaluator DataFileEvaluator() =>
        StorageDataFileEvaluator.New();

    /// <summary>
    /// Creates a new data file evaluator with the specified file size limits.
    /// </summary>
    public static StorageDataFileEvaluator DataFileEvaluator(int fileMinimumSize, int fileMaximumSize) =>
        StorageDataFileEvaluator.New(fileMinimumSize, fileMaximumSize);

    /// <summary>
    /// Creates a new data file evaluator with the specified file size limits and use ratio.
    /// </summary>
    public static StorageDataFileEvaluator DataFileEvaluator(
        int fileMinimumSize,
        int fileMaximumSize,
        double minimumUseRatio) =>
        StorageDataFileEvaluator.New(fileMinimumSize, fileMaximumSize, minimumUseRatio);

    /// <summary>
    /// Creates a new data file evaluator with the specified file size limits, use ratio, and head file cleanup setting.
    /// </summary>
    public static StorageDataFileEvaluator DataFileEvaluator(
        int fileMinimumSize,
        int fileMaximumSize,
        double minimumUseRatio,
        bool cleanUpHeadFile) =>
        StorageDataFileEvaluator.New(fileMinimumSize, fileMaximumSize, minimumUseRatio, cleanUpHeadFile);

    /// <summary>
    /// Creates a new data file evaluator with all parameters specified.
    /// </summary>
    public static StorageDataFileEvaluator DataFileEvaluator(
        int fileMinimumSize,
        int fileMaximumSize,
        double minimumUseRatio,
        bool cleanUpHeadFile,
        int transactionFileMaximumSize) =>
        StorageDataFileEvaluator.New(
            fileMinimumSize,
            fileMaximumSize,
            minimumUseRatio,
            cleanUpHeadFile,
            transactionFileMaximumSize);

    #endregion

    #region Storage Consolidation

    /// <summary>
    /// Consolidates the storage system by performing garbage collection, file check, and cache check.
    /// </summary>
    public static T Consolidate<T>(T storageConnection) where T : IStorageConnection
    {
        return Consolidate(storageConnection, null);
    }

    /// <summary>
    /// Consolidates the storage system with a custom entity cache evaluator.
    /// </summary>
    public static T Consolidate<T>(T storageConnection, IStorageEntityCacheEvaluator? entityEvaluator) 
        where T : IStorageConnection
    {
        // Perform consolidation operations in order:
        // 1. Garbage collection to remove unreachable entities
        // 2. File check to optimize file structure
        // 3. Cache check to clean up memory
        
        storageConnection.IssueFullGarbageCollection();
        // Note: File check and cache check methods will be added when we implement the full IStorageConnection interface
        
        return storageConnection;
    }

    #endregion
}
