using System;
using System.IO;

namespace NebulaStore.Storage.EmbeddedConfiguration;

/// <summary>
/// Default implementation of embedded storage configuration.
/// </summary>
public class EmbeddedStorageConfiguration : IEmbeddedStorageConfiguration
{
    public string StorageDirectory { get; init; } = "storage";
    public int ChannelCount { get; init; } = 1;
    public string ChannelDirectoryPrefix { get; init; } = "channel_";
    public string DataFilePrefix { get; init; } = "channel_";
    public string DataFileSuffix { get; init; } = ".dat";
    public string TransactionFilePrefix { get; init; } = "channel_";
    public string TransactionFileSuffix { get; init; } = ".trs";
    public string TypeDictionaryFileName { get; init; } = "PersistenceTypeDictionary.ptd";
    public long EntityCacheThreshold { get; init; } = 1000000; // 1MB
    public long EntityCacheTimeoutMs { get; init; } = 86400000; // 24 hours
    public long DataFileMinimumSize { get; init; } = 1024 * 1024; // 1MB
    public long DataFileMaximumSize { get; init; } = 1024 * 1024 * 1024; // 1GB
    public bool HousekeepingOnStartup { get; init; } = true;
    public long HousekeepingIntervalMs { get; init; } = 1000; // 1 second
    public long HousekeepingTimeBudgetNs { get; init; } = 10000000; // 10ms
    public bool ValidateOnStartup { get; init; } = true;
    public string? BackupDirectory { get; init; }
    public bool DeleteBackupFilesAfterRestore { get; init; } = false;
    public bool UseAfs { get; init; } = false;
    public string AfsStorageType { get; init; } = "blobstore";
    public string? AfsConnectionString { get; init; }
    public bool AfsUseCache { get; init; } = true;

    /// <summary>
    /// Creates a new configuration builder.
    /// </summary>
    /// <returns>A new configuration builder instance</returns>
    public static IEmbeddedStorageConfigurationBuilder New() => new Builder();

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    /// <returns>A default configuration instance</returns>
    public static IEmbeddedStorageConfiguration Default() => new EmbeddedStorageConfiguration();

    /// <summary>
    /// Creates a configuration with the specified storage directory.
    /// </summary>
    /// <param name="storageDirectory">The storage directory path</param>
    /// <returns>A configuration instance</returns>
    public static IEmbeddedStorageConfiguration WithStorageDirectory(string storageDirectory) =>
        new EmbeddedStorageConfiguration { StorageDirectory = storageDirectory };

    private class Builder : IEmbeddedStorageConfigurationBuilder
    {
        private string _storageDirectory = "storage";
        private int _channelCount = 1;
        private long _entityCacheThreshold = 1000000;
        private long _entityCacheTimeoutMs = 86400000;
        private long _dataFileMinimumSize = 1024 * 1024;
        private long _dataFileMaximumSize = 1024 * 1024 * 1024;
        private bool _housekeepingOnStartup = true;
        private long _housekeepingIntervalMs = 1000;
        private string? _backupDirectory;
        private bool _validateOnStartup = true;
        private bool _useAfs = false;
        private string _afsStorageType = "blobstore";
        private string? _afsConnectionString;
        private bool _afsUseCache = true;

        public IEmbeddedStorageConfigurationBuilder SetStorageDirectory(string directory)
        {
            _storageDirectory = directory ?? throw new ArgumentNullException(nameof(directory));
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetChannelCount(int channelCount)
        {
            if (channelCount <= 0)
                throw new ArgumentException("Channel count must be positive", nameof(channelCount));
            _channelCount = channelCount;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetEntityCacheThreshold(long threshold)
        {
            if (threshold < 0)
                throw new ArgumentException("Cache threshold must be non-negative", nameof(threshold));
            _entityCacheThreshold = threshold;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetEntityCacheTimeout(long timeoutMs)
        {
            if (timeoutMs < 0)
                throw new ArgumentException("Cache timeout must be non-negative", nameof(timeoutMs));
            _entityCacheTimeoutMs = timeoutMs;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetDataFileSize(long minimumSize, long maximumSize)
        {
            if (minimumSize <= 0)
                throw new ArgumentException("Minimum size must be positive", nameof(minimumSize));
            if (maximumSize <= minimumSize)
                throw new ArgumentException("Maximum size must be greater than minimum size", nameof(maximumSize));
            
            _dataFileMinimumSize = minimumSize;
            _dataFileMaximumSize = maximumSize;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetHousekeepingOnStartup(bool enabled)
        {
            _housekeepingOnStartup = enabled;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetHousekeepingInterval(long intervalMs)
        {
            if (intervalMs < 0)
                throw new ArgumentException("Housekeeping interval must be non-negative", nameof(intervalMs));
            _housekeepingIntervalMs = intervalMs;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetBackupDirectory(string directory)
        {
            _backupDirectory = directory;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetValidateOnStartup(bool enabled)
        {
            _validateOnStartup = enabled;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetUseAfs(bool enabled)
        {
            _useAfs = enabled;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetAfsStorageType(string storageType)
        {
            _afsStorageType = storageType ?? throw new ArgumentNullException(nameof(storageType));
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetAfsConnectionString(string? connectionString)
        {
            _afsConnectionString = connectionString;
            return this;
        }

        public IEmbeddedStorageConfigurationBuilder SetAfsUseCache(bool useCache)
        {
            _afsUseCache = useCache;
            return this;
        }

        public IEmbeddedStorageConfiguration Build()
        {
            return new EmbeddedStorageConfiguration
            {
                StorageDirectory = _storageDirectory,
                ChannelCount = _channelCount,
                EntityCacheThreshold = _entityCacheThreshold,
                EntityCacheTimeoutMs = _entityCacheTimeoutMs,
                DataFileMinimumSize = _dataFileMinimumSize,
                DataFileMaximumSize = _dataFileMaximumSize,
                HousekeepingOnStartup = _housekeepingOnStartup,
                HousekeepingIntervalMs = _housekeepingIntervalMs,
                BackupDirectory = _backupDirectory,
                ValidateOnStartup = _validateOnStartup,
                UseAfs = _useAfs,
                AfsStorageType = _afsStorageType,
                AfsConnectionString = _afsConnectionString,
                AfsUseCache = _afsUseCache
            };
        }
    }
}
