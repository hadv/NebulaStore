using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Interface for storage channels.
/// A channel represents the unity of a thread, a storage directory, and cached data.
/// </summary>
public interface IStorageChannel : IDisposable
{
    /// <summary>
    /// Gets the channel identifier.
    /// </summary>
    int ChannelId { get; }

    /// <summary>
    /// Gets the channel directory path.
    /// </summary>
    string ChannelDirectory { get; }

    /// <summary>
    /// Gets whether the channel is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets whether the channel is currently processing operations.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Gets the thread associated with this channel.
    /// </summary>
    Thread? ChannelThread { get; }

    /// <summary>
    /// Starts the channel operations.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the channel operations.
    /// </summary>
    void Stop();

    /// <summary>
    /// Creates a storer for this specific channel.
    /// </summary>
    /// <returns>A channel-specific storer</returns>
    IChannelStorer CreateStorer();

    /// <summary>
    /// Gets channel-specific statistics.
    /// </summary>
    /// <returns>Channel statistics</returns>
    IChannelStatistics GetStatistics();

    /// <summary>
    /// Issues garbage collection for this channel.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if completed within budget</returns>
    bool IssueGarbageCollection(long timeBudgetNanos);

    /// <summary>
    /// Issues file cleanup for this channel.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if completed within budget</returns>
    bool IssueFileCleanup(long timeBudgetNanos);

    /// <summary>
    /// Issues cache cleanup for this channel.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if completed within budget</returns>
    bool IssueCacheCleanup(long timeBudgetNanos);
}

/// <summary>
/// Interface for channel-specific storer operations.
/// </summary>
public interface IChannelStorer : IStorer
{
    /// <summary>
    /// Gets the channel this storer belongs to.
    /// </summary>
    int ChannelId { get; }

    /// <summary>
    /// Gets the current load factor of this channel.
    /// </summary>
    double LoadFactor { get; }
}

/// <summary>
/// Interface for storage channel manager.
/// Manages multiple storage channels and load balancing.
/// </summary>
public interface IStorageChannelManager : IDisposable
{
    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// Gets all channels.
    /// </summary>
    IReadOnlyList<IStorageChannel> Channels { get; }

    /// <summary>
    /// Gets a specific channel by ID.
    /// </summary>
    /// <param name="channelId">The channel ID</param>
    /// <returns>The channel</returns>
    IStorageChannel GetChannel(int channelId);

    /// <summary>
    /// Gets the optimal channel for storing an entity.
    /// </summary>
    /// <param name="entity">The entity to store</param>
    /// <returns>The optimal channel</returns>
    IStorageChannel GetOptimalChannel(object entity);

    /// <summary>
    /// Gets the least loaded channel.
    /// </summary>
    /// <returns>The channel with the lowest load</returns>
    IStorageChannel GetLeastLoadedChannel();

    /// <summary>
    /// Distributes an entity to the appropriate channel.
    /// </summary>
    /// <param name="entity">The entity to distribute</param>
    /// <returns>The channel ID where the entity was assigned</returns>
    int DistributeEntity(object entity);

    /// <summary>
    /// Starts all channels.
    /// </summary>
    void StartAllChannels();

    /// <summary>
    /// Stops all channels.
    /// </summary>
    void StopAllChannels();

    /// <summary>
    /// Gets aggregated statistics from all channels.
    /// </summary>
    /// <returns>Aggregated channel statistics</returns>
    IAggregatedChannelStatistics GetAggregatedStatistics();

    /// <summary>
    /// Performs load balancing across channels.
    /// </summary>
    /// <returns>Number of entities redistributed</returns>
    int PerformLoadBalancing();

    /// <summary>
    /// Issues housekeeping operations across all channels.
    /// </summary>
    /// <param name="timeBudgetNanos">Total time budget in nanoseconds</param>
    /// <returns>True if all channels completed within budget</returns>
    bool IssueHousekeeping(long timeBudgetNanos);
}

/// <summary>
/// Interface for channel statistics.
/// </summary>
public interface IChannelStatistics
{
    /// <summary>
    /// Gets the channel ID.
    /// </summary>
    int ChannelId { get; }

    /// <summary>
    /// Gets the number of entities stored in this channel.
    /// </summary>
    long EntityCount { get; }

    /// <summary>
    /// Gets the total storage size for this channel.
    /// </summary>
    long StorageSize { get; }

    /// <summary>
    /// Gets the number of data files in this channel.
    /// </summary>
    int DataFileCount { get; }

    /// <summary>
    /// Gets the current load factor (0.0 to 1.0).
    /// </summary>
    double LoadFactor { get; }

    /// <summary>
    /// Gets the number of pending operations.
    /// </summary>
    long PendingOperations { get; }

    /// <summary>
    /// Gets the last housekeeping time.
    /// </summary>
    DateTime? LastHousekeeping { get; }

    /// <summary>
    /// Gets the cache hit ratio.
    /// </summary>
    double CacheHitRatio { get; }

    /// <summary>
    /// Gets the average operation time in milliseconds.
    /// </summary>
    double AverageOperationTime { get; }
}

/// <summary>
/// Interface for aggregated channel statistics.
/// </summary>
public interface IAggregatedChannelStatistics
{
    /// <summary>
    /// Gets the total number of entities across all channels.
    /// </summary>
    long TotalEntityCount { get; }

    /// <summary>
    /// Gets the total storage size across all channels.
    /// </summary>
    long TotalStorageSize { get; }

    /// <summary>
    /// Gets the total number of data files across all channels.
    /// </summary>
    int TotalDataFileCount { get; }

    /// <summary>
    /// Gets the average load factor across all channels.
    /// </summary>
    double AverageLoadFactor { get; }

    /// <summary>
    /// Gets the load distribution variance (measure of load balance).
    /// </summary>
    double LoadDistributionVariance { get; }

    /// <summary>
    /// Gets the total number of pending operations across all channels.
    /// </summary>
    long TotalPendingOperations { get; }

    /// <summary>
    /// Gets the overall cache hit ratio.
    /// </summary>
    double OverallCacheHitRatio { get; }

    /// <summary>
    /// Gets statistics for each individual channel.
    /// </summary>
    IReadOnlyList<IChannelStatistics> ChannelStatistics { get; }
}

/// <summary>
/// Interface for channel load balancing strategy.
/// </summary>
public interface IChannelLoadBalancer
{
    /// <summary>
    /// Determines the optimal channel for an entity.
    /// </summary>
    /// <param name="entity">The entity to place</param>
    /// <param name="channels">Available channels</param>
    /// <returns>The optimal channel</returns>
    IStorageChannel SelectChannel(object entity, IReadOnlyList<IStorageChannel> channels);

    /// <summary>
    /// Evaluates whether load balancing is needed.
    /// </summary>
    /// <param name="statistics">Current channel statistics</param>
    /// <returns>True if rebalancing is recommended</returns>
    bool ShouldRebalance(IAggregatedChannelStatistics statistics);

    /// <summary>
    /// Creates a rebalancing plan.
    /// </summary>
    /// <param name="statistics">Current channel statistics</param>
    /// <returns>Rebalancing plan</returns>
    IRebalancingPlan CreateRebalancingPlan(IAggregatedChannelStatistics statistics);
}

/// <summary>
/// Interface for channel rebalancing plan.
/// </summary>
public interface IRebalancingPlan
{
    /// <summary>
    /// Gets the planned entity movements.
    /// </summary>
    IReadOnlyList<EntityMovement> EntityMovements { get; }

    /// <summary>
    /// Gets the estimated improvement in load distribution.
    /// </summary>
    double EstimatedImprovement { get; }

    /// <summary>
    /// Executes the rebalancing plan.
    /// </summary>
    /// <returns>Number of entities actually moved</returns>
    int Execute();
}

/// <summary>
/// Represents a planned entity movement between channels.
/// </summary>
public record EntityMovement(long EntityId, int FromChannelId, int ToChannelId, string Reason);

/// <summary>
/// Enumeration of channel load balancing strategies.
/// </summary>
public enum ChannelLoadBalancingStrategy
{
    /// <summary>
    /// Round-robin distribution.
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Least loaded channel first.
    /// </summary>
    LeastLoaded,

    /// <summary>
    /// Hash-based distribution by entity type.
    /// </summary>
    HashByType,

    /// <summary>
    /// Hash-based distribution by entity ID.
    /// </summary>
    HashById,

    /// <summary>
    /// Weighted distribution based on channel capacity.
    /// </summary>
    WeightedCapacity,

    /// <summary>
    /// Custom strategy implementation.
    /// </summary>
    Custom
}
