using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebulaStore.GigaMap;

/// <summary>
/// An indexed collection designed to cope with vast amounts of data.
/// It stores the data in nested, lazy-loaded segments backed by indices.
/// This allows for efficient querying of data without the need to load all of it into memory.
/// Instead, only the segments required to return the resulting entities are loaded on demand.
/// With this approach, GigaMap can handle billions of entities with exceptional performance.
/// </summary>
/// <typeparam name="T">The type of entities in this collection</typeparam>
public interface IGigaMap<T> : IEnumerable<T>, IDisposable where T : class
{
    /// <summary>
    /// Returns the total number of elements in this collection.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Returns the highest used id.
    /// </summary>
    long HighestUsedId { get; }

    /// <summary>
    /// Returns true if this collection contains no elements.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Checks if this GigaMap is in a read-only state.
    /// </summary>
    bool IsReadOnly { get; }

    /// <summary>
    /// Returns the element to which the specified id is mapped.
    /// </summary>
    /// <param name="entityId">The id of the requested element</param>
    /// <returns>The element with the requested id or null</returns>
    T? Get(long entityId);

    /// <summary>
    /// Adds the specified element to this collection.
    /// Null values are not allowed.
    /// </summary>
    /// <param name="element">The element to add, not null</param>
    /// <returns>The assigned id</returns>
    /// <exception cref="ArgumentNullException">If element is null</exception>
    long Add(T element);

    /// <summary>
    /// Adds all elements to this collection.
    /// Null values are not allowed.
    /// </summary>
    /// <param name="elements">The elements to add</param>
    /// <returns>The last assigned id</returns>
    /// <exception cref="ArgumentNullException">If an element is null</exception>
    long AddAll(IEnumerable<T> elements);

    /// <summary>
    /// Returns the element to which the specified id is mapped, if it is already loaded.
    /// </summary>
    /// <param name="entityId">The id of the requested element</param>
    /// <returns>The element with the requested id, or null if it isn't loaded</returns>
    T? Peek(long entityId);

    /// <summary>
    /// Removes the entity mapped to the specified id.
    /// </summary>
    /// <param name="entityId">The id of the element to be deleted</param>
    /// <returns>The deleted element, or null if none was deleted</returns>
    T? RemoveById(long entityId);

    /// <summary>
    /// Removes the specified entity if present in this collection and returns its previously mapped id.
    /// In order for this method to work, at least one bitmap index is needed.
    /// </summary>
    /// <param name="entity">The entity to be removed</param>
    /// <returns>The previously mapped id of the entity, or -1 if none was removed</returns>
    /// <exception cref="InvalidOperationException">If no bitmap index is present</exception>
    long Remove(T entity);

    /// <summary>
    /// Removes all entities, effectively clearing all data from this collection.
    /// </summary>
    void RemoveAll();

    /// <summary>
    /// Synonym for RemoveAll().
    /// </summary>
    void Clear();

    /// <summary>
    /// Marks this GigaMap as read-only, indicating that it cannot be modified further.
    /// </summary>
    void MarkReadOnly();

    /// <summary>
    /// Removes the read-only status from this GigaMap.
    /// </summary>
    void UnmarkReadOnly();

    /// <summary>
    /// Removes all read-only marks currently set.
    /// </summary>
    /// <returns>True if the read-only marks were successfully cleared, false otherwise</returns>
    bool ClearReadOnlyMarks();

    /// <summary>
    /// Replaces an entity if present in this collection, updates the indices accordingly,
    /// and returns the old entity mapped to the specified id.
    /// </summary>
    /// <param name="entityId">The entity id to replace</param>
    /// <param name="entity">The new entity</param>
    /// <returns>The old entity</returns>
    /// <exception cref="ArgumentException">If the entityId is not found</exception>
    T Set(long entityId, T entity);

    /// <summary>
    /// Replaces the specified entity if present with a different one, updates the indices accordingly,
    /// and returns its mapped id.
    /// </summary>
    /// <param name="current">The entity to be removed</param>
    /// <param name="replacement">The new entity instance</param>
    /// <returns>The mapped id of the entity</returns>
    /// <exception cref="InvalidOperationException">If no bitmap index is present</exception>
    /// <exception cref="ArgumentException">If current and replacement are the same object</exception>
    long Replace(T current, T replacement);

    /// <summary>
    /// Updates the specified entity and the indices accordingly.
    /// </summary>
    /// <param name="current">The entity to be updated</param>
    /// <param name="updateAction">The update logic to be executed</param>
    /// <returns>The updated entity</returns>
    /// <exception cref="InvalidOperationException">If no bitmap index is present</exception>
    T Update(T current, Action<T> updateAction);

    /// <summary>
    /// Applies the specified logic for the given entity and updates the indices accordingly.
    /// </summary>
    /// <param name="current">The entity to be updated</param>
    /// <param name="logic">The logic to be executed</param>
    /// <returns>The result of the given logic</returns>
    /// <exception cref="InvalidOperationException">If no bitmap index is present</exception>
    TResult Apply<TResult>(T current, Func<T, TResult> logic);

    /// <summary>
    /// Releases all strong references to on-demand loaded data.
    /// </summary>
    void Release();

    /// <summary>
    /// Returns the GigaIndices instance that represents the indices structure for this GigaMap.
    /// </summary>
    IGigaIndices<T> Index { get; }

    /// <summary>
    /// Retrieves the constraints associated with this GigaMap.
    /// </summary>
    IGigaConstraints<T> Constraints { get; }

    /// <summary>
    /// Registers index categories into the index management system of the GigaMap.
    /// </summary>
    /// <param name="indexCategory">The index category to be registered</param>
    /// <returns>The current instance of GigaMap for method chaining</returns>
    IGigaMap<T> RegisterIndices(IIndexCategory<T> indexCategory);

    /// <summary>
    /// Creates an empty query, meaning without any given condition.
    /// Executing this would return a result with all existing elements.
    /// </summary>
    /// <returns>A new query object</returns>
    IGigaQuery<T> Query();

    /// <summary>
    /// Creates a condition builder for a specific index, which can be used to start a query.
    /// </summary>
    /// <param name="index">The index identifier to build this condition for</param>
    /// <returns>A new condition builder</returns>
    IConditionBuilder<T, TKey> Query<TKey>(IIndexIdentifier<T, TKey> index);

    /// <summary>
    /// Creates a query for a specific index looking for a certain key.
    /// </summary>
    /// <param name="index">The index identifier to build a condition for</param>
    /// <param name="key">The key to compare to</param>
    /// <returns>A new query object</returns>
    IGigaQuery<T> Query<TKey>(IIndexIdentifier<T, TKey> index, TKey key);

    /// <summary>
    /// Creates a new query initialized with a certain condition.
    /// </summary>
    /// <param name="condition">The first condition for the query</param>
    /// <returns>A new query object</returns>
    IGigaQuery<T> Query(ICondition<T> condition);

    /// <summary>
    /// Creates a condition builder for a specific String index, which can be used to start a query.
    /// </summary>
    /// <param name="stringIndexName">The String index name</param>
    /// <returns>A new condition builder</returns>
    IConditionBuilder<T, string> Query(string stringIndexName);

    /// <summary>
    /// Creates a query for a specific String index looking for a certain key.
    /// </summary>
    /// <param name="stringIndexName">The String index identifier to build a condition for</param>
    /// <param name="key">The key to compare to</param>
    /// <returns>A new query object</returns>
    IGigaQuery<T> Query(string stringIndexName, string key);

    /// <summary>
    /// Stores this GigaMap instance and implicitly all changes to its component instances like indices, etc.
    /// </summary>
    /// <returns>The objectId of this instance</returns>
    /// <exception cref="InvalidOperationException">If this instance wasn't stored once initially by a storing context</exception>
    Task<long> StoreAsync();

    /// <summary>
    /// Provides this set Equalator instance of this GigaMap.
    /// </summary>
    IEqualityComparer<T> EqualityComparer { get; }
}