using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.GigaMap;

/// <summary>
/// Represents a query that can be executed against a GigaMap to retrieve entities.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
public interface IGigaQuery<T> : IEnumerable<T> where T : class
{
    /// <summary>
    /// Adds an AND condition to the query.
    /// </summary>
    /// <param name="condition">The condition to add</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> And(ICondition<T> condition);

    /// <summary>
    /// Adds an AND condition for a specific index.
    /// </summary>
    /// <param name="index">The index to create a condition for</param>
    /// <returns>A condition builder for the index</returns>
    IConditionBuilder<T, TKey> And<TKey>(IIndexIdentifier<T, TKey> index);

    /// <summary>
    /// Adds an AND condition for a string index.
    /// </summary>
    /// <param name="stringIndexName">The name of the string index</param>
    /// <returns>A condition builder for the string index</returns>
    IConditionBuilder<T, string> And(string stringIndexName);

    /// <summary>
    /// Adds an AND condition for a string index with a specific key.
    /// </summary>
    /// <param name="stringIndexName">The name of the string index</param>
    /// <param name="key">The key to match</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> And(string stringIndexName, string key);

    /// <summary>
    /// Adds an AND condition for a typed index.
    /// </summary>
    /// <param name="indexName">The name of the index</param>
    /// <param name="keyType">The type of the key</param>
    /// <returns>A condition builder for the index</returns>
    IConditionBuilder<T, TKey> And<TKey>(string indexName, Type keyType);

    /// <summary>
    /// Adds an AND condition for a typed index with a specific key.
    /// </summary>
    /// <param name="indexName">The name of the index</param>
    /// <param name="keyType">The type of the key</param>
    /// <param name="key">The key to match</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> And<TKey>(string indexName, Type keyType, TKey key);

    /// <summary>
    /// Adds an OR condition to the query.
    /// </summary>
    /// <param name="condition">The condition to add</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> Or(ICondition<T> condition);

    /// <summary>
    /// Adds an OR condition for a specific index.
    /// </summary>
    /// <param name="index">The index to create a condition for</param>
    /// <returns>A condition builder for the index</returns>
    IConditionBuilder<T, TKey> Or<TKey>(IIndexIdentifier<T, TKey> index);

    /// <summary>
    /// Adds an OR condition for a string index.
    /// </summary>
    /// <param name="stringIndexName">The name of the string index</param>
    /// <returns>A condition builder for the string index</returns>
    IConditionBuilder<T, string> Or(string stringIndexName);

    /// <summary>
    /// Adds an OR condition for a string index with a specific key.
    /// </summary>
    /// <param name="stringIndexName">The name of the string index</param>
    /// <param name="key">The key to match</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> Or(string stringIndexName, string key);

    /// <summary>
    /// Limits the number of results returned by the query.
    /// </summary>
    /// <param name="limit">The maximum number of results to return</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> Limit(int limit);

    /// <summary>
    /// Skips a number of results before returning the remaining ones.
    /// </summary>
    /// <param name="offset">The number of results to skip</param>
    /// <returns>The updated query</returns>
    IGigaQuery<T> Skip(int offset);

    /// <summary>
    /// Executes the query and returns all matching entities.
    /// </summary>
    /// <returns>A collection of matching entities</returns>
    IReadOnlyList<T> Execute();

    /// <summary>
    /// Executes the query asynchronously and returns all matching entities.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<IReadOnlyList<T>> ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the first matching entity, or null if none found.
    /// </summary>
    /// <returns>The first matching entity or null</returns>
    T? FirstOrDefault();

    /// <summary>
    /// Executes the query asynchronously and returns the first matching entity, or null if none found.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and returns the count of matching entities.
    /// </summary>
    /// <returns>The count of matching entities</returns>
    long Count();

    /// <summary>
    /// Executes the query asynchronously and returns the count of matching entities.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<long> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and checks if any entities match.
    /// </summary>
    /// <returns>True if any entities match, false otherwise</returns>
    bool Any();

    /// <summary>
    /// Executes the query asynchronously and checks if any entities match.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the query and applies an action to each matching entity.
    /// </summary>
    /// <param name="action">The action to apply to each entity</param>
    void ForEach(Action<T> action);

    /// <summary>
    /// Executes the query asynchronously and applies an action to each matching entity.
    /// </summary>
    /// <param name="action">The action to apply to each entity</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A task that represents the asynchronous operation</returns>
    Task ForEachAsync(Action<T> action, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a condition that can be applied to entities in a query.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
public interface ICondition<T> where T : class
{
    /// <summary>
    /// Evaluates the condition against the bitmap indices.
    /// </summary>
    /// <param name="bitmapIndices">The bitmap indices to evaluate against</param>
    /// <returns>A bitmap result containing matching entity IDs</returns>
    IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices);

    /// <summary>
    /// Combines this condition with another using AND logic.
    /// </summary>
    /// <param name="other">The other condition</param>
    /// <returns>A combined condition</returns>
    ICondition<T> And(ICondition<T> other);

    /// <summary>
    /// Combines this condition with another using OR logic.
    /// </summary>
    /// <param name="other">The other condition</param>
    /// <returns>A combined condition</returns>
    ICondition<T> Or(ICondition<T> other);

    /// <summary>
    /// Negates this condition.
    /// </summary>
    /// <returns>A negated condition</returns>
    ICondition<T> Not();
}

/// <summary>
/// Represents a condition builder for creating conditions on a specific index.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
public interface IConditionBuilder<T, TKey> where T : class
{
    /// <summary>
    /// Creates an equality condition.
    /// </summary>
    /// <param name="key">The key to test for equality</param>
    /// <returns>A query with the equality condition</returns>
    IGigaQuery<T> Is(TKey key);

    /// <summary>
    /// Creates an inequality condition.
    /// </summary>
    /// <param name="key">The key to test for inequality</param>
    /// <returns>A query with the inequality condition</returns>
    IGigaQuery<T> IsNot(TKey key);

    /// <summary>
    /// Creates a condition that tests if the value is in the given collection.
    /// </summary>
    /// <param name="keys">The collection of keys to test against</param>
    /// <returns>A query with the membership condition</returns>
    IGigaQuery<T> IsIn(IEnumerable<TKey> keys);

    /// <summary>
    /// Creates a condition that tests if the value is not in the given collection.
    /// </summary>
    /// <param name="keys">The collection of keys to test against</param>
    /// <returns>A query with the non-membership condition</returns>
    IGigaQuery<T> IsNotIn(IEnumerable<TKey> keys);

    /// <summary>
    /// Creates a condition using a custom predicate.
    /// </summary>
    /// <param name="predicate">The predicate to test keys against</param>
    /// <returns>A query with the custom condition</returns>
    IGigaQuery<T> Where(Func<TKey, bool> predicate);
}

/// <summary>
/// Represents the result of a bitmap index search.
/// </summary>
public interface IBitmapResult
{
    /// <summary>
    /// Gets the entity IDs that match the search criteria.
    /// </summary>
    IEnumerable<long> EntityIds { get; }

    /// <summary>
    /// Gets the count of matching entities.
    /// </summary>
    long Count { get; }

    /// <summary>
    /// Checks if the result is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Combines this result with another using AND logic.
    /// </summary>
    /// <param name="other">The other result</param>
    /// <returns>A combined result</returns>
    IBitmapResult And(IBitmapResult other);

    /// <summary>
    /// Combines this result with another using OR logic.
    /// </summary>
    /// <param name="other">The other result</param>
    /// <returns>A combined result</returns>
    IBitmapResult Or(IBitmapResult other);

    /// <summary>
    /// Negates this result.
    /// </summary>
    /// <param name="totalEntityCount">The total number of entities in the collection</param>
    /// <returns>A negated result</returns>
    IBitmapResult Not(long totalEntityCount);


}

/// <summary>
/// Represents statistics about a bitmap index.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
public interface IBitmapIndexStatistics<T> where T : class
{
    /// <summary>
    /// Gets the parent bitmap index.
    /// </summary>
    IBitmapIndex<T, object> Parent { get; }

    /// <summary>
    /// Gets the indexer associated with the statistics.
    /// </summary>
    IIndexer<T, object> Indexer { get; }

    /// <summary>
    /// Gets the type of keys in the index.
    /// </summary>
    Type KeyType { get; }

    /// <summary>
    /// Gets the number of unique keys in the index.
    /// </summary>
    int EntryCount { get; }

    /// <summary>
    /// Gets the total memory size used by the index data.
    /// </summary>
    int TotalDataMemorySize { get; }

    /// <summary>
    /// Gets detailed statistics for each key.
    /// </summary>
    IReadOnlyDictionary<object, IKeyStatistics> KeyStatistics { get; }
}

/// <summary>
/// Represents statistics for a specific key in a bitmap index.
/// </summary>
public interface IKeyStatistics
{
    /// <summary>
    /// Gets the total memory size used by this key's data.
    /// </summary>
    int TotalDataMemorySize { get; }

    /// <summary>
    /// Gets the number of entities associated with this key.
    /// </summary>
    long EntityCount { get; }
}