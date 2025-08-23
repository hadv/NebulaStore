using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NebulaStore.Storage.Embedded.Types.Channels;
using NebulaStore.Storage.Embedded.Types.Exceptions;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Default implementation of IStorageManager that manages the complete storage system.
/// </summary>
public class StorageManager : IStorageManager, IStorer
{
    #region Private Fields

    private readonly IStorageConfiguration _configuration;
    private readonly IStorageTypeDictionary _typeDictionary;
    private readonly List<StorageChannel> _channels;
    private readonly object _lock = new object();
    private readonly IPersistenceManager _persistenceManager;

    private object? _root;
    private bool _isRunning = false;
    private bool _isShuttingDown = false;
    private bool _disposed = false;

    // Object ID generation
    private long _nextObjectId = 1;
    private readonly object _objectIdLock = new object();

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageManager class.
    /// </summary>
    /// <param name="configuration">The storage configuration.</param>
    /// <param name="typeDictionary">The type dictionary.</param>
    /// <param name="persistenceManager">The persistence manager.</param>
    public StorageManager(
        IStorageConfiguration configuration,
        IStorageTypeDictionary typeDictionary,
        IPersistenceManager persistenceManager)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _typeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
        _persistenceManager = persistenceManager ?? throw new ArgumentNullException(nameof(persistenceManager));

        _channels = new List<StorageChannel>();
        InitializeChannels();
    }

    #endregion

    #region IStorageManager Implementation

    public IStorageConfiguration Configuration => _configuration;

    public IStorageTypeDictionary TypeDictionary => _typeDictionary;

    public IPersistenceManager PersistenceManager => _persistenceManager;

    public string DatabaseName => "NebulaStore";

    public IStorageManager Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return this;

            _isRunning = true;
            _isShuttingDown = false;

            // Start all channels
            foreach (var channel in _channels)
            {
                channel.Run();
            }
        }

        return this;
    }

    public bool Shutdown()
    {
        lock (_lock)
        {
            if (!_isRunning || _isShuttingDown)
                return true;

            _isShuttingDown = true;

            try
            {
                // Stop all channels
                foreach (var channel in _channels)
                {
                    channel.Stop();
                }

                _isRunning = false;
                _isShuttingDown = false;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public IStorageConnection CreateConnection()
    {
        return new StorageConnection(this);
    }

    public object? Root()
    {
        return _root;
    }

    public object SetRoot(object newRoot)
    {
        _root = newRoot;
        return newRoot;
    }

    public long StoreRoot()
    {
        if (_root == null)
            return -1;

        return Store(_root);
    }

    public IPersistenceRootsView ViewRoots()
    {
        return new BasicPersistenceRootsView(_root);
    }

    public IDatabase Database()
    {
        return new BasicDatabase(this);
    }

    #endregion

    #region IStorageController Implementation

    IStorageController IStorageController.Start()
    {
        Start();
        return this;
    }

    public bool IsRunning => _isRunning;

    public bool IsShuttingDown => _isShuttingDown;

    #endregion

    #region IStorageConnection Implementation

    public void IssueFullGarbageCollection()
    {
        foreach (var channel in _channels)
        {
            channel.IssuedGarbageCollection(TimeSpan.FromMinutes(5));
        }
    }

    public bool IssueGarbageCollection(TimeSpan timeBudget)
    {
        var budgetPerChannel = TimeSpan.FromMilliseconds(timeBudget.TotalMilliseconds / _channels.Count);
        var allCompleted = true;

        foreach (var channel in _channels)
        {
            if (!channel.IssuedGarbageCollection(budgetPerChannel))
            {
                allCompleted = false;
            }
        }

        return allCompleted;
    }

    public void IssueFullFileCheck()
    {
        foreach (var channel in _channels)
        {
            channel.IssuedFileCleanupCheck(TimeSpan.FromMinutes(5));
        }
    }

    public bool IssueFileCheck(TimeSpan timeBudget)
    {
        var budgetPerChannel = TimeSpan.FromMilliseconds(timeBudget.TotalMilliseconds / _channels.Count);
        var allCompleted = true;

        foreach (var channel in _channels)
        {
            if (!channel.IssuedFileCleanupCheck(budgetPerChannel))
            {
                allCompleted = false;
            }
        }

        return allCompleted;
    }

    public void IssueFullCacheCheck()
    {
        var evaluator = StorageEntityCacheEvaluatorFactory.New();
        foreach (var channel in _channels)
        {
            channel.IssuedEntityCacheCheck(TimeSpan.FromMinutes(5), evaluator);
        }
    }

    public bool IssueCacheCheck(TimeSpan timeBudget)
    {
        var evaluator = StorageEntityCacheEvaluatorFactory.New();
        var budgetPerChannel = TimeSpan.FromMilliseconds(timeBudget.TotalMilliseconds / _channels.Count);
        var allCompleted = true;

        foreach (var channel in _channels)
        {
            if (!channel.IssuedEntityCacheCheck(budgetPerChannel, evaluator))
            {
                allCompleted = false;
            }
        }

        return allCompleted;
    }

    public void IssueFullBackup(DirectoryInfo targetDirectory)
    {
        if (targetDirectory == null)
            throw new ArgumentNullException(nameof(targetDirectory));

        if (!targetDirectory.Exists)
        {
            targetDirectory.Create();
        }

        var fileProvider = new DirectoryFileProvider(targetDirectory);
        foreach (var channel in _channels)
        {
            channel.ExportData(fileProvider);
        }
    }

    public void IssueTransactionsLogCleanup()
    {
        foreach (var channel in _channels)
        {
            channel.IssuedTransactionsLogCleanup();
        }
    }

    public IStorageStatistics CreateStorageStatistics()
    {
        var totalFileCount = 0;
        var totalFileSize = 0L;
        var liveDataSize = 0L;

        foreach (var channel in _channels)
        {
            var stats = channel.CreateRawFileStatistics();
            totalFileCount += stats.FileCount;
            totalFileSize += stats.TotalFileSize;
            liveDataSize += stats.LiveDataSize;
        }

        return new BasicStorageStatistics(totalFileCount, totalFileSize, liveDataSize);
    }

    public void ExportChannels(DirectoryInfo targetDirectory, bool performGarbageCollection = true)
    {
        if (performGarbageCollection)
        {
            IssueFullGarbageCollection();
        }

        IssueFullBackup(targetDirectory);
    }

    public void ImportFiles(DirectoryInfo importDirectory)
    {
        if (importDirectory == null)
            throw new ArgumentNullException(nameof(importDirectory));

        var importSource = new DirectoryImportSource(importDirectory);
        foreach (var channel in _channels)
        {
            channel.ImportData(importSource);
        }
    }

    #endregion

    #region IStorer Implementation

    public long Store(object instance)
    {
        if (instance == null)
            return -1;

        try
        {
            // Get the channel for this object (hash-based distribution)
            var channelIndex = Math.Abs(instance.GetHashCode()) % _channels.Count;
            var channel = _channels[channelIndex];

            // Get or create type handler for this object type
            var typeHandler = _typeDictionary.GetTypeHandler(instance.GetType());
            if (typeHandler == null)
            {
                throw new StorageExceptionInitialization($"Failed to get type handler for type {instance.GetType().Name}");
            }

            // Serialize the object
            var serializedData = typeHandler.Serialize(instance);

            // Generate unique object ID
            var objectId = GenerateObjectId();

            // Create chunk data for storage
            // Create header with metadata
            var header = CreateChunkHeader(objectId, typeHandler.TypeId, serializedData.Length);
            var chunkData = new Channels.BasicChunk(new byte[][] { header, serializedData });

            // Store the chunk in the channel
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var storeResult = channel.StoreEntities(timestamp, chunkData);

            // Update entity cache
            _ = channel.PostStoreUpdateEntityCacheAsync(storeResult.Key, storeResult.Value);

            return objectId;
        }
        catch (Exception ex)
        {
            throw new StorageExceptionIoWriting($"Failed to store object of type {instance.GetType().Name}", ex);
        }
    }

    public long[] StoreAll(params object[] instances)
    {
        if (instances == null)
            return Array.Empty<long>();

        var objectIds = new long[instances.Length];
        for (int i = 0; i < instances.Length; i++)
        {
            objectIds[i] = Store(instances[i]);
        }
        return objectIds;
    }

    public long Commit()
    {
        try
        {
            long totalCommitted = 0;

            // Commit all pending operations in all channels
            foreach (var channel in _channels)
            {
                channel.CommitChunkStorage();
                // In a real implementation, we would track how many objects were committed per channel
                totalCommitted++; // Placeholder - should be actual count
            }

            return totalCommitted;
        }
        catch (Exception ex)
        {
            // Rollback all channels on failure
            foreach (var channel in _channels)
            {
                try
                {
                    channel.RollbackChunkStorage();
                }
                catch
                {
                    // Log rollback failures but don't throw
                }
            }

            throw new StorageOperationException("Failed to commit storage operations", ex);
        }
    }

    public long PendingObjectCount => 0; // No pending objects in this simple implementation

    public bool HasPendingOperations => false; // No pending operations in this simple implementation

    public IStorer Skip(object obj)
    {
        // Skip storing the object (mark as already stored)
        return this;
    }

    public long Ensure(object obj)
    {
        // Force storage even if already stored
        return Store(obj);
    }

    #endregion

    #region Private Methods

    private void InitializeChannels()
    {
        for (int i = 0; i < _configuration.ChannelCount; i++)
        {
            var channel = StorageChannel.Create(i, _typeDictionary, _configuration.StorageDirectory);
            _channels.Add(channel);
        }
    }

    private long GenerateObjectId()
    {
        lock (_objectIdLock)
        {
            return _nextObjectId++;
        }
    }

    private byte[] CreateChunkHeader(long objectId, long typeId, int dataLength)
    {
        // Header format: [ObjectId(8 bytes)][TypeId(8 bytes)][DataLength(4 bytes)]
        var header = new byte[20];

        // Write object ID
        BitConverter.GetBytes(objectId).CopyTo(header, 0);

        // Write type ID
        BitConverter.GetBytes(typeId).CopyTo(header, 8);

        // Write data length
        BitConverter.GetBytes(dataLength).CopyTo(header, 16);

        return header;
    }

    #endregion

    #region IDisposable Implementation

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Shutdown();
                foreach (var channel in _channels)
                {
                    channel.Dispose();
                }
                _channels.Clear();
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
    /// Creates a new StorageManager instance.
    /// </summary>
    /// <param name="configuration">The storage configuration.</param>
    /// <returns>A new StorageManager instance.</returns>
    public static StorageManager Create(IStorageConfiguration configuration)
    {
        var typeDictionary = StorageTypeDictionary.New();
        var persistenceManager = new BasicPersistenceManager(typeDictionary);
        return new StorageManager(configuration, typeDictionary, persistenceManager);
    }

    #endregion
}

/// <summary>
/// Basic implementation of IStorageConnection.
/// </summary>
internal class StorageConnection : IStorageConnection
{
    private readonly StorageManager _storageManager;

    public StorageConnection(StorageManager storageManager)
    {
        _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
    }

    public IPersistenceManager PersistenceManager => _storageManager.PersistenceManager;

    public long Store(object instance) => _storageManager.Store(instance);
    public long[] StoreAll(params object[] instances) => _storageManager.StoreAll(instances);
    public long Commit() => _storageManager.Commit();
    public long PendingObjectCount => _storageManager.PendingObjectCount;
    public bool HasPendingOperations => _storageManager.HasPendingOperations;
    public IStorer Skip(object obj) => _storageManager.Skip(obj);
    public long Ensure(object obj) => _storageManager.Ensure(obj);

    public void IssueFullGarbageCollection() => _storageManager.IssueFullGarbageCollection();
    public bool IssueGarbageCollection(TimeSpan timeBudget) => _storageManager.IssueGarbageCollection(timeBudget);
    public void IssueFullFileCheck() => _storageManager.IssueFullFileCheck();
    public bool IssueFileCheck(TimeSpan timeBudget) => _storageManager.IssueFileCheck(timeBudget);
    public void IssueFullCacheCheck() => _storageManager.IssueFullCacheCheck();
    public bool IssueCacheCheck(TimeSpan timeBudget) => _storageManager.IssueCacheCheck(timeBudget);
    public void IssueFullBackup(DirectoryInfo targetDirectory) => _storageManager.IssueFullBackup(targetDirectory);
    public void IssueTransactionsLogCleanup() => _storageManager.IssueTransactionsLogCleanup();
    public IStorageStatistics CreateStorageStatistics() => _storageManager.CreateStorageStatistics();
    public void ExportChannels(DirectoryInfo targetDirectory, bool performGarbageCollection = true) => _storageManager.ExportChannels(targetDirectory, performGarbageCollection);
    public void ImportFiles(DirectoryInfo importDirectory) => _storageManager.ImportFiles(importDirectory);

    public void Dispose()
    {
        // Connection disposal doesn't dispose the manager
    }
}

/// <summary>
/// Basic implementation of IPersistenceManager.
/// </summary>
internal class BasicPersistenceManager : IPersistenceManager
{
    private readonly IStorageTypeDictionary _typeDictionary;
    private readonly IPersistenceObjectRegistry _objectRegistry;

    // Object ID generation
    private long _nextObjectId = 1;
    private readonly object _objectIdLock = new object();

    public BasicPersistenceManager(IStorageTypeDictionary typeDictionary)
    {
        _typeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
        _objectRegistry = new BasicPersistenceObjectRegistry();
    }

    private long GenerateObjectId()
    {
        lock (_objectIdLock)
        {
            return _nextObjectId++;
        }
    }

    public IStorageTypeDictionary TypeDictionary => _typeDictionary;
    public IPersistenceObjectRegistry ObjectRegistry => _objectRegistry;

    public long Store(object instance)
    {
        if (instance == null) return -1;

        try
        {
            // Get or create type handler for this object type
            var typeHandler = _typeDictionary.GetTypeHandler(instance.GetType());
            if (typeHandler == null)
            {
                throw new StorageExceptionInitialization($"Failed to get type handler for type {instance.GetType().Name}");
            }

            // Serialize the object
            var serializedData = typeHandler.Serialize(instance);

            // Generate unique object ID
            var objectId = GenerateObjectId();

            // Register the object in the registry
            _objectRegistry.RegisterObject(instance, objectId);

            return objectId;
        }
        catch (Exception ex) when (!(ex is StorageException))
        {
            throw new StorageExceptionIoWriting($"Failed to store object of type {instance.GetType().Name}", ex);
        }
    }

    public long[] StoreAll(params object[] instances)
    {
        if (instances == null) return Array.Empty<long>();
        return instances.Select(Store).ToArray();
    }

    public object GetObject(long objectId)
    {
        try
        {
            // Try to get from object registry first (for recently stored objects)
            var cachedObject = _objectRegistry.GetObject(objectId);
            if (cachedObject != null)
            {
                return cachedObject;
            }

            // If not in cache, we would need to load from storage
            // For now, throw an exception as this requires more complex implementation
            throw new StorageExceptionNotRunning($"Object with ID {objectId} not found in cache. Loading from storage not yet implemented.");
        }
        catch (Exception ex) when (!(ex is StorageException))
        {
            throw new StorageExceptionIoReading($"Failed to get object with ID {objectId}", ex);
        }
    }

    public IStorer CreateLazyStorer() => new BasicStorer(this);
    public IStorer CreateStorer() => new BasicStorer(this);
    public IStorer CreateEagerStorer() => new BasicStorer(this);

    public IPersistenceRootsView ViewRoots()
    {
        return new BasicPersistenceRootsView(null);
    }
}

/// <summary>
/// Basic implementation of IPersistenceObjectRegistry.
/// </summary>
internal class BasicPersistenceObjectRegistry : IPersistenceObjectRegistry
{
    private readonly Dictionary<object, long> _objectToId = new();
    private readonly Dictionary<long, object> _idToObject = new();
    private readonly object _lock = new object();

    public long LookupObjectId(object instance)
    {
        if (instance == null) return -1;
        lock (_lock)
        {
            return _objectToId.TryGetValue(instance, out var id) ? id : -1;
        }
    }

    public void RegisterObject(object instance, long objectId)
    {
        if (instance == null) return;
        lock (_lock)
        {
            _objectToId[instance] = objectId;
            _idToObject[objectId] = instance;
        }
    }

    public object? GetObject(long objectId)
    {
        lock (_lock)
        {
            return _idToObject.TryGetValue(objectId, out var obj) ? obj : null;
        }
    }

    public void Consolidate()
    {
        // Placeholder for consolidation logic
    }
}

/// <summary>
/// Basic implementation of IStorer.
/// </summary>
internal class BasicStorer : IStorer
{
    private readonly BasicPersistenceManager _persistenceManager;
    private long _pendingObjectCount = 0;

    public BasicStorer(BasicPersistenceManager persistenceManager)
    {
        _persistenceManager = persistenceManager ?? throw new ArgumentNullException(nameof(persistenceManager));
    }

    public long Store(object instance)
    {
        var objectId = _persistenceManager.Store(instance);
        if (objectId > 0)
        {
            _pendingObjectCount++;
        }
        return objectId;
    }

    public long[] StoreAll(params object[] instances)
    {
        var objectIds = _persistenceManager.StoreAll(instances);
        _pendingObjectCount += objectIds.Count(id => id > 0);
        return objectIds;
    }

    public long Commit()
    {
        var committedCount = _pendingObjectCount;
        _pendingObjectCount = 0; // Reset after commit
        return committedCount;
    }

    public long PendingObjectCount => _pendingObjectCount;
    public bool HasPendingOperations => _pendingObjectCount > 0;
    public IStorer Skip(object obj) => this;
    public long Ensure(object obj) => Store(obj);

    public void Dispose()
    {
        // Nothing to dispose in this simple implementation
    }
}

/// <summary>
/// Basic implementation of IPersistenceRootsView.
/// </summary>
internal class BasicPersistenceRootsView : IPersistenceRootsView
{
    private readonly object? _rootReference;

    public BasicPersistenceRootsView(object? rootReference)
    {
        _rootReference = rootReference;
    }

    public object? RootReference() => _rootReference;

    public IEnumerable<object> AllRootReferences()
    {
        if (_rootReference != null)
            yield return _rootReference;
    }
}

/// <summary>
/// Basic implementation of IDatabase.
/// </summary>
internal class BasicDatabase : IDatabase
{
    public string DatabaseName { get; }
    public IStorageManager StorageManager { get; }

    public BasicDatabase(IStorageManager storageManager)
    {
        StorageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        DatabaseName = storageManager.DatabaseName;
    }
}

/// <summary>
/// Basic implementation of IStorageStatistics.
/// </summary>
internal class BasicStorageStatistics : IStorageStatistics
{
    public long TotalObjectCount { get; }
    public long TotalStorageSize { get; }
    public int DataFileCount { get; }
    public int TransactionFileCount { get; }
    public long LiveDataLength { get; }
    public DateTime CreationTime { get; }
    public DateTime LastModificationTime { get; }

    public BasicStorageStatistics(int totalFileCount, long totalFileSize, long liveDataSize)
    {
        TotalObjectCount = 0; // Not available in simple implementation
        TotalStorageSize = totalFileSize;
        DataFileCount = totalFileCount;
        TransactionFileCount = 0; // Not available in simple implementation
        LiveDataLength = liveDataSize;
        CreationTime = DateTime.UtcNow;
        LastModificationTime = DateTime.UtcNow;
    }
}

/// <summary>
/// Directory-based file provider for exports.
/// </summary>
internal class DirectoryFileProvider : IStorageLiveFileProvider
{
    private readonly DirectoryInfo _targetDirectory;

    public DirectoryFileProvider(DirectoryInfo targetDirectory)
    {
        _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
    }

    public Stream ProvideFile(string identifier)
    {
        var filePath = Path.Combine(_targetDirectory.FullName, identifier);
        return new FileStream(filePath, FileMode.Create, FileAccess.Write);
    }
}

/// <summary>
/// Directory-based import source.
/// </summary>
internal class DirectoryImportSource : IStorageImportSource
{
    public DirectoryInfo SourceDirectory { get; }

    public DirectoryImportSource(DirectoryInfo sourceDirectory)
    {
        SourceDirectory = sourceDirectory ?? throw new ArgumentNullException(nameof(sourceDirectory));
    }

    public IEnumerable<FileInfo> GetAvailableFiles()
    {
        if (SourceDirectory.Exists)
        {
            return SourceDirectory.GetFiles("*.dat");
        }
        return Enumerable.Empty<FileInfo>();
    }
}
