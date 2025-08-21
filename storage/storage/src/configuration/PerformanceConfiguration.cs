using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Configuration;

/// <summary>
/// High-performance configuration system with dynamic updates and validation.
/// </summary>
public class PerformanceConfiguration : IPerformanceConfiguration
{
    private readonly string _name;
    private readonly ConcurrentDictionary<string, object?> _values;
    private readonly ConcurrentDictionary<string, ConfigurationValidator> _validators;
    private readonly ReaderWriterLockSlim _lock;
    private volatile int _version;
    private volatile bool _isDisposed;

    public PerformanceConfiguration(string name, bool supportsDynamicUpdates = true)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        SupportsDynamicUpdates = supportsDynamicUpdates;
        _values = new ConcurrentDictionary<string, object?>();
        _validators = new ConcurrentDictionary<string, ConfigurationValidator>();
        _lock = new ReaderWriterLockSlim();
        _version = 1;

        InitializeDefaults();
    }

    public string Name => _name;
    public int Version => _version;
    public bool SupportsDynamicUpdates { get; }

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public T GetValue<T>(string key, T defaultValue = default(T)!)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));

        _lock.EnterReadLock();
        try
        {
            if (_values.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            return defaultValue;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool SetValue<T>(string key, T value, bool validate = true)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (_isDisposed) return false;

        _lock.EnterWriteLock();
        try
        {
            // Validate the value if requested
            if (validate && _validators.TryGetValue(key, out var validator))
            {
                var validationResult = validator.Validate(value);
                if (!validationResult.IsValid)
                {
                    return false;
                }
            }

            var oldValue = _values.TryGetValue(key, out var existing) ? existing : default(T);
            _values[key] = value;
            
            // Increment version for dynamic updates
            if (SupportsDynamicUpdates)
            {
                Interlocked.Increment(ref _version);
            }

            // Fire change event
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs(key, oldValue, value));

            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IEnumerable<string> GetKeys()
    {
        _lock.EnterReadLock();
        try
        {
            return _values.Keys.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IReadOnlyDictionary<string, object?> GetAllValues()
    {
        _lock.EnterReadLock();
        try
        {
            return new Dictionary<string, object?>(_values);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public ConfigurationValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        _lock.EnterReadLock();
        try
        {
            foreach (var kvp in _values)
            {
                if (_validators.TryGetValue(kvp.Key, out var validator))
                {
                    var result = validator.Validate(kvp.Value);
                    if (!result.IsValid)
                    {
                        errors.AddRange(result.Errors);
                        warnings.AddRange(result.Warnings);
                    }
                }
            }

            return new ConfigurationValidationResult(errors.Count == 0, errors, warnings);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would reload from external source
        await Task.CompletedTask;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        // In a real implementation, this would save to external source
        await Task.CompletedTask;
    }

    public void ResetToDefaults()
    {
        _lock.EnterWriteLock();
        try
        {
            _values.Clear();
            InitializeDefaults();
            Interlocked.Increment(ref _version);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IConfigurationSnapshot CreateSnapshot()
    {
        _lock.EnterReadLock();
        try
        {
            var data = new Dictionary<string, object?>(_values);
            var metadata = new Dictionary<string, string>
            {
                ["Name"] = _name,
                ["Version"] = _version.ToString(),
                ["SupportsDynamicUpdates"] = SupportsDynamicUpdates.ToString()
            };

            return new ConfigurationSnapshot(DateTime.UtcNow, _version, data, metadata);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void RestoreFromSnapshot(IConfigurationSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        _lock.EnterWriteLock();
        try
        {
            _values.Clear();
            
            foreach (var kvp in snapshot.Data)
            {
                _values[kvp.Key] = kvp.Value;
            }

            _version = snapshot.Version;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds a validator for a configuration key.
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="validator">Validator</param>
    public void AddValidator(string key, ConfigurationValidator validator)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (validator == null) throw new ArgumentNullException(nameof(validator));

        _validators[key] = validator;
    }

    private void InitializeDefaults()
    {
        // Cache configuration
        _values["Cache.L1.MaxSize"] = 1000;
        _values["Cache.L1.TTL"] = TimeSpan.FromMinutes(30);
        _values["Cache.L2.MaxSize"] = 10000;
        _values["Cache.L2.TTL"] = TimeSpan.FromHours(2);
        _values["Cache.EvictionPolicy"] = "LRU";

        // Memory configuration
        _values["Memory.ObjectPool.MaxSize"] = 1000;
        _values["Memory.ObjectPool.InitialSize"] = 100;
        _values["Memory.BufferPool.MaxBuffers"] = 500;
        _values["Memory.GCPressureThreshold"] = 0.8;

        // I/O configuration
        _values["IO.BatchSize"] = 100;
        _values["IO.AsyncTimeout"] = TimeSpan.FromSeconds(30);
        _values["IO.CompressionEnabled"] = true;
        _values["IO.CompressionLevel"] = 6;

        // Concurrency configuration
        _values["Concurrency.MaxThreads"] = Environment.ProcessorCount * 2;
        _values["Concurrency.WorkStealingEnabled"] = true;
        _values["Concurrency.LockFreeEnabled"] = true;

        // Index configuration
        _values["Index.HashTable.InitialCapacity"] = 1000;
        _values["Index.HashTable.LoadFactor"] = 0.75;
        _values["Index.BTree.Degree"] = 32;
        _values["Index.AutoRebuildThreshold"] = 100000;

        // Query configuration
        _values["Query.PlanCacheSize"] = 1000;
        _values["Query.ResultCacheSize"] = 500;
        _values["Query.ParallelExecutionThreshold"] = 1000;

        // Monitoring configuration
        _values["Monitoring.MetricsEnabled"] = true;
        _values["Monitoring.MetricsRetention"] = TimeSpan.FromHours(24);
        _values["Monitoring.AlertingEnabled"] = true;

        // Add validators for critical settings
        AddValidator("Cache.L1.MaxSize", new RangeValidator<int>(1, 100000));
        AddValidator("Cache.L2.MaxSize", new RangeValidator<int>(1, 1000000));
        AddValidator("Memory.GCPressureThreshold", new RangeValidator<double>(0.1, 1.0));
        AddValidator("IO.CompressionLevel", new RangeValidator<int>(1, 9));
        AddValidator("Concurrency.MaxThreads", new RangeValidator<int>(1, 1000));
        AddValidator("Index.HashTable.LoadFactor", new RangeValidator<double>(0.1, 1.0));
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _lock?.Dispose();
    }
}

/// <summary>
/// Configuration snapshot implementation.
/// </summary>
internal class ConfigurationSnapshot : IConfigurationSnapshot
{
    public ConfigurationSnapshot(
        DateTime timestamp,
        int version,
        IReadOnlyDictionary<string, object?> data,
        IReadOnlyDictionary<string, string> metadata)
    {
        Timestamp = timestamp;
        Version = version;
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
    }

    public DateTime Timestamp { get; }
    public int Version { get; }
    public IReadOnlyDictionary<string, object?> Data { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    public override string ToString()
    {
        return $"Configuration Snapshot v{Version} [{Timestamp:yyyy-MM-dd HH:mm:ss}]: {Data.Count} settings";
    }
}

/// <summary>
/// Base class for configuration validators.
/// </summary>
public abstract class ConfigurationValidator
{
    /// <summary>
    /// Validates a configuration value.
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <returns>Validation result</returns>
    public abstract ConfigurationValidationResult Validate(object? value);
}

/// <summary>
/// Range validator for numeric values.
/// </summary>
/// <typeparam name="T">Numeric type</typeparam>
public class RangeValidator<T> : ConfigurationValidator where T : IComparable<T>
{
    private readonly T _min;
    private readonly T _max;

    public RangeValidator(T min, T max)
    {
        _min = min;
        _max = max;
    }

    public override ConfigurationValidationResult Validate(object? value)
    {
        if (value is not T typedValue)
        {
            return new ConfigurationValidationResult(false, new[] { $"Value must be of type {typeof(T).Name}" });
        }

        if (typedValue.CompareTo(_min) < 0 || typedValue.CompareTo(_max) > 0)
        {
            return new ConfigurationValidationResult(false, 
                new[] { $"Value {typedValue} is outside valid range [{_min}, {_max}]" });
        }

        return new ConfigurationValidationResult(true);
    }

    public override string ToString()
    {
        return $"RangeValidator<{typeof(T).Name}>[{_min}, {_max}]";
    }
}

/// <summary>
/// Enum validator for enumeration values.
/// </summary>
/// <typeparam name="T">Enum type</typeparam>
public class EnumValidator<T> : ConfigurationValidator where T : struct, Enum
{
    public override ConfigurationValidationResult Validate(object? value)
    {
        if (value is string stringValue)
        {
            if (Enum.TryParse<T>(stringValue, true, out _))
            {
                return new ConfigurationValidationResult(true);
            }
        }
        else if (value is T)
        {
            return new ConfigurationValidationResult(true);
        }

        var validValues = string.Join(", ", Enum.GetNames<T>());
        return new ConfigurationValidationResult(false, 
            new[] { $"Value must be one of: {validValues}" });
    }

    public override string ToString()
    {
        return $"EnumValidator<{typeof(T).Name}>";
    }
}
