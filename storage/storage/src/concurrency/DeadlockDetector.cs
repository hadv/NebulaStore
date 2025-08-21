using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Advanced deadlock detector using wait-for graph analysis.
/// </summary>
public class DeadlockDetector : IDeadlockDetector
{
    private readonly string _name;
    private readonly ParallelProcessingConfiguration _configuration;
    private readonly ConcurrentDictionary<string, ResourceInfo> _resources;
    private readonly ConcurrentDictionary<int, ThreadInfo> _threads;
    private readonly ConcurrentDictionary<string, IResourceAcquisitionToken> _activeAcquisitions;
    private readonly DeadlockDetectionStatistics _statistics;
    private readonly Timer _detectionTimer;
    private volatile bool _isDisposed;

    public DeadlockDetector(string name, ParallelProcessingConfiguration configuration)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        
        _resources = new ConcurrentDictionary<string, ResourceInfo>();
        _threads = new ConcurrentDictionary<int, ThreadInfo>();
        _activeAcquisitions = new ConcurrentDictionary<string, IResourceAcquisitionToken>();
        _statistics = new DeadlockDetectionStatistics(_configuration);

        if (_configuration.EnableDeadlockDetection)
        {
            _detectionTimer = new Timer(PerformDeadlockDetection, null,
                _configuration.DeadlockDetectionInterval, _configuration.DeadlockDetectionInterval);
        }
    }

    public string Name => _name;
    public bool IsEnabled => _configuration.EnableDeadlockDetection;
    public IDeadlockDetectionStatistics Statistics => _statistics;

    public event EventHandler<DeadlockDetectedEventArgs>? DeadlockDetected;

    public void RegisterResource(string resourceId, string resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));
        if (string.IsNullOrWhiteSpace(resourceType)) throw new ArgumentException("Resource type cannot be null or empty", nameof(resourceType));

        _resources.TryAdd(resourceId, new ResourceInfo(resourceId, resourceType));
    }

    public IResourceAcquisitionToken AcquireResource(int threadId, string resourceId, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(resourceId)) throw new ArgumentException("Resource ID cannot be null or empty", nameof(resourceId));

        var token = new ResourceAcquisitionToken(threadId, resourceId, timeout);
        var tokenKey = $"{threadId}:{resourceId}";

        // Register thread if not exists
        _threads.TryAdd(threadId, new ThreadInfo(threadId));

        // Record acquisition
        _activeAcquisitions[tokenKey] = token;
        
        // Update thread state
        if (_threads.TryGetValue(threadId, out var threadInfo))
        {
            threadInfo.AddWaitingResource(resourceId);
        }

        _statistics.RecordAcquisition();
        return token;
    }

    public void ReleaseResource(IResourceAcquisitionToken token)
    {
        if (token == null) throw new ArgumentNullException(nameof(token));

        var tokenKey = $"{token.ThreadId}:{token.ResourceId}";
        
        if (_activeAcquisitions.TryRemove(tokenKey, out var removedToken))
        {
            // Update thread state
            if (_threads.TryGetValue(token.ThreadId, out var threadInfo))
            {
                threadInfo.RemoveWaitingResource(token.ResourceId);
                threadInfo.AddOwnedResource(token.ResourceId);
            }

            _statistics.RecordRelease(removedToken.HoldDuration);
        }
    }

    public IEnumerable<Deadlock> DetectDeadlocks()
    {
        if (!IsEnabled) return Enumerable.Empty<Deadlock>();

        var waitForGraph = BuildWaitForGraph();
        var cycles = DetectCycles(waitForGraph);
        
        return cycles.Select(cycle => new Deadlock(
            Guid.NewGuid().ToString("N"),
            "Circular Wait Deadlock",
            cycle.ToList(),
            GetInvolvedResources(cycle),
            DateTime.UtcNow,
            "Circular dependency detected in resource acquisition"
        ));
    }

    public DeadlockResolutionResult ResolveDeadlock(Deadlock deadlock)
    {
        if (deadlock == null) throw new ArgumentNullException(nameof(deadlock));

        try
        {
            // Simple resolution strategy: abort the youngest transaction
            var youngestThread = deadlock.InvolvedThreads.OrderByDescending(t => t).FirstOrDefault();
            
            if (youngestThread != 0 && _threads.TryGetValue(youngestThread, out var threadInfo))
            {
                // Force release all resources held by the youngest thread
                var releasedResources = new List<string>();
                
                foreach (var resourceId in threadInfo.OwnedResources.ToList())
                {
                    var tokenKey = $"{youngestThread}:{resourceId}";
                    if (_activeAcquisitions.TryRemove(tokenKey, out var token))
                    {
                        releasedResources.Add(resourceId);
                    }
                }

                threadInfo.ClearResources();
                _statistics.RecordDeadlockResolution();

                return new DeadlockResolutionResult(
                    true,
                    $"Aborted thread {youngestThread}",
                    releasedResources,
                    DateTime.UtcNow
                );
            }

            return new DeadlockResolutionResult(
                false,
                "No suitable thread found for abortion",
                new List<string>(),
                DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            return new DeadlockResolutionResult(
                false,
                $"Resolution failed: {ex.Message}",
                new List<string>(),
                DateTime.UtcNow
            );
        }
    }

    private Dictionary<int, List<int>> BuildWaitForGraph()
    {
        var graph = new Dictionary<int, List<int>>();

        // Build wait-for relationships
        foreach (var threadInfo in _threads.Values)
        {
            var threadId = threadInfo.ThreadId;
            graph[threadId] = new List<int>();

            // For each resource this thread is waiting for
            foreach (var waitingResource in threadInfo.WaitingResources)
            {
                // Find threads that own this resource
                var ownerThreads = _threads.Values
                    .Where(t => t.OwnedResources.Contains(waitingResource))
                    .Select(t => t.ThreadId);

                graph[threadId].AddRange(ownerThreads);
            }
        }

        return graph;
    }

    private IEnumerable<IEnumerable<int>> DetectCycles(Dictionary<int, List<int>> graph)
    {
        var cycles = new List<List<int>>();
        var visited = new HashSet<int>();
        var recursionStack = new HashSet<int>();

        foreach (var node in graph.Keys)
        {
            if (!visited.Contains(node))
            {
                var path = new List<int>();
                DetectCyclesRecursive(graph, node, visited, recursionStack, path, cycles);
            }
        }

        return cycles;
    }

    private bool DetectCyclesRecursive(
        Dictionary<int, List<int>> graph,
        int node,
        HashSet<int> visited,
        HashSet<int> recursionStack,
        List<int> path,
        List<List<int>> cycles)
    {
        visited.Add(node);
        recursionStack.Add(node);
        path.Add(node);

        if (graph.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    if (DetectCyclesRecursive(graph, neighbor, visited, recursionStack, path, cycles))
                        return true;
                }
                else if (recursionStack.Contains(neighbor))
                {
                    // Cycle detected
                    var cycleStart = path.IndexOf(neighbor);
                    var cycle = path.Skip(cycleStart).ToList();
                    cycles.Add(cycle);
                    return true;
                }
            }
        }

        recursionStack.Remove(node);
        path.RemoveAt(path.Count - 1);
        return false;
    }

    private List<string> GetInvolvedResources(IEnumerable<int> threadIds)
    {
        var resources = new HashSet<string>();
        
        foreach (var threadId in threadIds)
        {
            if (_threads.TryGetValue(threadId, out var threadInfo))
            {
                resources.UnionWith(threadInfo.WaitingResources);
                resources.UnionWith(threadInfo.OwnedResources);
            }
        }

        return resources.ToList();
    }

    private void PerformDeadlockDetection(object? state)
    {
        try
        {
            if (_isDisposed) return;

            var deadlocks = DetectDeadlocks().ToList();
            
            foreach (var deadlock in deadlocks)
            {
                _statistics.RecordDeadlockDetection();
                DeadlockDetected?.Invoke(this, new DeadlockDetectedEventArgs(deadlock));

                // Attempt automatic resolution
                var resolution = ResolveDeadlock(deadlock);
                if (!resolution.Successful)
                {
                    // Log resolution failure
                }
            }
        }
        catch
        {
            // Ignore detection errors
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _detectionTimer?.Dispose();
    }
}

/// <summary>
/// Represents a detected deadlock.
/// </summary>
public class Deadlock
{
    public Deadlock(
        string id,
        string name,
        IEnumerable<int> involvedThreads,
        IEnumerable<string> involvedResources,
        DateTime detectedAt,
        string description)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        InvolvedThreads = involvedThreads?.ToList() ?? new List<int>();
        InvolvedResources = involvedResources?.ToList() ?? new List<string>();
        DetectedAt = detectedAt;
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<int> InvolvedThreads { get; }
    public IReadOnlyList<string> InvolvedResources { get; }
    public DateTime DetectedAt { get; }
    public string Description { get; }

    public override string ToString()
    {
        return $"Deadlock '{Name}' [{DetectedAt:HH:mm:ss}]: " +
               $"{InvolvedThreads.Count} threads, {InvolvedResources.Count} resources - {Description}";
    }
}

/// <summary>
/// Result of deadlock resolution attempt.
/// </summary>
public class DeadlockResolutionResult
{
    public DeadlockResolutionResult(
        bool successful,
        string message,
        IEnumerable<string> releasedResources,
        DateTime resolvedAt)
    {
        Successful = successful;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        ReleasedResources = releasedResources?.ToList() ?? new List<string>();
        ResolvedAt = resolvedAt;
    }

    public bool Successful { get; }
    public string Message { get; }
    public IReadOnlyList<string> ReleasedResources { get; }
    public DateTime ResolvedAt { get; }

    public override string ToString()
    {
        var status = Successful ? "SUCCESS" : "FAILED";
        return $"Deadlock Resolution [{ResolvedAt:HH:mm:ss}]: {status} - {Message} " +
               $"(Released {ReleasedResources.Count} resources)";
    }
}

/// <summary>
/// Event arguments for deadlock detection.
/// </summary>
public class DeadlockDetectedEventArgs : EventArgs
{
    public DeadlockDetectedEventArgs(Deadlock deadlock)
    {
        Deadlock = deadlock ?? throw new ArgumentNullException(nameof(deadlock));
    }

    public Deadlock Deadlock { get; }
}

/// <summary>
/// Result of parallel execution with dependencies.
/// </summary>
public class ParallelExecutionResult
{
    public ParallelExecutionResult(
        IEnumerable<OperationResult> results,
        TimeSpan totalDuration,
        bool successful,
        string? errorMessage)
    {
        Results = results?.ToList() ?? new List<OperationResult>();
        TotalDuration = totalDuration;
        Successful = successful;
        ErrorMessage = errorMessage;
        Timestamp = DateTime.UtcNow;
    }

    public IReadOnlyList<OperationResult> Results { get; }
    public TimeSpan TotalDuration { get; }
    public bool Successful { get; }
    public string? ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public int SuccessfulOperations => Results.Count(r => r.Successful);
    public int FailedOperations => Results.Count(r => !r.Successful);

    public override string ToString()
    {
        var status = Successful ? "SUCCESS" : "FAILED";
        return $"Parallel Execution [{Timestamp:HH:mm:ss}]: {status} - " +
               $"{SuccessfulOperations}/{Results.Count} operations successful, " +
               $"Duration: {TotalDuration.TotalMilliseconds:F0}ms";
    }
}

/// <summary>
/// Result of an individual operation.
/// </summary>
public class OperationResult
{
    public OperationResult(
        string operationId,
        bool successful,
        object? result = null,
        string? errorMessage = null,
        TimeSpan? duration = null)
    {
        OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
        Successful = successful;
        Result = result;
        ErrorMessage = errorMessage;
        Duration = duration ?? TimeSpan.Zero;
        Timestamp = DateTime.UtcNow;
    }

    public string OperationId { get; }
    public bool Successful { get; }
    public object? Result { get; }
    public string? ErrorMessage { get; }
    public TimeSpan Duration { get; }
    public DateTime Timestamp { get; }

    public override string ToString()
    {
        var status = Successful ? "SUCCESS" : "FAILED";
        var error = !string.IsNullOrEmpty(ErrorMessage) ? $" - {ErrorMessage}" : "";
        return $"Operation '{OperationId}': {status} ({Duration.TotalMilliseconds:F0}ms){error}";
    }
}

/// <summary>
/// Operation execution context.
/// </summary>
public interface IOperationContext
{
    /// <summary>
    /// Gets results from previously executed operations.
    /// </summary>
    IReadOnlyDictionary<string, OperationResult> PreviousResults { get; }

    /// <summary>
    /// Gets a result from a specific operation.
    /// </summary>
    /// <param name="operationId">Operation identifier</param>
    /// <returns>Operation result, or null if not found</returns>
    OperationResult? GetResult(string operationId);
}

/// <summary>
/// Simple operation context implementation.
/// </summary>
public class OperationContext : IOperationContext
{
    public OperationContext(IReadOnlyDictionary<string, OperationResult> previousResults)
    {
        PreviousResults = previousResults ?? throw new ArgumentNullException(nameof(previousResults));
    }

    public IReadOnlyDictionary<string, OperationResult> PreviousResults { get; }

    public OperationResult? GetResult(string operationId)
    {
        return PreviousResults.TryGetValue(operationId, out var result) ? result : null;
    }
}

/// <summary>
/// Information about a resource in the system.
/// </summary>
internal class ResourceInfo
{
    public ResourceInfo(string id, string type)
    {
        Id = id;
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public string Id { get; }
    public string Type { get; }
    public DateTime CreatedAt { get; }
}

/// <summary>
/// Information about a thread in the system.
/// </summary>
internal class ThreadInfo
{
    private readonly object _lock = new();
    private readonly HashSet<string> _waitingResources = new();
    private readonly HashSet<string> _ownedResources = new();

    public ThreadInfo(int threadId)
    {
        ThreadId = threadId;
        CreatedAt = DateTime.UtcNow;
    }

    public int ThreadId { get; }
    public DateTime CreatedAt { get; }

    public IReadOnlySet<string> WaitingResources
    {
        get
        {
            lock (_lock)
            {
                return _waitingResources.ToHashSet();
            }
        }
    }

    public IReadOnlySet<string> OwnedResources
    {
        get
        {
            lock (_lock)
            {
                return _ownedResources.ToHashSet();
            }
        }
    }

    public void AddWaitingResource(string resourceId)
    {
        lock (_lock)
        {
            _waitingResources.Add(resourceId);
        }
    }

    public void RemoveWaitingResource(string resourceId)
    {
        lock (_lock)
        {
            _waitingResources.Remove(resourceId);
        }
    }

    public void AddOwnedResource(string resourceId)
    {
        lock (_lock)
        {
            _ownedResources.Add(resourceId);
        }
    }

    public void RemoveOwnedResource(string resourceId)
    {
        lock (_lock)
        {
            _ownedResources.Remove(resourceId);
        }
    }

    public void ClearResources()
    {
        lock (_lock)
        {
            _waitingResources.Clear();
            _ownedResources.Clear();
        }
    }
}

/// <summary>
/// Resource acquisition token implementation.
/// </summary>
public class ResourceAcquisitionToken : IResourceAcquisitionToken
{
    public ResourceAcquisitionToken(int threadId, string resourceId, TimeSpan timeout)
    {
        ThreadId = threadId;
        ResourceId = resourceId ?? throw new ArgumentNullException(nameof(resourceId));
        Timeout = timeout;
        AcquiredAt = DateTime.UtcNow;
    }

    public int ThreadId { get; }
    public string ResourceId { get; }
    public TimeSpan Timeout { get; }
    public DateTime AcquiredAt { get; }

    public TimeSpan HoldDuration => DateTime.UtcNow - AcquiredAt;
    public bool IsExpired => HoldDuration > Timeout;

    public override string ToString()
    {
        return $"ResourceToken[Thread={ThreadId}, Resource={ResourceId}, " +
               $"HoldTime={HoldDuration.TotalMilliseconds:F0}ms, Expired={IsExpired}]";
    }
}

/// <summary>
/// Interface for resource acquisition tokens.
/// </summary>
public interface IResourceAcquisitionToken
{
    int ThreadId { get; }
    string ResourceId { get; }
    TimeSpan Timeout { get; }
    DateTime AcquiredAt { get; }
    TimeSpan HoldDuration { get; }
    bool IsExpired { get; }
}

/// <summary>
/// Thread-safe statistics for deadlock detection.
/// </summary>
public class DeadlockDetectionStatistics : IDeadlockDetectionStatistics
{
    private readonly ParallelProcessingConfiguration _configuration;
    private long _totalAcquisitions;
    private long _totalReleases;
    private long _totalDeadlocksDetected;
    private long _totalDeadlocksResolved;
    private long _totalHoldTimeMs;

    public DeadlockDetectionStatistics(ParallelProcessingConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public long TotalAcquisitions => Interlocked.Read(ref _totalAcquisitions);
    public long TotalReleases => Interlocked.Read(ref _totalReleases);
    public long TotalDeadlocksDetected => Interlocked.Read(ref _totalDeadlocksDetected);
    public long TotalDeadlocksResolved => Interlocked.Read(ref _totalDeadlocksResolved);

    public double AverageResourceHoldTimeMs
    {
        get
        {
            var releases = TotalReleases;
            var holdTime = Interlocked.Read(ref _totalHoldTimeMs);
            return releases > 0 ? (double)holdTime / releases : 0.0;
        }
    }

    public int ActiveAcquisitions => (int)(TotalAcquisitions - TotalReleases);

    public void RecordAcquisition()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalAcquisitions);
        }
    }

    public void RecordRelease(TimeSpan holdDuration)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalReleases);
            Interlocked.Add(ref _totalHoldTimeMs, (long)holdDuration.TotalMilliseconds);
        }
    }

    public void RecordDeadlockDetection()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalDeadlocksDetected);
        }
    }

    public void RecordDeadlockResolution()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalDeadlocksResolved);
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalAcquisitions, 0);
        Interlocked.Exchange(ref _totalReleases, 0);
        Interlocked.Exchange(ref _totalDeadlocksDetected, 0);
        Interlocked.Exchange(ref _totalDeadlocksResolved, 0);
        Interlocked.Exchange(ref _totalHoldTimeMs, 0);
    }
}
