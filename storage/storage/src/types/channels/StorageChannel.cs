using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NebulaStore.Storage.Embedded.Types.Files;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types.Channels;

/// <summary>
/// Default implementation of IStorageChannel that manages storage operations for a specific partition of data.
/// Each channel runs in its own thread and manages its own entity cache and file operations.
/// </summary>
public class StorageChannel : IStorageChannel, IDisposable
{
    #region Private Fields

    private readonly int _channelIndex;
    private readonly IStorageTypeDictionary _typeDictionary;
    private readonly IStorageEntityCache _entityCache;
    private readonly IStorageFileManager _fileManager;
    private readonly object _lock = new object();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private Thread? _workerThread;
    private bool _isActive = false;
    private bool _disposed = false;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageChannel class.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeDictionary">The type dictionary.</param>
    /// <param name="entityCache">The entity cache.</param>
    /// <param name="fileManager">The file manager.</param>
    public StorageChannel(
        int channelIndex,
        IStorageTypeDictionary typeDictionary,
        IStorageEntityCache entityCache,
        IStorageFileManager fileManager)
    {
        _channelIndex = channelIndex;
        _typeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
        _entityCache = entityCache ?? throw new ArgumentNullException(nameof(entityCache));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
    }

    #endregion

    #region IStorageChannel Implementation

    public int ChannelIndex => _channelIndex;

    public IStorageTypeDictionary TypeDictionary => _typeDictionary;

    public IStorageFileManager FileManager => _fileManager;

    public bool IsActive => _isActive;

    public IChunksBuffer CollectLoadByOids(IChunksBuffer[] channelChunks, IPersistenceIdSet loadOids)
    {
        if (channelChunks == null)
            throw new ArgumentNullException(nameof(channelChunks));
        if (loadOids == null)
            throw new ArgumentNullException(nameof(loadOids));

        var dataCollector = new ChunksBuffer();

        loadOids.Iterate(objectId =>
        {
            var entity = _entityCache.GetEntry(objectId);
            if (entity != null && entity.IsLive)
            {
                entity.CopyCachedData(dataCollector);
            }
        });

        return dataCollector.Complete();
    }

    public IChunksBuffer CollectLoadRoots(IChunksBuffer[] channelChunks)
    {
        if (channelChunks == null)
            throw new ArgumentNullException(nameof(channelChunks));

        var dataCollector = new ChunksBuffer();
        _entityCache.CopyRoots(dataCollector);
        return dataCollector.Complete();
    }

    public IChunksBuffer CollectLoadByTids(IChunksBuffer[] channelChunks, IPersistenceIdSet loadTids)
    {
        if (channelChunks == null)
            throw new ArgumentNullException(nameof(channelChunks));
        if (loadTids == null)
            throw new ArgumentNullException(nameof(loadTids));

        var dataCollector = new ChunksBuffer();

        loadTids.Iterate(typeId =>
        {
            var entityType = _entityCache.LookupType(typeId);
            if (entityType != null)
            {
                entityType.IterateEntities(entity =>
                {
                    if (entity.IsLive)
                    {
                        entity.CopyCachedData(dataCollector);
                    }
                });
            }
        });

        return dataCollector.Complete();
    }

    public KeyValuePair<byte[][], long[]> StoreEntities(long timestamp, IChunk chunkData)
    {
        if (chunkData == null)
            throw new ArgumentNullException(nameof(chunkData));

        var chunks = chunkData.Buffers();
        var positions = _fileManager.StoreChunks(timestamp, chunks);

        return new KeyValuePair<byte[][], long[]>(chunks, positions);
    }

    public void RollbackChunkStorage()
    {
        _fileManager.RollbackWrite();
    }

    public void CommitChunkStorage()
    {
        _fileManager.CommitWrite();
    }

    public async Task PostStoreUpdateEntityCacheAsync(byte[][] chunks, long[] chunksStoragePositions, CancellationToken cancellationToken = default)
    {
        await _entityCache.PostStorePutEntitiesAsync(chunks, chunksStoragePositions, _fileManager.CurrentStorageFile, cancellationToken);
    }

    public IStorageInventory ReadStorage()
    {
        return _fileManager.ReadStorage();
    }

    public bool IssuedGarbageCollection(TimeSpan timeBudget)
    {
        return _entityCache.IssuedGarbageCollection(timeBudget, this);
    }

    public bool IssuedFileCleanupCheck(TimeSpan timeBudget)
    {
        return _fileManager.IssuedFileCleanupCheck(timeBudget);
    }

    public bool IssuedEntityCacheCheck(TimeSpan timeBudget, IStorageEntityCacheEvaluator entityEvaluator)
    {
        return _entityCache.IssuedEntityCacheCheck(timeBudget, entityEvaluator);
    }

    public bool IssuedTransactionsLogCleanup()
    {
        return _fileManager.IssuedTransactionFileCheck(true);
    }

    public void ExportData(IStorageLiveFileProvider fileProvider)
    {
        _fileManager.ExportData(fileProvider);
    }

    public IStorageEntityCache PrepareImportData()
    {
        _fileManager.PrepareImport();
        return _entityCache;
    }

    public void ImportData(IStorageImportSource importSource)
    {
        _fileManager.CopyData(importSource);
    }

    public void RollbackImportData(Exception cause)
    {
        _fileManager.RollbackImport();
    }

    public void CommitImportData(long taskTimestamp)
    {
        _fileManager.CommitImport(taskTimestamp);
    }

    public async Task<KeyValuePair<long, long>> ExportTypeEntitiesAsync(IStorageEntityTypeHandler type, Stream file)
    {
        return await ExportTypeEntitiesAsync(type, file, null);
    }

    public async Task<KeyValuePair<long, long>> ExportTypeEntitiesAsync(IStorageEntityTypeHandler type, Stream file, Predicate<IStorageEntity>? predicateEntity)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        if (file == null)
            throw new ArgumentNullException(nameof(file));

        return await Task.Run(() =>
        {
            long byteCount = 0;
            long entityCount = 0;

            var entityType = _entityCache.LookupType(type.TypeId);
            if (entityType != null)
            {
                entityType.IterateEntities(entity =>
                {
                    if (predicateEntity == null || predicateEntity(entity))
                    {
                        if (entity.IsLive)
                        {
                            // Export entity data to file
                            // This would typically serialize the entity and write to the stream
                            // For now, this is a placeholder
                            byteCount += entity.CachedDataLength;
                            entityCount++;
                        }
                    }
                });
            }

            return new KeyValuePair<long, long>(byteCount, entityCount);
        });
    }

    public IStorageRawFileStatistics CreateRawFileStatistics()
    {
        return _fileManager.CreateRawFileStatistics();
    }

    public IStorageIdAnalysis InitializeStorage(long taskTimestamp, long consistentStoreTimestamp, IStorageInventory storageInventory)
    {
        return _fileManager.InitializeStorage(taskTimestamp, consistentStoreTimestamp, storageInventory, this);
    }

    public void SignalGarbageCollectionSweepCompleted()
    {
        // Signal that garbage collection sweep has completed
        // This would typically notify any waiting threads or update internal state
    }

    public void CleanupStore()
    {
        _fileManager.RestartFileCleanupCursor();
    }

    public IAdjacencyFiles CollectAdjacencyData(DirectoryInfo exportDirectory)
    {
        if (exportDirectory == null)
            throw new ArgumentNullException(nameof(exportDirectory));

        // Collect adjacency data for export
        // This would typically analyze entity relationships and create adjacency files
        // For now, return a basic implementation
        return new BasicAdjacencyFiles(exportDirectory);
    }

    #endregion

    #region IRunnable Implementation

    public void Run()
    {
        lock (_lock)
        {
            if (_isActive)
                return;

            _isActive = true;
            _workerThread = new Thread(WorkerThreadProc)
            {
                Name = $"StorageChannel-{_channelIndex}",
                IsBackground = true
            };
            _workerThread.Start();
        }
    }

    #endregion

    #region IStorageChannelResetablePart Implementation

    public void Reset()
    {
        lock (_lock)
        {
            Stop();
            _entityCache.Reset();
            _fileManager.Reset();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Stops the storage channel.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isActive)
                return;

            _isActive = false;
            _cancellationTokenSource.Cancel();

            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(TimeSpan.FromSeconds(5));
                _workerThread = null;
            }
        }
    }

    #endregion

    #region Private Methods

    private void WorkerThreadProc()
    {
        try
        {
            while (_isActive && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Perform periodic housekeeping tasks
                PerformHousekeeping();

                // Wait for a short period before next iteration
                Thread.Sleep(100);
            }
        }
        catch (Exception)
        {
            // Log the exception (in a real implementation)
            // For now, we'll just stop the channel
            _isActive = false;
        }
    }

    private void PerformHousekeeping()
    {
        var timeBudget = TimeSpan.FromMilliseconds(50);

        try
        {
            // Perform incremental entity cache check
            _entityCache.IncrementalEntityCacheCheck(timeBudget);

            // Perform incremental file cleanup
            _fileManager.IncrementalFileCleanupCheck(timeBudget);

            // Perform incremental garbage collection
            _entityCache.IncrementalGarbageCollection(timeBudget, this);
        }
        catch (Exception)
        {
            // Ignore housekeeping errors to keep the channel running
        }
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Stop();
                _cancellationTokenSource.Dispose();
                _fileManager?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageChannel instance.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeDictionary">The type dictionary.</param>
    /// <param name="storageDirectory">The storage directory.</param>
    /// <returns>A new StorageChannel instance.</returns>
    public static StorageChannel Create(int channelIndex, IStorageTypeDictionary typeDictionary, string storageDirectory)
    {
        var entityCache = StorageEntityCache.New(channelIndex, typeDictionary);
        var fileEvaluator = DefaultStorageDataFileEvaluator.Create();
        var fileWriter = StorageFileWriter.Create();
        var fileManager = StorageFileManager.Create(channelIndex, storageDirectory, fileEvaluator, fileWriter);

        return new StorageChannel(channelIndex, typeDictionary, entityCache, fileManager);
    }

    #endregion
}

/// <summary>
/// Basic implementation of IChunksBuffer.
/// </summary>
internal class ChunksBuffer : IChunksBuffer
{
    private readonly List<byte[]> _buffers = new();
    private long _totalSize = 0;

    public byte[][] Buffers => _buffers.ToArray();

    public long TotalSize => _totalSize;

    public void AddBuffer(byte[] buffer)
    {
        if (buffer != null)
        {
            _buffers.Add(buffer);
            _totalSize += buffer.Length;
        }
    }

    public IChunksBuffer Complete()
    {
        return this;
    }
}

/// <summary>
/// Basic implementation of IChunk.
/// </summary>
internal class BasicChunk : IChunk
{
    private readonly byte[][] _buffers;
    private readonly long _totalSize;

    public BasicChunk(byte[][] buffers)
    {
        _buffers = buffers ?? throw new ArgumentNullException(nameof(buffers));
        _totalSize = buffers.Sum(b => b?.Length ?? 0);
    }

    public byte[][] Buffers() => _buffers;

    public long TotalSize => _totalSize;
}

/// <summary>
/// Basic implementation of IPersistenceIdSet.
/// </summary>
internal class BasicPersistenceIdSet : IPersistenceIdSet
{
    private readonly HashSet<long> _ids;

    public BasicPersistenceIdSet(IEnumerable<long> ids)
    {
        _ids = new HashSet<long>(ids ?? throw new ArgumentNullException(nameof(ids)));
    }

    public int Size => _ids.Count;

    public bool IsEmpty => _ids.Count == 0;

    public void Iterate(Action<long> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var id in _ids)
        {
            action(id);
        }
    }

    public IEnumerator<long> GetEnumerator() => _ids.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Basic implementation of IAdjacencyFiles.
/// </summary>
internal class BasicAdjacencyFiles : IAdjacencyFiles
{
    private readonly DirectoryInfo _exportDirectory;
    private readonly List<FileInfo> _exportedFiles = new();

    public BasicAdjacencyFiles(DirectoryInfo exportDirectory)
    {
        _exportDirectory = exportDirectory ?? throw new ArgumentNullException(nameof(exportDirectory));
    }

    public IEnumerable<FileInfo> ExportedFiles => _exportedFiles;

    public int TotalFileCount => _exportedFiles.Count;

    public long TotalFileSize => _exportedFiles.Sum(f => f.Length);

    public void AddExportedFile(FileInfo file)
    {
        if (file != null)
        {
            _exportedFiles.Add(file);
        }
    }
}
