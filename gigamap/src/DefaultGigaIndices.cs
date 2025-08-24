using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IGigaIndices.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
internal class DefaultGigaIndices<T> : IGigaIndices<T> where T : class
{
    private readonly IGigaMap<T> _parentMap;
    private readonly DefaultBitmapIndices<T> _bitmapIndices;
    private readonly Dictionary<string, IIndexer<T, object>> _indexers = new();

    public DefaultGigaIndices(IGigaMap<T> parentMap)
    {
        _parentMap = parentMap ?? throw new ArgumentNullException(nameof(parentMap));
        _bitmapIndices = new DefaultBitmapIndices<T>(parentMap);
    }

    public IBitmapIndices<T> Bitmap => _bitmapIndices;

    public IReadOnlyCollection<IIndexer<T, object>> Indexers => _indexers.Values.ToList();

    public void Register(IIndexCategory<T> indexCategory)
    {
        if (indexCategory == null)
            throw new ArgumentNullException(nameof(indexCategory));

        foreach (var indexer in indexCategory.Indexers)
        {
            _indexers[indexer.Name] = indexer;
        }
    }

    public IIndexer<T, object>? GetIndexer(string name)
    {
        return _indexers.TryGetValue(name, out var indexer) ? indexer : null;
    }

    public bool HasIndexer(string name)
    {
        return _indexers.ContainsKey(name);
    }

    internal void InternalAdd(long entityId, T entity)
    {
        _bitmapIndices.InternalAdd(entityId, entity);
    }

    internal void InternalRemove(long entityId, T entity)
    {
        _bitmapIndices.InternalRemove(entityId, entity);
    }

    internal void InternalUpdate(long entityId, T oldEntity, T newEntity)
    {
        _bitmapIndices.InternalUpdate(entityId, oldEntity, newEntity);
    }

    internal void InternalRemoveAll()
    {
        _bitmapIndices.InternalRemoveAll();
    }
}

/// <summary>
/// Default implementation of IBitmapIndices.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
internal class DefaultBitmapIndices<T> : IBitmapIndices<T> where T : class
{
    private readonly IGigaMap<T> _parentMap;
    private readonly Dictionary<string, IBitmapIndex<T, object>> _indices = new();
    private readonly HashSet<string> _identityIndices = new();
    private readonly HashSet<string> _uniqueConstraints = new();

    public DefaultBitmapIndices(IGigaMap<T> parentMap)
    {
        _parentMap = parentMap ?? throw new ArgumentNullException(nameof(parentMap));
    }

    public IReadOnlyCollection<IBitmapIndex<T, object>> All => _indices.Values.ToList();

    public int Count => _indices.Count;

    public bool IsEmpty => _indices.Count == 0;

    public void EnsureAll(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        foreach (var indexer in indexers)
        {
            if (!_indices.ContainsKey(indexer.Name))
            {
                Add(indexer);
            }
        }
    }

    public IBitmapIndex<T, TKey> Add<TKey>(IIndexer<T, TKey> indexer) where TKey : notnull
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        // Convert to object indexer for storage
        var objectIndexer = Indexer.AsObjectIndexer(indexer);
        var objectBitmapIndex = new DefaultBitmapIndex<T, object>(this, objectIndexer);
        _indices[indexer.Name] = objectBitmapIndex;

        // For now, return the object bitmap index cast to the expected type
        // This is a simplified approach that works with our current architecture
        return (IBitmapIndex<T, TKey>)(object)objectBitmapIndex;
    }

    public IBitmapIndex<T, TKey>? Get<TKey>(IIndexer<T, TKey> indexer) where TKey : notnull
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        return _indices.TryGetValue(indexer.Name, out var index)
            ? index as IBitmapIndex<T, TKey>
            : null;
    }

    public IBitmapIndex<T, object>? Get(string name)
    {
        return _indices.TryGetValue(name, out var index) ? index : null;
    }

    public void SetIdentityIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        _identityIndices.Clear();
        foreach (var indexer in indexers)
        {
            _identityIndices.Add(indexer.Name);
        }
    }

    public void AddUniqueConstraints(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        foreach (var indexer in indexers)
        {
            _uniqueConstraints.Add(indexer.Name);
        }
    }

    internal void InternalAdd(long entityId, T entity)
    {
        foreach (var index in _indices.Values)
        {
            (index as IInternalBitmapIndex<T>)?.InternalAdd(entityId, entity);
        }
    }

    internal void InternalRemove(long entityId, T entity)
    {
        foreach (var index in _indices.Values)
        {
            (index as IInternalBitmapIndex<T>)?.InternalRemove(entityId, entity);
        }
    }

    internal void InternalUpdate(long entityId, T oldEntity, T newEntity)
    {
        foreach (var index in _indices.Values)
        {
            (index as IInternalBitmapIndex<T>)?.InternalUpdate(entityId, oldEntity, newEntity);
        }
    }

    internal void InternalRemoveAll()
    {
        foreach (var index in _indices.Values)
        {
            (index as IInternalBitmapIndex<T>)?.InternalRemoveAll();
        }
    }
}

/// <summary>
/// Internal interface for bitmap index operations.
/// </summary>
/// <typeparam name="T">The type of entities being indexed</typeparam>
internal interface IInternalBitmapIndex<T> where T : class
{
    void InternalAdd(long entityId, T entity);
    void InternalRemove(long entityId, T entity);
    void InternalUpdate(long entityId, T oldEntity, T newEntity);
    void InternalRemoveAll();
}

