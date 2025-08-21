using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Indexing;

/// <summary>
/// High-performance hash-based index implementation with concurrent access support.
/// </summary>
/// <typeparam name="TKey">Type of index keys</typeparam>
/// <typeparam name="TValue">Type of indexed values</typeparam>
public class HashIndex<TKey, TValue> : IIndex<TKey, TValue>
    where TKey : notnull
{
    private readonly string _name;
    private readonly IndexConfiguration _configuration;
    private readonly ConcurrentDictionary<TKey, IndexEntry<TValue>> _index;
    private readonly IndexStatistics _statistics;
    private readonly Timer? _maintenanceTimer;
    private volatile bool _isDisposed;

    public HashIndex(string name, IndexConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new IndexConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid index configuration", nameof(configuration));

        var concurrencyLevel = _configuration.EnableConcurrency ? _configuration.ConcurrencyLevel : 1;
        _index = new ConcurrentDictionary<TKey, IndexEntry<TValue>>(concurrencyLevel, _configuration.InitialCapacity);
        _statistics = new IndexStatistics(_configuration);

        // Set up maintenance timer for auto-rebuild
        if (_configuration.EnableAutoRebuild)
        {
            _maintenanceTimer = new Timer(PerformMaintenance, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
    }

    public string Name => _name;
    public IndexType Type => IndexType.Hash;
    public long Count => _index.Count;
    public bool IsUnique => _configuration.IsUnique;
    public IIndexStatistics Statistics => _statistics;

    public bool Put(TKey key, TValue value)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));

        var stopwatch = Stopwatch.StartNew();
        bool isNewEntry;

        try
        {
            if (_configuration.IsUnique)
            {
                var entry = new IndexEntry<TValue>(value);
                var existingEntry = _index.AddOrUpdate(key, entry, (k, existing) =>
                {
                    existing.UpdateValue(value);
                    return existing;
                });

                isNewEntry = ReferenceEquals(existingEntry, entry);
            }
            else
            {
                _index.AddOrUpdate(key,
                    new IndexEntry<TValue>(value),
                    (k, existing) =>
                    {
                        existing.AddValue(value);
                        return existing;
                    });
                isNewEntry = true; // For non-unique indexes, we always consider it a new entry
            }

            stopwatch.Stop();
            
            if (isNewEntry)
            {
                _statistics.RecordInsertion(stopwatch.Elapsed);
            }
            else
            {
                _statistics.RecordUpdate(stopwatch.Elapsed);
            }

            return isNewEntry;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public bool TryGet(TKey key, out TValue value)
    {
        ThrowIfDisposed();
        value = default(TValue)!;
        
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_index.TryGetValue(key, out var entry))
            {
                value = entry.GetFirstValue();
                stopwatch.Stop();
                _statistics.RecordLookup(stopwatch.Elapsed, true);
                return true;
            }

            stopwatch.Stop();
            _statistics.RecordLookup(stopwatch.Elapsed, false);
            return false;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public IEnumerable<TValue> GetAll(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return Enumerable.Empty<TValue>();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_index.TryGetValue(key, out var entry))
            {
                var values = entry.GetAllValues().ToList();
                stopwatch.Stop();
                _statistics.RecordLookup(stopwatch.Elapsed, true);
                return values;
            }

            stopwatch.Stop();
            _statistics.RecordLookup(stopwatch.Elapsed, false);
            return Enumerable.Empty<TValue>();
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public bool Remove(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var removed = _index.TryRemove(key, out _);
            stopwatch.Stop();
            
            if (removed)
            {
                _statistics.RecordDeletion(stopwatch.Elapsed);
            }

            return removed;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public bool Remove(TKey key, TValue value)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (_index.TryGetValue(key, out var entry))
            {
                var removed = entry.RemoveValue(value);
                
                // If entry is now empty, remove it from the index
                if (entry.IsEmpty)
                {
                    _index.TryRemove(key, out _);
                }

                stopwatch.Stop();
                
                if (removed)
                {
                    _statistics.RecordDeletion(stopwatch.Elapsed);
                }

                return removed;
            }

            stopwatch.Stop();
            return false;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public bool ContainsKey(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var contains = _index.ContainsKey(key);
            stopwatch.Stop();
            _statistics.RecordLookup(stopwatch.Elapsed, contains);
            return contains;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
    }

    public IEnumerable<TKey> GetKeys()
    {
        ThrowIfDisposed();
        return _index.Keys.ToList();
    }

    public IEnumerable<TValue> GetValues()
    {
        ThrowIfDisposed();
        return _index.Values.SelectMany(entry => entry.GetAllValues()).ToList();
    }

    public void Clear()
    {
        ThrowIfDisposed();
        _index.Clear();
        _statistics.Reset();
    }

    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            // For hash indexes, rebuilding mainly involves rehashing
            // This is automatically handled by ConcurrentDictionary
            // We could implement custom rehashing logic here if needed
            
            // Force garbage collection to clean up any fragmented memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
        }, cancellationToken);
    }

    private void PerformMaintenance(object? state)
    {
        try
        {
            if (_isDisposed) return;

            // Check if rebuild is needed
            var totalOperations = _statistics.TotalInsertions + _statistics.TotalDeletions + _statistics.TotalUpdates;
            if (totalOperations >= _configuration.RebuildThreshold)
            {
                _ = Task.Run(async () => await RebuildAsync());
            }

            // Check memory pressure
            var memoryUsage = _statistics.MemoryUsageBytes;
            var availableMemory = GC.GetTotalMemory(false);
            var memoryPressure = (double)memoryUsage / availableMemory;
            
            if (memoryPressure >= _configuration.MemoryPressureThreshold)
            {
                // Trigger garbage collection
                GC.Collect();
            }
        }
        catch
        {
            // Ignore maintenance errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(HashIndex<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _maintenanceTimer?.Dispose();
        _index.Clear();
    }
}

/// <summary>
/// Entry in the hash index that can hold single or multiple values.
/// </summary>
/// <typeparam name="TValue">Type of indexed values</typeparam>
internal class IndexEntry<TValue>
{
    private readonly object _lock = new();
    private readonly List<TValue> _values;

    public IndexEntry(TValue value)
    {
        _values = new List<TValue> { value };
    }

    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _values.Count == 0;
            }
        }
    }

    public TValue GetFirstValue()
    {
        lock (_lock)
        {
            return _values.Count > 0 ? _values[0] : default(TValue)!;
        }
    }

    public IEnumerable<TValue> GetAllValues()
    {
        lock (_lock)
        {
            return _values.ToList();
        }
    }

    public void AddValue(TValue value)
    {
        lock (_lock)
        {
            _values.Add(value);
        }
    }

    public void UpdateValue(TValue value)
    {
        lock (_lock)
        {
            _values.Clear();
            _values.Add(value);
        }
    }

    public bool RemoveValue(TValue value)
    {
        lock (_lock)
        {
            return _values.Remove(value);
        }
    }
}
