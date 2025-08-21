using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of storage garbage collector.
/// Provides automatic memory management, reference tracking, and cleanup.
/// </summary>
public class GarbageCollector : IGarbageCollector
{
    private readonly IGarbageCollectionConfiguration _configuration;
    private readonly IReferenceTracker _referenceTracker;
    private readonly IStorageChannelManager _channelManager;
    private readonly ConcurrentHashSet<long> _rootObjects = new();
    private readonly ConcurrentHashSet<long> _reachableObjects = new();
    private readonly Timer? _collectionTimer;
    private readonly object _collectionLock = new();
    private volatile bool _isRunning;
    private volatile bool _isCollecting;
    private volatile GarbageCollectionPhase _currentPhase = GarbageCollectionPhase.Idle;
    private GarbageCollectionStatistics _statistics = new();
    private bool _isDisposed;

    public GarbageCollector(
        IGarbageCollectionConfiguration configuration,
        IReferenceTracker referenceTracker,
        IStorageChannelManager channelManager)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _referenceTracker = referenceTracker ?? throw new ArgumentNullException(nameof(referenceTracker));
        _channelManager = channelManager ?? throw new ArgumentNullException(nameof(channelManager));

        _collectionTimer = new Timer(OnTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsRunning => _isRunning;
    public bool IsCollecting => _isCollecting;
    public IGarbageCollectionConfiguration Configuration => _configuration;

    public event EventHandler<GarbageCollectionEventArgs>? CollectionStarted;
    public event EventHandler<GarbageCollectionEventArgs>? CollectionCompleted;
    public event EventHandler<OrphanedEntitiesEventArgs>? OrphanedEntitiesDetected;

    public void Start()
    {
        ThrowIfDisposed();

        lock (_collectionLock)
        {
            if (_isRunning) return;

            _isRunning = true;
            _collectionTimer?.Change(_configuration.CollectionInterval, _configuration.CollectionInterval);
        }
    }

    public void Stop()
    {
        ThrowIfDisposed();

        lock (_collectionLock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _collectionTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            // Wait for current collection to complete
            while (_isCollecting)
            {
                Thread.Sleep(10);
            }
        }
    }

    public IGarbageCollectionResult IssueFullCollection()
    {
        ThrowIfDisposed();
        return PerformCollection(TimeSpan.MaxValue);
    }

    public bool IssueCollection(long timeBudgetNanos)
    {
        ThrowIfDisposed();
        var timeBudget = TimeSpan.FromTicks(timeBudgetNanos / 100); // Convert nanoseconds to ticks
        var result = PerformCollection(timeBudget);
        return result.Success;
    }

    public async Task<IGarbageCollectionResult> IssueCollectionAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return await Task.Run(() => IssueFullCollection(), cancellationToken);
    }

    public void MarkReachable(long objectId)
    {
        ThrowIfDisposed();
        _reachableObjects.Add(objectId);
    }

    public void MarkReachable(IEnumerable<long> objectIds)
    {
        ThrowIfDisposed();
        foreach (var objectId in objectIds)
        {
            _reachableObjects.Add(objectId);
        }
    }

    public void AddRoot(long objectId)
    {
        ThrowIfDisposed();
        _rootObjects.Add(objectId);
    }

    public void RemoveRoot(long objectId)
    {
        ThrowIfDisposed();
        _rootObjects.TryRemove(objectId);
    }

    public IEnumerable<long> GetRoots()
    {
        ThrowIfDisposed();
        return _rootObjects.ToList();
    }

    public IEnumerable<long> FindOrphanedEntities()
    {
        ThrowIfDisposed();

        // Perform reachability analysis
        var reachableFromRoots = _referenceTracker.AnalyzeReachability(_rootObjects);
        
        // Find entities that are not reachable from any root
        var allEntities = GetAllEntityIds();
        var orphaned = allEntities.Except(reachableFromRoots).ToList();

        if (orphaned.Any())
        {
            OrphanedEntitiesDetected?.Invoke(this, new OrphanedEntitiesEventArgs(orphaned));
        }

        return orphaned;
    }

    public IGarbageCollectionStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return _statistics;
    }

    private IGarbageCollectionResult PerformCollection(TimeSpan timeBudget)
    {
        lock (_collectionLock)
        {
            if (_isCollecting)
            {
                return new GarbageCollectionResult
                {
                    Success = false,
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                    Errors = new List<string> { "Collection already in progress" }
                };
            }

            _isCollecting = true;
            _currentPhase = GarbageCollectionPhase.Marking;
        }

        var stopwatch = Stopwatch.StartNew();
        var result = new GarbageCollectionResult
        {
            StartTime = DateTime.UtcNow,
            Success = true,
            Errors = new List<string>()
        };

        try
        {
            CollectionStarted?.Invoke(this, new GarbageCollectionEventArgs(result));

            // Phase 1: Mark reachable entities
            var markingStopwatch = Stopwatch.StartNew();
            var reachableEntities = PerformMarkingPhase();
            markingStopwatch.Stop();

            if (stopwatch.Elapsed > timeBudget)
            {
                result.Success = false;
                result.Errors.Add("Time budget exceeded during marking phase");
                return result;
            }

            // Phase 2: Sweep orphaned entities
            _currentPhase = GarbageCollectionPhase.Sweeping;
            var sweepingStopwatch = Stopwatch.StartNew();
            var orphanedEntities = PerformSweepingPhase(reachableEntities);
            sweepingStopwatch.Stop();

            if (stopwatch.Elapsed > timeBudget)
            {
                result.Success = false;
                result.Errors.Add("Time budget exceeded during sweeping phase");
                return result;
            }

            // Phase 3: Reclaim storage
            _currentPhase = GarbageCollectionPhase.StorageReclaim;
            var reclaimStopwatch = Stopwatch.StartNew();
            var spaceReclaimed = PerformStorageReclaimPhase(orphanedEntities);
            reclaimStopwatch.Stop();

            // Update result
            result.EntitiesExamined = GetAllEntityIds().Count();
            result.EntitiesMarked = reachableEntities.Count;
            result.OrphanedEntitiesFound = orphanedEntities.Count;
            result.EntitiesCollected = orphanedEntities.Count;
            result.StorageSpaceReclaimed = spaceReclaimed;

            // Update metrics
            result.Metrics = new GarbageCollectionMetrics
            {
                MarkingTime = markingStopwatch.Elapsed,
                SweepingTime = sweepingStopwatch.Elapsed,
                StorageReclaimTime = reclaimStopwatch.Elapsed,
                ReferenceAnalysisTime = markingStopwatch.Elapsed, // Included in marking
                PeakMemoryUsage = GC.GetTotalMemory(false),
                ReferenceTraversals = reachableEntities.Count,
                MaxReferenceDepthReached = _configuration.MaxReferenceDepth
            };

            // Update statistics
            _statistics.RecordCollection(result);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Collection failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;
            _currentPhase = GarbageCollectionPhase.Idle;
            _isCollecting = false;

            CollectionCompleted?.Invoke(this, new GarbageCollectionEventArgs(result));
        }

        return result;
    }

    private HashSet<long> PerformMarkingPhase()
    {
        // Start from root objects and mark all reachable entities
        var reachable = _referenceTracker.AnalyzeReachability(_rootObjects);
        
        // Also mark explicitly marked objects
        foreach (var objectId in _reachableObjects)
        {
            reachable.Add(objectId);
        }

        return reachable;
    }

    private List<long> PerformSweepingPhase(HashSet<long> reachableEntities)
    {
        var allEntities = GetAllEntityIds();
        var orphaned = allEntities.Except(reachableEntities).ToList();

        if (orphaned.Any())
        {
            OrphanedEntitiesDetected?.Invoke(this, new OrphanedEntitiesEventArgs(orphaned));
        }

        return orphaned;
    }

    private long PerformStorageReclaimPhase(List<long> orphanedEntities)
    {
        long spaceReclaimed = 0;

        foreach (var entityId in orphanedEntities)
        {
            // Remove references
            _referenceTracker.RemoveAllReferences(entityId);
            
            // TODO: Calculate actual space reclaimed
            spaceReclaimed += 1024; // Placeholder
        }

        return spaceReclaimed;
    }

    private IEnumerable<long> GetAllEntityIds()
    {
        // TODO: Get all entity IDs from storage channels
        // For now, return combination of roots and reachable objects
        return _rootObjects.Concat(_reachableObjects).Distinct();
    }

    private void OnTimerCallback(object? state)
    {
        if (_isRunning && !_isCollecting)
        {
            try
            {
                IssueCollection(_configuration.TimeBudget.Ticks * 100); // Convert to nanoseconds
            }
            catch (Exception)
            {
                // Log error but don't throw
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(GarbageCollector));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Stop();
        _collectionTimer?.Dispose();
        _referenceTracker.Dispose();
        _rootObjects.Clear();
        _reachableObjects.Clear();

        _isDisposed = true;
    }
}

/// <summary>
/// Thread-safe hash set implementation.
/// </summary>
internal class ConcurrentHashSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _dictionary = new();

    public void Add(T item) => _dictionary.TryAdd(item, 0);
    public bool TryRemove(T item) => _dictionary.TryRemove(item, out _);
    public bool Contains(T item) => _dictionary.ContainsKey(item);
    public void Clear() => _dictionary.Clear();
    public IEnumerable<T> ToList() => _dictionary.Keys.ToList();
    public int Count => _dictionary.Count;
}

/// <summary>
/// Default implementation of garbage collection configuration.
/// </summary>
public class GarbageCollectionConfiguration : IGarbageCollectionConfiguration
{
    public TimeSpan CollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TimeBudget { get; set; } = TimeSpan.FromMilliseconds(10);
    public bool AdaptiveTimeBudget { get; set; } = true;
    public TimeSpan IncreaseThreshold { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan IncreaseAmount { get; set; } = TimeSpan.FromMilliseconds(50);
    public TimeSpan MaximumTimeBudget { get; set; } = TimeSpan.FromMilliseconds(500);
    public bool AutoCollectOrphans { get; set; } = true;
    public TimeSpan OrphanAgeThreshold { get; set; } = TimeSpan.FromMinutes(5);
    public bool DeepReferenceAnalysis { get; set; } = true;
    public int MaxReferenceDepth { get; set; } = 100;
}

/// <summary>
/// Default implementation of garbage collection result.
/// </summary>
internal class GarbageCollectionResult : IGarbageCollectionResult
{
    public bool Success { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;
    public long EntitiesExamined { get; set; }
    public long EntitiesMarked { get; set; }
    public long OrphanedEntitiesFound { get; set; }
    public long EntitiesCollected { get; set; }
    public long StorageSpaceReclaimed { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = new List<string>();
    public IGarbageCollectionMetrics Metrics { get; set; } = new GarbageCollectionMetrics();
}

/// <summary>
/// Default implementation of garbage collection metrics.
/// </summary>
internal class GarbageCollectionMetrics : IGarbageCollectionMetrics
{
    public TimeSpan MarkingTime { get; set; }
    public TimeSpan SweepingTime { get; set; }
    public TimeSpan ReferenceAnalysisTime { get; set; }
    public TimeSpan StorageReclaimTime { get; set; }
    public long PeakMemoryUsage { get; set; }
    public long ReferenceTraversals { get; set; }
    public int MaxReferenceDepthReached { get; set; }
}

/// <summary>
/// Default implementation of garbage collection statistics.
/// </summary>
internal class GarbageCollectionStatistics : IGarbageCollectionStatistics
{
    private readonly object _lock = new();
    private readonly List<IGarbageCollectionResult> _recentResults = new();
    private long _totalCollections;
    private TimeSpan _totalCollectionTime;
    private long _totalEntitiesCollected;
    private long _totalStorageSpaceReclaimed;

    public long TotalCollections => _totalCollections;
    public TimeSpan TotalCollectionTime => _totalCollectionTime;
    public TimeSpan AverageCollectionTime => _totalCollections > 0 ?
        TimeSpan.FromTicks(_totalCollectionTime.Ticks / _totalCollections) : TimeSpan.Zero;
    public long TotalEntitiesCollected => _totalEntitiesCollected;
    public long TotalStorageSpaceReclaimed => _totalStorageSpaceReclaimed;
    public DateTime? LastCollectionTime => LastCollectionResult?.StartTime;
    public IGarbageCollectionResult? LastCollectionResult { get; private set; }
    public long CurrentRootCount { get; set; }
    public long CurrentReachableEntities { get; set; }
    public long CurrentOrphanedEntities { get; set; }
    public double CollectionEfficiency => _totalCollections > 0 && TotalEntitiesCollected > 0 ?
        (double)TotalEntitiesCollected / _totalCollections : 0.0;

    public void RecordCollection(IGarbageCollectionResult result)
    {
        lock (_lock)
        {
            _totalCollections++;
            _totalCollectionTime = _totalCollectionTime.Add(result.Duration);
            _totalEntitiesCollected += result.EntitiesCollected;
            _totalStorageSpaceReclaimed += result.StorageSpaceReclaimed;
            LastCollectionResult = result;

            _recentResults.Add(result);
            if (_recentResults.Count > 100) // Keep only last 100 results
            {
                _recentResults.RemoveAt(0);
            }
        }
    }
}
