using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using NebulaStore.GigaMap;
using NebulaStore.Storage.Embedded.Types;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// AFS-aware GigaMap implementation that provides seamless integration with 
/// NebulaStore's Abstract File System following Eclipse Store patterns.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class AfsGigaMap<T> : IGigaMap<T> where T : class
{
    private readonly IGigaMap<T> _innerGigaMap;
    private readonly IStorageConnection _storageConnection;
    private readonly string _entityTypeName;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isLoaded;
    private GigaMapPersistenceMetadata? _metadata;

    /// <summary>
    /// Initializes a new instance of the AfsGigaMap class.
    /// </summary>
    /// <param name="innerGigaMap">The underlying GigaMap implementation</param>
    /// <param name="storageConnection">The AFS storage connection</param>
    public AfsGigaMap(IGigaMap<T> innerGigaMap, IStorageConnection storageConnection)
    {
        _innerGigaMap = innerGigaMap ?? throw new ArgumentNullException(nameof(innerGigaMap));
        _storageConnection = storageConnection ?? throw new ArgumentNullException(nameof(storageConnection));
        _entityTypeName = typeof(T).AssemblyQualifiedName ?? typeof(T).FullName ?? typeof(T).Name;
        _isLoaded = false;
    }

    #region IGigaMap<T> Implementation - Delegated with Lazy Loading

    public long Size 
    { 
        get 
        { 
            EnsureLoaded();
            return _innerGigaMap.Size; 
        } 
    }

    public bool IsEmpty 
    { 
        get 
        { 
            EnsureLoaded();
            return _innerGigaMap.IsEmpty; 
        } 
    }

    public bool IsReadOnly => _innerGigaMap.IsReadOnly;

    public IGigaIndices<T> Index 
    { 
        get 
        { 
            EnsureLoaded();
            return _innerGigaMap.Index; 
        } 
    }

    public IGigaConstraints<T> Constraints => _innerGigaMap.Constraints;

    public IEqualityComparer<T> EqualityComparer => _innerGigaMap.EqualityComparer;

    public T? Get(long entityId)
    {
        EnsureLoaded();
        return _innerGigaMap.Get(entityId);
    }

    public long Add(T element)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Add(element);
        
        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
        
        return result;
    }

    public long AddAll(IEnumerable<T> elements)
    {
        EnsureLoaded();
        var result = _innerGigaMap.AddAll(elements);
        
        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
        
        return result;
    }

    public T? Peek(long entityId)
    {
        EnsureLoaded();
        return _innerGigaMap.Peek(entityId);
    }

    public T? RemoveById(long entityId)
    {
        EnsureLoaded();
        var result = _innerGigaMap.RemoveById(entityId);
        
        if (result != null)
        {
            // Auto-store following Eclipse Store pattern
            _ = Task.Run(() => StoreAsync());
        }
        
        return result;
    }

    public long RemoveAll(IEnumerable<T> elements)
    {
        EnsureLoaded();

        if (elements == null)
            throw new ArgumentNullException(nameof(elements));

        var removedCount = 0L;
        foreach (var element in elements)
        {
            var result = _innerGigaMap.Remove(element);
            if (result != -1)
            {
                removedCount++;
            }
        }

        if (removedCount > 0)
        {
            // Auto-store following Eclipse Store pattern
            _ = Task.Run(() => StoreAsync());
        }

        return removedCount;
    }

    public T? Set(long entityId, T element)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Set(entityId, element);
        
        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
        
        return result;
    }

    public T Update(T current, Action<T> updater)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Update(current, updater);

        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());

        return result;
    }

    public TResult Apply<TResult>(T current, Func<T, TResult> logic)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Apply(current, logic);
        
        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
        
        return result;
    }

    public IGigaQuery<T> Query()
    {
        EnsureLoaded();
        return _innerGigaMap.Query();
    }

    public IGigaQuery<T> Query<TKey>(IIndexIdentifier<T, TKey> indexIdentifier, TKey key)
    {
        EnsureLoaded();
        return _innerGigaMap.Query(indexIdentifier, key);
    }

    public IGigaQuery<T> Query(string stringIndexName, string key)
    {
        EnsureLoaded();
        return _innerGigaMap.Query(stringIndexName, key);
    }

    public long Store()
    {
        return StoreAsync().GetAwaiter().GetResult();
    }

    // Additional IGigaMap<T> interface methods
    public long Remove(T element)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Remove(element);

        if (result != -1)
        {
            // Auto-store following Eclipse Store pattern
            _ = Task.Run(() => StoreAsync());
        }

        return result;
    }

    public long Remove<TKey>(T element, IIndexer<T, TKey> indexer)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Remove(element, indexer);

        if (result != -1)
        {
            // Auto-store following Eclipse Store pattern
            _ = Task.Run(() => StoreAsync());
        }

        return result;
    }

    public void RemoveAll()
    {
        EnsureLoaded();
        _innerGigaMap.RemoveAll();

        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
    }

    public void Clear()
    {
        EnsureLoaded();
        _innerGigaMap.Clear();

        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());
    }

    public void MarkReadOnly()
    {
        _innerGigaMap.MarkReadOnly();
    }

    public void UnmarkReadOnly()
    {
        _innerGigaMap.UnmarkReadOnly();
    }

    public bool ClearReadOnlyMarks()
    {
        return _innerGigaMap.ClearReadOnlyMarks();
    }

    public long Replace(T oldElement, T newElement)
    {
        EnsureLoaded();
        var result = _innerGigaMap.Replace(oldElement, newElement);

        // Auto-store following Eclipse Store pattern
        _ = Task.Run(() => StoreAsync());

        return result;
    }

    public void Release()
    {
        _innerGigaMap.Release();
    }

    public IGigaMap<T> RegisterIndices(IIndexCategory<T> indexCategory)
    {
        _innerGigaMap.RegisterIndices(indexCategory);
        return this;
    }

    public IConditionBuilder<T, TKey> Query<TKey>(IIndexIdentifier<T, TKey> indexIdentifier)
    {
        EnsureLoaded();
        return _innerGigaMap.Query(indexIdentifier);
    }

    public IGigaQuery<T> Query(ICondition<T> condition)
    {
        EnsureLoaded();
        return _innerGigaMap.Query(condition);
    }

    public IConditionBuilder<T, string> Query(string indexName)
    {
        EnsureLoaded();
        return _innerGigaMap.Query(indexName);
    }

    public void Iterate(Action<T> action)
    {
        EnsureLoaded();
        _innerGigaMap.Iterate(action);
    }

    public void IterateIndexed(Action<long, T> action)
    {
        EnsureLoaded();
        _innerGigaMap.IterateIndexed(action);
    }

    public string ToString(int maxLength)
    {
        EnsureLoaded();
        return _innerGigaMap.ToString(maxLength);
    }

    public string ToString(int maxLength, int maxElements)
    {
        EnsureLoaded();
        return _innerGigaMap.ToString(maxLength, maxElements);
    }

    public long HighestUsedId
    {
        get
        {
            EnsureLoaded();
            return _innerGigaMap.HighestUsedId;
        }
    }

    #endregion

    #region AFS Integration Methods

    /// <summary>
    /// Ensures the GigaMap data is loaded from AFS storage.
    /// This implements Eclipse Store's lazy loading pattern.
    /// </summary>
    private void EnsureLoaded()
    {
        if (_isLoaded || _disposed)
            return;

        lock (_lock)
        {
            if (_isLoaded || _disposed)
                return;

            try
            {
                LoadFromAfsAsync().GetAwaiter().GetResult();
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                // If loading fails, we start with an empty GigaMap
                // This follows Eclipse Store's resilient loading pattern
                Console.WriteLine($"Warning: Failed to load GigaMap for {_entityTypeName}: {ex.Message}");
                _isLoaded = true;
            }
        }
    }

    /// <summary>
    /// Loads GigaMap data from AFS storage.
    /// </summary>
    private async Task LoadFromAfsAsync()
    {
        try
        {
            // Load metadata first
            var metadataPath = GetMetadataPath();
            if (await AfsPathExistsAsync(metadataPath))
            {
                var metadataBytes = await ReadFromAfsAsync(metadataPath);
                _metadata = MessagePackSerializer.Deserialize<GigaMapPersistenceMetadata>(metadataBytes);

                // Load entities
                var entitiesPath = _metadata.EntitiesPath;
                if (await AfsPathExistsAsync(entitiesPath))
                {
                    var entitiesBytes = await ReadFromAfsAsync(entitiesPath);
                    var entities = MessagePackSerializer.Deserialize<Dictionary<long, T>>(entitiesBytes);

                    // Populate the inner GigaMap
                    foreach (var kvp in entities)
                    {
                        _innerGigaMap.Set(kvp.Key, kvp.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load GigaMap from AFS: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Stores GigaMap data to AFS storage.
    /// This implements Eclipse Store's incremental persistence pattern.
    /// </summary>
    private async Task<long> StoreAsync()
    {
        if (_disposed)
            return 0;

        lock (_lock)
        {
            try
            {
                // Prepare metadata
                _metadata = new GigaMapPersistenceMetadata
                {
                    EntityTypeName = _entityTypeName,
                    EntityCount = _innerGigaMap.Size,
                    NextEntityId = GetNextEntityId(),
                    IndexNames = _innerGigaMap.Index.Indexers.Select(i => i.Name).ToList(),
                    EntitiesPath = GetEntitiesPath(),
                    IndicesPath = GetIndicesPath(),
                    LastStoredAt = DateTime.UtcNow,
                    StorageVersion = 1
                };

                // Store entities
                var entities = ExtractEntities();
                var entitiesBytes = MessagePackSerializer.Serialize(entities);
                WriteToAfsAsync(_metadata.EntitiesPath, entitiesBytes).GetAwaiter().GetResult();

                // Store metadata
                var metadataBytes = MessagePackSerializer.Serialize(_metadata);
                WriteToAfsAsync(GetMetadataPath(), metadataBytes).GetAwaiter().GetResult();

                return entities.Count;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to store GigaMap to AFS: {ex.Message}", ex);
            }
        }
    }

    #endregion

    #region Helper Methods

    private Dictionary<long, T> ExtractEntities()
    {
        var entities = new Dictionary<long, T>();
        
        // Extract all entities from the inner GigaMap
        // This is a simplified approach - in a full implementation,
        // we would use the GigaMap's internal entity enumeration
        foreach (var entity in _innerGigaMap)
        {
            // For now, we'll use a simple approach to get entity IDs
            // In a full implementation, this would be more sophisticated
            var entityId = entities.Count;
            entities[entityId] = entity;
        }
        
        return entities;
    }

    private long GetNextEntityId()
    {
        // This would be tracked properly in a full implementation
        return _innerGigaMap.Size + 1;
    }

    private string GetMetadataPath() => $"gigamap/{_entityTypeName}/metadata.msgpack";
    private string GetEntitiesPath() => $"gigamap/{_entityTypeName}/entities.msgpack";
    private string GetIndicesPath() => $"gigamap/{_entityTypeName}/indices.msgpack";

    private async Task<bool> AfsPathExistsAsync(string path)
    {
        // This would use the actual AFS API to check if a path exists
        // For now, we'll return false to indicate no existing data
        await Task.CompletedTask;
        return false;
    }

    private async Task<byte[]> ReadFromAfsAsync(string path)
    {
        // This would use the actual AFS API to read data
        // For now, we'll return empty data
        await Task.CompletedTask;
        return Array.Empty<byte>();
    }

    private async Task WriteToAfsAsync(string path, byte[] data)
    {
        // This would use the actual AFS API to write data
        // For now, we'll just complete the task
        await Task.CompletedTask;
    }

    #endregion

    #region IEnumerable<T> Implementation

    public IEnumerator<T> GetEnumerator()
    {
        EnsureLoaded();
        return _innerGigaMap.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        if (!_disposed)
        {
            // Store any pending changes before disposing
            if (_isLoaded)
            {
                try
                {
                    Store();
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _innerGigaMap?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}
