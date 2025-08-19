namespace NebulaStore.Storage.Monitoring;

/// <summary>
/// Represents the result of a storage channel housekeeping operation.
/// </summary>
public class StorageChannelHousekeepingResult
{
    /// <summary>
    /// Gets the duration of the operation in nanoseconds.
    /// </summary>
    public long Duration { get; }

    /// <summary>
    /// Gets the start time of the operation in milliseconds since epoch.
    /// </summary>
    public long StartTime { get; }

    /// <summary>
    /// Gets the result of the operation.
    /// </summary>
    public bool Result { get; }

    /// <summary>
    /// Gets the time budget for the operation in nanoseconds.
    /// </summary>
    public long Budget { get; }

    /// <summary>
    /// Initializes a new instance of the StorageChannelHousekeepingResult class.
    /// </summary>
    /// <param name="duration">The duration in nanoseconds</param>
    /// <param name="startTime">The start time in milliseconds since epoch</param>
    /// <param name="result">The operation result</param>
    /// <param name="budget">The time budget in nanoseconds</param>
    public StorageChannelHousekeepingResult(long duration, long startTime, bool result, long budget)
    {
        Duration = duration;
        StartTime = startTime;
        Result = result;
        Budget = budget;
    }
}

/// <summary>
/// Monitors storage channel housekeeping metrics and provides monitoring data.
/// Replaces the Java StorageChannelHousekeepingMonitor class.
/// </summary>
public class StorageChannelHousekeepingMonitor : IStorageChannelHousekeepingMonitor
{
    private readonly int _channelIndex;
    private StorageChannelHousekeepingResult? _fileCleanupCheckResult;
    private StorageChannelHousekeepingResult? _garbageCollectionResult;
    private StorageChannelHousekeepingResult? _entityCacheCheckResult;

    /// <summary>
    /// Initializes a new instance of the StorageChannelHousekeepingMonitor class.
    /// </summary>
    /// <param name="channelIndex">The channel index</param>
    public StorageChannelHousekeepingMonitor(int channelIndex)
    {
        _channelIndex = channelIndex;
    }

    /// <summary>
    /// Gets the name of this monitor.
    /// </summary>
    public string Name => $"channel=channel-{_channelIndex},group=housekeeping";

    /// <summary>
    /// Sets the file cleanup check result.
    /// </summary>
    /// <param name="fileCleanupCheckResult">The file cleanup check result</param>
    public void SetFileCleanupCheckResult(StorageChannelHousekeepingResult fileCleanupCheckResult)
    {
        _fileCleanupCheckResult = fileCleanupCheckResult;
    }

    /// <summary>
    /// Sets the garbage collection result.
    /// </summary>
    /// <param name="garbageCollectionResult">The garbage collection result</param>
    public void SetGarbageCollectionResult(StorageChannelHousekeepingResult garbageCollectionResult)
    {
        _garbageCollectionResult = garbageCollectionResult;
    }

    /// <summary>
    /// Sets the entity cache check result.
    /// </summary>
    /// <param name="entityCacheCheckResult">The entity cache check result</param>
    public void SetEntityCacheCheckResult(StorageChannelHousekeepingResult entityCacheCheckResult)
    {
        _entityCacheCheckResult = entityCacheCheckResult;
    }

    /// <summary>
    /// Gets the result of the last housekeeping cache check cycle.
    /// </summary>
    public bool EntityCacheCheckResult => _entityCacheCheckResult?.Result ?? false;

    /// <summary>
    /// Gets the starting time of the last housekeeping cache check cycle.
    /// </summary>
    public long EntityCacheCheckStartTime => _entityCacheCheckResult?.StartTime ?? 0;

    /// <summary>
    /// Gets the duration of the last housekeeping cache check cycle in nanoseconds.
    /// </summary>
    public long EntityCacheCheckDuration => _entityCacheCheckResult?.Duration ?? 0;

    /// <summary>
    /// Gets the time budget of the last housekeeping cache check cycle in nanoseconds.
    /// </summary>
    public long EntityCacheCheckBudget => _entityCacheCheckResult?.Budget ?? 0;

    /// <summary>
    /// Gets the result of the last housekeeping garbage collection cycle.
    /// </summary>
    public bool GarbageCollectionResult => _garbageCollectionResult?.Result ?? false;

    /// <summary>
    /// Gets the starting time of the last housekeeping garbage collection cycle.
    /// </summary>
    public long GarbageCollectionStartTime => _garbageCollectionResult?.StartTime ?? 0;

    /// <summary>
    /// Gets the duration of the last housekeeping garbage collection cycle in nanoseconds.
    /// </summary>
    public long GarbageCollectionDuration => _garbageCollectionResult?.Duration ?? 0;

    /// <summary>
    /// Gets the time budget of the last housekeeping garbage collection cycle in nanoseconds.
    /// </summary>
    public long GarbageCollectionBudget => _garbageCollectionResult?.Budget ?? 0;

    /// <summary>
    /// Gets the result of the last housekeeping file cleanup cycle.
    /// </summary>
    public bool FileCleanupCheckResult => _fileCleanupCheckResult?.Result ?? false;

    /// <summary>
    /// Gets the starting time of the last housekeeping file cleanup cycle.
    /// </summary>
    public long FileCleanupCheckStartTime => _fileCleanupCheckResult?.StartTime ?? 0;

    /// <summary>
    /// Gets the duration of the last housekeeping file cleanup cycle in nanoseconds.
    /// </summary>
    public long FileCleanupCheckDuration => _fileCleanupCheckResult?.Duration ?? 0;

    /// <summary>
    /// Gets the time budget of the last housekeeping file cleanup cycle in nanoseconds.
    /// </summary>
    public long FileCleanupCheckBudget => _fileCleanupCheckResult?.Budget ?? 0;
}
