using System;
using System.Collections.Generic;

namespace NebulaStore.GigaMap;

/// <summary>
/// Represents an indexer that extracts keys from entities for indexing purposes.
/// This is the core component that defines how entities are indexed in a GigaMap.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys extracted from entities</typeparam>
public interface IIndexer<in T, TKey>
{
    /// <summary>
    /// Gets the name of this indexer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of keys this indexer produces.
    /// </summary>
    Type KeyType { get; }

    /// <summary>
    /// Gets the equality comparer used for comparing keys.
    /// </summary>
    IEqualityComparer<TKey> KeyEqualityComparer { get; }

    /// <summary>
    /// Extracts the index key from the given entity.
    /// </summary>
    /// <param name="entity">The entity to extract the key from</param>
    /// <returns>The extracted key</returns>
    TKey Index(T entity);

    /// <summary>
    /// Determines whether this indexer can be used as a unique constraint.
    /// </summary>
    bool IsSuitableAsUniqueConstraint { get; }
}

/// <summary>
/// Provides factory methods for creating common indexers.
/// </summary>
public static class Indexer
{
    /// <summary>
    /// Creates an indexer that extracts a property value from entities.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TKey">The property type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <param name="keyExtractor">Function to extract the key from an entity</param>
    /// <param name="keyEqualityComparer">Optional equality comparer for keys</param>
    /// <returns>A new property indexer</returns>
    public static IIndexer<T, TKey> Property<T, TKey>(
        string name,
        Func<T, TKey> keyExtractor,
        IEqualityComparer<TKey>? keyEqualityComparer = null)
    {
        return new PropertyIndexer<T, TKey>(name, keyExtractor, keyEqualityComparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>
    /// Creates an indexer for string properties with case-insensitive comparison.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <param name="keyExtractor">Function to extract the string key from an entity</param>
    /// <returns>A new case-insensitive string indexer</returns>
    public static IIndexer<T, string> StringIgnoreCase<T>(
        string name,
        Func<T, string> keyExtractor)
    {
        return new PropertyIndexer<T, string>(name, keyExtractor, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates an indexer for identity-based indexing (using object reference equality).
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <returns>A new identity indexer</returns>
    public static IIndexer<T, T> Identity<T>(string name) where T : class
    {
        return new IdentityIndexer<T>(name);
    }

    /// <summary>
    /// Creates an indexer for numeric properties.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <typeparam name="TNumber">The numeric type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <param name="keyExtractor">Function to extract the numeric key from an entity</param>
    /// <returns>A new numeric indexer</returns>
    public static IIndexer<T, TNumber> Numeric<T, TNumber>(
        string name,
        Func<T, TNumber> keyExtractor) where TNumber : struct, IComparable<TNumber>
    {
        return new PropertyIndexer<T, TNumber>(name, keyExtractor, EqualityComparer<TNumber>.Default);
    }

    /// <summary>
    /// Creates an indexer for DateTime properties.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <param name="keyExtractor">Function to extract the DateTime key from an entity</param>
    /// <returns>A new DateTime indexer</returns>
    public static IIndexer<T, DateTime> DateTime<T>(
        string name,
        Func<T, DateTime> keyExtractor)
    {
        return new PropertyIndexer<T, DateTime>(name, keyExtractor, EqualityComparer<DateTime>.Default);
    }

    /// <summary>
    /// Creates an indexer for Guid properties.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="name">The name of the indexer</param>
    /// <param name="keyExtractor">Function to extract the Guid key from an entity</param>
    /// <returns>A new Guid indexer</returns>
    public static IIndexer<T, Guid> Guid<T>(
        string name,
        Func<T, Guid> keyExtractor)
    {
        return new PropertyIndexer<T, Guid>(name, keyExtractor, EqualityComparer<Guid>.Default);
    }

    /// <summary>
    /// Wraps a strongly typed indexer to work with object keys.
    /// </summary>
    /// <param name="indexer">The strongly typed indexer to wrap</param>
    /// <returns>An object-based indexer wrapper</returns>
    internal static IIndexer<T, object> AsObjectIndexer<T, TKey>(IIndexer<T, TKey> indexer)
        where T : class
        where TKey : notnull
    {
        return new ObjectIndexerWrapper<T, TKey>(indexer);
    }
}

/// <summary>
/// Internal implementation of a property-based indexer.
/// </summary>
internal class PropertyIndexer<T, TKey> : IIndexer<T, TKey>
{
    private readonly Func<T, TKey> _keyExtractor;

    public PropertyIndexer(string name, Func<T, TKey> keyExtractor, IEqualityComparer<TKey> keyEqualityComparer)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _keyExtractor = keyExtractor ?? throw new ArgumentNullException(nameof(keyExtractor));
        KeyEqualityComparer = keyEqualityComparer ?? throw new ArgumentNullException(nameof(keyEqualityComparer));
        KeyType = typeof(TKey);
    }

    public string Name { get; }
    public Type KeyType { get; }
    public IEqualityComparer<TKey> KeyEqualityComparer { get; }
    public bool IsSuitableAsUniqueConstraint => true;

    public TKey Index(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        return _keyExtractor(entity);
    }
}

/// <summary>
/// Internal implementation of an identity-based indexer.
/// </summary>
internal class IdentityIndexer<T> : IIndexer<T, T> where T : class
{
    public IdentityIndexer(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        KeyType = typeof(T);
        KeyEqualityComparer = ReferenceEqualityComparer.Instance as IEqualityComparer<T>
                             ?? throw new InvalidOperationException("Reference equality comparer not available");
    }

    public string Name { get; }
    public Type KeyType { get; }
    public IEqualityComparer<T> KeyEqualityComparer { get; }
    public bool IsSuitableAsUniqueConstraint => true;

    public T Index(T entity)
    {
        return entity ?? throw new ArgumentNullException(nameof(entity));
    }
}

/// <summary>
/// Wrapper that converts a strongly typed indexer to an object-based indexer.
/// </summary>
internal class ObjectIndexerWrapper<T, TKey> : IIndexer<T, object>
    where T : class
    where TKey : notnull
{
    private readonly IIndexer<T, TKey> _innerIndexer;

    public ObjectIndexerWrapper(IIndexer<T, TKey> innerIndexer)
    {
        _innerIndexer = innerIndexer ?? throw new ArgumentNullException(nameof(innerIndexer));
    }

    public string Name => _innerIndexer.Name;
    public Type KeyType => _innerIndexer.KeyType;
    public bool IsSuitableAsUniqueConstraint => _innerIndexer.IsSuitableAsUniqueConstraint;
    public IEqualityComparer<object> KeyEqualityComparer => new ObjectEqualityComparerWrapper<TKey>(_innerIndexer.KeyEqualityComparer);

    public object Index(T entity)
    {
        return _innerIndexer.Index(entity);
    }
}

/// <summary>
/// Wrapper that converts a strongly typed equality comparer to an object-based one.
/// </summary>
internal class ObjectEqualityComparerWrapper<TKey> : IEqualityComparer<object> where TKey : notnull
{
    private readonly IEqualityComparer<TKey> _innerComparer;

    public ObjectEqualityComparerWrapper(IEqualityComparer<TKey> innerComparer)
    {
        _innerComparer = innerComparer ?? throw new ArgumentNullException(nameof(innerComparer));
    }

    public new bool Equals(object? x, object? y)
    {
        if (x is TKey xKey && y is TKey yKey)
        {
            return _innerComparer.Equals(xKey, yKey);
        }
        return object.Equals(x, y);
    }

    public int GetHashCode(object obj)
    {
        if (obj is TKey key)
        {
            return _innerComparer.GetHashCode(key);
        }
        return obj?.GetHashCode() ?? 0;
    }
}

/// <summary>
/// Wrapper that converts an object-based bitmap index to a strongly typed index identifier.
/// </summary>
internal class ObjectIndexIdentifierWrapper<T, TKey> : IIndexIdentifier<T, TKey>
    where T : class
    where TKey : notnull
{
    private readonly IBitmapIndex<T, object> _objectIndex;

    public ObjectIndexIdentifierWrapper(IBitmapIndex<T, object> objectIndex)
    {
        _objectIndex = objectIndex ?? throw new ArgumentNullException(nameof(objectIndex));

        // Validate that the index actually handles the expected key type
        if (_objectIndex.Indexer.KeyType != typeof(TKey))
        {
            throw new ArgumentException($"Index key type {_objectIndex.Indexer.KeyType} does not match expected type {typeof(TKey)}");
        }
    }

    public string Name => _objectIndex.Name;
    public Type KeyType => typeof(TKey);
    public bool IsSuitableAsUniqueConstraint => _objectIndex.IsSuitableAsUniqueConstraint;

    public bool Test(T entity, TKey key)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (key == null) throw new ArgumentNullException(nameof(key));

        var indexedValue = _objectIndex.Indexer.Index(entity);
        return _objectIndex.Indexer.KeyEqualityComparer.Equals(indexedValue, key);
    }

    public ICondition<T> Is(TKey key)
    {
        return new ObjectEqualityCondition<T>(_objectIndex, key);
    }

    public ICondition<T> IsIn(IEnumerable<TKey> keys)
    {
        return new ObjectMembershipCondition<T>(_objectIndex, keys.Cast<object>().ToHashSet());
    }

    public ICondition<T> IsNot(TKey key)
    {
        return new ObjectInequalityCondition<T>(_objectIndex, key);
    }
}

/// <summary>
/// Object-based equality condition that works with object indexers.
/// </summary>
internal class ObjectEqualityCondition<T> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, object> _index;
    private readonly object _key;

    public ObjectEqualityCondition(IBitmapIndex<T, object> index, object key)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    public IEnumerable<long> GetMatchingEntityIds()
    {
        return _index.GetEntityIds(_key);
    }

    public bool Test(T entity)
    {
        if (entity == null) return false;
        var indexedValue = _index.Indexer.Index(entity);
        return _index.Indexer.KeyEqualityComparer.Equals(indexedValue, _key);
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return new SimpleBitmapResult(GetMatchingEntityIds());
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Object-based membership condition that works with object indexers.
/// </summary>
internal class ObjectMembershipCondition<T> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, object> _index;
    private readonly HashSet<object> _keys;

    public ObjectMembershipCondition(IBitmapIndex<T, object> index, HashSet<object> keys)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    public IEnumerable<long> GetMatchingEntityIds()
    {
        var allEntityIds = new HashSet<long>();
        foreach (var key in _keys)
        {
            foreach (var entityId in _index.GetEntityIds(key))
            {
                allEntityIds.Add(entityId);
            }
        }
        return allEntityIds;
    }

    public bool Test(T entity)
    {
        if (entity == null) return false;
        var indexedValue = _index.Indexer.Index(entity);
        return _keys.Any(key => _index.Indexer.KeyEqualityComparer.Equals(indexedValue, key));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return new SimpleBitmapResult(GetMatchingEntityIds());
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Object-based inequality condition that works with object indexers.
/// </summary>
internal class ObjectInequalityCondition<T> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, object> _index;
    private readonly object _key;

    public ObjectInequalityCondition(IBitmapIndex<T, object> index, object key)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _key = key ?? throw new ArgumentNullException(nameof(key));
    }

    public IEnumerable<long> GetMatchingEntityIds()
    {
        // For inequality, we need to get all entity IDs and exclude those that match the key
        var allEntityIds = new HashSet<long>();
        _index.IterateEntityIds(entityId => allEntityIds.Add(entityId));

        var excludeEntityIds = _index.GetEntityIds(_key).ToHashSet();
        return allEntityIds.Except(excludeEntityIds);
    }

    public bool Test(T entity)
    {
        if (entity == null) return false;
        var indexedValue = _index.Indexer.Index(entity);
        return !_index.Indexer.KeyEqualityComparer.Equals(indexedValue, _key);
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return new SimpleBitmapResult(GetMatchingEntityIds());
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Simple implementation of IBitmapResult.
/// </summary>
internal class SimpleBitmapResult : IBitmapResult
{
    private readonly IEnumerable<long> _entityIds;

    public SimpleBitmapResult(IEnumerable<long> entityIds)
    {
        _entityIds = entityIds ?? throw new ArgumentNullException(nameof(entityIds));
    }

    public IEnumerable<long> EntityIds => _entityIds;
    public long Count => _entityIds.Count();
    public bool IsEmpty => !_entityIds.Any();

    public IBitmapResult And(IBitmapResult other)
    {
        return new SimpleBitmapResult(EntityIds.Intersect(other.EntityIds));
    }

    public IBitmapResult Or(IBitmapResult other)
    {
        return new SimpleBitmapResult(EntityIds.Union(other.EntityIds));
    }

    public IBitmapResult Not(long totalEntityCount)
    {
        var allIds = Enumerable.Range(0, (int)totalEntityCount).Select(i => (long)i);
        return new SimpleBitmapResult(allIds.Except(EntityIds));
    }

    public IBitmapResult Optimize()
    {
        return this; // Simple implementation - no optimization
    }
}

