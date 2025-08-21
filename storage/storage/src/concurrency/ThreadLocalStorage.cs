using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// High-performance thread-local storage implementation with automatic cleanup and monitoring.
/// </summary>
/// <typeparam name="T">Type of data stored per thread</typeparam>
public class ThreadLocalStorage<T> : IThreadLocalStorage<T>
{
    private readonly string _name;
    private readonly ThreadLocalConfiguration _configuration;
    private readonly ConcurrentDictionary<int, ThreadLocalValue<T>> _values;
    private readonly ThreadLocalStatistics _statistics;
    private readonly Timer? _cleanupTimer;
    private volatile bool _isDisposed;

    public ThreadLocalStorage(string name, ThreadLocalConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new ThreadLocalConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid thread-local configuration", nameof(configuration));

        _values = new ConcurrentDictionary<int, ThreadLocalValue<T>>();
        _statistics = new ThreadLocalStatistics(_configuration);

        // Set up cleanup timer
        if (_configuration.EnableAutoCleanup)
        {
            _cleanupTimer = new Timer(PerformCleanup, null, 
                _configuration.CleanupInterval, _configuration.CleanupInterval);
        }
    }

    public string Name => _name;

    public T Value
    {
        get
        {
            ThrowIfDisposed();
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            if (_values.TryGetValue(threadId, out var threadValue))
            {
                _statistics.RecordAccess();
                return threadValue.Value;
            }

            throw new InvalidOperationException($"No value set for current thread {threadId}");
        }
        set
        {
            ThrowIfDisposed();
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            var threadValue = _values.AddOrUpdate(threadId,
                _ => new ThreadLocalValue<T>(value, _configuration.TrackValueLifetimes),
                (_, existing) => 
                {
                    existing.UpdateValue(value);
                    return existing;
                });

            if (threadValue.IsNewValue)
            {
                _statistics.RecordCreation();
            }
            else
            {
                _statistics.RecordAccess();
            }
        }
    }

    public bool HasValue
    {
        get
        {
            ThrowIfDisposed();
            var threadId = Thread.CurrentThread.ManagedThreadId;
            return _values.ContainsKey(threadId);
        }
    }

    public int ThreadCount => _values.Count;

    public IThreadLocalStatistics Statistics => _statistics;

    public T GetOrCreate(Func<T> valueFactory)
    {
        ThrowIfDisposed();
        if (valueFactory == null) throw new ArgumentNullException(nameof(valueFactory));

        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        var threadValue = _values.GetOrAdd(threadId, _ =>
        {
            var newValue = valueFactory();
            _statistics.RecordCreation();
            return new ThreadLocalValue<T>(newValue, _configuration.TrackValueLifetimes);
        });

        if (!threadValue.IsNewValue)
        {
            _statistics.RecordAccess();
        }

        return threadValue.Value;
    }

    public bool TryGetValue(out T value)
    {
        ThrowIfDisposed();
        value = default(T)!;
        
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        if (_values.TryGetValue(threadId, out var threadValue))
        {
            value = threadValue.Value;
            _statistics.RecordAccess();
            return true;
        }

        return false;
    }

    public bool RemoveValue()
    {
        ThrowIfDisposed();
        var threadId = Thread.CurrentThread.ManagedThreadId;
        
        if (_values.TryRemove(threadId, out var threadValue))
        {
            if (_configuration.TrackValueLifetimes)
            {
                _statistics.RecordRemoval(threadValue.Lifetime);
            }
            else
            {
                _statistics.RecordRemoval(TimeSpan.Zero);
            }

            // Dispose value if it implements IDisposable
            if (threadValue.Value is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            return true;
        }

        return false;
    }

    public IEnumerable<T> GetAllValues()
    {
        ThrowIfDisposed();
        return _values.Values.Select(tv => tv.Value).ToList();
    }

    public void Clear()
    {
        ThrowIfDisposed();
        
        var removedValues = new List<ThreadLocalValue<T>>();
        
        foreach (var kvp in _values.ToList())
        {
            if (_values.TryRemove(kvp.Key, out var threadValue))
            {
                removedValues.Add(threadValue);
            }
        }

        // Dispose removed values and record statistics
        foreach (var threadValue in removedValues)
        {
            if (_configuration.TrackValueLifetimes)
            {
                _statistics.RecordRemoval(threadValue.Lifetime);
            }
            else
            {
                _statistics.RecordRemoval(TimeSpan.Zero);
            }

            if (threadValue.Value is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }

    public int CleanupDeadThreads()
    {
        ThrowIfDisposed();
        
        var cleanedUp = 0;

        // Simple cleanup: remove values that haven't been accessed recently
        var cutoffTime = DateTime.UtcNow.Subtract(_configuration.CleanupInterval);
        var deadThreads = _values.Where(kvp => kvp.Value.LastAccessed < cutoffTime)
                                .Select(kvp => kvp.Key)
                                .ToList();
        
        foreach (var threadId in deadThreads)
        {
            if (_values.TryRemove(threadId, out var threadValue))
            {
                cleanedUp++;
                
                if (_configuration.TrackValueLifetimes)
                {
                    _statistics.RecordRemoval(threadValue.Lifetime);
                }
                else
                {
                    _statistics.RecordRemoval(TimeSpan.Zero);
                }

                if (threadValue.Value is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }
            }
        }

        return cleanedUp;
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            if (!_isDisposed)
            {
                CleanupDeadThreads();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ThreadLocalStorage<T>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _cleanupTimer?.Dispose();
        Clear();
    }
}

/// <summary>
/// Wrapper for thread-local values with metadata.
/// </summary>
/// <typeparam name="T">Type of the wrapped value</typeparam>
internal class ThreadLocalValue<T>
{
    private readonly DateTime _createdAt;
    private readonly bool _trackLifetime;
    private T _value;
    private bool _isNewValue;
    private DateTime _lastAccessed;

    public ThreadLocalValue(T value, bool trackLifetime)
    {
        _value = value;
        _trackLifetime = trackLifetime;
        _createdAt = trackLifetime ? DateTime.UtcNow : default;
        _lastAccessed = DateTime.UtcNow;
        _isNewValue = true;
    }

    public T Value => _value;

    public bool IsNewValue
    {
        get
        {
            var wasNew = _isNewValue;
            _isNewValue = false;
            return wasNew;
        }
    }

    public TimeSpan Lifetime => _trackLifetime ? DateTime.UtcNow - _createdAt : TimeSpan.Zero;

    public DateTime LastAccessed
    {
        get
        {
            _lastAccessed = DateTime.UtcNow;
            return _lastAccessed;
        }
    }

    public void UpdateValue(T newValue)
    {
        _value = newValue;
        _isNewValue = false;
    }
}

/// <summary>
/// Thread-safe statistics implementation for thread-local storage.
/// </summary>
public class ThreadLocalStatistics : IThreadLocalStatistics
{
    private readonly ThreadLocalConfiguration _configuration;
    private long _totalAccesses;
    private long _totalCreations;
    private long _totalRemovals;
    private long _totalLifetimeMs;
    private long _peakActiveThreads;

    public ThreadLocalStatistics(ThreadLocalConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public long TotalAccesses => Interlocked.Read(ref _totalAccesses);
    public long TotalCreations => Interlocked.Read(ref _totalCreations);
    public long TotalRemovals => Interlocked.Read(ref _totalRemovals);
    public int ActiveThreads => Environment.ProcessorCount; // Approximation
    public int PeakActiveThreads => (int)Interlocked.Read(ref _peakActiveThreads);

    public double AverageValueLifetimeMs
    {
        get
        {
            var removals = TotalRemovals;
            return removals > 0 ? (double)Interlocked.Read(ref _totalLifetimeMs) / removals : 0.0;
        }
    }

    public long EstimatedMemoryUsage
    {
        get
        {
            // Rough estimation: assume each value uses 1KB on average
            const long averageValueSize = 1024;
            return (TotalCreations - TotalRemovals) * averageValueSize;
        }
    }

    public void RecordAccess()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalAccesses);
        }
    }

    public void RecordCreation()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalCreations);
            UpdatePeakActiveThreads();
        }
    }

    public void RecordRemoval(TimeSpan lifetime)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalRemovals);
            
            if (_configuration.TrackValueLifetimes)
            {
                Interlocked.Add(ref _totalLifetimeMs, (long)lifetime.TotalMilliseconds);
            }
        }
    }

    private void UpdatePeakActiveThreads()
    {
        var currentActive = (int)(TotalCreations - TotalRemovals);
        var currentPeak = _peakActiveThreads;
        
        while (currentActive > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakActiveThreads, currentActive, currentPeak);
            if (originalPeak == currentPeak)
                break;
            currentPeak = originalPeak;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalAccesses, 0);
        Interlocked.Exchange(ref _totalCreations, 0);
        Interlocked.Exchange(ref _totalRemovals, 0);
        Interlocked.Exchange(ref _totalLifetimeMs, 0);
        Interlocked.Exchange(ref _peakActiveThreads, 0);
    }
}
