using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using NebulaStore.Storage.EmbeddedConfiguration;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of storage connection.
/// Manages the connection to the underlying storage system.
/// </summary>
internal class StorageConnection : IStorageConnection
{
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly ITypeHandlerRegistry _typeHandlerRegistry;
    private readonly Dictionary<object, long> _objectIdMap = new();
    private long _nextObjectId = 1;
    private bool _isDisposed;

    public StorageConnection(IEmbeddedStorageConfiguration configuration, ITypeHandlerRegistry typeHandlerRegistry)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _typeHandlerRegistry = typeHandlerRegistry ?? throw new ArgumentNullException(nameof(typeHandlerRegistry));
    }

    public bool IsActive => !_isDisposed;

    public IStorer CreateStorer()
    {
        ThrowIfDisposed();
        return new Storer(this, _configuration, _typeHandlerRegistry);
    }

    public void IssueFullGarbageCollection()
    {
        ThrowIfDisposed();
        // Implementation for garbage collection
        // For now, this is a no-op as we don't have complex GC logic yet
    }

    public bool IssueGarbageCollection(long timeBudgetNanos)
    {
        ThrowIfDisposed();
        // Implementation for time-budgeted garbage collection
        // For now, return true indicating completion
        return true;
    }

    public IStorageStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return new StorageStatistics(_configuration);
    }

    internal long GetOrAssignObjectId(object obj)
    {
        if (_objectIdMap.TryGetValue(obj, out var existingId))
            return existingId;

        var newId = _nextObjectId++;
        _objectIdMap[obj] = newId;
        return newId;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StorageConnection));
    }
}

/// <summary>
/// Default implementation of storer for batch operations.
/// </summary>
internal class Storer : IStorer
{
    private readonly StorageConnection _connection;
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly ITypeHandlerRegistry _typeHandlerRegistry;
    private readonly List<object> _pendingObjects = new();
    private readonly HashSet<object> _storedObjects = new();
    private bool _isDisposed;

    public Storer(StorageConnection connection, IEmbeddedStorageConfiguration configuration, ITypeHandlerRegistry typeHandlerRegistry)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _typeHandlerRegistry = typeHandlerRegistry ?? throw new ArgumentNullException(nameof(typeHandlerRegistry));
    }

    public long PendingObjectCount => _pendingObjects.Count;

    public bool HasPendingOperations => _pendingObjects.Count > 0;

    public long Store(object obj)
    {
        ThrowIfDisposed();
        
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        if (_storedObjects.Contains(obj))
            return _connection.GetOrAssignObjectId(obj);

        _pendingObjects.Add(obj);
        return _connection.GetOrAssignObjectId(obj);
    }

    public long[] StoreAll(params object[] objects)
    {
        ThrowIfDisposed();
        
        if (objects == null)
            throw new ArgumentNullException(nameof(objects));

        var objectIds = new long[objects.Length];
        for (int i = 0; i < objects.Length; i++)
        {
            objectIds[i] = Store(objects[i]);
        }
        return objectIds;
    }

    public long Commit()
    {
        ThrowIfDisposed();
        
        if (_pendingObjects.Count == 0)
            return 0;

        try
        {
            // Serialize and store all pending objects
            var dataFilePath = Path.Combine(_configuration.StorageDirectory, "data.msgpack");
            Directory.CreateDirectory(_configuration.StorageDirectory);

            var objectsToStore = new List<StoredObject>();
            
            foreach (var obj in _pendingObjects)
            {
                var objectId = _connection.GetOrAssignObjectId(obj);
                var typeHandler = _typeHandlerRegistry.GetTypeHandler(obj.GetType());
                
                byte[] data;
                if (typeHandler != null)
                {
                    data = typeHandler.Serialize(obj);
                }
                else
                {
                    // Fallback to MessagePack
                    data = MessagePackSerializer.Serialize(obj);
                }

                objectsToStore.Add(new StoredObject
                {
                    ObjectId = objectId,
                    TypeName = obj.GetType().AssemblyQualifiedName!,
                    Data = data
                });

                _storedObjects.Add(obj);
            }

            // Append to data file
            var serializedData = MessagePackSerializer.Serialize(objectsToStore);
            File.AppendAllText(dataFilePath + ".tmp", Convert.ToBase64String(serializedData) + Environment.NewLine);
            
            if (File.Exists(dataFilePath))
                File.Delete(dataFilePath);
            File.Move(dataFilePath + ".tmp", dataFilePath);

            var committedCount = _pendingObjects.Count;
            _pendingObjects.Clear();
            
            return committedCount;
        }
        catch
        {
            // On error, don't clear pending objects so they can be retried
            throw;
        }
    }

    public IStorer Skip(object obj)
    {
        ThrowIfDisposed();
        
        if (obj != null)
        {
            _storedObjects.Add(obj);
        }
        return this;
    }

    public long Ensure(object obj)
    {
        ThrowIfDisposed();
        
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Remove from stored objects to force re-storage
        _storedObjects.Remove(obj);
        return Store(obj);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        try
        {
            if (HasPendingOperations)
            {
                Commit();
            }
        }
        finally
        {
            _isDisposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(Storer));
    }
}

[MessagePackObject(AllowPrivate = true)]
internal class StoredObject
{
    [Key(0)]
    public long ObjectId { get; set; }
    
    [Key(1)]
    public string TypeName { get; set; } = string.Empty;
    
    [Key(2)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Default implementation of storage statistics.
/// </summary>
internal class StorageStatistics : IStorageStatistics
{
    private readonly IEmbeddedStorageConfiguration _configuration;

    public StorageStatistics(IEmbeddedStorageConfiguration configuration)
    {
        _configuration = configuration;
        CreationTime = DateTime.UtcNow;
        LastModificationTime = DateTime.UtcNow;
    }

    public long TotalObjectCount { get; internal set; }
    public long TotalStorageSize { get; internal set; }
    public int DataFileCount { get; internal set; } = 1;
    public int TransactionFileCount { get; internal set; }
    public long LiveDataLength { get; internal set; }
    public DateTime CreationTime { get; }
    public DateTime LastModificationTime { get; internal set; }
}
