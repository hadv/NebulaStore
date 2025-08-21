using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.Storage;

/// <summary>
/// Represents the mapping between a storage entity type and the files where instances of that type are stored.
/// Enables efficient type-based storage organization and retrieval.
/// </summary>
public interface ITypeInFile
{
    /// <summary>
    /// Gets the storage entity type.
    /// </summary>
    IStorageEntityType EntityType { get; }

    /// <summary>
    /// Gets the storage file where instances of this type are stored.
    /// </summary>
    IStorageDataFile DataFile { get; }

    /// <summary>
    /// Gets the number of instances of this type stored in the file.
    /// </summary>
    long InstanceCount { get; }

    /// <summary>
    /// Gets the total size of all instances of this type in the file.
    /// </summary>
    long TotalSize { get; }

    /// <summary>
    /// Gets the average size of instances of this type in the file.
    /// </summary>
    double AverageSize { get; }

    /// <summary>
    /// Adds an instance of this type to the file mapping.
    /// </summary>
    /// <param name="instanceSize">The size of the instance being added</param>
    void AddInstance(long instanceSize);

    /// <summary>
    /// Removes an instance of this type from the file mapping.
    /// </summary>
    /// <param name="instanceSize">The size of the instance being removed</param>
    void RemoveInstance(long instanceSize);
}

/// <summary>
/// Interface for storage entity types.
/// Represents a type that can be stored in the storage system.
/// </summary>
public interface IStorageEntityType
{
    /// <summary>
    /// Gets the type ID.
    /// </summary>
    long TypeId { get; }

    /// <summary>
    /// Gets the type name.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Gets the .NET type.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the type definition.
    /// </summary>
    IStorageTypeDefinition TypeDefinition { get; }
}

/// <summary>
/// Interface for storage data files.
/// Represents a file where storage data is persisted.
/// </summary>
public interface IStorageDataFile
{
    /// <summary>
    /// Gets the file ID.
    /// </summary>
    long FileId { get; }

    /// <summary>
    /// Gets the file path.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    long FileSize { get; }

    /// <summary>
    /// Gets the channel ID this file belongs to.
    /// </summary>
    int ChannelId { get; }

    /// <summary>
    /// Gets a value indicating whether this file is currently active for writing.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the creation timestamp of the file.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the last modification timestamp of the file.
    /// </summary>
    DateTime ModifiedAt { get; }
}

/// <summary>
/// Default implementation of TypeInFile.
/// Manages the mapping between storage entity types and data files.
/// </summary>
public class TypeInFile : ITypeInFile
{
    private readonly object _lock = new();
    private long _instanceCount;
    private long _totalSize;

    public IStorageEntityType EntityType { get; }
    public IStorageDataFile DataFile { get; }

    public long InstanceCount
    {
        get
        {
            lock (_lock)
            {
                return _instanceCount;
            }
        }
    }

    public long TotalSize
    {
        get
        {
            lock (_lock)
            {
                return _totalSize;
            }
        }
    }

    public double AverageSize
    {
        get
        {
            lock (_lock)
            {
                return _instanceCount > 0 ? (double)_totalSize / _instanceCount : 0.0;
            }
        }
    }

    public TypeInFile(IStorageEntityType entityType, IStorageDataFile dataFile)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        DataFile = dataFile ?? throw new ArgumentNullException(nameof(dataFile));
    }

    public void AddInstance(long instanceSize)
    {
        if (instanceSize < 0)
            throw new ArgumentException("Instance size cannot be negative", nameof(instanceSize));

        lock (_lock)
        {
            _instanceCount++;
            _totalSize += instanceSize;
        }
    }

    public void RemoveInstance(long instanceSize)
    {
        if (instanceSize < 0)
            throw new ArgumentException("Instance size cannot be negative", nameof(instanceSize));

        lock (_lock)
        {
            if (_instanceCount > 0)
            {
                _instanceCount--;
                _totalSize = Math.Max(0, _totalSize - instanceSize);
            }
        }
    }

    public override string ToString()
    {
        return $"TypeInFile[{EntityType.TypeName} in {DataFile.FilePath}]: {InstanceCount} instances, {TotalSize} bytes";
    }
}

/// <summary>
/// Default implementation of storage entity type.
/// </summary>
public class StorageEntityType : IStorageEntityType
{
    public long TypeId { get; }
    public string TypeName { get; }
    public Type Type { get; }
    public IStorageTypeDefinition TypeDefinition { get; }

    public StorageEntityType(IStorageTypeDefinition typeDefinition)
    {
        TypeDefinition = typeDefinition ?? throw new ArgumentNullException(nameof(typeDefinition));
        TypeId = typeDefinition.TypeId;
        TypeName = typeDefinition.TypeName;
        Type = typeDefinition.Type;
    }

    public override string ToString()
    {
        return $"EntityType[{TypeId}]: {TypeName}";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageEntityType other && TypeId == other.TypeId;
    }

    public override int GetHashCode()
    {
        return TypeId.GetHashCode();
    }
}

/// <summary>
/// Default implementation of storage data file.
/// </summary>
public class StorageDataFile : IStorageDataFile
{
    public long FileId { get; }
    public string FilePath { get; }
    public long FileSize { get; private set; }
    public int ChannelId { get; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime ModifiedAt { get; private set; }

    public StorageDataFile(
        long fileId,
        string filePath,
        int channelId,
        bool isActive = true)
    {
        FileId = fileId;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        ChannelId = channelId;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
        
        // Get initial file size if file exists
        if (System.IO.File.Exists(filePath))
        {
            FileSize = new System.IO.FileInfo(filePath).Length;
        }
    }

    public void UpdateFileSize(long newSize)
    {
        FileSize = newSize;
        ModifiedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        ModifiedAt = DateTime.UtcNow;
    }

    public override string ToString()
    {
        return $"DataFile[{FileId}]: {FilePath} ({FileSize} bytes, Channel {ChannelId})";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageDataFile other && FileId == other.FileId;
    }

    public override int GetHashCode()
    {
        return FileId.GetHashCode();
    }
}

/// <summary>
/// Manager for TypeInFile mappings.
/// Provides efficient lookup and management of type-to-file relationships.
/// </summary>
public class TypeInFileManager
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, ITypeInFile>> _typeToFiles = new();
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<long, ITypeInFile>> _fileToTypes = new();

    /// <summary>
    /// Gets all TypeInFile mappings for a specific type.
    /// </summary>
    /// <param name="typeId">The type ID</param>
    /// <returns>Collection of TypeInFile mappings for the type</returns>
    public IEnumerable<ITypeInFile> GetTypeInFiles(long typeId)
    {
        if (_typeToFiles.TryGetValue(typeId, out var files))
        {
            return files.Values;
        }
        return Enumerable.Empty<ITypeInFile>();
    }

    /// <summary>
    /// Gets all TypeInFile mappings for a specific file.
    /// </summary>
    /// <param name="fileId">The file ID</param>
    /// <returns>Collection of TypeInFile mappings for the file</returns>
    public IEnumerable<ITypeInFile> GetTypesInFile(long fileId)
    {
        if (_fileToTypes.TryGetValue(fileId, out var types))
        {
            return types.Values;
        }
        return Enumerable.Empty<ITypeInFile>();
    }

    /// <summary>
    /// Gets a specific TypeInFile mapping.
    /// </summary>
    /// <param name="typeId">The type ID</param>
    /// <param name="fileId">The file ID</param>
    /// <returns>The TypeInFile mapping, or null if not found</returns>
    public ITypeInFile? GetTypeInFile(long typeId, long fileId)
    {
        if (_typeToFiles.TryGetValue(typeId, out var files))
        {
            files.TryGetValue(fileId, out var typeInFile);
            return typeInFile;
        }
        return null;
    }

    /// <summary>
    /// Adds or updates a TypeInFile mapping.
    /// </summary>
    /// <param name="typeInFile">The TypeInFile mapping to add</param>
    public void AddTypeInFile(ITypeInFile typeInFile)
    {
        if (typeInFile == null)
            throw new ArgumentNullException(nameof(typeInFile));

        var typeId = typeInFile.EntityType.TypeId;
        var fileId = typeInFile.DataFile.FileId;

        _typeToFiles.GetOrAdd(typeId, _ => new ConcurrentDictionary<long, ITypeInFile>())[fileId] = typeInFile;
        _fileToTypes.GetOrAdd(fileId, _ => new ConcurrentDictionary<long, ITypeInFile>())[typeId] = typeInFile;
    }

    /// <summary>
    /// Removes a TypeInFile mapping.
    /// </summary>
    /// <param name="typeId">The type ID</param>
    /// <param name="fileId">The file ID</param>
    /// <returns>True if the mapping was removed, false if it didn't exist</returns>
    public bool RemoveTypeInFile(long typeId, long fileId)
    {
        var removed = false;

        if (_typeToFiles.TryGetValue(typeId, out var files))
        {
            removed = files.TryRemove(fileId, out _);
            if (files.IsEmpty)
            {
                _typeToFiles.TryRemove(typeId, out _);
            }
        }

        if (_fileToTypes.TryGetValue(fileId, out var types))
        {
            types.TryRemove(typeId, out _);
            if (types.IsEmpty)
            {
                _fileToTypes.TryRemove(fileId, out _);
            }
        }

        return removed;
    }

    /// <summary>
    /// Gets statistics about type distribution across files.
    /// </summary>
    /// <returns>Dictionary mapping type IDs to file counts</returns>
    public Dictionary<long, int> GetTypeDistributionStatistics()
    {
        return _typeToFiles.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Count
        );
    }

    /// <summary>
    /// Gets statistics about file utilization by types.
    /// </summary>
    /// <returns>Dictionary mapping file IDs to type counts</returns>
    public Dictionary<long, int> GetFileUtilizationStatistics()
    {
        return _fileToTypes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Count
        );
    }
}
