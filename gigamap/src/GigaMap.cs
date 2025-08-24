using System;

namespace NebulaStore.GigaMap;

/// <summary>
/// Factory class for creating GigaMap instances.
/// Provides both simplified LINQ-based and traditional query-based implementations.
/// </summary>
public static class GigaMap
{
    /// <summary>
    /// Creates a new GigaMap instance for the specified entity type.
    /// This version uses LINQ for querying, similar to how Eclipse Store uses Java Stream API.
    /// 
    /// Usage:
    /// var gigaMap = GigaMap.New&lt;Person&gt;();
    /// gigaMap.AddIndex(Indexer.Property&lt;Person, string&gt;("Department", p => p.Department));
    /// 
    /// // Use LINQ for querying
    /// var engineers = gigaMap.Where(p => p.Department == "Engineering").ToList();
    /// var count = gigaMap.Count(p => p.Age > 30);
    /// var grouped = gigaMap.GroupBy(p => p.Department).ToList();
    /// </summary>
    /// <typeparam name="T">The type of entities to store</typeparam>
    /// <returns>A new simplified GigaMap instance</returns>
    public static SimplifiedGigaMap<T> New<T>() where T : class
    {
        return new SimplifiedGigaMap<T>();
    }

    /// <summary>
    /// Creates a new traditional GigaMap instance with custom query system.
    /// Use this if you need the original Eclipse Store-style query API.
    /// 
    /// Usage:
    /// var gigaMap = GigaMap.NewTraditional&lt;Person&gt;();
    /// gigaMap.Index.Bitmap.Add(Indexer.Property&lt;Person, string&gt;("Department", p => p.Department));
    /// 
    /// // Use custom query system
    /// var engineers = gigaMap.Query("Department", "Engineering").Execute().ToList();
    /// </summary>
    /// <typeparam name="T">The type of entities to store</typeparam>
    /// <returns>A new traditional GigaMap instance</returns>
    public static IGigaMap<T> NewTraditional<T>() where T : class
    {
        return new DefaultGigaMap<T>(
            EqualityComparer<T>.Default,
            lowLevelLengthExponent: 8,
            midLevelLengthExponent: 12,
            highLevelMinimumLengthExponent: 16,
            highLevelMaximumLengthExponent: 20);
    }
}
