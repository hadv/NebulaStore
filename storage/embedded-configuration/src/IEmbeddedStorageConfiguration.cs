using System;
using System.IO;

namespace NebulaStore.Storage.EmbeddedConfiguration;

/// <summary>
/// Configuration interface for embedded storage.
/// </summary>
public interface IEmbeddedStorageConfiguration
{
    /// <summary>
    /// Gets the storage directory path.
    /// </summary>
    string StorageDirectory { get; }

    /// <summary>
    /// Gets the channel count for parallel processing.
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// Gets the channel directory prefix.
    /// </summary>
    string ChannelDirectoryPrefix { get; }

    /// <summary>
    /// Gets the data file prefix.
    /// </summary>
    string DataFilePrefix { get; }

    /// <summary>
    /// Gets the data file suffix.
    /// </summary>
    string DataFileSuffix { get; }

    /// <summary>
    /// Gets the transaction file prefix.
    /// </summary>
    string TransactionFilePrefix { get; }

    /// <summary>
    /// Gets the transaction file suffix.
    /// </summary>
    string TransactionFileSuffix { get; }

    /// <summary>
    /// Gets the type dictionary file name.
    /// </summary>
    string TypeDictionaryFileName { get; }

    /// <summary>
    /// Gets the entity cache threshold.
    /// </summary>
    long EntityCacheThreshold { get; }

    /// <summary>
    /// Gets the entity cache timeout in milliseconds.
    /// </summary>
    long EntityCacheTimeoutMs { get; }

    /// <summary>
    /// Gets the data file minimum size.
    /// </summary>
    long DataFileMinimumSize { get; }

    /// <summary>
    /// Gets the data file maximum size.
    /// </summary>
    long DataFileMaximumSize { get; }

    /// <summary>
    /// Gets a value indicating whether to perform housekeeping on startup.
    /// </summary>
    bool HousekeepingOnStartup { get; }

    /// <summary>
    /// Gets the housekeeping interval in milliseconds.
    /// </summary>
    long HousekeepingIntervalMs { get; }

    /// <summary>
    /// Gets the housekeeping time budget in nanoseconds.
    /// </summary>
    long HousekeepingTimeBudgetNs { get; }

    /// <summary>
    /// Gets a value indicating whether to validate on startup.
    /// </summary>
    bool ValidateOnStartup { get; }

    /// <summary>
    /// Gets the backup directory path.
    /// </summary>
    string? BackupDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether to delete backup files after successful restore.
    /// </summary>
    bool DeleteBackupFilesAfterRestore { get; }
}

/// <summary>
/// Builder for creating embedded storage configurations.
/// </summary>
public interface IEmbeddedStorageConfigurationBuilder
{
    /// <summary>
    /// Sets the storage directory.
    /// </summary>
    /// <param name="directory">The storage directory path</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetStorageDirectory(string directory);

    /// <summary>
    /// Sets the channel count.
    /// </summary>
    /// <param name="channelCount">The number of channels</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetChannelCount(int channelCount);

    /// <summary>
    /// Sets the entity cache threshold.
    /// </summary>
    /// <param name="threshold">The cache threshold</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetEntityCacheThreshold(long threshold);

    /// <summary>
    /// Sets the entity cache timeout.
    /// </summary>
    /// <param name="timeoutMs">The cache timeout in milliseconds</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetEntityCacheTimeout(long timeoutMs);

    /// <summary>
    /// Sets the data file size limits.
    /// </summary>
    /// <param name="minimumSize">The minimum file size</param>
    /// <param name="maximumSize">The maximum file size</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetDataFileSize(long minimumSize, long maximumSize);

    /// <summary>
    /// Enables or disables housekeeping on startup.
    /// </summary>
    /// <param name="enabled">Whether to enable housekeeping on startup</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetHousekeepingOnStartup(bool enabled);

    /// <summary>
    /// Sets the housekeeping interval.
    /// </summary>
    /// <param name="intervalMs">The housekeeping interval in milliseconds</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetHousekeepingInterval(long intervalMs);

    /// <summary>
    /// Sets the backup directory.
    /// </summary>
    /// <param name="directory">The backup directory path</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetBackupDirectory(string directory);

    /// <summary>
    /// Enables or disables validation on startup.
    /// </summary>
    /// <param name="enabled">Whether to enable validation on startup</param>
    /// <returns>This builder instance for method chaining</returns>
    IEmbeddedStorageConfigurationBuilder SetValidateOnStartup(bool enabled);

    /// <summary>
    /// Builds the configuration.
    /// </summary>
    /// <returns>The built configuration</returns>
    IEmbeddedStorageConfiguration Build();
}
