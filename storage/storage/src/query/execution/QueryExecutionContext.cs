using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Query.Execution;

/// <summary>
/// Execution context for query operations providing access to storage, memory management,
/// and execution state.
/// </summary>
public class QueryExecutionContext : IQueryExecutionContext
{
    private readonly ConcurrentDictionary<string, object> _properties = new();
    private readonly ConcurrentDictionary<string, ITemporaryStorage> _temporaryStorages = new();

    public Guid ExecutionId { get; }
    public IStorageEngine StorageEngine { get; }
    public IExecutionMemoryManager MemoryManager { get; }
    public IMemoryBudget MemoryBudget { get; }
    public QueryExecutionOptions Options { get; }
    public CancellationToken CancellationToken { get; }
    public DateTime StartTime { get; }

    public QueryExecutionContext(
        Guid executionId,
        IStorageEngine storageEngine,
        IExecutionMemoryManager memoryManager,
        IMemoryBudget memoryBudget,
        QueryExecutionOptions options,
        CancellationToken cancellationToken)
    {
        ExecutionId = executionId;
        StorageEngine = storageEngine ?? throw new ArgumentNullException(nameof(storageEngine));
        MemoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
        MemoryBudget = memoryBudget ?? throw new ArgumentNullException(nameof(memoryBudget));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        CancellationToken = cancellationToken;
        StartTime = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets or sets a property value.
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        return _properties.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default;
    }

    public void SetProperty<T>(string key, T value)
    {
        _properties[key] = value!;
    }

    /// <summary>
    /// Creates temporary storage for intermediate results.
    /// </summary>
    public async Task<ITemporaryStorage> CreateTemporaryStorageAsync(string name, TemporaryStorageOptions? options = null)
    {
        var tempStorage = await MemoryManager.CreateTemporaryStorageAsync(name, options ?? new TemporaryStorageOptions(), CancellationToken);
        _temporaryStorages[name] = tempStorage;
        return tempStorage;
    }

    /// <summary>
    /// Gets temporary storage by name.
    /// </summary>
    public ITemporaryStorage? GetTemporaryStorage(string name)
    {
        return _temporaryStorages.TryGetValue(name, out var storage) ? storage : null;
    }

    /// <summary>
    /// Disposes all temporary storages.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var storage in _temporaryStorages.Values)
        {
            await storage.DisposeAsync();
        }
        _temporaryStorages.Clear();
    }
}

/// <summary>
/// Memory budget for query execution with tracking and limits.
/// </summary>
public class MemoryBudget : IMemoryBudget
{
    private long _usedMemory;
    private readonly object _lock = new();

    public long TotalBudget { get; }
    public long UsedMemory => _usedMemory;
    public long AvailableMemory => TotalBudget - _usedMemory;
    public double UsagePercentage => TotalBudget > 0 ? (double)_usedMemory / TotalBudget : 0;

    public MemoryBudget(long totalBudget)
    {
        TotalBudget = totalBudget;
    }

    /// <summary>
    /// Allocates memory from the budget.
    /// </summary>
    public bool TryAllocate(long bytes)
    {
        lock (_lock)
        {
            if (_usedMemory + bytes <= TotalBudget)
            {
                _usedMemory += bytes;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Releases memory back to the budget.
    /// </summary>
    public void Release(long bytes)
    {
        lock (_lock)
        {
            _usedMemory = Math.Max(0, _usedMemory - bytes);
        }
    }

    /// <summary>
    /// Checks if allocation would exceed budget.
    /// </summary>
    public bool CanAllocate(long bytes)
    {
        return _usedMemory + bytes <= TotalBudget;
    }
}

/// <summary>
/// Memory manager for query execution with budget allocation and temporary storage.
/// </summary>
public class ExecutionMemoryManager : IExecutionMemoryManager
{
    private readonly ConcurrentDictionary<Guid, IMemoryBudget> _budgets = new();
    private readonly long _totalSystemMemory;
    private readonly double _maxMemoryUsagePercentage;

    public ExecutionMemoryManager(long totalSystemMemory, double maxMemoryUsagePercentage = 0.8)
    {
        _totalSystemMemory = totalSystemMemory;
        _maxMemoryUsagePercentage = maxMemoryUsagePercentage;
    }

    /// <summary>
    /// Allocates a memory budget for query execution.
    /// </summary>
    public async Task<IMemoryBudget> AllocateMemoryBudgetAsync(double estimatedCost, CancellationToken cancellationToken = default)
    {
        // Calculate budget based on estimated cost and available memory
        var maxAvailableMemory = (long)(_totalSystemMemory * _maxMemoryUsagePercentage);
        var budgetSize = Math.Min(maxAvailableMemory / 4, (long)(estimatedCost * 1024 * 1024)); // Rough estimation
        budgetSize = Math.Max(budgetSize, 64 * 1024 * 1024); // Minimum 64MB

        var budget = new MemoryBudget(budgetSize);
        return budget;
    }

    /// <summary>
    /// Releases a memory budget.
    /// </summary>
    public async Task ReleaseMemoryBudgetAsync(IMemoryBudget budget)
    {
        // Budget cleanup is handled by the budget itself
        await Task.CompletedTask;
    }

    /// <summary>
    /// Checks memory pressure and potentially triggers cleanup.
    /// </summary>
    public async Task CheckMemoryPressureAsync(IMemoryBudget budget, CancellationToken cancellationToken = default)
    {
        if (budget.UsagePercentage > 0.9) // 90% usage
        {
            // Trigger garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        if (budget.UsagePercentage > 0.95) // 95% usage
        {
            throw new OutOfMemoryException("Query execution exceeded memory budget");
        }
    }

    /// <summary>
    /// Creates temporary storage for intermediate results.
    /// </summary>
    public async Task<ITemporaryStorage> CreateTemporaryStorageAsync(
        string name, 
        TemporaryStorageOptions options, 
        CancellationToken cancellationToken = default)
    {
        return options.StorageType switch
        {
            TemporaryStorageType.Memory => new MemoryTemporaryStorage(name, options),
            TemporaryStorageType.Disk => new DiskTemporaryStorage(name, options),
            TemporaryStorageType.Hybrid => new HybridTemporaryStorage(name, options),
            _ => throw new NotSupportedException($"Storage type {options.StorageType} not supported")
        };
    }
}

/// <summary>
/// Options for temporary storage.
/// </summary>
public class TemporaryStorageOptions
{
    public TemporaryStorageType StorageType { get; set; } = TemporaryStorageType.Memory;
    public long MaxMemorySize { get; set; } = 64 * 1024 * 1024; // 64MB
    public string? DiskPath { get; set; }
    public bool EnableCompression { get; set; } = false;
    public TimeSpan ExpirationTime { get; set; } = TimeSpan.FromHours(1);
}

/// <summary>
/// Types of temporary storage.
/// </summary>
public enum TemporaryStorageType
{
    Memory,
    Disk,
    Hybrid
}

/// <summary>
/// In-memory temporary storage implementation.
/// </summary>
public class MemoryTemporaryStorage : ITemporaryStorage
{
    private readonly List<IDataRow> _rows = new();
    private readonly object _lock = new();
    private bool _disposed;

    public string Name { get; }
    public TemporaryStorageOptions Options { get; }
    public long RowCount => _rows.Count;
    public long EstimatedSize { get; private set; }

    public MemoryTemporaryStorage(string name, TemporaryStorageOptions options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task AddRowAsync(IDataRow row, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryTemporaryStorage));

        lock (_lock)
        {
            _rows.Add(row);
            EstimatedSize += EstimateRowSize(row);

            // Check memory limits
            if (EstimatedSize > Options.MaxMemorySize)
            {
                throw new OutOfMemoryException($"Temporary storage {Name} exceeded memory limit");
            }
        }
    }

    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MemoryTemporaryStorage));

        IDataRow[] rowsCopy;
        lock (_lock)
        {
            rowsCopy = _rows.ToArray();
        }

        foreach (var row in rowsCopy)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return row;
        }
    }

    public async Task ClearAsync()
    {
        lock (_lock)
        {
            _rows.Clear();
            EstimatedSize = 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await ClearAsync();
            _disposed = true;
        }
    }

    private long EstimateRowSize(IDataRow row)
    {
        // Rough estimation of row size in memory
        return 100; // Simplified estimation
    }
}

/// <summary>
/// Disk-based temporary storage implementation.
/// </summary>
public class DiskTemporaryStorage : ITemporaryStorage
{
    private readonly string _filePath;
    private long _rowCount;
    private bool _disposed;

    public string Name { get; }
    public TemporaryStorageOptions Options { get; }
    public long RowCount => _rowCount;
    public long EstimatedSize { get; private set; }

    public DiskTemporaryStorage(string name, TemporaryStorageOptions options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _filePath = Path.Combine(options.DiskPath ?? Path.GetTempPath(), $"{name}_{Guid.NewGuid()}.tmp");
    }

    public async Task AddRowAsync(IDataRow row, CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DiskTemporaryStorage));

        // Implementation would serialize row to disk
        _rowCount++;
        EstimatedSize += 100; // Simplified estimation
    }

    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DiskTemporaryStorage));

        // Implementation would deserialize rows from disk
        yield break; // Simplified
    }

    public async Task ClearAsync()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
        _rowCount = 0;
        EstimatedSize = 0;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            await ClearAsync();
            _disposed = true;
        }
    }
}

/// <summary>
/// Hybrid temporary storage that starts in memory and spills to disk.
/// </summary>
public class HybridTemporaryStorage : ITemporaryStorage
{
    private ITemporaryStorage _currentStorage;
    private bool _spilledToDisk;

    public string Name { get; }
    public TemporaryStorageOptions Options { get; }
    public long RowCount => _currentStorage.RowCount;
    public long EstimatedSize => _currentStorage.EstimatedSize;

    public HybridTemporaryStorage(string name, TemporaryStorageOptions options)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        _currentStorage = new MemoryTemporaryStorage(name, options);
    }

    public async Task AddRowAsync(IDataRow row, CancellationToken cancellationToken = default)
    {
        try
        {
            await _currentStorage.AddRowAsync(row, cancellationToken);
        }
        catch (OutOfMemoryException) when (!_spilledToDisk)
        {
            // Spill to disk
            await SpillToDiskAsync(cancellationToken);
            await _currentStorage.AddRowAsync(row, cancellationToken);
        }
    }

    public async IAsyncEnumerable<IDataRow> GetRowsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var row in _currentStorage.GetRowsAsync(cancellationToken))
        {
            yield return row;
        }
    }

    public async Task ClearAsync()
    {
        await _currentStorage.ClearAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _currentStorage.DisposeAsync();
    }

    private async Task SpillToDiskAsync(CancellationToken cancellationToken)
    {
        var diskStorage = new DiskTemporaryStorage(Name, Options);
        
        // Copy data from memory to disk
        await foreach (var row in _currentStorage.GetRowsAsync(cancellationToken))
        {
            await diskStorage.AddRowAsync(row, cancellationToken);
        }

        // Switch to disk storage
        await _currentStorage.DisposeAsync();
        _currentStorage = diskStorage;
        _spilledToDisk = true;
    }
}
