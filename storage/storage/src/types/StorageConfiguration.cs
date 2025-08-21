using System;
using System.IO;

namespace NebulaStore.Storage;

/// <summary>
/// Default implementation of storage configuration.
/// Provides immutable configuration for all storage components.
/// </summary>
public class StorageConfiguration : IStorageConfiguration
{
    public IStorageChannelCountProvider ChannelCountProvider { get; }
    public IStorageHousekeepingController HousekeepingController { get; }
    public IStorageEntityCacheEvaluator EntityCacheEvaluator { get; }
    public IStorageLiveFileProvider FileProvider { get; }
    public IStorageDataFileEvaluator DataFileEvaluator { get; }
    public IStorageBackupSetup? BackupSetup { get; }

    public StorageConfiguration(
        IStorageChannelCountProvider channelCountProvider,
        IStorageHousekeepingController housekeepingController,
        IStorageEntityCacheEvaluator entityCacheEvaluator,
        IStorageLiveFileProvider fileProvider,
        IStorageDataFileEvaluator dataFileEvaluator,
        IStorageBackupSetup? backupSetup = null)
    {
        ChannelCountProvider = channelCountProvider ?? throw new ArgumentNullException(nameof(channelCountProvider));
        HousekeepingController = housekeepingController ?? throw new ArgumentNullException(nameof(housekeepingController));
        EntityCacheEvaluator = entityCacheEvaluator ?? throw new ArgumentNullException(nameof(entityCacheEvaluator));
        FileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        DataFileEvaluator = dataFileEvaluator ?? throw new ArgumentNullException(nameof(dataFileEvaluator));
        BackupSetup = backupSetup;
    }

    /// <summary>
    /// Creates a new storage configuration with default settings.
    /// </summary>
    public static StorageConfiguration New()
    {
        return Builder().CreateConfiguration();
    }

    /// <summary>
    /// Creates a new storage configuration with the specified file provider.
    /// </summary>
    public static StorageConfiguration New(IStorageLiveFileProvider fileProvider)
    {
        return Builder()
            .SetFileProvider(fileProvider)
            .CreateConfiguration();
    }

    /// <summary>
    /// Creates a new configuration builder.
    /// </summary>
    public static StorageConfigurationBuilder Builder()
    {
        return new StorageConfigurationBuilder();
    }

    public override string ToString()
    {
        return $"{GetType().Name}:\n" +
               $"ChannelCountProvider: {ChannelCountProvider}\n" +
               $"HousekeepingController: {HousekeepingController}\n" +
               $"EntityCacheEvaluator: {EntityCacheEvaluator}\n" +
               $"FileProvider: {FileProvider}\n" +
               $"DataFileEvaluator: {DataFileEvaluator}\n" +
               $"BackupSetup: {BackupSetup?.ToString() ?? "null"}";
    }
}

/// <summary>
/// Builder for creating storage configurations with fluent API.
/// </summary>
public class StorageConfigurationBuilder
{
    private IStorageChannelCountProvider? _channelCountProvider;
    private IStorageHousekeepingController? _housekeepingController;
    private IStorageEntityCacheEvaluator? _entityCacheEvaluator;
    private IStorageLiveFileProvider? _fileProvider;
    private IStorageDataFileEvaluator? _dataFileEvaluator;
    private IStorageBackupSetup? _backupSetup;

    public StorageConfigurationBuilder SetChannelCountProvider(IStorageChannelCountProvider? channelCountProvider)
    {
        _channelCountProvider = channelCountProvider;
        return this;
    }

    public StorageConfigurationBuilder SetHousekeepingController(IStorageHousekeepingController? housekeepingController)
    {
        _housekeepingController = housekeepingController;
        return this;
    }

    public StorageConfigurationBuilder SetEntityCacheEvaluator(IStorageEntityCacheEvaluator? entityCacheEvaluator)
    {
        _entityCacheEvaluator = entityCacheEvaluator;
        return this;
    }

    public StorageConfigurationBuilder SetFileProvider(IStorageLiveFileProvider? fileProvider)
    {
        _fileProvider = fileProvider;
        return this;
    }

    public StorageConfigurationBuilder SetDataFileEvaluator(IStorageDataFileEvaluator? dataFileEvaluator)
    {
        _dataFileEvaluator = dataFileEvaluator;
        return this;
    }

    public StorageConfigurationBuilder SetBackupSetup(IStorageBackupSetup? backupSetup)
    {
        _backupSetup = backupSetup;
        return this;
    }

    public StorageConfiguration CreateConfiguration()
    {
        return new StorageConfiguration(
            _channelCountProvider ?? Storage.ChannelCountProvider(),
            _housekeepingController ?? Storage.HousekeepingController(),
            _entityCacheEvaluator ?? Storage.EntityCacheEvaluator(),
            _fileProvider ?? Storage.FileProvider(),
            _dataFileEvaluator ?? Storage.DataFileEvaluator(),
            _backupSetup
        );
    }
}

/// <summary>
/// Default implementation of storage channel count provider.
/// </summary>
public class StorageChannelCountProvider : IStorageChannelCountProvider
{
    public int ChannelCount { get; }

    public StorageChannelCountProvider(int channelCount)
    {
        if (channelCount <= 0)
            throw new ArgumentException("Channel count must be greater than 0", nameof(channelCount));
        
        if (!IsPowerOfTwo(channelCount))
            throw new ArgumentException("Channel count must be a power of 2", nameof(channelCount));

        ChannelCount = channelCount;
    }

    private static bool IsPowerOfTwo(int value)
    {
        return value > 0 && (value & (value - 1)) == 0;
    }

    public static StorageChannelCountProvider New() => New(Environment.ProcessorCount);

    public static StorageChannelCountProvider New(int channelCount) => new(channelCount);

    public override string ToString() => $"ChannelCount: {ChannelCount}";
}

/// <summary>
/// Default implementation of storage housekeeping controller.
/// </summary>
public class StorageHousekeepingController : IStorageHousekeepingController
{
    public long HousekeepingIntervalMs { get; }
    public long HousekeepingTimeBudgetNs { get; }
    public long GarbageCollectionTimeBudgetNs { get; }
    public long FileCheckTimeBudgetNs { get; }
    public long LiveCheckTimeBudgetNs { get; }

    public StorageHousekeepingController(
        long housekeepingIntervalMs,
        long housekeepingTimeBudgetNs,
        long garbageCollectionTimeBudgetNs,
        long fileCheckTimeBudgetNs,
        long liveCheckTimeBudgetNs)
    {
        if (housekeepingIntervalMs <= 0)
            throw new ArgumentException("Housekeeping interval must be greater than 0", nameof(housekeepingIntervalMs));
        if (housekeepingTimeBudgetNs <= 0)
            throw new ArgumentException("Housekeeping time budget must be greater than 0", nameof(housekeepingTimeBudgetNs));

        HousekeepingIntervalMs = housekeepingIntervalMs;
        HousekeepingTimeBudgetNs = housekeepingTimeBudgetNs;
        GarbageCollectionTimeBudgetNs = garbageCollectionTimeBudgetNs;
        FileCheckTimeBudgetNs = fileCheckTimeBudgetNs;
        LiveCheckTimeBudgetNs = liveCheckTimeBudgetNs;
    }

    public static StorageHousekeepingController New() => New(1000, 10_000_000); // 1 second interval, 10ms budget

    public static StorageHousekeepingController New(long housekeepingIntervalMs, long housekeepingTimeBudgetNs)
    {
        var gcBudget = housekeepingTimeBudgetNs / 4;
        var fileCheckBudget = housekeepingTimeBudgetNs / 4;
        var liveCheckBudget = housekeepingTimeBudgetNs / 4;
        
        return new StorageHousekeepingController(
            housekeepingIntervalMs,
            housekeepingTimeBudgetNs,
            gcBudget,
            fileCheckBudget,
            liveCheckBudget);
    }

    public override string ToString() => 
        $"HousekeepingInterval: {HousekeepingIntervalMs}ms, TimeBudget: {HousekeepingTimeBudgetNs}ns";
}

/// <summary>
/// Default implementation of storage entity cache evaluator.
/// </summary>
public class StorageEntityCacheEvaluator : IStorageEntityCacheEvaluator
{
    public long TimeoutMs { get; }
    public long Threshold { get; }

    public StorageEntityCacheEvaluator(long timeoutMs, long threshold)
    {
        if (timeoutMs <= 0)
            throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutMs));
        if (threshold <= 0)
            throw new ArgumentException("Threshold must be greater than 0", nameof(threshold));

        TimeoutMs = timeoutMs;
        Threshold = threshold;
    }

    public bool ShouldClearFromCache(long entityAge, long entitySize, long currentCacheSize)
    {
        // Clear if entity is older than timeout
        if (entityAge > TimeoutMs)
            return true;

        // Clear based on size and age product relative to cache size
        var sizeAgeProduct = entitySize * entityAge;
        var cacheThreshold = currentCacheSize * Threshold;
        
        return sizeAgeProduct > cacheThreshold;
    }

    public static StorageEntityCacheEvaluator New() => New(86_400_000, 1000); // 24 hours, threshold 1000

    public static StorageEntityCacheEvaluator New(long timeoutMs) => New(timeoutMs, 1000);

    public static StorageEntityCacheEvaluator New(long timeoutMs, long threshold) => new(timeoutMs, threshold);

    public override string ToString() => $"Timeout: {TimeoutMs}ms, Threshold: {Threshold}";
}

/// <summary>
/// Default implementation of storage live file provider.
/// </summary>
public class StorageLiveFileProvider : IStorageLiveFileProvider
{
    public string StorageDirectory { get; }
    public string DataDirectory { get; }
    public string TransactionDirectory { get; }
    public string TypeDictionaryDirectory { get; }
    public string DataFileNamePattern { get; }
    public string TransactionFileNamePattern { get; }
    public string DataFileExtension { get; }
    public string TransactionFileExtension { get; }

    public StorageLiveFileProvider(
        string storageDirectory,
        string dataFileNamePattern = "channel_{0}_{1}.dat",
        string transactionFileNamePattern = "transactions_{0}.log",
        string dataFileExtension = ".dat",
        string transactionFileExtension = ".log")
    {
        if (string.IsNullOrWhiteSpace(storageDirectory))
            throw new ArgumentException("Storage directory cannot be null or empty", nameof(storageDirectory));

        StorageDirectory = Path.GetFullPath(storageDirectory);
        DataDirectory = Path.Combine(StorageDirectory, "data");
        TransactionDirectory = Path.Combine(StorageDirectory, "transactions");
        TypeDictionaryDirectory = Path.Combine(StorageDirectory, "types");
        DataFileNamePattern = dataFileNamePattern;
        TransactionFileNamePattern = transactionFileNamePattern;
        DataFileExtension = dataFileExtension;
        TransactionFileExtension = transactionFileExtension;
    }

    public static StorageLiveFileProvider New() => New("storage");

    public static StorageLiveFileProvider New(string storageDirectory) => new(storageDirectory);

    public override string ToString() => $"StorageDirectory: {StorageDirectory}";
}

/// <summary>
/// Default implementation of storage data file evaluator.
/// </summary>
public class StorageDataFileEvaluator : IStorageDataFileEvaluator
{
    public int FileMinimumSize { get; }
    public int FileMaximumSize { get; }
    public double MinimumUseRatio { get; }
    public bool CleanUpHeadFile { get; }
    public int TransactionFileMaximumSize { get; }

    public StorageDataFileEvaluator(
        int fileMinimumSize,
        int fileMaximumSize,
        double minimumUseRatio,
        bool cleanUpHeadFile,
        int transactionFileMaximumSize)
    {
        if (fileMinimumSize <= 0)
            throw new ArgumentException("File minimum size must be greater than 0", nameof(fileMinimumSize));
        if (fileMaximumSize <= fileMinimumSize)
            throw new ArgumentException("File maximum size must be greater than minimum size", nameof(fileMaximumSize));
        if (minimumUseRatio <= 0.0 || minimumUseRatio > 1.0)
            throw new ArgumentException("Minimum use ratio must be between 0.0 and 1.0", nameof(minimumUseRatio));
        if (transactionFileMaximumSize <= 0)
            throw new ArgumentException("Transaction file maximum size must be greater than 0", nameof(transactionFileMaximumSize));

        FileMinimumSize = fileMinimumSize;
        FileMaximumSize = fileMaximumSize;
        MinimumUseRatio = minimumUseRatio;
        CleanUpHeadFile = cleanUpHeadFile;
        TransactionFileMaximumSize = transactionFileMaximumSize;
    }

    public bool ShouldCleanUpFile(long fileSize, long usedSize, bool isHeadFile)
    {
        if (isHeadFile && !CleanUpHeadFile)
            return false;

        if (fileSize < FileMinimumSize)
            return true;

        if (fileSize > FileMaximumSize)
            return true;

        var useRatio = (double)usedSize / fileSize;
        return useRatio < MinimumUseRatio;
    }

    public static StorageDataFileEvaluator New() =>
        New(1024 * 1024, 8 * 1024 * 1024, 0.75, false, 64 * 1024 * 1024); // 1MB min, 8MB max, 75% ratio, 64MB tx

    public static StorageDataFileEvaluator New(int fileMinimumSize, int fileMaximumSize) =>
        New(fileMinimumSize, fileMaximumSize, 0.75, false, 64 * 1024 * 1024);

    public static StorageDataFileEvaluator New(int fileMinimumSize, int fileMaximumSize, double minimumUseRatio) =>
        New(fileMinimumSize, fileMaximumSize, minimumUseRatio, false, 64 * 1024 * 1024);

    public static StorageDataFileEvaluator New(int fileMinimumSize, int fileMaximumSize, double minimumUseRatio, bool cleanUpHeadFile) =>
        New(fileMinimumSize, fileMaximumSize, minimumUseRatio, cleanUpHeadFile, 64 * 1024 * 1024);

    public static StorageDataFileEvaluator New(
        int fileMinimumSize,
        int fileMaximumSize,
        double minimumUseRatio,
        bool cleanUpHeadFile,
        int transactionFileMaximumSize) =>
        new(fileMinimumSize, fileMaximumSize, minimumUseRatio, cleanUpHeadFile, transactionFileMaximumSize);

    public override string ToString() =>
        $"FileSize: {FileMinimumSize}-{FileMaximumSize}, UseRatio: {MinimumUseRatio:P}, CleanHead: {CleanUpHeadFile}";
}

/// <summary>
/// Default implementation of storage backup setup.
/// </summary>
public class StorageBackupSetup : IStorageBackupSetup
{
    public IStorageBackupFileProvider BackupFileProvider { get; }
    public bool IsEnabled { get; }

    public StorageBackupSetup(IStorageBackupFileProvider backupFileProvider, bool isEnabled = true)
    {
        BackupFileProvider = backupFileProvider ?? throw new ArgumentNullException(nameof(backupFileProvider));
        IsEnabled = isEnabled;
    }

    public static StorageBackupSetup New(string backupDirectory) =>
        New(StorageBackupFileProvider.New(backupDirectory));

    public static StorageBackupSetup New(IStorageBackupFileProvider backupFileProvider) =>
        new(backupFileProvider);

    public override string ToString() => $"BackupEnabled: {IsEnabled}, Provider: {BackupFileProvider}";
}

/// <summary>
/// Default implementation of storage backup file provider.
/// </summary>
public class StorageBackupFileProvider : IStorageBackupFileProvider
{
    public string BackupDirectory { get; }
    public string BackupFileNamePattern { get; }
    public string BackupFileExtension { get; }

    public StorageBackupFileProvider(
        string backupDirectory,
        string backupFileNamePattern = "backup_{0:yyyyMMdd_HHmmss}",
        string backupFileExtension = ".bak")
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

        BackupDirectory = Path.GetFullPath(backupDirectory);
        BackupFileNamePattern = backupFileNamePattern;
        BackupFileExtension = backupFileExtension;
    }

    public static StorageBackupFileProvider New(string backupDirectory) => new(backupDirectory);

    public override string ToString() => $"BackupDirectory: {BackupDirectory}";
}
