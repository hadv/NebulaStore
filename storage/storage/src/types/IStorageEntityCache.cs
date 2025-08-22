using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Interface for storage entity cache that manages in-memory entity storage and garbage collection.
/// </summary>
public interface IStorageEntityCache : IStorageChannelResetablePart
{
    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the entity count.
    /// </summary>
    long EntityCount { get; }

    /// <summary>
    /// Gets the cache size in bytes.
    /// </summary>
    long CacheSize { get; }

    /// <summary>
    /// Looks up a type by its type ID.
    /// </summary>
    /// <param name="typeId">The type ID to look up.</param>
    /// <returns>The storage entity type, or null if not found.</returns>
    IStorageEntityType? LookupType(long typeId);

    /// <summary>
    /// Performs incremental entity cache check within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for cache checking.</param>
    /// <returns>True if cache checking completed within the time budget.</returns>
    bool IncrementalEntityCacheCheck(TimeSpan timeBudget);

    /// <summary>
    /// Performs incremental garbage collection within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for garbage collection.</param>
    /// <param name="channel">The storage channel.</param>
    /// <returns>True if garbage collection completed within the time budget.</returns>
    bool IncrementalGarbageCollection(TimeSpan timeBudget, IStorageChannel channel);

    /// <summary>
    /// Issues a garbage collection operation within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for garbage collection.</param>
    /// <param name="channel">The storage channel.</param>
    /// <returns>True if garbage collection completed within the time budget.</returns>
    bool IssuedGarbageCollection(TimeSpan timeBudget, IStorageChannel channel);

    /// <summary>
    /// Issues an entity cache check within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for cache checking.</param>
    /// <param name="entityEvaluator">The entity cache evaluator to use.</param>
    /// <returns>True if cache checking completed within the time budget.</returns>
    bool IssuedEntityCacheCheck(TimeSpan timeBudget, IStorageEntityCacheEvaluator entityEvaluator);

    /// <summary>
    /// Copies root entities to the specified data collector.
    /// </summary>
    /// <param name="dataCollector">The data collector to copy roots to.</param>
    void CopyRoots(IChunksBuffer dataCollector);

    /// <summary>
    /// Clears the cache and returns the amount of memory freed.
    /// </summary>
    /// <returns>The amount of cache memory cleared in bytes.</returns>
    long ClearCache();

    /// <summary>
    /// Gets an entity by its object ID.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <returns>The storage entity, or null if not found.</returns>
    IStorageEntity? GetEntry(long objectId);

    /// <summary>
    /// Puts an entity with the specified object ID and type.
    /// </summary>
    /// <param name="objectId">The object ID.</param>
    /// <param name="type">The entity type.</param>
    /// <returns>The storage entity.</returns>
    IStorageEntity PutEntity(long objectId, IStorageEntityType type);

    /// <summary>
    /// Registers a pending store update.
    /// </summary>
    void RegisterPendingStoreUpdate();

    /// <summary>
    /// Clears a pending store update.
    /// </summary>
    void ClearPendingStoreUpdate();

    /// <summary>
    /// Updates entities after a store operation.
    /// </summary>
    /// <param name="chunks">The stored chunks.</param>
    /// <param name="chunksStoragePositions">The storage positions of the chunks.</param>
    /// <param name="dataFile">The data file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PostStorePutEntitiesAsync(byte[][] chunks, long[] chunksStoragePositions, IStorageLiveDataFile dataFile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates entities and returns ID analysis.
    /// </summary>
    /// <returns>The storage ID analysis.</returns>
    IStorageIdAnalysis ValidateEntities();

    /// <summary>
    /// Modifies the used cache size by the specified amount.
    /// </summary>
    /// <param name="cacheChange">The cache size change (can be negative).</param>
    void ModifyUsedCacheSize(long cacheChange);

    /// <summary>
    /// Loads data for the specified entity.
    /// </summary>
    /// <param name="dataFile">The data file containing the entity.</param>
    /// <param name="entity">The entity to load data for.</param>
    /// <param name="length">The length of data to load.</param>
    /// <param name="cacheChange">The cache size change.</param>
    void LoadData(IStorageLiveDataFile dataFile, IStorageEntity entity, long length, long cacheChange);
}

/// <summary>
/// Interface for storage entity types.
/// </summary>
public interface IStorageEntityType
{
    /// <summary>
    /// Gets the type ID.
    /// </summary>
    long TypeId { get; }

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    int ChannelIndex { get; }

    /// <summary>
    /// Gets the type handler.
    /// </summary>
    IStorageEntityTypeHandler TypeHandler { get; }

    /// <summary>
    /// Gets the entity count for this type.
    /// </summary>
    long EntityCount { get; }

    /// <summary>
    /// Gets a value indicating whether this type is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Gets a value indicating whether entities of this type have references.
    /// </summary>
    bool HasReferences { get; }

    /// <summary>
    /// Gets the simple reference data count.
    /// </summary>
    int SimpleReferenceDataCount { get; }

    /// <summary>
    /// Adds an entity to this type.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(IStorageEntity entity);

    /// <summary>
    /// Removes an entity from this type.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    /// <param name="previousInType">The previous entity in the type chain.</param>
    void Remove(IStorageEntity entity, IStorageEntity previousInType);

    /// <summary>
    /// Iterates over all entities of this type.
    /// </summary>
    /// <param name="action">The action to perform for each entity.</param>
    void IterateEntities(Action<IStorageEntity> action);

    /// <summary>
    /// Validates entities of this type and returns ID analysis.
    /// </summary>
    /// <returns>The storage ID analysis for this type.</returns>
    IStorageIdAnalysis ValidateEntities();
}

/// <summary>
/// Enhanced interface for storage entities with additional functionality.
/// </summary>
public interface IStorageEntity
{
    /// <summary>
    /// Gets the object ID.
    /// </summary>
    long ObjectId { get; }

    /// <summary>
    /// Gets the type ID.
    /// </summary>
    long TypeId { get; }

    /// <summary>
    /// Gets the entity length.
    /// </summary>
    long Length { get; }

    /// <summary>
    /// Gets the storage position.
    /// </summary>
    long StoragePosition { get; }

    /// <summary>
    /// Gets the time when this entity was last touched (accessed).
    /// </summary>
    long LastTouched { get; }

    /// <summary>
    /// Gets the length of the cached data for this entity.
    /// </summary>
    long CachedDataLength { get; }

    /// <summary>
    /// Gets a value indicating whether this entity has references to other entities.
    /// </summary>
    bool HasReferences { get; }

    /// <summary>
    /// Gets a value indicating whether this entity is live (has cached data).
    /// </summary>
    bool IsLive { get; }

    /// <summary>
    /// Gets a value indicating whether this entity is deleted.
    /// </summary>
    bool IsDeleted { get; }

    /// <summary>
    /// Gets a value indicating whether this entity is a proper entity (not a head/tail dummy).
    /// </summary>
    bool IsProper { get; }

    /// <summary>
    /// Gets the type in file reference.
    /// </summary>
    IStorageEntityTypeInFile TypeInFile { get; }

    /// <summary>
    /// Gets the next entity in the file chain.
    /// </summary>
    IStorageEntity? FileNext { get; set; }

    /// <summary>
    /// Gets the next entity in the type chain.
    /// </summary>
    IStorageEntity? TypeNext { get; set; }

    /// <summary>
    /// Gets the next entity in the hash chain.
    /// </summary>
    IStorageEntity? HashNext { get; set; }

    /// <summary>
    /// Touches the entity to update its last accessed time.
    /// </summary>
    void Touch();

    /// <summary>
    /// Updates the storage information for this entity.
    /// </summary>
    /// <param name="length">The entity length.</param>
    /// <param name="storagePosition">The storage position.</param>
    void UpdateStorageInformation(long length, long storagePosition);

    /// <summary>
    /// Puts cached data for this entity.
    /// </summary>
    /// <param name="dataAddress">The data address.</param>
    /// <param name="length">The data length.</param>
    void PutCacheData(IntPtr dataAddress, long length);

    /// <summary>
    /// Clears the cached data for this entity.
    /// </summary>
    /// <returns>The amount of cache memory cleared.</returns>
    long ClearCache();

    /// <summary>
    /// Detaches the entity from its file.
    /// </summary>
    void DetachFromFile();

    /// <summary>
    /// Sets the entity as deleted.
    /// </summary>
    void SetDeleted();

    /// <summary>
    /// Copies cached data to the specified data collector.
    /// </summary>
    /// <param name="dataCollector">The data collector.</param>
    void CopyCachedData(IChunksBuffer dataCollector);

    /// <summary>
    /// Iterates over reference IDs in this entity.
    /// </summary>
    /// <param name="referenceMarker">The reference marker to use.</param>
    /// <returns>True if marking required loading, false otherwise.</returns>
    bool IterateReferenceIds(IStorageReferenceMarker referenceMarker);

    /// <summary>
    /// Marks the entity as white (unmarked) for garbage collection.
    /// </summary>
    void MarkWhite();

    /// <summary>
    /// Marks the entity as gray (marked but references not processed) for garbage collection.
    /// </summary>
    void MarkGray();

    /// <summary>
    /// Marks the entity as black (marked and references processed) for garbage collection.
    /// </summary>
    void MarkBlack();

    /// <summary>
    /// Gets a value indicating whether the entity is marked for garbage collection.
    /// </summary>
    bool IsGcMarked { get; }

    /// <summary>
    /// Gets a value indicating whether the entity is marked as black for garbage collection.
    /// </summary>
    bool IsGcBlack { get; }
}

/// <summary>
/// Interface for storage entity type in file references.
/// </summary>
public interface IStorageEntityTypeInFile
{
    /// <summary>
    /// Gets the storage entity type.
    /// </summary>
    IStorageEntityType Type { get; }

    /// <summary>
    /// Gets the storage file.
    /// </summary>
    IStorageLiveDataFile File { get; }
}

/// <summary>
/// Interface for storage reference markers.
/// </summary>
public interface IStorageReferenceMarker
{
    /// <summary>
    /// Marks a reference with the specified object ID.
    /// </summary>
    /// <param name="objectId">The object ID to mark.</param>
    void Mark(long objectId);

    /// <summary>
    /// Tries to flush pending marks.
    /// </summary>
    void TryFlush();
}

/// <summary>
/// Interface for storage mark monitors.
/// </summary>
public interface IStorageMarkMonitor
{
    /// <summary>
    /// Resets the mark monitor.
    /// </summary>
    void Reset();

    /// <summary>
    /// Signals a pending store update.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    void SignalPendingStoreUpdate(IStorageEntityCache entityCache);

    /// <summary>
    /// Clears a pending store update.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    void ClearPendingStoreUpdate(IStorageEntityCache entityCache);

    /// <summary>
    /// Resets completion state.
    /// </summary>
    void ResetCompletion();

    /// <summary>
    /// Gets a value indicating whether there is a pending sweep.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    /// <returns>True if there is a pending sweep.</returns>
    bool IsPendingSweep(IStorageEntityCache entityCache);

    /// <summary>
    /// Gets a value indicating whether garbage collection is complete.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    /// <returns>True if garbage collection is complete.</returns>
    bool IsComplete(IStorageEntityCache entityCache);

    /// <summary>
    /// Gets a value indicating whether marking is complete.
    /// </summary>
    /// <returns>True if marking is complete.</returns>
    bool IsMarkingComplete();

    /// <summary>
    /// Gets a value indicating whether a sweep is needed.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    /// <returns>True if a sweep is needed.</returns>
    bool NeedsSweep(IStorageEntityCache entityCache);

    /// <summary>
    /// Enqueues an object ID for marking.
    /// </summary>
    /// <param name="markQueue">The mark queue.</param>
    /// <param name="objectId">The object ID to enqueue.</param>
    void Enqueue(IStorageObjectIdMarkQueue markQueue, long objectId);

    /// <summary>
    /// Advances marking by the specified count.
    /// </summary>
    /// <param name="markQueue">The mark queue.</param>
    /// <param name="count">The count to advance.</param>
    void AdvanceMarking(IStorageObjectIdMarkQueue markQueue, int count);

    /// <summary>
    /// Completes a sweep operation.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    /// <param name="rootOidSelector">The root OID selector.</param>
    /// <param name="channelRootOid">The channel root OID.</param>
    void CompleteSweep(IStorageEntityCache entityCache, IStorageRootOidSelector rootOidSelector, long channelRootOid);

    /// <summary>
    /// Provides a reference marker for the specified entity cache.
    /// </summary>
    /// <param name="entityCache">The entity cache.</param>
    /// <returns>The reference marker.</returns>
    IStorageReferenceMarker ProvideReferenceMarker(IStorageEntityCache entityCache);
}

/// <summary>
/// Interface for storage object ID mark queues.
/// </summary>
public interface IStorageObjectIdMarkQueue
{
    /// <summary>
    /// Gets a value indicating whether the queue has elements.
    /// </summary>
    bool HasElements { get; }

    /// <summary>
    /// Gets the next batch of object IDs to mark.
    /// </summary>
    /// <param name="buffer">The buffer to fill with object IDs.</param>
    /// <returns>The number of object IDs retrieved.</returns>
    int GetNext(long[] buffer);
}

/// <summary>
/// Interface for storage root OID selectors.
/// </summary>
public interface IStorageRootOidSelector
{
    /// <summary>
    /// Resets the selector.
    /// </summary>
    void Reset();

    /// <summary>
    /// Accepts an object ID for consideration.
    /// </summary>
    /// <param name="objectId">The object ID to accept.</param>
    void Accept(long objectId);

    /// <summary>
    /// Yields the selected root object ID.
    /// </summary>
    /// <returns>The selected root object ID.</returns>
    long Yield();
}
