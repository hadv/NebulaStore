using MessagePack;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Storage.Monitoring;

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

    public bool IsAcceptingTasks => IsRunning && _connection.IsActive;

    public bool IsActive => IsRunning && _connection.IsActive;

    public T Root<T>() where T : new()
    {
        ThrowIfDisposed();

        if (_root == null)
        {
            _root = new T();
            _rootType = typeof(T);
        }
        else if (_root is not T)
        {
            throw new InvalidOperationException($"Root object is of type {_root.GetType().Name}, not {typeof(T).Name}");
        }

        // Ensure root type is set
        if (_rootType == null)
        {
            _rootType = typeof(T);
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

    public IEnumerable<T> Query<T>()
    {
        ThrowIfDisposed();
        
        if (_root == null) 
            yield break;

        foreach (var item in TraverseGraphLazy(_root, new HashSet<object>()))
        {
            if (item is T t) 
                yield return t;
        }
    }

    public IStorer CreateStorer()
    {
        ThrowIfDisposed();
        return _connection.CreateStorer();
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
        return _connection.IssueGarbageCollection(timeBudgetNanos);
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
        return _connection.GetStatistics();
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

    private IEnumerable<object> TraverseGraphLazy(object obj, HashSet<object> visited)
    {
        if (obj == null || visited.Contains(obj)) 
            yield break;

        visited.Add(obj);
        yield return obj;

        // Traverse collections
        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            foreach (var element in enumerable)
            {
                if (element != null)
                {
                    foreach (var child in TraverseGraphLazy(element, visited))
                        yield return child;
                }
            }
        }

        // Traverse properties
        var type = obj.GetType();
        foreach (var prop in type.GetProperties())
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;

            object? value = null;
            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                // Skip properties that can't be accessed
                continue;
            }

            if (value != null && value is not string && _typeEvaluator!(value.GetType()))
            {
                foreach (var child in TraverseGraphLazy(value, visited))
                    yield return child;
            }
        }
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
internal class PlaceholderObjectRegistry : IPersistenceObjectRegistry
{
    public long Capacity => 1000000; // Default capacity
    public long Size => 0; // No objects registered yet
}

/// <summary>
/// Placeholder implementation of entity cache for monitoring.
/// This will be replaced when the actual entity cache is implemented.
/// </summary>
internal class PlaceholderEntityCache : IStorageEntityCache
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
