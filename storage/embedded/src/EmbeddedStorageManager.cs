using MessagePack;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Storage.Monitoring;
using NebulaStore.GigaMap;
using NebulaStore.Storage.Embedded.Types;
using System.Linq;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of the embedded storage manager.
/// Manages object persistence and provides high-level storage operations.
/// </summary>
public class EmbeddedStorageManager : IEmbeddedStorageManager, IMonitorableStorageManager
{
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly IStorageConnection _connection;
    private readonly Func<Type, bool>? _typeEvaluator;
    private object? _root;
    private Type? _rootType;
    private bool _isRunning;
    private bool _isDisposed;
    private IStorageMonitoringManager? _monitoringManager;

    // GigaMap management
    private readonly Dictionary<Type, object> _gigaMaps = new();
    private readonly object _gigaMapLock = new();

    internal EmbeddedStorageManager(
        IEmbeddedStorageConfiguration configuration,
        IStorageConnection connection,
        object? root = null,
        Func<Type, bool>? typeEvaluator = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _root = root;
        _rootType = root?.GetType();
        _typeEvaluator = typeEvaluator ?? DefaultTypeEvaluator;
    }

    public IEmbeddedStorageConfiguration Configuration => _configuration;

    public bool IsRunning => _isRunning && !_isDisposed;

    public bool IsAcceptingTasks => IsRunning && _connection != null;

    public bool IsActive => IsRunning && _connection != null;

    public T? Root<T>()
    {
        ThrowIfDisposed();

        if (_root == null)
        {
            return default(T); // Return null/default if no root has been set (Eclipse Store behavior)
        }
        else if (_root is not T)
        {
            throw new InvalidOperationException($"Root object is of type {_root.GetType().Name}, not {typeof(T).Name}");
        }

        return (T)_root;
    }

    public object SetRoot(object newRoot)
    {
        ThrowIfDisposed();
        
        if (newRoot == null)
            throw new ArgumentNullException(nameof(newRoot));

        _root = newRoot;
        _rootType = newRoot.GetType();
        return newRoot;
    }

    public long StoreRoot()
    {
        ThrowIfDisposed();

        if (_root == null)
            return 0; // No root to store

        // Store the root object in the storage system
        var objectId = Store(_root);

        // Also persist the root to the root file for loading on next startup
        SaveRootToFile();

        return objectId;
    }

    public long Store(object obj)
    {
        ThrowIfDisposed();
        
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        using var storer = CreateStorer();
        var objectId = storer.Store(obj);
        storer.Commit();
        return objectId;
    }

    public long[] StoreAll(params object[] objects)
    {
        ThrowIfDisposed();
        
        if (objects == null)
            throw new ArgumentNullException(nameof(objects));

        using var storer = CreateStorer();
        var objectIds = storer.StoreAll(objects);
        storer.Commit();
        return objectIds;
    }

    // Note: EclipseStore doesn't have a Query method - use direct object graph navigation instead
    // Example: root.Customers.Where(c => c.Age > 25) using LINQ on your object graph
    // The root object contains your entire object graph that you can navigate directly

    public IStorer CreateStorer()
    {
        ThrowIfDisposed();
        return new EmbeddedStorer(_connection);
    }

    public IEmbeddedStorageManager Start()
    {
        ThrowIfDisposed();
        
        if (_isRunning)
            return this;

        // Ensure storage directory exists
        Directory.CreateDirectory(_configuration.StorageDirectory);

        // Load existing root if available
        LoadExistingRoot();

        _isRunning = true;
        return this;
    }

    public bool Shutdown()
    {
        if (!_isRunning)
            return true;

        try
        {
            // Store root before shutdown
            if (_root != null)
            {
                StoreRoot();
            }

            _isRunning = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void IssueFullGarbageCollection()
    {
        ThrowIfDisposed();
        _connection.IssueFullGarbageCollection();
    }

    public bool IssueGarbageCollection(long timeBudgetNanos)
    {
        ThrowIfDisposed();
        var timeBudget = TimeSpan.FromTicks(timeBudgetNanos / 100); // Convert nanoseconds to TimeSpan
        return _connection.IssueGarbageCollection(timeBudget);
    }

    public async Task CreateBackupAsync(string backupDirectory)
    {
        ThrowIfDisposed();
        
        if (string.IsNullOrEmpty(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

        await Task.Run(() =>
        {
            Directory.CreateDirectory(backupDirectory);
            
            // Copy storage files to backup directory
            var sourceDir = new DirectoryInfo(_configuration.StorageDirectory);
            var targetDir = new DirectoryInfo(backupDirectory);

            foreach (var file in sourceDir.GetFiles("*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDir.FullName, file.FullName);
                var targetPath = Path.Combine(targetDir.FullName, relativePath);
                
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                file.CopyTo(targetPath, true);
            }
        });
    }

    public IStorageStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return _connection.CreateStorageStatistics();
    }

    public IStorageMonitoringManager GetMonitoringManager()
    {
        ThrowIfDisposed();

        if (_monitoringManager == null)
        {
            _monitoringManager = CreateMonitoringManager();
        }

        return _monitoringManager;
    }

    // IMonitorableStorageManager implementation
    void IMonitorableStorageManager.IssueFullFileCheck()
    {
        // For now, this is a placeholder as the current implementation
        // doesn't have specific file check functionality
        // This would be implemented when the full storage system is developed
    }

    void IMonitorableStorageManager.IssueFullCacheCheck()
    {
        // For now, this is a placeholder as the current implementation
        // doesn't have specific cache check functionality
        // This would be implemented when the full cache system is developed
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            Shutdown();
        }
        finally
        {
            _connection?.Dispose();
            _isDisposed = true;
        }
    }

    private void LoadExistingRoot()
    {
        // Use AFS storage connection if available
        if (_connection is NebulaStore.Afs.Blobstore.AfsStorageConnection afsConnection)
        {
            var loadedRoot = afsConnection.LoadRoot(_rootType);
            if (loadedRoot != null)
            {
                _root = loadedRoot;
                _rootType = loadedRoot.GetType();
                return;
            }
        }

        // Fallback to traditional file-based loading
        var rootFilePath = Path.Combine(_configuration.StorageDirectory, "root.msgpack");

        if (!File.Exists(rootFilePath))
            return;

        try
        {
            var bytes = File.ReadAllBytes(rootFilePath);
            if (bytes.Length > 0)
            {
                var wrapper = MessagePackSerializer.Deserialize<RootWrapper>(bytes);
                _rootType = Type.GetType(wrapper.TypeName);
                if (_rootType != null && wrapper.Data != null)
                {
                    var dataBytes = MessagePackSerializer.Serialize(wrapper.Data);
                    _root = MessagePackSerializer.Deserialize(_rootType, dataBytes);
                }
            }
        }
        catch
        {
            // Ignore errors during root loading - will create new root if needed
        }
    }

    private void SaveRootToFile()
    {
        if (_root == null || _rootType == null)
            return;

        // Use AFS storage connection if available
        if (_connection is NebulaStore.Afs.Blobstore.AfsStorageConnection afsConnection)
        {
            afsConnection.SaveRoot(_root);
            return;
        }

        // Fallback to traditional file-based saving
        var rootFilePath = Path.Combine(_configuration.StorageDirectory, "root.msgpack");

        try
        {
            var wrapper = new RootWrapper
            {
                Data = _root,
                TypeName = _rootType.AssemblyQualifiedName!
            };
            var bytes = MessagePackSerializer.Serialize(wrapper);
            File.WriteAllBytes(rootFilePath, bytes);
        }
        catch
        {
            // Ignore errors during root saving
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(EmbeddedStorageManager));
    }

    private static bool DefaultTypeEvaluator(Type type)
    {
        // Default logic for determining if a type is persistable
        return !type.IsPrimitive &&
               type != typeof(string) &&
               !type.IsEnum &&
               type.IsClass;
    }

    private IStorageMonitoringManager CreateMonitoringManager()
    {
        // Create placeholder monitors for the current implementation
        // These will be enhanced when the full storage system is implemented

        // Create storage manager monitor
        var storageManagerMonitor = new StorageManagerMonitor(this);

        // Create placeholder object registry monitor
        var objectRegistryMonitor = new ObjectRegistryMonitor(new PlaceholderObjectRegistry());

        // Create entity cache monitors for each channel
        var entityCacheMonitors = new List<EntityCacheMonitor>();
        var housekeepingMonitors = new List<StorageChannelHousekeepingMonitor>();

        for (int i = 0; i < _configuration.ChannelCount; i++)
        {
            // Create placeholder entity cache monitor
            var entityCache = new PlaceholderEntityCache(i);
            entityCacheMonitors.Add(new EntityCacheMonitor(entityCache));

            // Create housekeeping monitor
            housekeepingMonitors.Add(new StorageChannelHousekeepingMonitor(i));
        }

        // Create entity cache summary monitor
        var entityCacheSummaryMonitor = new EntityCacheSummaryMonitor(entityCacheMonitors);

        return new StorageMonitoringManager(
            storageManagerMonitor,
            entityCacheSummaryMonitor,
            objectRegistryMonitor,
            entityCacheMonitors,
            housekeepingMonitors
        );
    }

    // TraverseGraphLazy method removed - not needed for EclipseStore's direct object navigation approach

    // ========== GigaMap Implementation ==========

    public IGigaMapBuilder<T> CreateGigaMap<T>() where T : class
    {
        ThrowIfDisposed();
        return new StorageAwareGigaMapBuilder<T>(_connection);
    }

    public IGigaMap<T>? GetGigaMap<T>() where T : class
    {
        ThrowIfDisposed();

        lock (_gigaMapLock)
        {
            return _gigaMaps.TryGetValue(typeof(T), out var gigaMap)
                ? (IGigaMap<T>)gigaMap
                : null;
        }
    }

    public void RegisterGigaMap<T>(IGigaMap<T> gigaMap) where T : class
    {
        ThrowIfDisposed();

        if (gigaMap == null)
            throw new ArgumentNullException(nameof(gigaMap));

        lock (_gigaMapLock)
        {
            _gigaMaps[typeof(T)] = gigaMap;
        }
    }

    public async Task StoreGigaMapsAsync()
    {
        ThrowIfDisposed();

        lock (_gigaMapLock)
        {
            foreach (var kvp in _gigaMaps)
            {
                if (kvp.Value is IGigaMap<object> gigaMap)
                {
                    // Store the GigaMap using the existing storage system
                    // For now, we'll use the basic Store method
                    // In a full implementation, this would be more sophisticated
                    Store(gigaMap);
                }
            }
        }

        await Task.CompletedTask;
    }

    #region Eclipse Store Compatibility Methods

    public void IssueFullFileCheck()
    {
        // No-op for now - file system is managed by AFS
    }

    public bool IssueFileCheck(TimeSpan timeBudget)
    {
        // No-op for now - file system is managed by AFS
        return true;
    }

    public void IssueFullCacheCheck()
    {
        // No-op for now - caching is managed by MessagePack
    }

    public bool IssueCacheCheck(TimeSpan timeBudget)
    {
        // No-op for now - caching is managed by MessagePack
        return true;
    }

    public void IssueFullBackup(System.IO.DirectoryInfo targetDirectory)
    {
        CreateBackupAsync(targetDirectory.FullName).Wait();
    }

    public IStorageStatistics CreateStorageStatistics()
    {
        return GetStatistics();
    }

    public void ExportChannels(System.IO.DirectoryInfo targetDirectory, bool performGarbageCollection = true)
    {
        IssueFullBackup(targetDirectory);
    }

    public void ImportFiles(System.IO.DirectoryInfo importDirectory)
    {
        // No-op for now - import not implemented
    }

    public IStorageTypeDictionary TypeDictionary => new SimpleTypeDictionary();

    public IDatabase Database()
    {
        return new SimpleDatabase(this);
    }

    public IPersistenceRootsView ViewRoots()
    {
        return new SimplePersistenceRootsView(_root);
    }

    public IStorageConnection CreateConnection()
    {
        return new SimpleStorageConnection(this);
    }

    #endregion
}

/// <summary>
/// A GigaMap builder that automatically sets the storage connection.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
internal class StorageAwareGigaMapBuilder<T> : IGigaMapBuilder<T> where T : class
{
    private readonly IStorageConnection _storageConnection;
    private readonly GigaMapBuilder<T> _innerBuilder;

    public StorageAwareGigaMapBuilder(IStorageConnection storageConnection)
    {
        _storageConnection = storageConnection ?? throw new ArgumentNullException(nameof(storageConnection));
        _innerBuilder = new GigaMapBuilder<T>();
    }

    public IGigaMapBuilder<T> WithBitmapIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        _innerBuilder.WithBitmapIndex(indexer);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        _innerBuilder.WithBitmapIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIndices(params IIndexer<T, object>[] indexers)
    {
        _innerBuilder.WithBitmapIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        _innerBuilder.WithBitmapIdentityIndex(indexer);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        _innerBuilder.WithBitmapIdentityIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapIdentityIndices(params IIndexer<T, object>[] indexers)
    {
        _innerBuilder.WithBitmapIdentityIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndex<TKey>(IIndexer<T, TKey> indexer)
    {
        _innerBuilder.WithBitmapUniqueIndex(indexer);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndices(IEnumerable<IIndexer<T, object>> indexers)
    {
        _innerBuilder.WithBitmapUniqueIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithBitmapUniqueIndices(params IIndexer<T, object>[] indexers)
    {
        _innerBuilder.WithBitmapUniqueIndices(indexers);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraint(ICustomConstraint<T> constraint)
    {
        _innerBuilder.WithCustomConstraint(constraint);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraints(IEnumerable<ICustomConstraint<T>> constraints)
    {
        _innerBuilder.WithCustomConstraints(constraints);
        return this;
    }

    public IGigaMapBuilder<T> WithCustomConstraints(params ICustomConstraint<T>[] constraints)
    {
        _innerBuilder.WithCustomConstraints(constraints);
        return this;
    }

    public IGigaMapBuilder<T> WithEqualityComparer(IEqualityComparer<T> equalityComparer)
    {
        _innerBuilder.WithEqualityComparer(equalityComparer);
        return this;
    }

    public IGigaMapBuilder<T> WithValueEquality()
    {
        _innerBuilder.WithValueEquality();
        return this;
    }

    public IGigaMapBuilder<T> WithIdentityEquality()
    {
        _innerBuilder.WithIdentityEquality();
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int segmentSizeExponent)
    {
        _innerBuilder.WithSegmentSize(segmentSizeExponent);
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent, int midLevelLengthExponent)
    {
        _innerBuilder.WithSegmentSize(lowLevelLengthExponent, midLevelLengthExponent);
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent, int midLevelLengthExponent, int highLevelMaximumLengthExponent)
    {
        _innerBuilder.WithSegmentSize(lowLevelLengthExponent, midLevelLengthExponent, highLevelMaximumLengthExponent);
        return this;
    }

    public IGigaMapBuilder<T> WithSegmentSize(int lowLevelLengthExponent, int midLevelLengthExponent, int highLevelMinimumLengthExponent, int highLevelMaximumLengthExponent)
    {
        _innerBuilder.WithSegmentSize(lowLevelLengthExponent, midLevelLengthExponent, highLevelMinimumLengthExponent, highLevelMaximumLengthExponent);
        return this;
    }



    public IGigaMap<T> Build()
    {
        var gigaMap = _innerBuilder.Build();

        // Set the storage connection on the built GigaMap
        if (gigaMap is DefaultGigaMap<T> defaultGigaMap)
        {
            defaultGigaMap.SetStorageConnection(_storageConnection);
        }

        return gigaMap;
    }
}

[MessagePackObject(AllowPrivate = true)]
internal class RootWrapper
{
    [Key(0)]
    public object? Data { get; set; }

    [Key(1)]
    public string TypeName { get; set; } = string.Empty;
}

/// <summary>
/// Placeholder implementation of object registry for monitoring.
/// This will be replaced when the actual object registry is implemented.
/// </summary>
internal class PlaceholderObjectRegistry : NebulaStore.Storage.Monitoring.IPersistenceObjectRegistry
{
    public long Capacity => 1000000; // Default capacity
    public long Size => 0; // No objects registered yet
}

/// <summary>
/// Placeholder implementation of entity cache for monitoring.
/// This will be replaced when the actual entity cache is implemented.
/// </summary>
internal class PlaceholderEntityCache : NebulaStore.Storage.Monitoring.IStorageEntityCache
{
    public int ChannelIndex { get; }
    public long LastSweepStart => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long LastSweepEnd => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long EntityCount => 0; // No entities cached yet
    public long CacheSize => 0; // No cache size yet

    public PlaceholderEntityCache(int channelIndex)
    {
        ChannelIndex = channelIndex;
    }
}



/// <summary>
/// Embedded storage storer that properly tracks pending objects for commit.
/// </summary>
internal class EmbeddedStorer : IStorer
{
    private readonly IStorageConnection _connection;
    private long _pendingObjectCount = 0;
    private bool _disposed = false;

    public EmbeddedStorer(IStorageConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public long Store(object obj)
    {
        ThrowIfDisposed();
        var objectId = _connection.Store(obj);
        if (objectId > 0)
        {
            _pendingObjectCount++;
        }
        return objectId;
    }

    public long[] StoreAll(params object[] objects)
    {
        ThrowIfDisposed();
        var objectIds = _connection.StoreAll(objects);
        _pendingObjectCount += objectIds.Count(id => id > 0);
        return objectIds;
    }

    public long Commit()
    {
        ThrowIfDisposed();
        var committedCount = _pendingObjectCount;
        _pendingObjectCount = 0; // Reset after commit
        _connection.Commit(); // Delegate to underlying connection
        return committedCount;
    }

    public long PendingObjectCount => _pendingObjectCount;
    public bool HasPendingOperations => _pendingObjectCount > 0;

    public IStorer Skip(object obj)
    {
        ThrowIfDisposed();
        _connection.Skip(obj);
        return this;
    }

    public long Ensure(object obj)
    {
        ThrowIfDisposed();
        return Store(obj); // Force storage
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Simple type dictionary implementation for Eclipse Store compatibility.
/// </summary>
internal class SimpleTypeDictionary : IStorageTypeDictionary
{
    private readonly Dictionary<Type, ITypeHandler> _typeHandlers = new();

    public void RegisterType(Type type) { /* No-op - types handled by MessagePack */ }
    public bool IsTypeRegistered(Type type) => true; // All types supported via MessagePack
    public long GetTypeId(Type type) => type.GetHashCode(); // Simple type ID
    public Type? GetType(long typeId) => null; // Not needed for simple implementation

    // Additional required methods
    public ITypeHandler? GetTypeHandler(Type type)
    {
        if (!_typeHandlers.TryGetValue(type, out var handler))
        {
            handler = new SimpleTypeHandler(type);
            _typeHandlers[type] = handler;
        }
        return handler;
    }
    public ITypeHandler? GetTypeHandlerByTypeId(long typeId) => null; // Not needed for simple implementation
    public IEnumerable<ITypeHandler> GetAllTypeHandlers() => _typeHandlers.Values;
    public void RegisterTypeHandler(Type type, ITypeHandler handler) { _typeHandlers[type] = handler; }
}

/// <summary>
/// Simple type handler implementation for Eclipse Store compatibility.
/// </summary>
internal class SimpleTypeHandler : ITypeHandler
{
    public Type HandledType { get; }
    public long TypeId { get; }

    public SimpleTypeHandler(Type type)
    {
        HandledType = type;
        TypeId = Math.Abs(type.GetHashCode()) + 1000; // Ensure positive ID starting from 1000
    }

    public byte[] Serialize(object obj) => MessagePack.MessagePackSerializer.Serialize(obj);
    public object Deserialize(byte[] data) => MessagePack.MessagePackSerializer.Deserialize<object>(data);
    public long GetSerializedLength(object obj) => Serialize(obj).Length;
    public bool CanHandle(Type type) => type == HandledType;
}

/// <summary>
/// Simple database implementation for Eclipse Store compatibility.
/// </summary>
internal class SimpleDatabase : IDatabase
{
    private readonly IEmbeddedStorageManager _storageManager;

    public SimpleDatabase(IEmbeddedStorageManager storageManager)
    {
        _storageManager = storageManager;
    }

    public string Name => "NebulaStore";
    public string DatabaseName => "NebulaStore";
    public IStorageManager StorageManager => throw new NotSupportedException("Use EmbeddedStorageManager directly");
}

/// <summary>
/// Simple persistence roots view for Eclipse Store compatibility.
/// </summary>
internal class SimplePersistenceRootsView : IPersistenceRootsView
{
    private readonly object? _root;

    public SimplePersistenceRootsView(object? root)
    {
        _root = root;
    }

    public object? Root => _root;
    public object? RootReference() => _root;
    public IEnumerable<object> AllRootReferences()
    {
        if (_root != null)
            yield return _root;
    }
}

/// <summary>
/// Simple storage connection for Eclipse Store compatibility.
/// </summary>
internal class SimpleStorageConnection : IStorageConnection
{
    private readonly IEmbeddedStorageManager _embeddedStorage;

    public SimpleStorageConnection(IEmbeddedStorageManager embeddedStorage)
    {
        _embeddedStorage = embeddedStorage;
    }

    public IPersistenceManager PersistenceManager => new SimplePersistenceManager(_embeddedStorage);

    public long Store(object instance) => _embeddedStorage.Store(instance);
    public long[] StoreAll(params object[] instances) => _embeddedStorage.StoreAll(instances);
    public long Commit() => 0; // Simple implementation
    public long PendingObjectCount => 0;
    public bool HasPendingOperations => false;
    public IStorer Skip(object obj) => _embeddedStorage.CreateStorer();
    public long Ensure(object obj) => Store(obj);

    // Eclipse Store housekeeping methods - delegate to storage manager
    public void IssueFullGarbageCollection() { }
    public bool IssueGarbageCollection(TimeSpan timeBudget) => true;
    public void IssueFullFileCheck() => _embeddedStorage.IssueFullFileCheck();
    public bool IssueFileCheck(TimeSpan timeBudget) => _embeddedStorage.IssueFileCheck(timeBudget);
    public void IssueFullCacheCheck() => _embeddedStorage.IssueFullCacheCheck();
    public bool IssueCacheCheck(TimeSpan timeBudget) => _embeddedStorage.IssueCacheCheck(timeBudget);
    public void IssueFullBackup(System.IO.DirectoryInfo targetDirectory) => _embeddedStorage.IssueFullBackup(targetDirectory);
    public void IssueTransactionsLogCleanup() { }
    public IStorageStatistics CreateStorageStatistics() => _embeddedStorage.CreateStorageStatistics();
    public void ExportChannels(System.IO.DirectoryInfo targetDirectory, bool performGarbageCollection = true) => _embeddedStorage.ExportChannels(targetDirectory, performGarbageCollection);
    public void ImportFiles(System.IO.DirectoryInfo importDirectory) => _embeddedStorage.ImportFiles(importDirectory);

    public void Dispose() { }
}

/// <summary>
/// Simple persistence manager adapter for Eclipse Store compatibility.
/// </summary>
internal class SimplePersistenceManager : IPersistenceManager
{
    private readonly IEmbeddedStorageManager _embeddedStorage;

    public SimplePersistenceManager(IEmbeddedStorageManager embeddedStorage)
    {
        _embeddedStorage = embeddedStorage;
    }

    public long Store(object instance) => _embeddedStorage.Store(instance);
    public long[] StoreAll(params object[] instances) => _embeddedStorage.StoreAll(instances);
    public object GetObject(long objectId) => throw new NotSupportedException("Use object graph navigation instead");
    public IStorer CreateLazyStorer() => _embeddedStorage.CreateStorer();
    public IStorer CreateStorer() => _embeddedStorage.CreateStorer();
    public IStorer CreateEagerStorer() => _embeddedStorage.CreateStorer();
    public IStorageTypeDictionary TypeDictionary => _embeddedStorage.TypeDictionary;
    public NebulaStore.Storage.Embedded.Types.IPersistenceObjectRegistry ObjectRegistry => new SimpleObjectRegistry();
    public IPersistenceRootsView ViewRoots() => _embeddedStorage.ViewRoots();
}

/// <summary>
/// Simple object registry for Eclipse Store compatibility.
/// </summary>
internal class SimpleObjectRegistry : NebulaStore.Storage.Embedded.Types.IPersistenceObjectRegistry
{
    public long LookupObjectId(object instance) => 0; // Simple implementation
    public void RegisterObject(object instance, long objectId) { } // No-op
    public object? GetObject(long objectId) => null; // Simple implementation
    public void Consolidate() { } // No-op
}
