using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IBitmapIndex.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class DefaultBitmapIndex<T, TKey> : IBitmapIndex<T, TKey>, IInternalBitmapIndex<T> where T : class where TKey : notnull
{
    private readonly IBitmapIndices<T> _parent;
    private readonly IIndexer<T, TKey> _indexer;
    private readonly Dictionary<TKey, HashSet<long>> _keyToEntityIds;
    private readonly Dictionary<long, TKey> _entityIdToKey = new();

    public DefaultBitmapIndex(IBitmapIndices<T> parent, IIndexer<T, TKey> indexer)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _keyToEntityIds = new Dictionary<TKey, HashSet<long>>(indexer.KeyEqualityComparer);
    }

    public IBitmapIndices<T> Parent => _parent;

    public IIndexer<T, TKey> Indexer => _indexer;

    public string Name => _indexer.Name;

    public Type KeyType => _indexer.KeyType;

    public bool IsSuitableAsUniqueConstraint => _indexer.IsSuitableAsUniqueConstraint;

    public int Size => _keyToEntityIds.Count;

    public IBitmapResult Search(Func<TKey, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        var matchingEntityIds = new HashSet<long>();

        foreach (var kvp in _keyToEntityIds)
        {
            if (predicate(kvp.Key))
            {
                matchingEntityIds.UnionWith(kvp.Value);
            }
        }

        return new DefaultBitmapResult(matchingEntityIds);
    }



    public IBitmapIndexStatistics<T> CreateStatistics()
    {
        return new DefaultBitmapIndexStatistics<T, TKey>(this);
    }

    public void IterateKeys(Action<TKey> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var key in _keyToEntityIds.Keys)
        {
            action(key);
        }
    }

    public bool EqualKeys(T entity1, T entity2)
    {
        if (entity1 == null && entity2 == null)
            return true;
        if (entity1 == null || entity2 == null)
            return false;

        var key1 = _indexer.Index(entity1);
        var key2 = _indexer.Index(entity2);

        return _indexer.KeyEqualityComparer.Equals(key1, key2);
    }

    public bool Test(T entity, TKey key)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var entityKey = _indexer.Index(entity);
        return _indexer.KeyEqualityComparer.Equals(entityKey, key);
    }

    public ICondition<T> Is(TKey key)
    {
        return new EqualityCondition<T, TKey>(this, key);
    }

    public ICondition<T> IsIn(IEnumerable<TKey> keys)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        return new MembershipCondition<T, TKey>(this, keys.ToHashSet(_indexer.KeyEqualityComparer));
    }

    public ICondition<T> IsNot(TKey key)
    {
        return new InequalityCondition<T, TKey>(this, key);
    }

    public IEnumerable<long> GetEntityIds(TKey key)
    {
        if (_keyToEntityIds.TryGetValue(key, out var entityIds))
        {
            return entityIds.ToList(); // Return a copy to avoid modification
        }
        return Enumerable.Empty<long>();
    }

    public void IterateEntityIds(Action<long> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var entityId in _entityIdToKey.Keys)
        {
            action(entityId);
        }
    }

    // IInternalBitmapIndex implementation
    public void InternalAdd(long entityId, T entity)
    {
        if (entity == null)
            return;

        var key = _indexer.Index(entity);

        if (!_keyToEntityIds.TryGetValue(key, out var entityIds))
        {
            entityIds = new HashSet<long>();
            _keyToEntityIds[key] = entityIds;
        }

        entityIds.Add(entityId);
        _entityIdToKey[entityId] = key;
    }

    public void InternalRemove(long entityId, T entity)
    {
        if (_entityIdToKey.TryGetValue(entityId, out var key))
        {
            _entityIdToKey.Remove(entityId);

            if (_keyToEntityIds.TryGetValue(key, out var entityIds))
            {
                entityIds.Remove(entityId);
                if (entityIds.Count == 0)
                {
                    _keyToEntityIds.Remove(key);
                }
            }
        }
    }

    public void InternalUpdate(long entityId, T oldEntity, T newEntity)
    {
        // Remove old mapping
        if (oldEntity != null)
        {
            InternalRemove(entityId, oldEntity);
        }

        // Add new mapping
        if (newEntity != null)
        {
            InternalAdd(entityId, newEntity);
        }
    }

    public void InternalRemoveAll()
    {
        _keyToEntityIds.Clear();
        _entityIdToKey.Clear();
    }
}

/// <summary>
/// Default implementation of IBitmapResult.
/// </summary>
internal class DefaultBitmapResult : IBitmapResult
{
    private readonly HashSet<long> _entityIds;

    public DefaultBitmapResult(HashSet<long> entityIds)
    {
        _entityIds = entityIds ?? new HashSet<long>();
    }

    public IEnumerable<long> EntityIds => _entityIds;

    public long Count => _entityIds.Count;

    public bool IsEmpty => _entityIds.Count == 0;

    public IBitmapResult And(IBitmapResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        var otherEntityIds = other.EntityIds.ToHashSet();
        var intersection = new HashSet<long>(_entityIds);
        intersection.IntersectWith(otherEntityIds);

        return new DefaultBitmapResult(intersection);
    }

    public IBitmapResult Or(IBitmapResult other)
    {
        if (other == null)
            throw new ArgumentNullException(nameof(other));

        var otherEntityIds = other.EntityIds.ToHashSet();
        var union = new HashSet<long>(_entityIds);
        union.UnionWith(otherEntityIds);

        return new DefaultBitmapResult(union);
    }

    public IBitmapResult Not(long totalEntityCount)
    {
        var complement = new HashSet<long>();
        for (long i = 0; i < totalEntityCount; i++)
        {
            if (!_entityIds.Contains(i))
            {
                complement.Add(i);
            }
        }

        return new DefaultBitmapResult(complement);
    }


}