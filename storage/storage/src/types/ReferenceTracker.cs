using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of reference tracker.
/// Tracks references between entities for garbage collection.
/// </summary>
public class ReferenceTracker : IReferenceTracker
{
    private readonly ConcurrentDictionary<long, ConcurrentHashSet<long>> _outgoingReferences = new();
    private readonly ConcurrentDictionary<long, ConcurrentHashSet<long>> _incomingReferences = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public long TotalReferenceCount
    {
        get
        {
            return _outgoingReferences.Values.Sum(refs => refs.Count);
        }
    }

    public void AddReference(long sourceId, long targetId)
    {
        ThrowIfDisposed();

        // Add outgoing reference
        var outgoingRefs = _outgoingReferences.GetOrAdd(sourceId, _ => new ConcurrentHashSet<long>());
        outgoingRefs.Add(targetId);

        // Add incoming reference
        var incomingRefs = _incomingReferences.GetOrAdd(targetId, _ => new ConcurrentHashSet<long>());
        incomingRefs.Add(sourceId);
    }

    public void RemoveReference(long sourceId, long targetId)
    {
        ThrowIfDisposed();

        // Remove outgoing reference
        if (_outgoingReferences.TryGetValue(sourceId, out var outgoingRefs))
        {
            outgoingRefs.TryRemove(targetId);
            if (outgoingRefs.Count == 0)
            {
                _outgoingReferences.TryRemove(sourceId, out _);
            }
        }

        // Remove incoming reference
        if (_incomingReferences.TryGetValue(targetId, out var incomingRefs))
        {
            incomingRefs.TryRemove(sourceId);
            if (incomingRefs.Count == 0)
            {
                _incomingReferences.TryRemove(targetId, out _);
            }
        }
    }

    public IEnumerable<long> GetReferences(long objectId)
    {
        ThrowIfDisposed();

        if (_outgoingReferences.TryGetValue(objectId, out var references))
        {
            return references.ToList();
        }

        return Enumerable.Empty<long>();
    }

    public IEnumerable<long> GetReferencedBy(long objectId)
    {
        ThrowIfDisposed();

        if (_incomingReferences.TryGetValue(objectId, out var references))
        {
            return references.ToList();
        }

        return Enumerable.Empty<long>();
    }

    public void RemoveAllReferences(long objectId)
    {
        ThrowIfDisposed();

        // Remove all outgoing references
        if (_outgoingReferences.TryRemove(objectId, out var outgoingRefs))
        {
            foreach (var targetId in outgoingRefs.ToList())
            {
                if (_incomingReferences.TryGetValue(targetId, out var incomingRefs))
                {
                    incomingRefs.TryRemove(objectId);
                    if (incomingRefs.Count == 0)
                    {
                        _incomingReferences.TryRemove(targetId, out _);
                    }
                }
            }
        }

        // Remove all incoming references
        if (_incomingReferences.TryRemove(objectId, out var incomingRefsToRemove))
        {
            foreach (var sourceId in incomingRefsToRemove.ToList())
            {
                if (_outgoingReferences.TryGetValue(sourceId, out var outgoingRefsToUpdate))
                {
                    outgoingRefsToUpdate.TryRemove(objectId);
                    if (outgoingRefsToUpdate.Count == 0)
                    {
                        _outgoingReferences.TryRemove(sourceId, out _);
                    }
                }
            }
        }
    }

    public HashSet<long> AnalyzeReachability(IEnumerable<long> rootIds)
    {
        ThrowIfDisposed();

        var reachable = new HashSet<long>();
        var toVisit = new Queue<long>();
        var visited = new HashSet<long>();

        // Start with all root objects
        foreach (var rootId in rootIds)
        {
            if (!visited.Contains(rootId))
            {
                toVisit.Enqueue(rootId);
                visited.Add(rootId);
            }
        }

        // Breadth-first traversal of the reference graph
        while (toVisit.Count > 0)
        {
            var currentId = toVisit.Dequeue();
            reachable.Add(currentId);

            // Add all referenced objects to the queue
            var references = GetReferences(currentId);
            foreach (var referencedId in references)
            {
                if (!visited.Contains(referencedId))
                {
                    toVisit.Enqueue(referencedId);
                    visited.Add(referencedId);
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// Analyzes reachability with depth limit.
    /// </summary>
    /// <param name="rootIds">Root object IDs</param>
    /// <param name="maxDepth">Maximum traversal depth</param>
    /// <returns>Set of reachable entity IDs</returns>
    public HashSet<long> AnalyzeReachability(IEnumerable<long> rootIds, int maxDepth)
    {
        ThrowIfDisposed();

        var reachable = new HashSet<long>();
        var toVisit = new Queue<(long objectId, int depth)>();
        var visited = new HashSet<long>();

        // Start with all root objects at depth 0
        foreach (var rootId in rootIds)
        {
            if (!visited.Contains(rootId))
            {
                toVisit.Enqueue((rootId, 0));
                visited.Add(rootId);
            }
        }

        // Breadth-first traversal with depth limit
        while (toVisit.Count > 0)
        {
            var (currentId, currentDepth) = toVisit.Dequeue();
            reachable.Add(currentId);

            // Only continue if we haven't reached max depth
            if (currentDepth < maxDepth)
            {
                var references = GetReferences(currentId);
                foreach (var referencedId in references)
                {
                    if (!visited.Contains(referencedId))
                    {
                        toVisit.Enqueue((referencedId, currentDepth + 1));
                        visited.Add(referencedId);
                    }
                }
            }
        }

        return reachable;
    }

    /// <summary>
    /// Finds strongly connected components in the reference graph.
    /// Useful for detecting circular references.
    /// </summary>
    /// <returns>List of strongly connected components</returns>
    public List<List<long>> FindStronglyConnectedComponents()
    {
        ThrowIfDisposed();

        var allNodes = _outgoingReferences.Keys.Union(_incomingReferences.Keys).ToHashSet();
        var visited = new HashSet<long>();
        var stack = new Stack<long>();
        var components = new List<List<long>>();

        // First DFS to fill the stack
        foreach (var node in allNodes)
        {
            if (!visited.Contains(node))
            {
                DfsForStack(node, visited, stack);
            }
        }

        // Create transpose graph
        var transposeGraph = CreateTransposeGraph();

        // Second DFS on transpose graph
        visited.Clear();
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!visited.Contains(node))
            {
                var component = new List<long>();
                DfsForComponent(node, visited, component, transposeGraph);
                if (component.Count > 0)
                {
                    components.Add(component);
                }
            }
        }

        return components;
    }

    private void DfsForStack(long node, HashSet<long> visited, Stack<long> stack)
    {
        visited.Add(node);

        var references = GetReferences(node);
        foreach (var neighbor in references)
        {
            if (!visited.Contains(neighbor))
            {
                DfsForStack(neighbor, visited, stack);
            }
        }

        stack.Push(node);
    }

    private void DfsForComponent(long node, HashSet<long> visited, List<long> component, 
        ConcurrentDictionary<long, ConcurrentHashSet<long>> transposeGraph)
    {
        visited.Add(node);
        component.Add(node);

        if (transposeGraph.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors.ToList())
            {
                if (!visited.Contains(neighbor))
                {
                    DfsForComponent(neighbor, visited, component, transposeGraph);
                }
            }
        }
    }

    private ConcurrentDictionary<long, ConcurrentHashSet<long>> CreateTransposeGraph()
    {
        var transpose = new ConcurrentDictionary<long, ConcurrentHashSet<long>>();

        foreach (var kvp in _outgoingReferences)
        {
            var sourceId = kvp.Key;
            var targets = kvp.Value;

            foreach (var targetId in targets.ToList())
            {
                var transposeRefs = transpose.GetOrAdd(targetId, _ => new ConcurrentHashSet<long>());
                transposeRefs.Add(sourceId);
            }
        }

        return transpose;
    }

    /// <summary>
    /// Gets statistics about the reference graph.
    /// </summary>
    /// <returns>Reference graph statistics</returns>
    public ReferenceGraphStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var totalNodes = _outgoingReferences.Keys.Union(_incomingReferences.Keys).Count();
        var totalReferences = TotalReferenceCount;
        var averageOutDegree = totalNodes > 0 ? (double)totalReferences / totalNodes : 0;
        
        var maxOutDegree = _outgoingReferences.Values.Any() ? 
            _outgoingReferences.Values.Max(refs => refs.Count) : 0;
        
        var maxInDegree = _incomingReferences.Values.Any() ? 
            _incomingReferences.Values.Max(refs => refs.Count) : 0;

        return new ReferenceGraphStatistics
        {
            TotalNodes = totalNodes,
            TotalReferences = totalReferences,
            AverageOutDegree = averageOutDegree,
            MaxOutDegree = maxOutDegree,
            MaxInDegree = maxInDegree,
            NodesWithoutIncomingReferences = _outgoingReferences.Keys.Except(_incomingReferences.Keys).Count(),
            NodesWithoutOutgoingReferences = _incomingReferences.Keys.Except(_outgoingReferences.Keys).Count()
        };
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ReferenceTracker));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _outgoingReferences.Clear();
        _incomingReferences.Clear();
        _isDisposed = true;
    }
}

/// <summary>
/// Statistics about the reference graph.
/// </summary>
public class ReferenceGraphStatistics
{
    public long TotalNodes { get; set; }
    public long TotalReferences { get; set; }
    public double AverageOutDegree { get; set; }
    public int MaxOutDegree { get; set; }
    public int MaxInDegree { get; set; }
    public long NodesWithoutIncomingReferences { get; set; }
    public long NodesWithoutOutgoingReferences { get; set; }
}
