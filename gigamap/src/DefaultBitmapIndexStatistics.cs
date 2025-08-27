using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IBitmapIndexStatistics.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class DefaultBitmapIndexStatistics<T, TKey> : IBitmapIndexStatistics<T> where T : class where TKey : notnull
{
    private readonly IBitmapIndex<T, TKey> _parent;
    private readonly IIndexer<T, TKey> _indexer;
    private readonly Dictionary<object, IKeyStatistics> _keyStatistics;

    public DefaultBitmapIndexStatistics(IBitmapIndex<T, TKey> parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _indexer = parent.Indexer;
        _keyStatistics = CalculateKeyStatistics();
    }

    public IBitmapIndex<T, object> Parent => _parent as IBitmapIndex<T, object> ?? throw new InvalidOperationException("Cannot cast to object index");

    public IIndexer<T, object> Indexer => _indexer as IIndexer<T, object> ?? throw new InvalidOperationException("Cannot cast to object indexer");

    public Type KeyType => _indexer.KeyType;

    public int EntryCount => _keyStatistics.Count;

    public int TotalEntityCount => (int)_keyStatistics.Values.Sum(s => s.EntityCount);

    public int UniqueKeyCount => EntryCount;

    public int TotalDataMemorySize => _keyStatistics.Values.Sum(s => s.TotalDataMemorySize);

    public IReadOnlyDictionary<object, IKeyStatistics> KeyStatistics => _keyStatistics;

    private Dictionary<object, IKeyStatistics> CalculateKeyStatistics()
    {
        var statistics = new Dictionary<object, IKeyStatistics>();

        // In a real implementation, this would analyze the actual bitmap index data
        // For now, we'll create placeholder statistics

        if (_parent is DefaultBitmapIndex<T, TKey> defaultIndex)
        {
            // Access internal data through reflection or internal interface
            // This is a simplified implementation
            var keyToEntityIds = GetKeyToEntityIdsMapping(defaultIndex);

            foreach (var kvp in keyToEntityIds)
            {
                var keyStats = new DefaultKeyStatistics(
                    entityCount: kvp.Value.Count,
                    totalDataMemorySize: EstimateMemorySize(kvp.Key, kvp.Value.Count)
                );

                statistics[kvp.Key] = keyStats;
            }
        }

        return statistics;
    }

    private Dictionary<object, HashSet<long>> GetKeyToEntityIdsMapping(DefaultBitmapIndex<T, TKey> defaultIndex)
    {
        // Access the internal data through reflection since we need the actual data
        var field = typeof(DefaultBitmapIndex<T, TKey>).GetField("_keyToEntityIds",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field?.GetValue(defaultIndex) is Dictionary<TKey, HashSet<long>> internalData)
        {
            var result = new Dictionary<object, HashSet<long>>();
            foreach (var kvp in internalData)
            {
                result[kvp.Key!] = kvp.Value;
            }
            return result;
        }

        return new Dictionary<object, HashSet<long>>();
    }

    private int EstimateMemorySize(object key, int entityCount)
    {
        // Rough estimation of memory usage
        var keySize = key?.ToString()?.Length * 2 ?? 0; // Approximate string size
        var bitmapSize = (entityCount / 8) + 1; // Rough bitmap size estimation
        return keySize + bitmapSize + 32; // Add overhead
    }
}

/// <summary>
/// Default implementation of IKeyStatistics.
/// </summary>
internal class DefaultKeyStatistics : IKeyStatistics
{
    public DefaultKeyStatistics(long entityCount, int totalDataMemorySize)
    {
        EntityCount = entityCount;
        TotalDataMemorySize = totalDataMemorySize;
    }

    public int TotalDataMemorySize { get; }

    public long EntityCount { get; }
}