using System;
using System.Collections.Generic;

namespace NebulaStore.GigaMap;

/// <summary>
/// Represents the indices management system for a GigaMap.
/// Provides access to bitmap indices and their configuration.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
public interface IGigaIndices<T> where T : class
{
    /// <summary>
    /// Gets the bitmap indices associated with this GigaMap.
    /// </summary>
    IBitmapIndices<T> Bitmap { get; }

    /// <summary>
    /// Registers an index category with this indices manager.
    /// </summary>
    /// <param name="indexCategory">The index category to register</param>
    void Register(IIndexCategory<T> indexCategory);

    /// <summary>
    /// Gets all registered indexers.
    /// </summary>
    IReadOnlyCollection<IIndexer<T, object>> Indexers { get; }

    /// <summary>
    /// Gets an indexer by name.
    /// </summary>
    /// <param name="name">The name of the indexer</param>
    /// <returns>The indexer if found, null otherwise</returns>
    IIndexer<T, object>? GetIndexer(string name);

    /// <summary>
    /// Checks if an indexer with the given name exists.
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>True if the indexer exists, false otherwise</returns>
    bool HasIndexer(string name);
}

/// <summary>
/// Represents the bitmap indices system for efficient querying.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
public interface IBitmapIndices<T> where T : class
{
    /// <summary>
    /// Ensures that all specified indexers have corresponding bitmap indices.
    /// </summary>
    /// <param name="indexers">The indexers to ensure indices for</param>
    void EnsureAll(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Adds a bitmap index for the specified indexer.
    /// </summary>
    /// <param name="indexer">The indexer to create an index for</param>
    /// <returns>The created bitmap index</returns>
    IBitmapIndex<T, TKey> Add<TKey>(IIndexer<T, TKey> indexer) where TKey : notnull;

    /// <summary>
    /// Gets a bitmap index by indexer.
    /// </summary>
    /// <param name="indexer">The indexer to get the index for</param>
    /// <returns>The bitmap index if found, null otherwise</returns>
    IBitmapIndex<T, TKey>? Get<TKey>(IIndexer<T, TKey> indexer) where TKey : notnull;

    /// <summary>
    /// Gets a bitmap index by name.
    /// </summary>
    /// <param name="name">The name of the index</param>
    /// <returns>The bitmap index if found, null otherwise</returns>
    IBitmapIndex<T, object>? Get(string name);

    /// <summary>
    /// Sets the identity indices for efficient entity lookup.
    /// </summary>
    /// <param name="indexers">The indexers to use as identity indices</param>
    void SetIdentityIndices(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Adds unique constraints for the specified indexers.
    /// </summary>
    /// <param name="indexers">The indexers to add unique constraints for</param>
    void AddUniqueConstraints(IEnumerable<IIndexer<T, object>> indexers);

    /// <summary>
    /// Gets all bitmap indices.
    /// </summary>
    IReadOnlyCollection<IBitmapIndex<T, object>> All { get; }

    /// <summary>
    /// Gets the count of bitmap indices.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Checks if any bitmap indices exist.
    /// </summary>
    bool IsEmpty { get; }
}

/// <summary>
/// Represents a bitmap index for efficient querying of entities.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
public interface IBitmapIndex<T, TKey> : IIndexIdentifier<T, TKey> where T : class where TKey : notnull
{
    /// <summary>
    /// Gets the parent bitmap indices.
    /// </summary>
    IBitmapIndices<T> Parent { get; }

    /// <summary>
    /// Gets the indexer associated with this bitmap index.
    /// </summary>
    IIndexer<T, TKey> Indexer { get; }

    /// <summary>
    /// Searches the index using the provided predicate.
    /// </summary>
    /// <param name="predicate">The predicate to filter keys</param>
    /// <returns>A bitmap result containing matching entity IDs</returns>
    IBitmapResult Search(Func<TKey, bool> predicate);



    /// <summary>
    /// Gets the number of unique keys in this index.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Creates statistics for this bitmap index.
    /// </summary>
    /// <returns>Statistics about the index</returns>
    IBitmapIndexStatistics<T> CreateStatistics();

    /// <summary>
    /// Iterates over all keys in the index.
    /// </summary>
    /// <param name="action">The action to perform on each key</param>
    void IterateKeys(Action<TKey> action);

    /// <summary>
    /// Checks if the keys of two entities are equal.
    /// </summary>
    /// <param name="entity1">The first entity</param>
    /// <param name="entity2">The second entity</param>
    /// <returns>True if the keys are equal, false otherwise</returns>
    bool EqualKeys(T entity1, T entity2);

    /// <summary>
    /// Gets the entity IDs that match the specified key.
    /// </summary>
    /// <param name="key">The key to search for</param>
    /// <returns>A collection of entity IDs that match the key</returns>
    IEnumerable<long> GetEntityIds(TKey key);

    /// <summary>
    /// Iterates over all entity IDs in the index.
    /// </summary>
    /// <param name="action">The action to perform for each entity ID</param>
    void IterateEntityIds(Action<long> action);
}

/// <summary>
/// Represents an index identifier that can be used to reference an index.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
public interface IIndexIdentifier<T, TKey> where T : class where TKey : notnull
{
    /// <summary>
    /// Gets the name of the index.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of keys in the index.
    /// </summary>
    Type KeyType { get; }

    /// <summary>
    /// Tests if an entity matches a given key.
    /// </summary>
    /// <param name="entity">The entity to test</param>
    /// <param name="key">The key to match against</param>
    /// <returns>True if the entity matches the key, false otherwise</returns>
    bool Test(T entity, TKey key);

    /// <summary>
    /// Creates an equality condition for the given key.
    /// </summary>
    /// <param name="key">The key to create a condition for</param>
    /// <returns>A condition that tests for equality with the key</returns>
    ICondition<T> Is(TKey key);

    /// <summary>
    /// Creates a condition that tests if the indexed value is in the given collection.
    /// </summary>
    /// <param name="keys">The collection of keys to test against</param>
    /// <returns>A condition that tests for membership in the collection</returns>
    ICondition<T> IsIn(IEnumerable<TKey> keys);

    /// <summary>
    /// Creates a condition that tests if the indexed value is not equal to the given key.
    /// </summary>
    /// <param name="key">The key to test against</param>
    /// <returns>A condition that tests for inequality with the key</returns>
    ICondition<T> IsNot(TKey key);

    /// <summary>
    /// Determines whether this index is suitable as a unique constraint.
    /// </summary>
    bool IsSuitableAsUniqueConstraint { get; }
}

/// <summary>
/// Represents an index category that groups related indices.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
public interface IIndexCategory<T> where T : class
{
    /// <summary>
    /// Gets the name of the index category.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the indexers in this category.
    /// </summary>
    IReadOnlyCollection<IIndexer<T, object>> Indexers { get; }

    /// <summary>
    /// Creates the index group for the given GigaMap.
    /// </summary>
    /// <param name="gigaMap">The GigaMap to create the index group for</param>
    /// <returns>The created index group</returns>
    IBitmapIndices<T> CreateIndexGroup(IGigaMap<T> gigaMap);
}