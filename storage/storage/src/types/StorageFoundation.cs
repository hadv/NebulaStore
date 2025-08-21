using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage;

/// <summary>
/// Default implementation of storage foundation.
/// Provides the framework for creating and configuring storage instances.
/// </summary>
internal class StorageFoundation : IStorageFoundation
{
    private IStorageConfiguration _configuration;
    private IDatabase _database;

    public IStorageConfiguration Configuration => _configuration;
    public IDatabase Database => _database;

    public StorageFoundation()
        : this(Storage.Configuration(), Databases.New())
    {
    }

    public StorageFoundation(IStorageConfiguration configuration)
        : this(configuration, Databases.New("NebulaStore", configuration))
    {
    }

    public StorageFoundation(IDatabase database)
        : this(database.Configuration, database)
    {
    }

    public StorageFoundation(IStorageConfiguration configuration, IDatabase database)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public IStorageFoundation SetConfiguration(IStorageConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    public IStorageFoundation SetDatabase(IDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        return this;
    }

    public IStorageManager Start(object? root = null)
    {
        var manager = CreateStorageManager(root);
        return manager.Start();
    }

    public IStorageManager CreateStorageManager(object? root = null)
    {
        return new StorageManager(_configuration, _database, root);
    }
}

/// <summary>
/// Default implementation of storage manager.
/// Manages the storage system and provides the main interface for storage operations.
/// </summary>
internal class StorageManager : IStorageManager
{
    private readonly object _lock = new();
    private readonly IStorageTypeDictionary _typeDictionary;
    private readonly ITypeHandlerRegistry _typeHandlerRegistry;
    private object? _root;
    private bool _isRunning;
    private bool _isAcceptingTasks;
    private bool _isDisposed;

    public IStorageConfiguration Configuration { get; }
    public IDatabase Database { get; }
    public IStorageTypeDictionary TypeDictionary => _typeDictionary;
    public string DatabaseName => Database.DatabaseName;

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _isRunning;
            }
        }
    }

    public bool IsAcceptingTasks
    {
        get
        {
            lock (_lock)
            {
                return _isAcceptingTasks;
            }
        }
    }

    public bool IsActive => IsRunning && !_isDisposed;

    public StorageManager(IStorageConfiguration configuration, IDatabase database, object? root = null)
    {
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        Database = database ?? throw new ArgumentNullException(nameof(database));
        _root = root;
        _typeDictionary = new StorageTypeDictionary();
        _typeHandlerRegistry = new TypeHandlerRegistry();
    }

    public IStorageManager Start()
    {
        lock (_lock)
        {
            if (_isRunning)
                return this;

            try
            {
                // Start the database if not already running
                if (!Database.IsRunning)
                    Database.Start();

                // Initialize storage manager components
                InitializeStorageManager();

                _isRunning = true;
                _isAcceptingTasks = true;

                return this;
            }
            catch
            {
                _isRunning = false;
                _isAcceptingTasks = false;
                throw;
            }
        }
    }

    public bool Shutdown()
    {
        lock (_lock)
        {
            if (!_isRunning)
                return true;

            try
            {
                _isAcceptingTasks = false;

                // Perform shutdown operations
                ShutdownStorageManager();

                _isRunning = false;
                return true;
            }
            catch
            {
                _isRunning = false;
                _isAcceptingTasks = false;
                return false;
            }
        }
    }

    public object? Root()
    {
        lock (_lock)
        {
            return _root;
        }
    }

    public object? SetRoot(object? newRoot)
    {
        lock (_lock)
        {
            _root = newRoot;
            return _root;
        }
    }

    public long StoreRoot()
    {
        var root = Root();
        if (root == null)
            return 0;

        using var storer = CreateStorer();
        var objectId = storer.Store(root);
        storer.Commit();
        return objectId;
    }

    public IStorageConnection CreateConnection()
    {
        return new StorageConnection(Configuration, _typeHandlerRegistry);
    }

    public IStorer CreateStorer()
    {
        return CreateConnection().CreateStorer();
    }

    public void IssueFullGarbageCollection()
    {
        // Implementation will be added when we implement garbage collection
        // For now, this is a no-op
    }

    public bool IssueGarbageCollection(long timeBudgetNanos)
    {
        // Implementation will be added when we implement garbage collection
        // For now, return true indicating completion
        return true;
    }

    public async Task CreateBackupAsync(string backupDirectory)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
            throw new ArgumentException("Backup directory cannot be null or empty", nameof(backupDirectory));

        // Implementation will be added when we implement backup functionality
        await Task.CompletedTask;
    }

    public IStorageStatistics GetStatistics()
    {
        // Return basic statistics for now
        return new StorageStatistics(Configuration);
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
            _isDisposed = true;
        }
    }

    private void InitializeStorageManager()
    {
        // Initialize storage manager components
        // This will be expanded as we add more components
    }

    private void ShutdownStorageManager()
    {
        // Perform shutdown operations
        // This will be expanded as we add more components
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(StorageManager));
    }
}

/// <summary>
/// Default implementation of storage type dictionary.
/// Manages type metadata and mappings between types and type IDs.
/// </summary>
internal class StorageTypeDictionary : IStorageTypeDictionary
{
    private readonly ConcurrentDictionary<Type, long> _typeToId = new();
    private readonly ConcurrentDictionary<long, Type> _idToType = new();
    private long _nextTypeId = 1;

    public int TypeCount => _typeToId.Count;

    public long RegisterType(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _typeToId.GetOrAdd(type, t =>
        {
            var typeId = Interlocked.Increment(ref _nextTypeId);
            _idToType.TryAdd(typeId, t);
            return typeId;
        });
    }

    public long GetTypeId(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _typeToId.TryGetValue(type, out var typeId) ? typeId : -1;
    }

    public Type? GetType(long typeId)
    {
        return _idToType.TryGetValue(typeId, out var type) ? type : null;
    }

    public bool IsTypeRegistered(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _typeToId.ContainsKey(type);
    }

    public bool IsTypeIdRegistered(long typeId)
    {
        return _idToType.ContainsKey(typeId);
    }
}
