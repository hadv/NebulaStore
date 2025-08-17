using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MessagePack;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of the embedded storage manager.
/// Manages object persistence and provides high-level storage operations.
/// </summary>
public class EmbeddedStorageManager : IEmbeddedStorageManager
{
    private readonly IEmbeddedStorageConfiguration _configuration;
    private readonly IStorageConnection _connection;
    private readonly Func<Type, bool>? _typeEvaluator;
    private object? _root;
    private Type? _rootType;
    private bool _isRunning;
    private bool _isDisposed;

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
