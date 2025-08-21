using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Indexing;

/// <summary>
/// Interface for high-performance in-memory indexes with support for multiple data types and operations.
/// </summary>
/// <typeparam name="TKey">Type of index keys</typeparam>
/// <typeparam name="TValue">Type of indexed values</typeparam>
public interface IIndex<TKey, TValue> : IDisposable
    where TKey : notnull
{
    /// <summary>
    /// Gets the index name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the index type.
    /// </summary>
    IndexType Type { get; }

    /// <summary>
    /// Gets the current number of entries in the index.
    /// </summary>
    long Count { get; }

    /// <summary>
    /// Gets whether the index is unique (no duplicate keys allowed).
    /// </summary>
    bool IsUnique { get; }

    /// <summary>
    /// Gets index statistics.
    /// </summary>
    IIndexStatistics Statistics { get; }

    /// <summary>
    /// Adds or updates an entry in the index.
    /// </summary>
    /// <param name="key">Index key</param>
    /// <param name="value">Value to index</param>
    /// <returns>True if added, false if updated</returns>
    bool Put(TKey key, TValue value);

    /// <summary>
    /// Gets a value by key.
    /// </summary>
    /// <param name="key">Index key</param>
    /// <param name="value">Retrieved value</param>
    /// <returns>True if found, false otherwise</returns>
    bool TryGet(TKey key, out TValue value);

    /// <summary>
    /// Gets all values for a key (useful for non-unique indexes).
    /// </summary>
    /// <param name="key">Index key</param>
    /// <returns>Collection of values</returns>
    IEnumerable<TValue> GetAll(TKey key);

    /// <summary>
    /// Removes an entry from the index.
    /// </summary>
    /// <param name="key">Index key</param>
    /// <returns>True if removed, false if not found</returns>
    bool Remove(TKey key);

    /// <summary>
    /// Removes a specific key-value pair from the index.
    /// </summary>
    /// <param name="key">Index key</param>
    /// <param name="value">Value to remove</param>
    /// <returns>True if removed, false if not found</returns>
    bool Remove(TKey key, TValue value);

    /// <summary>
    /// Checks if the index contains a key.
    /// </summary>
    /// <param name="key">Index key</param>
    /// <returns>True if key exists, false otherwise</returns>
    bool ContainsKey(TKey key);

    /// <summary>
    /// Gets all keys in the index.
    /// </summary>
    /// <returns>Collection of keys</returns>
    IEnumerable<TKey> GetKeys();

    /// <summary>
    /// Gets all values in the index.
    /// </summary>
    /// <returns>Collection of values</returns>
    IEnumerable<TValue> GetValues();

    /// <summary>
    /// Clears all entries from the index.
    /// </summary>
    void Clear();

    /// <summary>
    /// Rebuilds the index (useful for optimization).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RebuildAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for range-queryable indexes.
/// </summary>
/// <typeparam name="TKey">Type of index keys</typeparam>
/// <typeparam name="TValue">Type of indexed values</typeparam>
public interface IRangeIndex<TKey, TValue> : IIndex<TKey, TValue>
    where TKey : notnull, IComparable<TKey>
{
    /// <summary>
    /// Gets values within a range.
    /// </summary>
    /// <param name="startKey">Start key (inclusive)</param>
    /// <param name="endKey">End key (inclusive)</param>
    /// <returns>Values within the range</returns>
    IEnumerable<TValue> GetRange(TKey startKey, TKey endKey);

    /// <summary>
    /// Gets values greater than the specified key.
    /// </summary>
    /// <param name="key">Key to compare against</param>
    /// <param name="inclusive">Whether to include the key itself</param>
    /// <returns>Values greater than the key</returns>
    IEnumerable<TValue> GetGreaterThan(TKey key, bool inclusive = false);

    /// <summary>
    /// Gets values less than the specified key.
    /// </summary>
    /// <param name="key">Key to compare against</param>
    /// <param name="inclusive">Whether to include the key itself</param>
    /// <returns>Values less than the key</returns>
    IEnumerable<TValue> GetLessThan(TKey key, bool inclusive = false);

    /// <summary>
    /// Gets the minimum key in the index.
    /// </summary>
    /// <returns>Minimum key, or default if empty</returns>
    TKey? GetMinKey();

    /// <summary>
    /// Gets the maximum key in the index.
    /// </summary>
    /// <returns>Maximum key, or default if empty</returns>
    TKey? GetMaxKey();
}

/// <summary>
/// Interface for index statistics.
/// </summary>
public interface IIndexStatistics
{
    /// <summary>
    /// Gets the total number of lookups performed.
    /// </summary>
    long TotalLookups { get; }

    /// <summary>
    /// Gets the total number of insertions performed.
    /// </summary>
    long TotalInsertions { get; }

    /// <summary>
    /// Gets the total number of deletions performed.
    /// </summary>
    long TotalDeletions { get; }

    /// <summary>
    /// Gets the total number of updates performed.
    /// </summary>
    long TotalUpdates { get; }

    /// <summary>
    /// Gets the cache hit ratio for lookups.
    /// </summary>
    double HitRatio { get; }

    /// <summary>
    /// Gets the average lookup time in microseconds.
    /// </summary>
    double AverageLookupTimeMicroseconds { get; }

    /// <summary>
    /// Gets the average insertion time in microseconds.
    /// </summary>
    double AverageInsertionTimeMicroseconds { get; }

    /// <summary>
    /// Gets the index memory usage in bytes.
    /// </summary>
    long MemoryUsageBytes { get; }

    /// <summary>
    /// Gets the index load factor (for hash-based indexes).
    /// </summary>
    double LoadFactor { get; }

    /// <summary>
    /// Gets the index depth (for tree-based indexes).
    /// </summary>
    int Depth { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Enumeration of index types.
/// </summary>
public enum IndexType
{
    /// <summary>
    /// Hash-based index for fast equality lookups.
    /// </summary>
    Hash,

    /// <summary>
    /// B-tree index for range queries and ordered access.
    /// </summary>
    BTree,

    /// <summary>
    /// Trie index for prefix-based searches.
    /// </summary>
    Trie,

    /// <summary>
    /// Bitmap index for boolean and categorical data.
    /// </summary>
    Bitmap,

    /// <summary>
    /// Composite index combining multiple fields.
    /// </summary>
    Composite
}

/// <summary>
/// Configuration for indexes.
/// </summary>
public class IndexConfiguration
{
    /// <summary>
    /// Gets or sets the index type.
    /// </summary>
    public IndexType Type { get; set; } = IndexType.Hash;

    /// <summary>
    /// Gets or sets whether the index is unique.
    /// </summary>
    public bool IsUnique { get; set; } = true;

    /// <summary>
    /// Gets or sets the initial capacity.
    /// </summary>
    public int InitialCapacity { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the load factor threshold for resizing.
    /// </summary>
    public double LoadFactorThreshold { get; set; } = 0.75;

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable concurrent access.
    /// </summary>
    public bool EnableConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets the concurrency level (number of segments for concurrent access).
    /// </summary>
    public int ConcurrencyLevel { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets whether to enable automatic rebuilding.
    /// </summary>
    public bool EnableAutoRebuild { get; set; } = false;

    /// <summary>
    /// Gets or sets the rebuild threshold (operations count).
    /// </summary>
    public long RebuildThreshold { get; set; } = 100000;

    /// <summary>
    /// Gets or sets the memory pressure threshold for cleanup.
    /// </summary>
    public double MemoryPressureThreshold { get; set; } = 0.8; // 80%

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return InitialCapacity > 0 &&
               LoadFactorThreshold > 0 && LoadFactorThreshold <= 1.0 &&
               ConcurrencyLevel > 0 &&
               RebuildThreshold > 0 &&
               MemoryPressureThreshold > 0 && MemoryPressureThreshold <= 1.0;
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public IndexConfiguration Clone()
    {
        return new IndexConfiguration
        {
            Type = Type,
            IsUnique = IsUnique,
            InitialCapacity = InitialCapacity,
            LoadFactorThreshold = LoadFactorThreshold,
            EnableStatistics = EnableStatistics,
            EnableConcurrency = EnableConcurrency,
            ConcurrencyLevel = ConcurrencyLevel,
            EnableAutoRebuild = EnableAutoRebuild,
            RebuildThreshold = RebuildThreshold,
            MemoryPressureThreshold = MemoryPressureThreshold
        };
    }

    public override string ToString()
    {
        return $"IndexConfiguration[Type={Type}, Unique={IsUnique}, " +
               $"InitialCapacity={InitialCapacity}, LoadFactor={LoadFactorThreshold:P1}, " +
               $"Concurrency={EnableConcurrency}, Statistics={EnableStatistics}]";
    }
}

/// <summary>
/// Interface for index managers that coordinate multiple indexes.
/// </summary>
public interface IIndexManager : IDisposable
{
    /// <summary>
    /// Gets the number of managed indexes.
    /// </summary>
    int IndexCount { get; }

    /// <summary>
    /// Creates or gets an index.
    /// </summary>
    /// <typeparam name="TKey">Type of index keys</typeparam>
    /// <typeparam name="TValue">Type of indexed values</typeparam>
    /// <param name="name">Index name</param>
    /// <param name="configuration">Index configuration</param>
    /// <returns>Index instance</returns>
    IIndex<TKey, TValue> GetOrCreateIndex<TKey, TValue>(string name, IndexConfiguration? configuration = null)
        where TKey : notnull;

    /// <summary>
    /// Gets an existing index.
    /// </summary>
    /// <typeparam name="TKey">Type of index keys</typeparam>
    /// <typeparam name="TValue">Type of indexed values</typeparam>
    /// <param name="name">Index name</param>
    /// <returns>Index instance, or null if not found</returns>
    IIndex<TKey, TValue>? GetIndex<TKey, TValue>(string name)
        where TKey : notnull;

    /// <summary>
    /// Removes an index.
    /// </summary>
    /// <param name="name">Index name</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveIndex(string name);

    /// <summary>
    /// Gets all index names.
    /// </summary>
    /// <returns>Collection of index names</returns>
    IEnumerable<string> GetIndexNames();

    /// <summary>
    /// Gets aggregate statistics for all indexes.
    /// </summary>
    /// <returns>Aggregate statistics</returns>
    IndexManagerStatistics GetAggregateStatistics();

    /// <summary>
    /// Rebuilds all indexes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RebuildAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs maintenance on all indexes.
    /// </summary>
    /// <returns>Maintenance result</returns>
    Task<IndexMaintenanceResult> PerformMaintenanceAsync();
}
