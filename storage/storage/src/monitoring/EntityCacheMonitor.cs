namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Represents a storage entity cache for monitoring purposes.
/// This is a placeholder interface that will be implemented when the actual cache system is developed.
/// </summary>
public interface IStorageEntityCache
{
    /// <summary>
    /// Gets the channel index for this cache.
    /// </summary>
    int ChannelIndex { get; }

    /// <summary>
    /// Gets the timestamp of the last cache sweep start.
    /// </summary>
    long LastSweepStart { get; }

    /// <summary>
    /// Gets the timestamp of the last cache sweep end.
    /// </summary>
    long LastSweepEnd { get; }

    /// <summary>
    /// Gets the number of entities in the cache.
    /// </summary>
    long EntityCount { get; }

    /// <summary>
    /// Gets the cache size in bytes.
    /// </summary>
    long CacheSize { get; }
}

/// <summary>
/// Monitors entity cache metrics and provides monitoring data.
/// Replaces the Java EntityCacheMonitor class.
/// </summary>
public class EntityCacheMonitor : IEntityCacheMonitor
{
    private readonly WeakReference<IStorageEntityCache> _storageEntityCache;
    private readonly int _channelIndex;

    /// <summary>
    /// Initializes a new instance of the EntityCacheMonitor class.
    /// </summary>
    /// <param name="storageEntityCache">The storage entity cache to monitor</param>
    public EntityCacheMonitor(IStorageEntityCache storageEntityCache)
    {
        _storageEntityCache = new WeakReference<IStorageEntityCache>(storageEntityCache ?? throw new ArgumentNullException(nameof(storageEntityCache)));
        _channelIndex = storageEntityCache.ChannelIndex;
    }

    /// <summary>
    /// Gets the name of this monitor.
    /// </summary>
    public string Name => $"channel=channel-{_channelIndex},group=Entity cache";

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    public int ChannelIndex => _channelIndex;

    /// <summary>
    /// Gets the timestamp of the last start of a cache sweep.
    /// </summary>
    public long LastSweepStart
    {
        get
        {
            if (_storageEntityCache.TryGetTarget(out var cache))
            {
                return cache.LastSweepStart;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the timestamp of the last end of a cache sweep.
    /// </summary>
    public long LastSweepEnd
    {
        get
        {
            if (_storageEntityCache.TryGetTarget(out var cache))
            {
                return cache.LastSweepEnd;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the number of entries in the channel's entity cache.
    /// </summary>
    public long EntityCount
    {
        get
        {
            if (_storageEntityCache.TryGetTarget(out var cache))
            {
                return cache.EntityCount;
            }
            return 0;
        }
    }

    /// <summary>
    /// Gets the used cache size in bytes.
    /// </summary>
    public long UsedCacheSize
    {
        get
        {
            if (_storageEntityCache.TryGetTarget(out var cache))
            {
                return cache.CacheSize;
            }
            return 0;
        }
    }
}
