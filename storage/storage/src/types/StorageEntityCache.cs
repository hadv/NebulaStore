using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Default implementation of IStorageEntityCache that manages in-memory entity storage and garbage collection.
/// </summary>
public class StorageEntityCache : IStorageEntityCache
{
    #region Private Fields

    private readonly int _channelIndex;
    private readonly IStorageTypeDictionary _typeDictionary;
    private readonly ConcurrentDictionary<long, IStorageEntity> _entityCache;
    private readonly ConcurrentDictionary<long, IStorageEntityType> _typeCache;
    private readonly object _lock = new object();
    
    private long _cacheSize;
    private long _entityCount;
    private bool _hasPendingStoreUpdate;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageEntityCache class.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeDictionary">The type dictionary.</param>
    public StorageEntityCache(int channelIndex, IStorageTypeDictionary typeDictionary)
    {
        _channelIndex = channelIndex;
        _typeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
        _entityCache = new ConcurrentDictionary<long, IStorageEntity>();
        _typeCache = new ConcurrentDictionary<long, IStorageEntityType>();
    }

    #endregion

    #region IStorageEntityCache Implementation

    public IStorageTypeDictionary TypeDictionary => _typeDictionary;

    public long EntityCount => Interlocked.Read(ref _entityCount);

    public long CacheSize => Interlocked.Read(ref _cacheSize);

    public IStorageEntityType? LookupType(long typeId)
    {
        _typeCache.TryGetValue(typeId, out var type);
        return type;
    }

    public void Reset()
    {
        lock (_lock)
        {
            // Clear all entities and free their cache data
            foreach (var entity in _entityCache.Values)
            {
                if (entity is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _entityCache.Clear();
            _typeCache.Clear();
            Interlocked.Exchange(ref _cacheSize, 0);
            Interlocked.Exchange(ref _entityCount, 0);
            _hasPendingStoreUpdate = false;
        }
    }

    public bool IncrementalEntityCacheCheck(TimeSpan timeBudget)
    {
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.Add(timeBudget);
        
        // Use default cache evaluator if none provided
        var evaluator = StorageEntityCacheEvaluatorFactory.New();
        
        return InternalCacheCheck(evaluator, endTime);
    }

    public bool IncrementalGarbageCollection(TimeSpan timeBudget, IStorageChannel channel)
    {
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.Add(timeBudget);
        
        return InternalGarbageCollection(endTime, channel);
    }

    public bool IssuedGarbageCollection(TimeSpan timeBudget, IStorageChannel channel)
    {
        return IncrementalGarbageCollection(timeBudget, channel);
    }

    public bool IssuedEntityCacheCheck(TimeSpan timeBudget, IStorageEntityCacheEvaluator entityEvaluator)
    {
        var startTime = DateTimeOffset.UtcNow;
        var endTime = startTime.Add(timeBudget);
        
        return InternalCacheCheck(entityEvaluator, endTime);
    }

    public void CopyRoots(IChunksBuffer dataCollector)
    {
        // Copy root entities to the data collector
        // This would typically iterate through root entities and copy their data
        // For now, this is a placeholder implementation
        foreach (var entity in _entityCache.Values)
        {
            if (entity.IsLive)
            {
                entity.CopyCachedData(dataCollector);
            }
        }
    }

    public long ClearCache()
    {
        long clearedSize = 0;
        
        lock (_lock)
        {
            foreach (var entity in _entityCache.Values)
            {
                clearedSize += entity.ClearCache();
            }
            
            Interlocked.Exchange(ref _cacheSize, 0);
        }
        
        return clearedSize;
    }

    public IStorageEntity? GetEntry(long objectId)
    {
        _entityCache.TryGetValue(objectId, out var entity);
        return entity;
    }

    public IStorageEntity PutEntity(long objectId, IStorageEntityType type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        // Check if entity already exists
        if (_entityCache.TryGetValue(objectId, out var existingEntity))
        {
            return existingEntity;
        }

        // Create type in file reference (file will be set later)
        var typeInFile = new StorageEntityTypeInFile(type, null!);
        
        // Create new entity
        var entity = StorageEntity.New(
            objectId,
            typeInFile,
            null, // hashNext
            type.HasReferences,
            type.SimpleReferenceDataCount);

        // Add to cache
        if (_entityCache.TryAdd(objectId, entity))
        {
            // Add to type
            type.Add(entity);
            
            // Update counters
            Interlocked.Increment(ref _entityCount);
            
            return entity;
        }

        // If another thread added it first, return the existing one
        return _entityCache[objectId];
    }

    public void RegisterPendingStoreUpdate()
    {
        _hasPendingStoreUpdate = true;
    }

    public void ClearPendingStoreUpdate()
    {
        _hasPendingStoreUpdate = false;
    }

    public async Task PostStorePutEntitiesAsync(byte[][] chunks, long[] chunksStoragePositions, IStorageLiveDataFile dataFile, CancellationToken cancellationToken = default)
    {
        // Update entities with their storage positions after a store operation
        await Task.Run(() =>
        {
            // Process chunks and update entity storage positions
            for (int i = 0; i < chunks.Length && i < chunksStoragePositions.Length; i++)
            {
                var chunk = chunks[i];
                var position = chunksStoragePositions[i];
                
                // Parse chunk and update entities
                // This would involve deserializing the chunk header to get entity information
                // and updating the corresponding entities in the cache
            }
        }, cancellationToken);
    }

    public IStorageIdAnalysis ValidateEntities()
    {
        long highestObjectId = 0;
        long highestTypeId = 0;
        long entityCount = 0;

        foreach (var entity in _entityCache.Values)
        {
            if (entity.ObjectId > highestObjectId)
            {
                highestObjectId = entity.ObjectId;
            }
            
            if (entity.TypeId > highestTypeId)
            {
                highestTypeId = entity.TypeId;
            }
            
            entityCount++;
        }

        return new StorageIdAnalysis(highestObjectId, highestTypeId, entityCount);
    }

    public void ModifyUsedCacheSize(long cacheChange)
    {
        Interlocked.Add(ref _cacheSize, cacheChange);
    }

    public void LoadData(IStorageLiveDataFile dataFile, IStorageEntity entity, long length, long cacheChange)
    {
        if (dataFile == null)
            throw new ArgumentNullException(nameof(dataFile));
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        // Load entity data from file
        // This would read the data from the file and put it in the entity's cache
        
        // Update cache size
        ModifyUsedCacheSize(cacheChange);
        
        // Touch the entity to update its last accessed time
        entity.Touch();
    }

    #endregion

    #region Private Methods

    private bool InternalCacheCheck(IStorageEntityCacheEvaluator evaluator, DateTimeOffset endTime)
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var currentCacheSize = CacheSize;
        
        var entitiesToClear = new List<IStorageEntity>();
        
        // Evaluate entities for cache clearing
        foreach (var entity in _entityCache.Values)
        {
            if (DateTimeOffset.UtcNow > endTime)
            {
                return false; // Time budget exceeded
            }
            
            if (entity.IsLive && evaluator.ClearEntityCache(currentCacheSize, currentTime, entity))
            {
                entitiesToClear.Add(entity);
            }
        }
        
        // Clear cache for selected entities
        foreach (var entity in entitiesToClear)
        {
            var clearedSize = entity.ClearCache();
            ModifyUsedCacheSize(-clearedSize);
        }
        
        return true; // Completed within time budget
    }

    private bool InternalGarbageCollection(DateTimeOffset endTime, IStorageChannel channel)
    {
        // Mark phase: Mark all reachable entities starting from roots
        var markQueue = new Queue<long>();
        
        // Add root entities to mark queue
        // This would typically get root object IDs from the channel
        // For now, this is a simplified implementation
        
        // Mark entities as white (unmarked) initially
        foreach (var entity in _entityCache.Values)
        {
            entity.MarkWhite();
        }
        
        // Mark reachable entities
        while (markQueue.Count > 0 && DateTimeOffset.UtcNow <= endTime)
        {
            var objectId = markQueue.Dequeue();
            
            if (_entityCache.TryGetValue(objectId, out var entity))
            {
                if (!entity.IsGcMarked)
                {
                    entity.MarkGray();
                    
                    // Add referenced entities to mark queue
                    var referenceMarker = new SimpleReferenceMarker(markQueue);
                    entity.IterateReferenceIds(referenceMarker);
                    
                    entity.MarkBlack();
                }
            }
        }
        
        if (DateTimeOffset.UtcNow > endTime)
        {
            return false; // Time budget exceeded
        }
        
        // Sweep phase: Remove unmarked entities
        var entitiesToRemove = new List<long>();
        
        foreach (var kvp in _entityCache)
        {
            if (!kvp.Value.IsGcMarked)
            {
                entitiesToRemove.Add(kvp.Key);
            }
        }
        
        foreach (var objectId in entitiesToRemove)
        {
            if (_entityCache.TryRemove(objectId, out var entity))
            {
                var clearedSize = entity.ClearCache();
                ModifyUsedCacheSize(-clearedSize);
                Interlocked.Decrement(ref _entityCount);
                
                if (entity is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }
        
        return true; // Completed within time budget
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageEntityCache instance.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeDictionary">The type dictionary.</param>
    /// <returns>A new StorageEntityCache instance.</returns>
    public static StorageEntityCache New(int channelIndex, IStorageTypeDictionary typeDictionary)
    {
        return new StorageEntityCache(channelIndex, typeDictionary);
    }

    #endregion
}

/// <summary>
/// Simple implementation of IStorageReferenceMarker for garbage collection.
/// </summary>
internal class SimpleReferenceMarker : IStorageReferenceMarker
{
    private readonly Queue<long> _markQueue;

    public SimpleReferenceMarker(Queue<long> markQueue)
    {
        _markQueue = markQueue ?? throw new ArgumentNullException(nameof(markQueue));
    }

    public void Mark(long objectId)
    {
        _markQueue.Enqueue(objectId);
    }

    public void TryFlush()
    {
        // Nothing to flush in this simple implementation
    }
}
