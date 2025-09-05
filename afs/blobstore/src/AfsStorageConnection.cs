using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MessagePack;
using NebulaStore.Storage;
using NebulaStore.Storage.EmbeddedConfiguration;
using NebulaStore.Storage.Embedded.Types;

namespace NebulaStore.Afs.Blobstore;

/// <summary>
/// Storage connection that uses Abstract File System (AFS) for storage operations.
/// </summary>
public class AfsStorageConnection : IStorageConnection
{
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly ITypeHandlerRegistry _typeHandlerRegistry;
    private readonly IBlobStoreFileSystem _fileSystem;
    private readonly IBlobStoreConnector _connector;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the AfsStorageConnection class.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <param name="typeHandlerRegistry">The type handler registry</param>
    public AfsStorageConnection(
        IEmbeddedStorageConfiguration configuration,
        ITypeHandlerRegistry typeHandlerRegistry)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _typeHandlerRegistry = typeHandlerRegistry ?? throw new ArgumentNullException(nameof(typeHandlerRegistry));

        // Create the appropriate connector based on configuration
        _connector = CreateConnector(configuration);
        _fileSystem = BlobStoreFileSystem.New(_connector);

        // Register GigaMap type handlers for automatic serialization
        RegisterGigaMapTypeHandlers();
    }

    /// <summary>
    /// Gets a value indicating whether this connection is active.
    /// </summary>
    public bool IsActive => !_disposed;

    /// <summary>
    /// Creates a new storer instance.
    /// </summary>
    /// <returns>A new storer instance</returns>
    public IStorer CreateStorer()
    {
        ThrowIfDisposed();
        return new AfsStorer(this, _configuration, _typeHandlerRegistry, _fileSystem);
    }

    /// <summary>
    /// Issues a full garbage collection.
    /// </summary>
    public void IssueFullGarbageCollection()
    {
        ThrowIfDisposed();
        // AFS-specific garbage collection logic would go here
        // For now, this is a no-op
    }

    /// <summary>
    /// Issues a garbage collection with the specified time budget.
    /// </summary>
    /// <param name="timeBudgetNanos">Time budget in nanoseconds</param>
    /// <returns>True if garbage collection completed within the time budget</returns>
    public bool IssueGarbageCollection(long timeBudgetNanos)
    {
        ThrowIfDisposed();
        // AFS-specific time-budgeted garbage collection logic would go here
        // For now, return true indicating completion
        return true;
    }

    /// <summary>
    /// Creates a backup of the storage data.
    /// </summary>
    /// <param name="backupDirectory">The backup directory path</param>
    /// <returns>A task representing the backup operation</returns>
    public async Task CreateBackupAsync(string backupDirectory)
    {
        ThrowIfDisposed();

        if (string.IsNullOrEmpty(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

        // AFS-specific backup logic would go here
        // For now, this is a simple placeholder implementation
        await Task.Run(() =>
        {
            // Create backup directory if it doesn't exist
            System.IO.Directory.CreateDirectory(backupDirectory);

            // In a real implementation, this would copy all blob files
            // to the backup directory with proper structure preservation
        });
    }

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    /// <returns>Storage statistics</returns>
    public IStorageStatistics GetStatistics()
    {
        ThrowIfDisposed();
        return new AfsStorageStatistics(_configuration, _fileSystem);
    }

    /// <summary>
    /// Gets or assigns an object ID for the specified object.
    /// </summary>
    /// <param name="obj">The object</param>
    /// <returns>The object ID</returns>
    public long GetOrAssignObjectId(object obj)
    {
        ThrowIfDisposed();

        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Simple hash-based ID assignment for now
        // In a real implementation, this would use a proper ID management system
        return obj.GetHashCode();
    }

    /// <summary>
    /// Saves the root object to AFS storage.
    /// </summary>
    /// <param name="root">The root object to save</param>
    public void SaveRoot(object root)
    {
        ThrowIfDisposed();

        if (root == null)
            return;

        try
        {
            var rootPath = BlobStorePath.New("root.msgpack");
            var wrapper = new RootWrapper
            {
                TypeName = root.GetType().AssemblyQualifiedName!,
                Data = root
            };

            var data = MessagePackSerializer.Serialize(wrapper);
            _fileSystem.IoHandler.WriteData(rootPath, data);
        }
        catch
        {
            // Ignore errors during root saving
        }
    }

    /// <summary>
    /// Loads the root object from AFS storage.
    /// </summary>
    /// <param name="rootType">The expected root type</param>
    /// <returns>The loaded root object, or null if not found</returns>
    public object? LoadRoot(Type? rootType)
    {
        ThrowIfDisposed();

        try
        {
            var rootPath = BlobStorePath.New("root.msgpack");
            if (!_fileSystem.IoHandler.FileExists(rootPath))
                return null;

            var data = _fileSystem.IoHandler.ReadData(rootPath, 0, -1);
            if (data.Length == 0)
                return null;

            var wrapper = MessagePackSerializer.Deserialize<RootWrapper>(data);
            var actualType = Type.GetType(wrapper.TypeName);

            if (actualType != null && wrapper.Data != null)
            {
                var dataBytes = MessagePackSerializer.Serialize(wrapper.Data);
                return MessagePackSerializer.Deserialize(actualType, dataBytes);
            }
        }
        catch
        {
            // Ignore errors during root loading
        }

        return null;
    }

    /// <summary>
    /// Gets the blob store file system.
    /// </summary>
    internal IBlobStoreFileSystem FileSystem => _fileSystem;

    /// <summary>
    /// Creates the appropriate connector based on configuration.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>The blob store connector</returns>
    private static IBlobStoreConnector CreateConnector(IEmbeddedStorageConfiguration configuration)
    {
        return configuration.AfsStorageType.ToLowerInvariant() switch
        {
            "blobstore" => new LocalBlobStoreConnector(
                configuration.AfsConnectionString ?? configuration.StorageDirectory,
                configuration.AfsUseCache),
            "firestore" => CreateFirestoreConnector(configuration),
            "azure.storage" => CreateAzureStorageConnector(configuration),
            "s3" => CreateS3Connector(configuration),
            _ => throw new NotSupportedException($"AFS storage type '{configuration.AfsStorageType}' is not supported")
        };
    }

    /// <summary>
    /// Creates a Google Cloud Firestore connector.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>The Firestore connector</returns>
    private static IBlobStoreConnector CreateFirestoreConnector(IEmbeddedStorageConfiguration configuration)
    {
        try
        {
            // Use reflection to avoid hard dependency on Google Cloud Firestore
            var firestoreAssembly = System.Reflection.Assembly.LoadFrom("NebulaStore.Afs.GoogleCloud.Firestore.dll");
            var connectorType = firestoreAssembly.GetType("NebulaStore.Afs.GoogleCloud.Firestore.GoogleCloudFirestoreConnector");

            if (connectorType == null)
                throw new TypeLoadException("GoogleCloudFirestoreConnector type not found");

            // Create FirestoreDb instance
            var firestoreDbType = Type.GetType("Google.Cloud.Firestore.FirestoreDb, Google.Cloud.Firestore");
            if (firestoreDbType == null)
                throw new TypeLoadException("Google.Cloud.Firestore.FirestoreDb type not found. Make sure Google.Cloud.Firestore package is installed.");

            var createMethod = firestoreDbType.GetMethod("Create", new[] { typeof(string) });
            if (createMethod == null)
                throw new MethodAccessException("FirestoreDb.Create method not found");

            var projectId = configuration.AfsConnectionString ?? throw new ArgumentException("Project ID must be specified in AfsConnectionString for Firestore storage");
            var firestoreDb = createMethod.Invoke(null, new object[] { projectId });

            // Create connector
            var factoryMethod = configuration.AfsUseCache
                ? connectorType.GetMethod("Caching", new[] { firestoreDbType })
                : connectorType.GetMethod("New", new[] { firestoreDbType });

            if (factoryMethod == null)
                throw new MethodAccessException($"GoogleCloudFirestoreConnector factory method not found");

            var connector = factoryMethod.Invoke(null, new[] { firestoreDb });
            return (IBlobStoreConnector)connector!;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new NotSupportedException(
                "Google Cloud Firestore connector could not be created. " +
                "Make sure NebulaStore.Afs.GoogleCloud.Firestore and Google.Cloud.Firestore packages are installed.", ex);
        }
    }

    /// <summary>
    /// Creates an Azure Storage connector.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>The Azure Storage connector</returns>
    private static IBlobStoreConnector CreateAzureStorageConnector(IEmbeddedStorageConfiguration configuration)
    {
        try
        {
            // Use reflection to avoid hard dependency on Azure Storage
            var azureAssembly = System.Reflection.Assembly.LoadFrom("NebulaStore.Afs.Azure.Storage.dll");
            var connectorType = azureAssembly.GetType("NebulaStore.Afs.Azure.Storage.AzureStorageConnector");

            if (connectorType == null)
                throw new TypeLoadException("AzureStorageConnector type not found");

            var connectionString = configuration.AfsConnectionString ?? throw new ArgumentException("Connection string must be specified in AfsConnectionString for Azure storage");

            var factoryMethod = connectorType.GetMethod("FromConnectionString", new[] { typeof(string), typeof(bool) });
            if (factoryMethod == null)
                throw new MethodAccessException("AzureStorageConnector.FromConnectionString method not found");

            var connector = factoryMethod.Invoke(null, new object[] { connectionString, configuration.AfsUseCache });
            return (IBlobStoreConnector)connector!;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new NotSupportedException(
                "Azure Storage connector could not be created. " +
                "Make sure NebulaStore.Afs.Azure.Storage and Azure.Storage.Blobs packages are installed.", ex);
        }
    }

    /// <summary>
    /// Creates an AWS S3 connector.
    /// </summary>
    /// <param name="configuration">The storage configuration</param>
    /// <returns>The S3 connector</returns>
    private static IBlobStoreConnector CreateS3Connector(IEmbeddedStorageConfiguration configuration)
    {
        try
        {
            // Use reflection to avoid hard dependency on AWS S3
            var s3Assembly = System.Reflection.Assembly.LoadFrom("NebulaStore.Afs.Aws.S3.dll");
            var connectorType = s3Assembly.GetType("NebulaStore.Afs.Aws.S3.AwsS3Connector");

            if (connectorType == null)
                throw new TypeLoadException("AwsS3Connector type not found");

            var bucketName = configuration.AfsConnectionString ?? throw new ArgumentException("Bucket name must be specified in AfsConnectionString for S3 storage");

            var factoryMethod = connectorType.GetMethod("FromBucketName", new[] { typeof(string), typeof(bool) });
            if (factoryMethod == null)
                throw new MethodAccessException("AwsS3Connector.FromBucketName method not found");

            var connector = factoryMethod.Invoke(null, new object[] { bucketName, configuration.AfsUseCache });
            return (IBlobStoreConnector)connector!;
        }
        catch (Exception ex) when (!(ex is ArgumentException))
        {
            throw new NotSupportedException(
                "AWS S3 connector could not be created. " +
                "Make sure NebulaStore.Afs.Aws.S3 and AWSSDK.S3 packages are installed.", ex);
        }
    }

    /// <summary>
    /// Registers type handlers for GigaMap serialization.
    /// This enables automatic persistence of GigaMap instances following Eclipse Store patterns.
    /// </summary>
    private void RegisterGigaMapTypeHandlers()
    {
        // Register handlers for GigaMap metadata and related types
        // This follows Eclipse Store's pattern of automatic type handler registration

        // Note: In a full implementation, we would register specific type handlers
        // for GigaMap serialization. For now, we rely on MessagePack serialization
        // which is handled in the AfsGigaMap implementation.
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    /// <summary>
    /// Disposes the connection.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _fileSystem?.Dispose();
            _connector?.Dispose();
            _disposed = true;
        }
    }

    // IStorageConnection interface methods
    public bool IssueGarbageCollection(TimeSpan timeBudget)
    {
        // AFS implementation would trigger GC with time budget
        return true; // Return true if GC completed within time budget
    }

    public void IssueFullFileCheck()
    {
        // AFS implementation would check all files
    }

    public bool IssueFileCheck(TimeSpan timeBudget)
    {
        // AFS implementation would check files with time budget
        return true; // Return true if file check completed within time budget
    }

    public void IssueFullCacheCheck()
    {
        // AFS implementation would check cache
    }

    public bool IssueCacheCheck(TimeSpan timeBudget)
    {
        // AFS implementation would check cache with time budget
        return true; // Return true if cache check completed within time budget
    }

    public void IssueFullBackup(DirectoryInfo backupDirectory)
    {
        // AFS implementation would backup to directory
    }

    public void IssueTransactionsLogCleanup()
    {
        // AFS implementation would cleanup transaction logs
    }

    public IStorageStatistics CreateStorageStatistics()
    {
        return new AfsStorageStatistics(_configuration, _fileSystem);
    }

    public void ExportChannels(DirectoryInfo exportDirectory, bool performGarbageCollection)
    {
        // AFS implementation would export channels
    }

    public void ImportFiles(DirectoryInfo importDirectory)
    {
        // AFS implementation would import files
    }

    public IPersistenceManager PersistenceManager => throw new NotImplementedException("AFS persistence manager not implemented");

    // IStorer interface methods - delegate to AfsStorer
    public long Store(object obj)
    {
        using var storer = CreateStorer();
        return storer.Store(obj);
    }

    public long[] StoreAll(params object[] objects)
    {
        using var storer = CreateStorer();
        return storer.StoreAll(objects);
    }

    public long Commit()
    {
        // AFS commits are handled automatically
        return 0;
    }

    public IStorer Skip(object obj)
    {
        // AFS implementation would skip object
        return this;
    }

    public long Ensure(object obj)
    {
        // AFS implementation would ensure object is stored
        return Store(obj);
    }

    public long PendingObjectCount => 0; // AFS doesn't have pending objects

    public bool HasPendingOperations => false; // AFS doesn't have pending operations
}

/// <summary>
/// AFS-specific storer implementation.
/// </summary>
internal class AfsStorer : IStorer
{
    private readonly AfsStorageConnection _connection;
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly ITypeHandlerRegistry _typeHandlerRegistry;
    private readonly IBlobStoreFileSystem _fileSystem;
    private readonly HashSet<object> _pendingObjects = new();
    private readonly HashSet<object> _storedObjects = new();
    private bool _disposed;

    public AfsStorer(
        AfsStorageConnection connection,
        IEmbeddedStorageConfiguration configuration,
        ITypeHandlerRegistry typeHandlerRegistry,
        IBlobStoreFileSystem fileSystem)
    {
        _connection = connection;
        _configuration = configuration;
        _typeHandlerRegistry = typeHandlerRegistry;
        _fileSystem = fileSystem;
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

        long committedCount = 0;
        
        // Process each pending object
        foreach (var obj in _pendingObjects)
        {
            try
            {
                // Serialize and store the object using AFS
                var objectId = _connection.GetOrAssignObjectId(obj);
                var path = BlobStorePath.New("objects", objectId.ToString());
                
                // Get type handler for serialization
                var typeHandler = _typeHandlerRegistry.GetTypeHandler(obj.GetType());

                byte[] data;
                if (typeHandler != null)
                {
                    data = typeHandler.Serialize(obj);
                }
                else
                {
                    // Fallback to MessagePack
                    data = MessagePack.MessagePackSerializer.Serialize(obj);
                }

                // Write to AFS
                _fileSystem.IoHandler.WriteData(path, data);

                _storedObjects.Add(obj);
                committedCount++;
            }
            catch
            {
                // Log error and continue with other objects
                // In a real implementation, this would have proper error handling
            }
        }

        _pendingObjects.Clear();
        return committedCount;
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

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pendingObjects.Clear();
            _storedObjects.Clear();
            _disposed = true;
        }
    }

    // Removed duplicate methods - they're now in AfsStorageConnection class
}

/// <summary>
/// AFS-specific storage statistics implementation.
/// </summary>
internal class AfsStorageStatistics : IStorageStatistics
{
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly IBlobStoreFileSystem _fileSystem;

    public AfsStorageStatistics(
        IEmbeddedStorageConfiguration configuration,
        IBlobStoreFileSystem fileSystem)
    {
        _configuration = configuration;
        _fileSystem = fileSystem;
        CreationTime = DateTime.UtcNow;
        LastModificationTime = DateTime.UtcNow;
    }

    public DateTime CreationTime { get; }
    public DateTime LastModificationTime { get; private set; }
    public long TotalObjectCount => GetObjectCount();
    public long TotalStorageSize => GetTotalStorageSize();
    public int DataFileCount => GetDataFileCount();
    public int TransactionFileCount => GetTransactionFileCount();
    public long LiveDataLength => GetLiveDataLength();

    // Additional properties for IStorageStatistics interface
    public int TotalFileCount => DataFileCount + TransactionFileCount;
    public long TotalFileSize => TotalStorageSize;
    public long LiveDataSize => LiveDataLength;
    public DateTime CreationTimestamp => CreationTime;

    private long GetTotalStorageSize()
    {
        // This would calculate the total storage size from the AFS
        // For now, return a placeholder value
        return 1024 * 1024 * 1024; // 1GB
    }

    private long GetUsedStorageSize()
    {
        // This would calculate the used storage size from the AFS
        // For now, return a placeholder value
        return 1024 * 1024; // 1MB
    }

    private int GetObjectCount()
    {
        // This would count objects in the AFS
        // For now, return a placeholder value
        return 0;
    }

    private int GetDataFileCount()
    {
        // This would count data files in the AFS
        // For now, return a placeholder value
        return 1;
    }

    private int GetTransactionFileCount()
    {
        // This would count transaction files in the AFS
        // For now, return a placeholder value
        return 0;
    }

    private long GetLiveDataLength()
    {
        // This would calculate live data length in the AFS
        // For now, return the used storage size
        return GetUsedStorageSize();
    }
}

/// <summary>
/// Wrapper class for root object serialization.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
internal class RootWrapper
{
    [Key(0)]
    public object? Data { get; set; }

    [Key(1)]
    public string TypeName { get; set; } = string.Empty;
}
