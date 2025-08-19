using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface for storage managers that can be monitored.
/// This allows the monitoring system to work with different storage manager implementations.
/// </summary>
public interface IMonitorableStorageManager
{
    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    IEmbeddedStorageConfiguration Configuration { get; }

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    IStorageStatistics GetStatistics();

    /// <summary>
    /// Issues a full garbage collection.
    /// </summary>
    void IssueFullGarbageCollection();

    /// <summary>
    /// Issues a full file check.
    /// </summary>
    void IssueFullFileCheck();

    /// <summary>
    /// Issues a full cache check.
    /// </summary>
    void IssueFullCacheCheck();
}

/// <summary>
/// Monitors storage manager metrics and provides monitoring data.
/// Replaces the Java StorageManagerMonitor class.
/// </summary>
public class StorageManagerMonitor : IStorageManagerMonitor
{
    private readonly WeakReference<IMonitorableStorageManager> _storageManager;

    /// <summary>
    /// Initializes a new instance of the StorageManagerMonitor class.
    /// </summary>
    /// <param name="storageManager">The storage manager to monitor</param>
    public StorageManagerMonitor(IMonitorableStorageManager storageManager)
    {
        _storageManager = new WeakReference<IMonitorableStorageManager>(storageManager ?? throw new ArgumentNullException(nameof(storageManager)));
    }

    /// <summary>
    /// Gets the name of this monitor.
    /// </summary>
    public string Name => "name=EmbeddedStorage";

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    public StorageStatistics StorageStatistics
    {
        get
        {
            if (_storageManager.TryGetTarget(out var manager))
            {
                return CreateStorageStatistics(manager);
            }
            
            // Return empty statistics if manager is no longer available
            return new StorageStatistics(0, 0, 0, 0, Array.Empty<ChannelStatistics>());
        }
    }

    /// <summary>
    /// Issues a full storage garbage collection.
    /// </summary>
    public void IssueFullGarbageCollection()
    {
        if (_storageManager.TryGetTarget(out var manager))
        {
            manager.IssueFullGarbageCollection();
        }
    }

    /// <summary>
    /// Issues a full storage file check.
    /// </summary>
    public void IssueFullFileCheck()
    {
        if (_storageManager.TryGetTarget(out var manager))
        {
            manager.IssueFullFileCheck();
        }
    }

    /// <summary>
    /// Issues a full storage cache check.
    /// </summary>
    public void IssueFullCacheCheck()
    {
        if (_storageManager.TryGetTarget(out var manager))
        {
            manager.IssueFullCacheCheck();
        }
    }

    /// <summary>
    /// Creates storage statistics from the current storage manager state.
    /// </summary>
    /// <param name="manager">The storage manager</param>
    /// <returns>Storage statistics</returns>
    private StorageStatistics CreateStorageStatistics(IMonitorableStorageManager manager)
    {
        var config = manager.Configuration;
        var baseStats = manager.GetStatistics();
        
        // Create channel statistics based on configuration
        var channelStatistics = new List<ChannelStatistics>();
        
        for (int i = 0; i < config.ChannelCount; i++)
        {
            var channelFiles = GetChannelFileStatistics(config, i);
            var channelFileCount = channelFiles.Count;
            var channelTotalData = channelFiles.Sum(f => f.TotalDataLength);
            var channelLiveData = channelFiles.Sum(f => f.LiveDataLength);
            
            channelStatistics.Add(new ChannelStatistics(
                channelFileCount,
                channelTotalData,
                channelLiveData,
                channelFiles
            ));
        }

        return new StorageStatistics(
            config.ChannelCount,
            baseStats.DataFileCount + baseStats.TransactionFileCount,
            baseStats.TotalStorageSize,
            baseStats.LiveDataLength,
            channelStatistics
        );
    }

    /// <summary>
    /// Gets file statistics for a specific channel.
    /// </summary>
    /// <param name="config">The storage configuration</param>
    /// <param name="channelIndex">The channel index</param>
    /// <returns>List of file statistics</returns>
    private List<FileStatistics> GetChannelFileStatistics(IEmbeddedStorageConfiguration config, int channelIndex)
    {
        var fileStats = new List<FileStatistics>();
        
        try
        {
            var channelDir = Path.Combine(config.StorageDirectory, $"{config.ChannelDirectoryPrefix}{channelIndex}");
            
            if (Directory.Exists(channelDir))
            {
                var files = Directory.GetFiles(channelDir, "*" + config.DataFileSuffix)
                    .Concat(Directory.GetFiles(channelDir, "*" + config.TransactionFileSuffix));
                
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists)
                    {
                        // For now, assume live data equals total data
                        // This would be more sophisticated in a full implementation
                        var fileSize = fileInfo.Length;
                        fileStats.Add(new FileStatistics(
                            fileInfo.Name,
                            fileSize,
                            fileSize // Assuming all data is live for now
                        ));
                    }
                }
            }
        }
        catch
        {
            // Ignore errors when reading file statistics
        }
        
        return fileStats;
    }
}
