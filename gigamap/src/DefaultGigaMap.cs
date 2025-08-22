using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NebulaStore.Storage;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IGigaMap with hierarchical segment structure and bitmap indexing.
/// </summary>
/// <typeparam name="T">The type of entities stored in this GigaMap</typeparam>
public class DefaultGigaMap<T> : IGigaMap<T> where T : class
{
    private readonly IEqualityComparer<T> _equalityComparer;
    private readonly int _lowLevelLengthExponent;
    private readonly int _midLevelLengthExponent;
    private readonly int _highLevelMinimumLengthExponent;
    private readonly int _highLevelMaximumLengthExponent;

    private readonly object _lock = new();
    private readonly Dictionary<long, T> _entities = new(); // Temporary storage - will be replaced with hierarchical structure
    private long _nextId = 0;
    private int _readOnlyCount = 0;
    private bool _disposed = false;

    private readonly DefaultGigaIndices<T> _indices;
    private readonly DefaultGigaConstraints<T> _constraints;
    private IStorageConnection? _storageConnection;

    public DefaultGigaMap(
        IEqualityComparer<T> equalityComparer,
        int lowLevelLengthExponent,
        int midLevelLengthExponent,
        int highLevelMinimumLengthExponent,
        int highLevelMaximumLengthExponent)
    {
        _equalityComparer = equalityComparer ?? throw new ArgumentNullException(nameof(equalityComparer));
        _lowLevelLengthExponent = lowLevelLengthExponent;
        _midLevelLengthExponent = midLevelLengthExponent;
        _highLevelMinimumLengthExponent = highLevelMinimumLengthExponent;
        _highLevelMaximumLengthExponent = highLevelMaximumLengthExponent;

        _indices = new DefaultGigaIndices<T>(this);
        _constraints = new DefaultGigaConstraints<T>();
    }

    public long Size
    {
        get
        {
            lock (_lock)
            {
                return _entities.Count;
            }
        }
    }

    public long HighestUsedId
    {
        get
        {
            lock (_lock)
            {
                return _nextId - 1;
            }
        }
    }

    public bool IsEmpty => Size == 0;

    public bool IsReadOnly
    {
        get
        {
            lock (_lock)
            {
                return _readOnlyCount > 0;
            }
        }
    }

    public IGigaIndices<T> Index => _indices;

    public IGigaConstraints<T> Constraints => _constraints;

    public IEqualityComparer<T> EqualityComparer => _equalityComparer;

    public T? Get(long entityId)
    {
        lock (_lock)
        {
            return _entities.TryGetValue(entityId, out var entity) ? entity : null;
        }
    }

    public long Add(T element)
    {
        if (element == null)
            throw new ArgumentNullException(nameof(element));

        lock (_lock)
        {
            EnsureMutable();

            var entityId = _nextId++;
            _constraints.Check(entityId, null, element);

            _entities[entityId] = element;
            _indices.InternalAdd(entityId, element);

            return entityId;
        }
    }

    public long AddAll(IEnumerable<T> elements)
    {
        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        lock (_lock)
        {
            EnsureMutable();

            var lastId = -1L;
            foreach (var element in elements)
            {
                if (element == null)
                    throw new ArgumentNullException(nameof(elements), "Collection contains null element");

                lastId = Add(element);
            }

            return lastId;
        }
    }

    public T? Peek(long entityId)
    {
        // For now, same as Get - in full implementation this would check if loaded
        return Get(entityId);
    }

    public T? RemoveById(long entityId)
    {
        lock (_lock)
        {
            EnsureMutable();

            if (_entities.TryGetValue(entityId, out var entity))
            {
                _entities.Remove(entityId);
                _indices.InternalRemove(entityId, entity);
                return entity;
            }

            return null;
        }
    }

    public long Remove(T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        lock (_lock)
        {
            EnsureMutable();

            // Find entity by scanning (in full implementation, would use bitmap indices)
            foreach (var kvp in _entities)
            {
                if (_equalityComparer.Equals(kvp.Value, entity))
                {
                    _entities.Remove(kvp.Key);
                    _indices.InternalRemove(kvp.Key, entity);
                    return kvp.Key;
                }
            }

            return -1;
        }
    }

    public void RemoveAll()
    {
        lock (_lock)
        {
            EnsureMutable();

            _entities.Clear();
            _indices.InternalRemoveAll();
            _nextId = 0;
        }
    }

    public void Clear() => RemoveAll();

    public void MarkReadOnly()
    {
        lock (_lock)
        {
            _readOnlyCount++;
        }
    }

    public void UnmarkReadOnly()
    {
        lock (_lock)
        {
            if (_readOnlyCount > 0)
                _readOnlyCount--;
        }
    }

    public bool ClearReadOnlyMarks()
    {
        lock (_lock)
        {
            var hadMarks = _readOnlyCount > 0;
            _readOnlyCount = 0;
            return hadMarks;
        }
    }

    public T Set(long entityId, T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        lock (_lock)
        {
            EnsureMutable();

            if (!_entities.TryGetValue(entityId, out var oldEntity))
                throw new ArgumentException($"Entity with ID {entityId} not found", nameof(entityId));

            _constraints.Check(entityId, oldEntity, entity);

            _entities[entityId] = entity;
            _indices.InternalUpdate(entityId, oldEntity, entity);

            return oldEntity;
        }
    }

    public long Replace(T current, T replacement)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        if (ReferenceEquals(current, replacement))
            throw new ArgumentException("Current and replacement cannot be the same object");

        lock (_lock)
        {
            EnsureMutable();

            // Find current entity
            foreach (var kvp in _entities)
            {
                if (_equalityComparer.Equals(kvp.Value, current))
                {
                    _constraints.Check(kvp.Key, current, replacement);

                    _entities[kvp.Key] = replacement;
                    _indices.InternalUpdate(kvp.Key, current, replacement);

                    return kvp.Key;
                }
            }

            throw new ArgumentException("Current entity not found in collection");
        }
    }

    public T Update(T current, Action<T> updateAction)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        if (updateAction == null)
            throw new ArgumentNullException(nameof(updateAction));

        lock (_lock)
        {
            EnsureMutable();

            // Find current entity
            foreach (var kvp in _entities)
            {
                if (_equalityComparer.Equals(kvp.Value, current))
                {
                    var oldEntity = kvp.Value;
                    updateAction(current);

                    _constraints.Check(kvp.Key, oldEntity, current);
                    _indices.InternalUpdate(kvp.Key, oldEntity, current);

                    return current;
                }
            }

            throw new ArgumentException("Current entity not found in collection");
        }
    }

    public TResult Apply<TResult>(T current, Func<T, TResult> logic)
    {
        if (current == null)
            throw new ArgumentNullException(nameof(current));
        if (logic == null)
            throw new ArgumentNullException(nameof(logic));

        lock (_lock)
        {
            EnsureMutable();

            // Find current entity
            foreach (var kvp in _entities)
            {
                if (_equalityComparer.Equals(kvp.Value, current))
                {
                    var oldEntity = kvp.Value;
                    var result = logic(current);

                    _constraints.Check(kvp.Key, oldEntity, current);
                    _indices.InternalUpdate(kvp.Key, oldEntity, current);

                    return result;
                }
            }

            throw new ArgumentException("Current entity not found in collection");
        }
    }

    public void Release()
    {
        lock (_lock)
        {
            // In full implementation, this would release lazy-loaded segments
            // For now, just ensure we're not read-only
            EnsureMutable();
        }
    }

    public IGigaMap<T> RegisterIndices(IIndexCategory<T> indexCategory)
    {
        if (indexCategory == null)
            throw new ArgumentNullException(nameof(indexCategory));

        _indices.Register(indexCategory);
        return this;
    }

    public IGigaQuery<T> Query()
    {
        return new DefaultGigaQuery<T>(this);
    }

    public IConditionBuilder<T, TKey> Query<TKey>(IIndexIdentifier<T, TKey> index)
    {
        return Query().And(index);
    }

    public IGigaQuery<T> Query<TKey>(IIndexIdentifier<T, TKey> index, TKey key)
    {
        return Query().And(index.Is(key));
    }

    public IGigaQuery<T> Query(ICondition<T> condition)
    {
        return Query().And(condition);
    }

    public IConditionBuilder<T, string> Query(string stringIndexName)
    {
        return Query().And(stringIndexName);
    }

    public IGigaQuery<T> Query(string stringIndexName, string key)
    {
        return Query().And(stringIndexName, key);
    }

    public async Task<long> StoreAsync()
    {
        if (_storageConnection == null)
        {
            // If no storage connection is set, return 0 (no persistence)
            await Task.CompletedTask;
            return 0;
        }

        lock (_lock)
        {
            var storer = _storageConnection; // IStorageConnection implements IStorer directly

            // Store all entities
            var entityCount = 0;
            foreach (var entity in _entities.Values)
            {
                storer.Store(entity);
                entityCount++;
            }

            // Store the indices metadata (simplified for now)
            var indexMetadata = new GigaMapMetadata
            {
                EntityCount = _entities.Count,
                NextId = _nextId,
                TypeName = typeof(T).AssemblyQualifiedName!,
                IndexNames = _indices.Indexers.Select(i => i.Name).ToList()
            };
            storer.Store(indexMetadata);

            // Commit all changes
            var committedCount = storer.Commit();
            return committedCount;
        }
    }

    /// <summary>
    /// Sets the storage connection for persistence operations.
    /// This is called internally by the storage manager.
    /// </summary>
    /// <param name="storageConnection">The storage connection to use</param>
    public void SetStorageConnection(IStorageConnection storageConnection)
    {
        _storageConnection = storageConnection;
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (_lock)
        {
            // Create a snapshot to avoid modification during enumeration
            var snapshot = new List<T>(_entities.Values);
            return snapshot.GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        if (!_disposed)
        {
            lock (_lock)
            {
                _entities.Clear();
                _disposed = true;
            }
        }
    }

    private void EnsureMutable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(DefaultGigaMap<T>));

        if (_readOnlyCount > 0)
            throw new InvalidOperationException("GigaMap is currently read-only");
    }
}

/// <summary>
/// Metadata for GigaMap persistence.
/// </summary>
public class GigaMapMetadata
{
    public long EntityCount { get; set; }
    public long NextId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public List<string> IndexNames { get; set; } = new();
}