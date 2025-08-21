using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// Interface for high-performance object pooling with automatic sizing and cleanup.
/// </summary>
/// <typeparam name="T">Type of objects to pool</typeparam>
public interface IObjectPool<T> : IDisposable where T : class
{
    /// <summary>
    /// Gets the pool name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the current number of objects in the pool.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum pool capacity.
    /// </summary>
    int MaxCapacity { get; }

    /// <summary>
    /// Gets the current number of objects in use.
    /// </summary>
    int InUseCount { get; }

    /// <summary>
    /// Gets pool statistics.
    /// </summary>
    IObjectPoolStatistics Statistics { get; }

    /// <summary>
    /// Gets an object from the pool or creates a new one.
    /// </summary>
    /// <returns>Pooled object</returns>
    T Get();

    /// <summary>
    /// Returns an object to the pool.
    /// </summary>
    /// <param name="obj">Object to return</param>
    /// <returns>True if the object was returned to the pool, false if discarded</returns>
    bool Return(T obj);

    /// <summary>
    /// Clears all objects from the pool.
    /// </summary>
    void Clear();

    /// <summary>
    /// Trims the pool to the specified size.
    /// </summary>
    /// <param name="targetSize">Target pool size</param>
    /// <returns>Number of objects removed</returns>
    int Trim(int targetSize);

    /// <summary>
    /// Preloads the pool with the specified number of objects.
    /// </summary>
    /// <param name="count">Number of objects to preload</param>
    void Preload(int count);
}

/// <summary>
/// Interface for object pool statistics.
/// </summary>
public interface IObjectPoolStatistics
{
    /// <summary>
    /// Gets the total number of objects created.
    /// </summary>
    long TotalCreated { get; }

    /// <summary>
    /// Gets the total number of objects retrieved from the pool.
    /// </summary>
    long TotalRetrieved { get; }

    /// <summary>
    /// Gets the total number of objects returned to the pool.
    /// </summary>
    long TotalReturned { get; }

    /// <summary>
    /// Gets the total number of objects discarded.
    /// </summary>
    long TotalDiscarded { get; }

    /// <summary>
    /// Gets the pool hit ratio (objects retrieved from pool vs created).
    /// </summary>
    double HitRatio { get; }

    /// <summary>
    /// Gets the current pool utilization.
    /// </summary>
    double Utilization { get; }

    /// <summary>
    /// Gets the peak pool size.
    /// </summary>
    int PeakSize { get; }

    /// <summary>
    /// Gets the peak in-use count.
    /// </summary>
    int PeakInUse { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Interface for object factory used by pools.
/// </summary>
/// <typeparam name="T">Type of objects to create</typeparam>
public interface IObjectFactory<T> where T : class
{
    /// <summary>
    /// Creates a new object instance.
    /// </summary>
    /// <returns>New object instance</returns>
    T Create();

    /// <summary>
    /// Resets an object to its initial state for reuse.
    /// </summary>
    /// <param name="obj">Object to reset</param>
    /// <returns>True if the object was successfully reset, false if it should be discarded</returns>
    bool Reset(T obj);

    /// <summary>
    /// Validates whether an object is suitable for pooling.
    /// </summary>
    /// <param name="obj">Object to validate</param>
    /// <returns>True if the object can be pooled, false otherwise</returns>
    bool Validate(T obj);
}

/// <summary>
/// Configuration for object pools.
/// </summary>
public class ObjectPoolConfiguration
{
    /// <summary>
    /// Gets or sets the initial pool size.
    /// </summary>
    public int InitialSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum pool size.
    /// </summary>
    public int MaxSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether to enable automatic pool sizing.
    /// </summary>
    public bool EnableAutoSizing { get; set; } = true;

    /// <summary>
    /// Gets or sets the auto-sizing interval.
    /// </summary>
    public TimeSpan AutoSizingInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the target utilization for auto-sizing.
    /// </summary>
    public double TargetUtilization { get; set; } = 0.8; // 80%

    /// <summary>
    /// Gets or sets whether to enable object validation.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets the cleanup interval for removing stale objects.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum object age before cleanup.
    /// </summary>
    public TimeSpan MaxObjectAge { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return InitialSize >= 0 &&
               MaxSize > 0 &&
               InitialSize <= MaxSize &&
               TargetUtilization > 0 && TargetUtilization <= 1.0 &&
               AutoSizingInterval > TimeSpan.Zero &&
               CleanupInterval > TimeSpan.Zero &&
               MaxObjectAge > TimeSpan.Zero;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public ObjectPoolConfiguration Clone()
    {
        return new ObjectPoolConfiguration
        {
            InitialSize = InitialSize,
            MaxSize = MaxSize,
            EnableAutoSizing = EnableAutoSizing,
            AutoSizingInterval = AutoSizingInterval,
            TargetUtilization = TargetUtilization,
            EnableValidation = EnableValidation,
            EnableStatistics = EnableStatistics,
            CleanupInterval = CleanupInterval,
            MaxObjectAge = MaxObjectAge
        };
    }

    public override string ToString()
    {
        return $"ObjectPoolConfiguration[InitialSize={InitialSize}, MaxSize={MaxSize}, " +
               $"AutoSizing={EnableAutoSizing}, TargetUtil={TargetUtilization:P1}, " +
               $"Validation={EnableValidation}, Statistics={EnableStatistics}]";
    }
}

/// <summary>
/// Default object factory that uses parameterless constructor.
/// </summary>
/// <typeparam name="T">Type of objects to create</typeparam>
public class DefaultObjectFactory<T> : IObjectFactory<T> where T : class, new()
{
    public virtual T Create()
    {
        return new T();
    }

    public virtual bool Reset(T obj)
    {
        // For objects that implement IResettable
        if (obj is IResettable resettable)
        {
            try
            {
                resettable.Reset();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // For other objects, assume they're always valid for reuse
        return true;
    }

    public virtual bool Validate(T obj)
    {
        return obj != null;
    }
}

/// <summary>
/// Interface for objects that can be reset for reuse.
/// </summary>
public interface IResettable
{
    /// <summary>
    /// Resets the object to its initial state.
    /// </summary>
    void Reset();
}

/// <summary>
/// Wrapper for pooled objects that automatically returns them to the pool when disposed.
/// </summary>
/// <typeparam name="T">Type of pooled object</typeparam>
public class PooledObject<T> : IDisposable where T : class
{
    private readonly IObjectPool<T> _pool;
    private T? _object;
    private bool _isDisposed;

    public PooledObject(IObjectPool<T> pool, T obj)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _object = obj ?? throw new ArgumentNullException(nameof(obj));
    }

    /// <summary>
    /// Gets the pooled object.
    /// </summary>
    public T Object
    {
        get
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PooledObject<T>));
            return _object!;
        }
    }

    /// <summary>
    /// Gets whether this pooled object has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    public void Dispose()
    {
        if (_isDisposed || _object == null)
            return;

        _isDisposed = true;
        _pool.Return(_object);
        _object = null;
    }
}
