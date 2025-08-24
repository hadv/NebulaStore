using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NebulaStore.GigaMap;
using NebulaStore.Storage;
using MessagePack;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Extension methods for EmbeddedStorage to support GigaMap integration with AFS.
/// Following Eclipse Store patterns for seamless GigaMap persistence.
/// </summary>
public static class GigaMapStorageExtensions
{
    /// <summary>
    /// Creates a new GigaMap builder that automatically integrates with the storage system.
    /// This follows Eclipse Store's pattern of transparent integration.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="storage">The storage manager</param>
    /// <returns>A GigaMap builder with automatic persistence</returns>
    public static IGigaMapBuilder<T> CreateGigaMap<T>(this IEmbeddedStorageManager storage) where T : class
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        return storage.CreateGigaMap<T>();
    }

    /// <summary>
    /// Registers a GigaMap for automatic persistence and loading.
    /// Following Eclipse Store's pattern where GigaMaps are automatically managed.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="storage">The storage manager</param>
    /// <param name="gigaMap">The GigaMap to register</param>
    public static void RegisterGigaMap<T>(this IEmbeddedStorageManager storage, IGigaMap<T> gigaMap) where T : class
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));
        if (gigaMap == null)
            throw new ArgumentNullException(nameof(gigaMap));

        storage.RegisterGigaMap(gigaMap);
    }

    /// <summary>
    /// Gets an existing GigaMap for the specified type.
    /// Returns null if no GigaMap is registered for the type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="storage">The storage manager</param>
    /// <returns>The GigaMap instance or null</returns>
    public static IGigaMap<T>? GetGigaMap<T>(this IEmbeddedStorageManager storage) where T : class
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        return storage.GetGigaMap<T>();
    }

    /// <summary>
    /// Stores all registered GigaMaps to persistent storage.
    /// Following Eclipse Store's pattern of batch persistence operations.
    /// </summary>
    /// <param name="storage">The storage manager</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task StoreGigaMapsAsync(this IEmbeddedStorageManager storage)
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        await storage.StoreGigaMapsAsync();
    }

    /// <summary>
    /// Stores a specific GigaMap to persistent storage.
    /// Following Eclipse Store's pattern where individual GigaMaps can be stored.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="storage">The storage manager</param>
    /// <param name="gigaMap">The GigaMap to store</param>
    /// <returns>The object ID of the stored GigaMap</returns>
    public static long Store<T>(this IEmbeddedStorageManager storage, IGigaMap<T> gigaMap) where T : class
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));
        if (gigaMap == null)
            throw new ArgumentNullException(nameof(gigaMap));

        return storage.Store(gigaMap);
    }

    /// <summary>
    /// Creates a GigaMap with automatic registration and AFS integration.
    /// This is a convenience method that combines creation, building, and registration.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="storage">The storage manager</param>
    /// <param name="configure">Optional configuration action for the GigaMap builder</param>
    /// <returns>A configured and registered GigaMap</returns>
    public static IGigaMap<T> CreateAndRegisterGigaMap<T>(
        this IEmbeddedStorageManager storage,
        Action<IGigaMapBuilder<T>>? configure = null) where T : class
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        var builder = storage.CreateGigaMap<T>();
        
        // Apply configuration if provided
        configure?.Invoke(builder);
        
        var gigaMap = builder.Build();
        storage.RegisterGigaMap(gigaMap);
        
        return gigaMap;
    }

    /// <summary>
    /// Loads all GigaMaps from persistent storage.
    /// This method is called automatically during storage startup.
    /// </summary>
    /// <param name="storage">The storage manager</param>
    /// <returns>A task representing the asynchronous operation</returns>
    internal static async Task LoadGigaMapsAsync(this IEmbeddedStorageManager storage)
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        // Implementation will be added to load GigaMaps from AFS
        // This follows Eclipse Store's pattern of automatic loading
        await Task.CompletedTask;
    }
}

/// <summary>
/// GigaMap metadata for AFS persistence.
/// This stores the essential information needed to reconstruct GigaMaps from AFS.
/// </summary>
[MessagePackObject]
public class GigaMapPersistenceMetadata
{
    /// <summary>
    /// The entity type name for this GigaMap.
    /// </summary>
    [Key(0)]
    public string EntityTypeName { get; set; } = string.Empty;

    /// <summary>
    /// The number of entities in this GigaMap.
    /// </summary>
    [Key(1)]
    public long EntityCount { get; set; }

    /// <summary>
    /// The next entity ID to be assigned.
    /// </summary>
    [Key(2)]
    public long NextEntityId { get; set; }

    /// <summary>
    /// The names of indices configured for this GigaMap.
    /// </summary>
    [Key(3)]
    public List<string> IndexNames { get; set; } = new();

    /// <summary>
    /// The AFS path where entities are stored.
    /// </summary>
    [Key(4)]
    public string EntitiesPath { get; set; } = string.Empty;

    /// <summary>
    /// The AFS path where indices are stored.
    /// </summary>
    [Key(5)]
    public string IndicesPath { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this GigaMap was last stored.
    /// </summary>
    [Key(6)]
    public DateTime LastStoredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Version of the GigaMap storage format.
    /// </summary>
    [Key(7)]
    public int StorageVersion { get; set; } = 1;
}

/// <summary>
/// Registry for tracking GigaMap instances and their persistence metadata.
/// This enables automatic loading and storing of GigaMaps with AFS.
/// </summary>
internal class GigaMapRegistry
{
    private readonly Dictionary<Type, object> _gigaMaps = new();
    private readonly Dictionary<Type, GigaMapPersistenceMetadata> _metadata = new();
    private readonly object _lock = new();

    /// <summary>
    /// Registers a GigaMap instance with its metadata.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="gigaMap">The GigaMap instance</param>
    /// <param name="metadata">The persistence metadata</param>
    public void Register<T>(IGigaMap<T> gigaMap, GigaMapPersistenceMetadata metadata) where T : class
    {
        lock (_lock)
        {
            _gigaMaps[typeof(T)] = gigaMap;
            _metadata[typeof(T)] = metadata;
        }
    }

    /// <summary>
    /// Gets a registered GigaMap instance.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>The GigaMap instance or null if not found</returns>
    public IGigaMap<T>? Get<T>() where T : class
    {
        lock (_lock)
        {
            return _gigaMaps.TryGetValue(typeof(T), out var gigaMap) 
                ? (IGigaMap<T>)gigaMap 
                : null;
        }
    }

    /// <summary>
    /// Gets the persistence metadata for a GigaMap type.
    /// </summary>
    /// <param name="entityType">The entity type</param>
    /// <returns>The metadata or null if not found</returns>
    public GigaMapPersistenceMetadata? GetMetadata(Type entityType)
    {
        lock (_lock)
        {
            return _metadata.TryGetValue(entityType, out var metadata) ? metadata : null;
        }
    }

    /// <summary>
    /// Gets all registered GigaMap types.
    /// </summary>
    /// <returns>Collection of registered types</returns>
    public IEnumerable<Type> GetRegisteredTypes()
    {
        lock (_lock)
        {
            return _gigaMaps.Keys.ToList();
        }
    }

    /// <summary>
    /// Removes a GigaMap registration.
    /// </summary>
    /// <param name="entityType">The entity type to remove</param>
    public void Unregister(Type entityType)
    {
        lock (_lock)
        {
            _gigaMaps.Remove(entityType);
            _metadata.Remove(entityType);
        }
    }

    /// <summary>
    /// Clears all registrations.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _gigaMaps.Clear();
            _metadata.Clear();
        }
    }
}
