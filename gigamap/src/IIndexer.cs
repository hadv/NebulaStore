using System;
using System.Collections.Generic;

namespace NebulaStore.GigaMap;

/// <summary>
/// Represents an indexer that extracts keys from entities for indexing purposes.
/// This is the core component that defines how entities are indexed in a GigaMap.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys extracted from entities</typeparam>
public interface IIndexer<T, TKey> where T : class
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

    /// <summary>
    /// Tests whether the given entity matches the specified key.
    /// </summary>
    /// <param name="entity">The entity to test</param>
    /// <param name="key">The key to test against</param>
    /// <returns>True if the entity matches the key</returns>
    bool Test(T entity, TKey key);

    /// <summary>
    /// Creates a condition that matches entities that do NOT have the specified key.
    /// </summary>
    /// <param name="key">The key to exclude</param>
    /// <returns>A condition for entities not matching the key</returns>
    ICondition<T> Not(TKey key);

    /// <summary>
    /// Resolves all unique keys present in the given GigaMap for this indexer.
    /// </summary>
    /// <param name="gigaMap">The GigaMap to resolve keys from</param>
    /// <returns>A collection of unique keys</returns>
    IEnumerable<TKey> ResolveKeys(IGigaMap<T> gigaMap);
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
        where T : class
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
        where T : class
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
        Func<T, TNumber> keyExtractor)
        where T : class
        where TNumber : struct, IComparable<TNumber>
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
        where T : class
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
        where T : class
    {
        return new PropertyIndexer<T, Guid>(name, keyExtractor, EqualityComparer<Guid>.Default);
    }

    /// <summary>
    /// Wraps a strongly typed indexer to work with object keys.
    /// </summary>
    /// <param name="indexer">The strongly typed indexer to wrap</param>
    /// <returns>An object-based indexer wrapper</returns>
    public static IIndexer<T, object> AsObjectIndexer<T, TKey>(IIndexer<T, TKey> indexer)
        where T : class
        where TKey : notnull
    {
        return new ObjectIndexerWrapper<T, TKey>(indexer);
    }
}

/// <summary>
/// Internal implementation of a property-based indexer.
/// </summary>
internal class PropertyIndexer<T, TKey> : IIndexer<T, TKey> where T : class
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

    public bool Test(T entity, TKey key)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (key == null) throw new ArgumentNullException(nameof(key));

        var indexedValue = Index(entity);
        return KeyEqualityComparer.Equals(indexedValue, key);
    }

    public ICondition<T> Not(TKey key)
    {
        return new NotCondition<T, TKey>(this, key);
    }

    public IEnumerable<TKey> ResolveKeys(IGigaMap<T> gigaMap)
    {
        if (gigaMap == null) throw new ArgumentNullException(nameof(gigaMap));

        var keys = new HashSet<TKey>(KeyEqualityComparer);
        gigaMap.Iterate(entity =>
        {
            var key = Index(entity);
            keys.Add(key);
        });
        return keys;
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

    public bool Test(T entity, T key)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (key == null) throw new ArgumentNullException(nameof(key));

        return ReferenceEquals(entity, key);
    }

    public ICondition<T> Not(T key)
    {
        return new NotCondition<T, T>(this, key);
    }

    public IEnumerable<T> ResolveKeys(IGigaMap<T> gigaMap)
    {
        if (gigaMap == null) throw new ArgumentNullException(nameof(gigaMap));

        var keys = new HashSet<T>(KeyEqualityComparer);
        gigaMap.Iterate(entity => keys.Add(entity));
        return keys;
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

    public bool Test(T entity, object key)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (key is TKey typedKey)
        {
            return _innerIndexer.Test(entity, typedKey);
        }
        return false;
    }

    public ICondition<T> Not(object key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        if (key is TKey typedKey)
        {
            return _innerIndexer.Not(typedKey);
        }
        throw new ArgumentException($"Key must be of type {typeof(TKey)}", nameof(key));
    }

    public IEnumerable<object> ResolveKeys(IGigaMap<T> gigaMap)
    {
        return _innerIndexer.ResolveKeys(gigaMap).Cast<object>();
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

    public ICondition<T> Not(TKey key)
    {
        return IsNot(key);
    }

    public IEnumerable<TKey> ResolveKeys(IGigaMap<T> gigaMap)
    {
        return _objectIndex.Indexer.ResolveKeys(gigaMap).OfType<TKey>();
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


}

/// <summary>
/// Condition that matches entities that do NOT have the specified key for an indexer.
/// </summary>
internal class NotCondition<T, TKey> : ICondition<T> where T : class
{
    private readonly IIndexer<T, TKey> _indexer;
    private readonly TKey _key;

    public NotCondition(IIndexer<T, TKey> indexer, TKey key)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _key = key;
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        if (bitmapIndices == null)
            throw new ArgumentNullException(nameof(bitmapIndices));

        // Get the bitmap index for this indexer
        var bitmapIndex = bitmapIndices.Get(_indexer);
        if (bitmapIndex == null)
            throw new InvalidOperationException($"No bitmap index found for indexer '{_indexer.Name}'");

        // Get all entity IDs that match the key
        var matchingIds = bitmapIndex.GetEntityIds(_key);

        // Return the inverse (all IDs that don't match)
        var result = new SimpleBitmapResult(matchingIds);

        // For now, use a large number as total entity count
        // In a real implementation, this would come from the GigaMap
        return result.Not(long.MaxValue);
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
/// Flexible wrapper that allows any object-based index to be queried with string keys.
/// </summary>
internal class FlexibleStringIndexWrapper<T> : IIndexIdentifier<T, string> where T : class
{
    private readonly IBitmapIndex<T, object> _objectIndex;

    public FlexibleStringIndexWrapper(IBitmapIndex<T, object> objectIndex)
    {
        _objectIndex = objectIndex ?? throw new ArgumentNullException(nameof(objectIndex));
    }

    public string Name => _objectIndex.Name;
    public Type KeyType => typeof(string);
    public bool IsSuitableAsUniqueConstraint => _objectIndex.IsSuitableAsUniqueConstraint;

    public bool Test(T entity, string key)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (key == null) throw new ArgumentNullException(nameof(key));

        // Get the actual value from the entity using the underlying indexer
        var actualValue = _objectIndex.Indexer.Index(entity);

        // Convert the actual value to string and compare
        return actualValue?.ToString() == key;
    }

    public ICondition<T> Is(string key)
    {
        return new FlexibleStringCondition<T>(_objectIndex, key, false);
    }

    public ICondition<T> IsIn(IEnumerable<string> keys)
    {
        return new FlexibleStringInCondition<T>(_objectIndex, keys, false);
    }

    public ICondition<T> IsNot(string key)
    {
        return new FlexibleStringCondition<T>(_objectIndex, key, true);
    }

    public ICondition<T> Not(string key)
    {
        return IsNot(key);
    }

    public IEnumerable<string> ResolveKeys(IGigaMap<T> gigaMap)
    {
        return _objectIndex.Indexer.ResolveKeys(gigaMap).Select(k => k?.ToString() ?? "").Distinct();
    }
}

/// <summary>
/// Condition that compares object values with string keys using string conversion.
/// </summary>
internal class FlexibleStringCondition<T> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, object> _objectIndex;
    private readonly string _key;
    private readonly bool _negate;

    public FlexibleStringCondition(IBitmapIndex<T, object> objectIndex, string key, bool negate)
    {
        _objectIndex = objectIndex ?? throw new ArgumentNullException(nameof(objectIndex));
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _negate = negate;
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        if (bitmapIndices == null)
            throw new ArgumentNullException(nameof(bitmapIndices));

        // Get the bitmap index
        var bitmapIndex = bitmapIndices.Get(_objectIndex.Indexer);
        if (bitmapIndex == null)
            throw new InvalidOperationException($"No bitmap index found for indexer '{_objectIndex.Name}'");

        // For string-based queries, we need to find the actual object key that matches the string
        // This is a simplified approach - convert the string key to the appropriate type
        object actualKey = ConvertStringToActualKey(_key);

        // Get entity IDs that match the converted key
        var matchingIds = bitmapIndex.GetEntityIds(actualKey);

        var result = new SimpleBitmapResult(matchingIds);
        return _negate ? result.Not(long.MaxValue) : result;
    }

    private object ConvertStringToActualKey(string stringKey)
    {
        // Try to convert the string to the appropriate type based on the indexer
        var indexer = _objectIndex.Indexer;

        // If it's already a string indexer, return as-is
        if (indexer.KeyType == typeof(string))
            return stringKey;

        // Try common type conversions
        if (indexer.KeyType == typeof(int) && int.TryParse(stringKey, out var intValue))
            return intValue;

        if (indexer.KeyType == typeof(Guid) && Guid.TryParse(stringKey, out var guidValue))
            return guidValue;

        if (indexer.KeyType == typeof(DateTime) && DateTime.TryParse(stringKey, out var dateValue))
            return dateValue;

        if (indexer.KeyType == typeof(double) && double.TryParse(stringKey, out var doubleValue))
            return doubleValue;

        // For object-based indexers, we need a different approach
        // Return the string and let the indexer handle the comparison
        return stringKey;
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
        return new FlexibleStringCondition<T>(_objectIndex, _key, !_negate);
    }
}

/// <summary>
/// Condition that checks if object values (converted to strings) are in a set of string keys.
/// </summary>
internal class FlexibleStringInCondition<T> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, object> _objectIndex;
    private readonly HashSet<string> _keys;
    private readonly bool _negate;

    public FlexibleStringInCondition(IBitmapIndex<T, object> objectIndex, IEnumerable<string> keys, bool negate)
    {
        _objectIndex = objectIndex ?? throw new ArgumentNullException(nameof(objectIndex));
        _keys = new HashSet<string>(keys ?? throw new ArgumentNullException(nameof(keys)));
        _negate = negate;
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        if (bitmapIndices == null)
            throw new ArgumentNullException(nameof(bitmapIndices));

        // Similar simplified implementation
        var matchingIds = new List<long>();
        var result = new SimpleBitmapResult(matchingIds);
        return _negate ? result.Not(long.MaxValue) : result;
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
        return new FlexibleStringInCondition<T>(_objectIndex, _keys, !_negate);
    }
}

