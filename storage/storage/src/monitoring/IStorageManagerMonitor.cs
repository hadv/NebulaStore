namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Interface that provides monitoring and metrics for a storage manager instance.
/// Replaces the Java StorageManagerMonitorMXBean interface.
/// </summary>
[MonitorDescription("Provides storage statistics and house keeping operations")]
public interface IStorageManagerMonitor : IMetricMonitor
{
    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    [MonitorDescription("query the storage for storage statistics. " +
                       "This will block storage operations until it is completed.")]
    StorageStatistics StorageStatistics { get; }

    /// <summary>
    /// Issues a full storage garbage collection.
    /// </summary>
    [MonitorDescription("issue a full storage garbage collection run. " +
                       "This will block storage operations until it is completed.")]
    void IssueFullGarbageCollection();

    /// <summary>
    /// Issues a full storage file check.
    /// </summary>
    [MonitorDescription("issue a full storage file check run. " +
                       "This will block storage operations until it is completed.")]
    void IssueFullFileCheck();

    /// <summary>
    /// Issues a full storage cache check.
    /// </summary>
    [MonitorDescription("issue a full storage cache check run. " +
                       "This will block storage operations until it is completed.")]
    void IssueFullCacheCheck();
}

/// <summary>
/// Represents file statistics for monitoring.
/// Replaces the Java StorageManagerMonitor.FileStatistics class.
/// </summary>
public class FileStatistics
{
    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the total data length in bytes.
    /// </summary>
    public long TotalDataLength { get; }

    /// <summary>
    /// Gets the live data length in bytes.
    /// </summary>
    public long LiveDataLength { get; }

    /// <summary>
    /// Initializes a new instance of the FileStatistics class.
    /// </summary>
    /// <param name="fileName">The file name</param>
    /// <param name="totalDataLength">The total data length</param>
    /// <param name="liveDataLength">The live data length</param>
    public FileStatistics(string fileName, long totalDataLength, long liveDataLength)
    {
        FileName = fileName ?? throw new System.ArgumentNullException(nameof(fileName));
        TotalDataLength = totalDataLength;
        LiveDataLength = liveDataLength;
    }
}

/// <summary>
/// Represents channel statistics for monitoring.
/// Replaces the Java StorageManagerMonitor.ChannelStatistics class.
/// </summary>
public class ChannelStatistics
{
    /// <summary>
    /// Gets the file count.
    /// </summary>
    public long FileCount { get; }

    /// <summary>
    /// Gets the total data length in bytes.
    /// </summary>
    public long TotalDataLength { get; }

    /// <summary>
    /// Gets the live data length in bytes.
    /// </summary>
    public long LiveDataLength { get; }

    /// <summary>
    /// Gets the file statistics.
    /// </summary>
    public IReadOnlyList<FileStatistics> FileStatistics { get; }

    /// <summary>
    /// Initializes a new instance of the ChannelStatistics class.
    /// </summary>
    /// <param name="fileCount">The file count</param>
    /// <param name="totalDataLength">The total data length</param>
    /// <param name="liveDataLength">The live data length</param>
    /// <param name="fileStatistics">The file statistics</param>
    public ChannelStatistics(long fileCount, long totalDataLength, long liveDataLength,
                           IReadOnlyList<FileStatistics> fileStatistics)
    {
        FileCount = fileCount;
        TotalDataLength = totalDataLength;
        LiveDataLength = liveDataLength;
        FileStatistics = fileStatistics ?? throw new System.ArgumentNullException(nameof(fileStatistics));
    }
}

/// <summary>
/// Represents storage statistics for monitoring.
/// Replaces the Java StorageManagerMonitor.StorageStatistics class.
/// </summary>
public class StorageStatistics
{
    /// <summary>
    /// Gets the channel count.
    /// </summary>
    public int ChannelCount { get; }

    /// <summary>
    /// Gets the file count.
    /// </summary>
    public long FileCount { get; }

    /// <summary>
    /// Gets the total data length in bytes.
    /// </summary>
    public long TotalDataLength { get; }

    /// <summary>
    /// Gets the live data length in bytes.
    /// </summary>
    public long LiveDataLength { get; }

    /// <summary>
    /// Gets the usage ratio (live data / total data).
    /// </summary>
    public double UsageRatio => TotalDataLength > 0 ? (double)LiveDataLength / TotalDataLength : 0.0;

    /// <summary>
    /// Gets the channel statistics.
    /// </summary>
    public IReadOnlyList<ChannelStatistics> ChannelStatistics { get; }

    /// <summary>
    /// Initializes a new instance of the StorageStatistics class.
    /// </summary>
    /// <param name="channelCount">The channel count</param>
    /// <param name="fileCount">The file count</param>
    /// <param name="totalDataLength">The total data length</param>
    /// <param name="liveDataLength">The live data length</param>
    /// <param name="channelStatistics">The channel statistics</param>
    public StorageStatistics(int channelCount, long fileCount, long totalDataLength,
                           long liveDataLength, IReadOnlyList<ChannelStatistics> channelStatistics)
    {
        ChannelCount = channelCount;
        FileCount = fileCount;
        TotalDataLength = totalDataLength;
        LiveDataLength = liveDataLength;
        ChannelStatistics = channelStatistics ?? throw new System.ArgumentNullException(nameof(channelStatistics));
    }
}
