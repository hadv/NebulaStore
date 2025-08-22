using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Represents a storage channel that handles storage operations for a specific partition of data.
/// Each channel runs in its own thread and manages its own entity cache and file operations.
/// </summary>
public interface IStorageChannel : IRunnable, IStorageChannelResetablePart, IStorageActivePart, IDisposable
{
    /// <summary>
    /// Gets the channel index.
    /// </summary>
    new int ChannelIndex { get; }

    /// <summary>
    /// Gets the type dictionary for this channel.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Collects and loads entities by their object IDs.
    /// </summary>
    /// <param name="channelChunks">The channel chunks buffer array.</param>
    /// <param name="loadOids">The object IDs to load.</param>
    /// <returns>A chunks buffer containing the loaded entities.</returns>
    IChunksBuffer CollectLoadByOids(IChunksBuffer[] channelChunks, IPersistenceIdSet loadOids);

    /// <summary>
    /// Collects and loads all root entities.
    /// </summary>
    /// <param name="channelChunks">The channel chunks buffer array.</param>
    /// <returns>A chunks buffer containing the root entities.</returns>
    IChunksBuffer CollectLoadRoots(IChunksBuffer[] channelChunks);

    /// <summary>
    /// Collects and loads entities by their type IDs.
    /// </summary>
    /// <param name="channelChunks">The channel chunks buffer array.</param>
    /// <param name="loadTids">The type IDs to load.</param>
    /// <returns>A chunks buffer containing the loaded entities.</returns>
    IChunksBuffer CollectLoadByTids(IChunksBuffer[] channelChunks, IPersistenceIdSet loadTids);

    /// <summary>
    /// Stores entities from chunk data.
    /// </summary>
    /// <param name="timestamp">The timestamp for the store operation.</param>
    /// <param name="chunkData">The chunk data containing entities to store.</param>
    /// <returns>A key-value pair of byte buffers and their storage positions.</returns>
    KeyValuePair<byte[][], long[]> StoreEntities(long timestamp, IChunk chunkData);

    /// <summary>
    /// Rolls back the current chunk storage operation.
    /// </summary>
    void RollbackChunkStorage();

    /// <summary>
    /// Commits the current chunk storage operation.
    /// </summary>
    void CommitChunkStorage();

    /// <summary>
    /// Updates the entity cache after a store operation.
    /// </summary>
    /// <param name="chunks">The stored chunks.</param>
    /// <param name="chunksStoragePositions">The storage positions of the chunks.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task PostStoreUpdateEntityCacheAsync(byte[][] chunks, long[] chunksStoragePositions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the storage inventory.
    /// </summary>
    /// <returns>The storage inventory.</returns>
    IStorageInventory ReadStorage();

    /// <summary>
    /// Issues a garbage collection operation with the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for garbage collection.</param>
    /// <returns>True if garbage collection completed within the time budget.</returns>
    bool IssuedGarbageCollection(TimeSpan timeBudget);

    /// <summary>
    /// Issues a file cleanup check with the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for file cleanup.</param>
    /// <returns>True if file cleanup completed within the time budget.</returns>
    bool IssuedFileCleanupCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues an entity cache check with the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for cache checking.</param>
    /// <param name="entityEvaluator">The entity cache evaluator to use.</param>
    /// <returns>True if cache checking completed within the time budget.</returns>
    bool IssuedEntityCacheCheck(TimeSpan timeBudget, IStorageEntityCacheEvaluator entityEvaluator);

    /// <summary>
    /// Issues a transaction log cleanup operation.
    /// </summary>
    /// <returns>True if the cleanup was successful.</returns>
    bool IssuedTransactionsLogCleanup();

    /// <summary>
    /// Exports data using the specified file provider.
    /// </summary>
    /// <param name="fileProvider">The file provider for export.</param>
    void ExportData(IStorageLiveFileProvider fileProvider);

    /// <summary>
    /// Prepares for data import.
    /// </summary>
    /// <returns>The entity cache for import operations.</returns>
    IStorageEntityCache PrepareImportData();

    /// <summary>
    /// Imports data from the specified source.
    /// </summary>
    /// <param name="importSource">The import source.</param>
    void ImportData(IStorageImportSource importSource);

    /// <summary>
    /// Rolls back a data import operation.
    /// </summary>
    /// <param name="cause">The cause of the rollback.</param>
    void RollbackImportData(Exception cause);

    /// <summary>
    /// Commits a data import operation.
    /// </summary>
    /// <param name="taskTimestamp">The task timestamp.</param>
    void CommitImportData(long taskTimestamp);

    /// <summary>
    /// Exports entities of a specific type to a file.
    /// </summary>
    /// <param name="type">The type handler for the entities to export.</param>
    /// <param name="file">The file to export to.</param>
    /// <returns>A key-value pair of byte count and entity count.</returns>
    Task<KeyValuePair<long, long>> ExportTypeEntitiesAsync(IStorageEntityTypeHandler type, Stream file);

    /// <summary>
    /// Exports entities of a specific type to a file with a predicate filter.
    /// </summary>
    /// <param name="type">The type handler for the entities to export.</param>
    /// <param name="file">The file to export to.</param>
    /// <param name="predicateEntity">The predicate to filter entities.</param>
    /// <returns>A key-value pair of byte count and entity count.</returns>
    Task<KeyValuePair<long, long>> ExportTypeEntitiesAsync(IStorageEntityTypeHandler type, Stream file, Predicate<IStorageEntity> predicateEntity);

    /// <summary>
    /// Creates raw file statistics for this channel.
    /// </summary>
    /// <returns>The raw file statistics.</returns>
    IStorageRawFileStatistics CreateRawFileStatistics();

    /// <summary>
    /// Initializes storage with the specified parameters.
    /// </summary>
    /// <param name="taskTimestamp">The task timestamp.</param>
    /// <param name="consistentStoreTimestamp">The consistent store timestamp.</param>
    /// <param name="storageInventory">The storage inventory.</param>
    /// <returns>The storage ID analysis result.</returns>
    IStorageIdAnalysis InitializeStorage(long taskTimestamp, long consistentStoreTimestamp, IStorageInventory storageInventory);

    /// <summary>
    /// Signals that garbage collection sweep has completed.
    /// </summary>
    void SignalGarbageCollectionSweepCompleted();

    /// <summary>
    /// Cleans up the store.
    /// </summary>
    void CleanupStore();

    /// <summary>
    /// Collects adjacency data for export.
    /// </summary>
    /// <param name="exportDirectory">The export directory.</param>
    /// <returns>The adjacency files.</returns>
    IAdjacencyFiles CollectAdjacencyData(DirectoryInfo exportDirectory);
}

/// <summary>
/// Interface for runnable components.
/// </summary>
public interface IRunnable
{
    /// <summary>
    /// Runs the component.
    /// </summary>
    void Run();
}

/// <summary>
/// Interface for storage channel resetable parts.
/// </summary>
public interface IStorageChannelResetablePart
{
    /// <summary>
    /// Resets the component.
    /// </summary>
    void Reset();
}

/// <summary>
/// Interface for storage active parts.
/// </summary>
public interface IStorageActivePart
{
    /// <summary>
    /// Gets a value indicating whether the component is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    int ChannelIndex { get; }
}

/// <summary>
/// Interface for chunks buffer.
/// </summary>
public interface IChunksBuffer
{
    /// <summary>
    /// Completes the buffer.
    /// </summary>
    /// <returns>The completed chunks buffer.</returns>
    IChunksBuffer Complete();

    /// <summary>
    /// Gets the buffers.
    /// </summary>
    byte[][] Buffers { get; }

    /// <summary>
    /// Gets the total size.
    /// </summary>
    long TotalSize { get; }
}

/// <summary>
/// Interface for persistence ID set.
/// </summary>
public interface IPersistenceIdSet : IEnumerable<long>
{
    /// <summary>
    /// Gets the size of the set.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets a value indicating whether the set is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Iterates over the IDs with the specified action.
    /// </summary>
    /// <param name="action">The action to perform for each ID.</param>
    void Iterate(Action<long> action);
}

/// <summary>
/// Interface for chunk data.
/// </summary>
public interface IChunk
{
    /// <summary>
    /// Gets the buffers.
    /// </summary>
    /// <returns>The buffers.</returns>
    byte[][] Buffers();

    /// <summary>
    /// Gets the total size.
    /// </summary>
    long TotalSize { get; }
}

/// <summary>
/// Interface for storage inventory.
/// </summary>
public interface IStorageInventory
{
    /// <summary>
    /// Gets the channel count.
    /// </summary>
    int ChannelCount { get; }

    /// <summary>
    /// Gets the type dictionary.
    /// </summary>
    IStorageTypeDictionary TypeDictionary { get; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    long CreationTimestamp { get; }
}

/// <summary>
/// Interface for storage live file provider.
/// </summary>
public interface IStorageLiveFileProvider
{
    /// <summary>
    /// Provides a file for the specified identifier.
    /// </summary>
    /// <param name="identifier">The file identifier.</param>
    /// <returns>The file stream.</returns>
    Stream ProvideFile(string identifier);
}

/// <summary>
/// Interface for storage import source.
/// </summary>
public interface IStorageImportSource
{
    /// <summary>
    /// Gets the source directory.
    /// </summary>
    DirectoryInfo SourceDirectory { get; }

    /// <summary>
    /// Gets the available files.
    /// </summary>
    /// <returns>The available files.</returns>
    IEnumerable<FileInfo> GetAvailableFiles();
}

// IStorageEntityCache interface is already defined in IStorageEntityCache.cs

/// <summary>
/// Interface for storage entity type handler.
/// </summary>
public interface IStorageEntityTypeHandler
{
    /// <summary>
    /// Gets the type ID.
    /// </summary>
    long TypeId { get; }

    /// <summary>
    /// Gets the type.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    string TypeName { get; }
}

/// <summary>
/// Interface for storage raw file statistics.
/// </summary>
public interface IStorageRawFileStatistics
{
    /// <summary>
    /// Gets the file count.
    /// </summary>
    int FileCount { get; }

    /// <summary>
    /// Gets the total file size.
    /// </summary>
    long TotalFileSize { get; }

    /// <summary>
    /// Gets the live data size.
    /// </summary>
    long LiveDataSize { get; }
}

/// <summary>
/// Interface for storage ID analysis.
/// </summary>
public interface IStorageIdAnalysis
{
    /// <summary>
    /// Gets the highest object ID.
    /// </summary>
    long HighestObjectId { get; }

    /// <summary>
    /// Gets the highest type ID.
    /// </summary>
    long HighestTypeId { get; }

    /// <summary>
    /// Gets the entity count.
    /// </summary>
    long EntityCount { get; }
}

/// <summary>
/// Interface for adjacency files.
/// </summary>
public interface IAdjacencyFiles
{
    /// <summary>
    /// Gets the exported files.
    /// </summary>
    IEnumerable<FileInfo> ExportedFiles { get; }

    /// <summary>
    /// Gets the total file count.
    /// </summary>
    int TotalFileCount { get; }

    /// <summary>
    /// Gets the total file size.
    /// </summary>
    long TotalFileSize { get; }
}
