using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// High-performance thread-safe object pool with automatic sizing and cleanup.
/// </summary>
/// <typeparam name="T">Type of objects to pool</typeparam>
public class ObjectPool<T> : IObjectPool<T> where T : class
{
    private readonly string _name;
    private readonly IObjectFactory<T> _factory;
    private readonly ObjectPoolConfiguration _configuration;
    private readonly ConcurrentQueue<PooledObjectWrapper<T>> _objects;
    private readonly ObjectPoolStatistics _statistics;
    private readonly Timer? _autoSizingTimer;
    private readonly Timer? _cleanupTimer;
    private volatile int _count;
    private volatile int _inUseCount;
    private volatile bool _isDisposed;

    public ObjectPool(string name, IObjectFactory<T> factory, ObjectPoolConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _configuration = configuration ?? new ObjectPoolConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid object pool configuration", nameof(configuration));

        _objects = new ConcurrentQueue<PooledObjectWrapper<T>>();
        _statistics = new ObjectPoolStatistics(_configuration.MaxSize);

        // Preload initial objects
        if (_configuration.InitialSize > 0)
        {
            Preload(_configuration.InitialSize);
        }

        // Set up auto-sizing timer
        if (_configuration.EnableAutoSizing)
        {
            _autoSizingTimer = new Timer(PerformAutoSizing, null, 
                _configuration.AutoSizingInterval, _configuration.AutoSizingInterval);
        }

        // Set up cleanup timer
        if (_configuration.CleanupInterval > TimeSpan.Zero)
        {
            _cleanupTimer = new Timer(PerformCleanup, null, 
                _configuration.CleanupInterval, _configuration.CleanupInterval);
        }
    }

    public string Name => _name;
    public int Count => _count;
    public int MaxCapacity => _configuration.MaxSize;
    public int InUseCount => _inUseCount;
    public IObjectPoolStatistics Statistics => _statistics;

    public T Get()
    {
        ThrowIfDisposed();

        // Try to get from pool first
        while (_objects.TryDequeue(out var wrapper))
        {
            Interlocked.Decrement(ref _count);

            // Check if object is still valid
            if (!IsExpired(wrapper) && (_configuration.EnableValidation ? _factory.Validate(wrapper.Object) : true))
            {
                Interlocked.Increment(ref _inUseCount);
                _statistics.RecordRetrieved();
                return wrapper.Object;
            }
            else
            {
                // Object is expired or invalid, discard it
                DisposeWrapper(wrapper);
                _statistics.RecordDiscarded();
            }
        }

        // Pool is empty, create new object
        var newObject = _factory.Create();
        Interlocked.Increment(ref _inUseCount);
        _statistics.RecordCreated();
        return newObject;
    }

    public bool Return(T obj)
    {
        ThrowIfDisposed();
        if (obj == null) return false;

        Interlocked.Decrement(ref _inUseCount);

        // Check if pool is full
        if (_count >= _configuration.MaxSize)
        {
            _statistics.RecordDiscarded();
            return false;
        }

        // Reset object if possible
        if (!_factory.Reset(obj))
        {
            _statistics.RecordDiscarded();
            return false;
        }

        // Validate object if enabled
        if (_configuration.EnableValidation && !_factory.Validate(obj))
        {
            _statistics.RecordDiscarded();
            return false;
        }

        // Add to pool
        var wrapper = new PooledObjectWrapper<T>(obj);
        _objects.Enqueue(wrapper);
        Interlocked.Increment(ref _count);
        _statistics.RecordReturned();
        return true;
    }

    public void Clear()
    {
        ThrowIfDisposed();

        while (_objects.TryDequeue(out var wrapper))
        {
            DisposeWrapper(wrapper);
        }

        Interlocked.Exchange(ref _count, 0);
    }

    public int Trim(int targetSize)
    {
        ThrowIfDisposed();
        if (targetSize < 0) throw new ArgumentOutOfRangeException(nameof(targetSize));

        var removed = 0;
        var currentCount = _count;

        while (currentCount > targetSize && _objects.TryDequeue(out var wrapper))
        {
            DisposeWrapper(wrapper);
            Interlocked.Decrement(ref _count);
            removed++;
            currentCount = _count;
        }

        return removed;
    }

    public void Preload(int count)
    {
        ThrowIfDisposed();
        if (count <= 0) return;

        var targetCount = Math.Min(count, _configuration.MaxSize - _count);
        
        for (int i = 0; i < targetCount; i++)
        {
            try
            {
                var obj = _factory.Create();
                var wrapper = new PooledObjectWrapper<T>(obj);
                _objects.Enqueue(wrapper);
                Interlocked.Increment(ref _count);
                _statistics.RecordCreated();
            }
            catch
            {
                // Ignore preload errors
                break;
            }
        }
    }

    /// <summary>
    /// Gets a pooled object wrapper that automatically returns the object when disposed.
    /// </summary>
    /// <returns>Pooled object wrapper</returns>
    public PooledObject<T> GetPooledObject()
    {
        var obj = Get();
        return new PooledObject<T>(this, obj);
    }

    private void PerformAutoSizing(object? state)
    {
        try
        {
            if (_isDisposed) return;

            var utilization = _statistics.Utilization;
            var targetUtilization = _configuration.TargetUtilization;

            if (utilization > targetUtilization * 1.2) // 20% above target
            {
                // Pool is over-utilized, try to grow
                var growthSize = Math.Max(1, _count / 10); // Grow by 10%
                var maxGrowth = _configuration.MaxSize - _count;
                var actualGrowth = Math.Min(growthSize, maxGrowth);
                
                if (actualGrowth > 0)
                {
                    Preload(actualGrowth);
                }
            }
            else if (utilization < targetUtilization * 0.5) // 50% below target
            {
                // Pool is under-utilized, try to shrink
                var shrinkSize = Math.Max(1, _count / 20); // Shrink by 5%
                var minSize = Math.Max(_configuration.InitialSize, 1);
                var targetSize = Math.Max(minSize, _count - shrinkSize);
                
                Trim(targetSize);
            }
        }
        catch
        {
            // Ignore auto-sizing errors
        }
    }

    private void PerformCleanup(object? state)
    {
        try
        {
            if (_isDisposed) return;

            var expiredObjects = 0;
            var tempQueue = new ConcurrentQueue<PooledObjectWrapper<T>>();

            // Check all objects for expiration
            while (_objects.TryDequeue(out var wrapper))
            {
                if (IsExpired(wrapper))
                {
                    DisposeWrapper(wrapper);
                    expiredObjects++;
                }
                else
                {
                    tempQueue.Enqueue(wrapper);
                }
            }

            // Put non-expired objects back
            while (tempQueue.TryDequeue(out var wrapper))
            {
                _objects.Enqueue(wrapper);
            }

            // Update count
            Interlocked.Add(ref _count, -expiredObjects);
            
            if (expiredObjects > 0)
            {
                _statistics.RecordDiscarded(expiredObjects);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private bool IsExpired(PooledObjectWrapper<T> wrapper)
    {
        return DateTime.UtcNow - wrapper.CreatedAt > _configuration.MaxObjectAge;
    }

    private void DisposeWrapper(PooledObjectWrapper<T> wrapper)
    {
        if (wrapper.Object is IDisposable disposable)
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

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(ObjectPool<T>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _autoSizingTimer?.Dispose();
        _cleanupTimer?.Dispose();
        Clear();
    }
}

/// <summary>
/// Wrapper for pooled objects with metadata.
/// </summary>
/// <typeparam name="T">Type of wrapped object</typeparam>
internal class PooledObjectWrapper<T> where T : class
{
    public PooledObjectWrapper(T obj)
    {
        Object = obj;
        CreatedAt = DateTime.UtcNow;
    }

    public T Object { get; }
    public DateTime CreatedAt { get; }
}
