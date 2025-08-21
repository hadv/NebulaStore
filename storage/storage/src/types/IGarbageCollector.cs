using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Interface for storage garbage collection operations.
/// Provides automatic memory management, reference tracking, and cleanup.
/// </summary>
public interface IGarbageCollector : IDisposable
{
    /// <summary>
    /// Gets whether the garbage collector is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets whether garbage collection is currently in progress.
    /// </summary>
    bool IsCollecting { get; }

    /// <summary>
    /// Gets the garbage collection configuration.
    /// </summary>
    IGarbageCollectionConfiguration Configuration { get; }

    /// <summary>
    /// Starts the garbage collector.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the garbage collector.
    /// </summary>
    void Stop();

    /// <summary>
    /// Issues a full garbage collection immediately.
    /// </summary>
    /// <returns>Garbage collection result</returns>
    IGarbageCollectionResult IssueFullCollection();

    /// <summary>
    /// Issues a garbage collection with a time budget.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if collection completed within budget</returns>
    bool IssueCollection(long timeBudgetNanos);

    /// <summary>
    /// Issues a garbage collection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Garbage collection result</returns>
    Task<IGarbageCollectionResult> IssueCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entity as reachable from a root.
    /// </summary>
    /// <param name="objectId">The object ID</param>
    void MarkReachable(long objectId);

    /// <summary>
    /// Marks multiple entities as reachable from roots.
    /// </summary>
    /// <param name="objectIds">The object IDs</param>
    void MarkReachable(IEnumerable<long> objectIds);

    /// <summary>
    /// Adds a root object for garbage collection analysis.
    /// </summary>
    /// <param name="objectId">The root object ID</param>
    void AddRoot(long objectId);

    /// <summary>
    /// Removes a root object from garbage collection analysis.
    /// </summary>
    /// <param name="objectId">The root object ID</param>
    void RemoveRoot(long objectId);

    /// <summary>
    /// Gets all current root objects.
    /// </summary>
    /// <returns>Collection of root object IDs</returns>
    IEnumerable<long> GetRoots();

    /// <summary>
    /// Finds orphaned entities (not reachable from any root).
    /// </summary>
    /// <returns>Collection of orphaned entity IDs</returns>
    IEnumerable<long> FindOrphanedEntities();

    /// <summary>
    /// Gets garbage collection statistics.
    /// </summary>
    /// <returns>GC statistics</returns>
    IGarbageCollectionStatistics GetStatistics();

    /// <summary>
    /// Event raised when garbage collection starts.
    /// </summary>
    event EventHandler<GarbageCollectionEventArgs>? CollectionStarted;

    /// <summary>
    /// Event raised when garbage collection completes.
    /// </summary>
    event EventHandler<GarbageCollectionEventArgs>? CollectionCompleted;

    /// <summary>
    /// Event raised when orphaned entities are detected.
    /// </summary>
    event EventHandler<OrphanedEntitiesEventArgs>? OrphanedEntitiesDetected;
}

/// <summary>
/// Interface for garbage collection configuration.
/// </summary>
public interface IGarbageCollectionConfiguration
{
    /// <summary>
    /// Gets the garbage collection interval.
    /// </summary>
    TimeSpan CollectionInterval { get; }

    /// <summary>
    /// Gets the time budget per collection cycle.
    /// </summary>
    TimeSpan TimeBudget { get; }

    /// <summary>
    /// Gets whether adaptive time budgeting is enabled.
    /// </summary>
    bool AdaptiveTimeBudget { get; }

    /// <summary>
    /// Gets the threshold for increasing time budget.
    /// </summary>
    TimeSpan IncreaseThreshold { get; }

    /// <summary>
    /// Gets the amount to increase time budget by.
    /// </summary>
    TimeSpan IncreaseAmount { get; }

    /// <summary>
    /// Gets the maximum time budget.
    /// </summary>
    TimeSpan MaximumTimeBudget { get; }

    /// <summary>
    /// Gets whether to collect orphaned entities automatically.
    /// </summary>
    bool AutoCollectOrphans { get; }

    /// <summary>
    /// Gets the minimum age for orphaned entities before collection.
    /// </summary>
    TimeSpan OrphanAgeThreshold { get; }

    /// <summary>
    /// Gets whether to perform deep reference analysis.
    /// </summary>
    bool DeepReferenceAnalysis { get; }

    /// <summary>
    /// Gets the maximum depth for reference traversal.
    /// </summary>
    int MaxReferenceDepth { get; }
}

/// <summary>
/// Interface for garbage collection results.
/// </summary>
public interface IGarbageCollectionResult
{
    /// <summary>
    /// Gets whether the collection completed successfully.
    /// </summary>
    bool Success { get; }

    /// <summary>
    /// Gets the collection start time.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the collection end time.
    /// </summary>
    DateTime EndTime { get; }

    /// <summary>
    /// Gets the collection duration.
    /// </summary>
    TimeSpan Duration { get; }

    /// <summary>
    /// Gets the number of entities examined.
    /// </summary>
    long EntitiesExamined { get; }

    /// <summary>
    /// Gets the number of entities marked as reachable.
    /// </summary>
    long EntitiesMarked { get; }

    /// <summary>
    /// Gets the number of orphaned entities found.
    /// </summary>
    long OrphanedEntitiesFound { get; }

    /// <summary>
    /// Gets the number of entities collected (removed).
    /// </summary>
    long EntitiesCollected { get; }

    /// <summary>
    /// Gets the amount of storage space reclaimed.
    /// </summary>
    long StorageSpaceReclaimed { get; }

    /// <summary>
    /// Gets any errors that occurred during collection.
    /// </summary>
    IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Gets performance metrics for the collection.
    /// </summary>
    IGarbageCollectionMetrics Metrics { get; }
}

/// <summary>
/// Interface for garbage collection statistics.
/// </summary>
public interface IGarbageCollectionStatistics
{
    /// <summary>
    /// Gets the total number of collections performed.
    /// </summary>
    long TotalCollections { get; }

    /// <summary>
    /// Gets the total time spent in garbage collection.
    /// </summary>
    TimeSpan TotalCollectionTime { get; }

    /// <summary>
    /// Gets the average collection time.
    /// </summary>
    TimeSpan AverageCollectionTime { get; }

    /// <summary>
    /// Gets the total number of entities collected.
    /// </summary>
    long TotalEntitiesCollected { get; }

    /// <summary>
    /// Gets the total storage space reclaimed.
    /// </summary>
    long TotalStorageSpaceReclaimed { get; }

    /// <summary>
    /// Gets the last collection time.
    /// </summary>
    DateTime? LastCollectionTime { get; }

    /// <summary>
    /// Gets the last collection result.
    /// </summary>
    IGarbageCollectionResult? LastCollectionResult { get; }

    /// <summary>
    /// Gets the current number of root objects.
    /// </summary>
    long CurrentRootCount { get; }

    /// <summary>
    /// Gets the current number of reachable entities.
    /// </summary>
    long CurrentReachableEntities { get; }

    /// <summary>
    /// Gets the current number of orphaned entities.
    /// </summary>
    long CurrentOrphanedEntities { get; }

    /// <summary>
    /// Gets the collection efficiency (entities collected / entities examined).
    /// </summary>
    double CollectionEfficiency { get; }
}

/// <summary>
/// Interface for garbage collection performance metrics.
/// </summary>
public interface IGarbageCollectionMetrics
{
    /// <summary>
    /// Gets the time spent marking reachable entities.
    /// </summary>
    TimeSpan MarkingTime { get; }

    /// <summary>
    /// Gets the time spent sweeping orphaned entities.
    /// </summary>
    TimeSpan SweepingTime { get; }

    /// <summary>
    /// Gets the time spent analyzing references.
    /// </summary>
    TimeSpan ReferenceAnalysisTime { get; }

    /// <summary>
    /// Gets the time spent reclaiming storage.
    /// </summary>
    TimeSpan StorageReclaimTime { get; }

    /// <summary>
    /// Gets the peak memory usage during collection.
    /// </summary>
    long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets the number of reference traversals performed.
    /// </summary>
    long ReferenceTraversals { get; }

    /// <summary>
    /// Gets the maximum reference depth reached.
    /// </summary>
    int MaxReferenceDepthReached { get; }
}

/// <summary>
/// Interface for reference tracker.
/// Tracks references between entities for garbage collection.
/// </summary>
public interface IReferenceTracker : IDisposable
{
    /// <summary>
    /// Adds a reference between two entities.
    /// </summary>
    /// <param name="sourceId">Source entity ID</param>
    /// <param name="targetId">Target entity ID</param>
    void AddReference(long sourceId, long targetId);

    /// <summary>
    /// Removes a reference between two entities.
    /// </summary>
    /// <param name="sourceId">Source entity ID</param>
    /// <param name="targetId">Target entity ID</param>
    void RemoveReference(long sourceId, long targetId);

    /// <summary>
    /// Gets all entities referenced by the specified entity.
    /// </summary>
    /// <param name="objectId">The entity ID</param>
    /// <returns>Collection of referenced entity IDs</returns>
    IEnumerable<long> GetReferences(long objectId);

    /// <summary>
    /// Gets all entities that reference the specified entity.
    /// </summary>
    /// <param name="objectId">The entity ID</param>
    /// <returns>Collection of referencing entity IDs</returns>
    IEnumerable<long> GetReferencedBy(long objectId);

    /// <summary>
    /// Removes all references for the specified entity.
    /// </summary>
    /// <param name="objectId">The entity ID</param>
    void RemoveAllReferences(long objectId);

    /// <summary>
    /// Gets the total number of references tracked.
    /// </summary>
    long TotalReferenceCount { get; }

    /// <summary>
    /// Performs reference graph analysis starting from roots.
    /// </summary>
    /// <param name="rootIds">Root entity IDs</param>
    /// <returns>Set of reachable entity IDs</returns>
    HashSet<long> AnalyzeReachability(IEnumerable<long> rootIds);
}

/// <summary>
/// Event arguments for garbage collection events.
/// </summary>
public class GarbageCollectionEventArgs : EventArgs
{
    public GarbageCollectionEventArgs(IGarbageCollectionResult result)
    {
        Result = result;
    }

    public IGarbageCollectionResult Result { get; }
}

/// <summary>
/// Event arguments for orphaned entities detection.
/// </summary>
public class OrphanedEntitiesEventArgs : EventArgs
{
    public OrphanedEntitiesEventArgs(IEnumerable<long> orphanedEntityIds)
    {
        OrphanedEntityIds = orphanedEntityIds.ToList().AsReadOnly();
    }

    public IReadOnlyList<long> OrphanedEntityIds { get; }
}

/// <summary>
/// Enumeration of garbage collection phases.
/// </summary>
public enum GarbageCollectionPhase
{
    /// <summary>
    /// Not currently collecting.
    /// </summary>
    Idle,

    /// <summary>
    /// Marking reachable entities.
    /// </summary>
    Marking,

    /// <summary>
    /// Sweeping orphaned entities.
    /// </summary>
    Sweeping,

    /// <summary>
    /// Analyzing references.
    /// </summary>
    ReferenceAnalysis,

    /// <summary>
    /// Reclaiming storage space.
    /// </summary>
    StorageReclaim,

    /// <summary>
    /// Finalizing collection.
    /// </summary>
    Finalizing
}

/// <summary>
/// Enumeration of garbage collection strategies.
/// </summary>
public enum GarbageCollectionStrategy
{
    /// <summary>
    /// Mark and sweep collection.
    /// </summary>
    MarkAndSweep,

    /// <summary>
    /// Incremental collection.
    /// </summary>
    Incremental,

    /// <summary>
    /// Generational collection.
    /// </summary>
    Generational,

    /// <summary>
    /// Concurrent collection.
    /// </summary>
    Concurrent
}
