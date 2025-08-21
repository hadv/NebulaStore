using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Default implementation of advanced entity management operations.
/// Provides comprehensive CRUD operations, relationship handling, and validation.
/// </summary>
public class EntityManager : IEntityManager
{
    private readonly IStorageConnection _storageConnection;
    private readonly IEnhancedStorageTypeDictionary _typeDictionary;
    private readonly ConcurrentDictionary<long, object> _entityCache = new();
    private readonly ConcurrentDictionary<long, HashSet<EntityReference>> _entityReferences = new();
    private readonly ConcurrentDictionary<long, Type> _entityTypes = new();
    private readonly object _transactionLock = new();
    private IEntityTransaction? _currentTransaction;
    private bool _isDisposed;

    public EntityManager(IStorageConnection storageConnection, IEnhancedStorageTypeDictionary typeDictionary)
    {
        _storageConnection = storageConnection ?? throw new ArgumentNullException(nameof(storageConnection));
        _typeDictionary = typeDictionary ?? throw new ArgumentNullException(nameof(typeDictionary));
    }

    public IEntityTransaction? CurrentTransaction => _currentTransaction;

    #region Entity Lifecycle Operations

    public long Create<T>(T entity) where T : class
    {
        ThrowIfDisposed();
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        // Validate entity before creation
        var validationResult = Validate(entity);
        if (!validationResult.IsValid)
        {
            throw new EntityValidationException($"Entity validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        using var storer = _storageConnection.CreateStorer();
        var objectId = storer.Store(entity);
        storer.Commit();

        // Cache the entity and register its type
        _entityCache.TryAdd(objectId, entity);
        _entityTypes.TryAdd(objectId, typeof(T));
        _typeDictionary.RegisterType(typeof(T));

        return objectId;
    }

    public long[] CreateAll<T>(IEnumerable<T> entities) where T : class
    {
        ThrowIfDisposed();
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        
        // Validate all entities first
        var validationResults = ValidateAll(entityList);
        var invalidEntities = validationResults.Where(kvp => !kvp.Value.IsValid).ToList();
        if (invalidEntities.Any())
        {
            var errors = invalidEntities.SelectMany(kvp => kvp.Value.Errors);
            throw new EntityValidationException($"Entity validation failed: {string.Join(", ", errors)}");
        }

        using var storer = _storageConnection.CreateStorer();
        var objectIds = new long[entityList.Count];
        
        for (int i = 0; i < entityList.Count; i++)
        {
            var entity = entityList[i];
            objectIds[i] = storer.Store(entity);
            
            // Cache the entity and register its type
            _entityCache.TryAdd(objectIds[i], entity);
            _entityTypes.TryAdd(objectIds[i], typeof(T));
        }
        
        storer.Commit();
        _typeDictionary.RegisterType(typeof(T));

        return objectIds;
    }

    public T? Read<T>(long objectId) where T : class
    {
        ThrowIfDisposed();

        // Check cache first
        if (_entityCache.TryGetValue(objectId, out var cachedEntity) && cachedEntity is T typedEntity)
        {
            return typedEntity;
        }

        // TODO: Implement loading from storage
        // This would require implementing a loader/reader interface
        // For now, return null if not in cache
        return null;
    }

    public Dictionary<long, T?> ReadAll<T>(IEnumerable<long> objectIds) where T : class
    {
        ThrowIfDisposed();
        if (objectIds == null) throw new ArgumentNullException(nameof(objectIds));

        var result = new Dictionary<long, T?>();
        foreach (var objectId in objectIds)
        {
            result[objectId] = Read<T>(objectId);
        }
        return result;
    }

    public long Update<T>(T entity) where T : class
    {
        ThrowIfDisposed();
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        // Validate entity before update
        var validationResult = Validate(entity);
        if (!validationResult.IsValid)
        {
            throw new EntityValidationException($"Entity validation failed: {string.Join(", ", validationResult.Errors)}");
        }

        using var storer = _storageConnection.CreateStorer();
        var objectId = storer.Ensure(entity); // Force re-storage
        storer.Commit();

        // Update cache
        _entityCache.AddOrUpdate(objectId, entity, (key, oldValue) => entity);
        _entityTypes.TryAdd(objectId, typeof(T));

        return objectId;
    }

    public long[] UpdateAll<T>(IEnumerable<T> entities) where T : class
    {
        ThrowIfDisposed();
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        var entityList = entities.ToList();
        
        // Validate all entities first
        var validationResults = ValidateAll(entityList);
        var invalidEntities = validationResults.Where(kvp => !kvp.Value.IsValid).ToList();
        if (invalidEntities.Any())
        {
            var errors = invalidEntities.SelectMany(kvp => kvp.Value.Errors);
            throw new EntityValidationException($"Entity validation failed: {string.Join(", ", errors)}");
        }

        using var storer = _storageConnection.CreateStorer();
        var objectIds = new long[entityList.Count];
        
        for (int i = 0; i < entityList.Count; i++)
        {
            var entity = entityList[i];
            objectIds[i] = storer.Ensure(entity);
            
            // Update cache
            _entityCache.AddOrUpdate(objectIds[i], entity, (key, oldValue) => entity);
            _entityTypes.TryAdd(objectIds[i], typeof(T));
        }
        
        storer.Commit();

        return objectIds;
    }

    public bool Delete<T>(T entity) where T : class
    {
        ThrowIfDisposed();
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        // Find the object ID for this entity
        var objectId = _entityCache.FirstOrDefault(kvp => ReferenceEquals(kvp.Value, entity)).Key;
        if (objectId == 0) return false;

        return Delete(objectId);
    }

    public bool Delete(long objectId)
    {
        ThrowIfDisposed();

        // Remove from cache and type registry
        var removed = _entityCache.TryRemove(objectId, out _);
        _entityTypes.TryRemove(objectId, out _);

        // Remove all references involving this entity
        _entityReferences.TryRemove(objectId, out _);
        foreach (var kvp in _entityReferences)
        {
            kvp.Value.RemoveWhere(r => r.TargetId == objectId);
        }

        // Note: In Eclipse Store pattern, entities are deleted by becoming unreachable
        // The actual cleanup happens during garbage collection
        return removed;
    }

    public int DeleteAll(IEnumerable<long> objectIds)
    {
        ThrowIfDisposed();
        if (objectIds == null) throw new ArgumentNullException(nameof(objectIds));

        int deletedCount = 0;
        foreach (var objectId in objectIds)
        {
            if (Delete(objectId))
                deletedCount++;
        }
        return deletedCount;
    }

    #endregion

    #region Entity Relationships

    public void AddReference(long sourceId, long targetId, string relationshipType)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(relationshipType)) throw new ArgumentException("Relationship type cannot be null or empty", nameof(relationshipType));

        var references = _entityReferences.GetOrAdd(sourceId, _ => new HashSet<EntityReference>());
        references.Add(new EntityReference(targetId, relationshipType));
    }

    public void RemoveReference(long sourceId, long targetId, string relationshipType)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(relationshipType)) throw new ArgumentException("Relationship type cannot be null or empty", nameof(relationshipType));

        if (_entityReferences.TryGetValue(sourceId, out var references))
        {
            references.RemoveWhere(r => r.TargetId == targetId && r.RelationshipType == relationshipType);
        }
    }

    public IEnumerable<long> GetReferences(long objectId, string? relationshipType = null)
    {
        ThrowIfDisposed();

        if (!_entityReferences.TryGetValue(objectId, out var references))
            return Enumerable.Empty<long>();

        var query = references.AsEnumerable();
        if (!string.IsNullOrEmpty(relationshipType))
        {
            query = query.Where(r => r.RelationshipType == relationshipType);
        }

        return query.Select(r => r.TargetId);
    }

    public IEnumerable<long> GetReferencedBy(long objectId, string? relationshipType = null)
    {
        ThrowIfDisposed();

        var referencingIds = new List<long>();
        foreach (var kvp in _entityReferences)
        {
            var hasReference = kvp.Value.Any(r => r.TargetId == objectId && 
                (string.IsNullOrEmpty(relationshipType) || r.RelationshipType == relationshipType));
            
            if (hasReference)
                referencingIds.Add(kvp.Key);
        }

        return referencingIds;
    }

    #endregion

    #region Entity Validation

    public EntityValidationResult Validate<T>(T entity) where T : class
    {
        ThrowIfDisposed();
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        var result = new EntityValidationResult { IsValid = true };

        // Basic validation - check for null required properties
        var properties = typeof(T).GetProperties();
        foreach (var property in properties)
        {
            var value = property.GetValue(entity);
            if (value == null && !IsNullable(property.PropertyType))
            {
                result.Errors.Add($"Property '{property.Name}' cannot be null");
                result.IsValid = false;
            }
        }

        // TODO: Add custom validation attributes support
        // TODO: Add business rule validation

        return result;
    }

    public Dictionary<T, EntityValidationResult> ValidateAll<T>(IEnumerable<T> entities) where T : class
    {
        ThrowIfDisposed();
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        var results = new Dictionary<T, EntityValidationResult>();
        foreach (var entity in entities)
        {
            results[entity] = Validate(entity);
        }
        return results;
    }

    public EntityIntegrityResult CheckIntegrity()
    {
        ThrowIfDisposed();

        var result = new EntityIntegrityResult { IsIntegrityMaintained = true };

        // Check for orphaned entities (entities not reachable from any root)
        // TODO: Implement root reachability analysis

        // Check for broken references
        foreach (var kvp in _entityReferences)
        {
            foreach (var reference in kvp.Value)
            {
                if (!_entityCache.ContainsKey(reference.TargetId))
                {
                    result.BrokenReferences.Add($"Entity {kvp.Key} references non-existent entity {reference.TargetId} via {reference.RelationshipType}");
                    result.IsIntegrityMaintained = false;
                }
            }
        }

        return result;
    }

    #endregion

    #region Entity Queries

    public IEnumerable<T> FindByType<T>() where T : class
    {
        ThrowIfDisposed();

        return _entityCache.Values.OfType<T>();
    }

    public IEnumerable<T> FindWhere<T>(Func<T, bool> predicate) where T : class
    {
        ThrowIfDisposed();
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));

        return FindByType<T>().Where(predicate);
    }

    public long CountByType<T>() where T : class
    {
        ThrowIfDisposed();

        return _entityCache.Values.OfType<T>().Count();
    }

    #endregion

    #region Transaction Support

    public IEntityTransaction BeginTransaction()
    {
        ThrowIfDisposed();

        lock (_transactionLock)
        {
            if (_currentTransaction != null && _currentTransaction.IsActive)
                throw new InvalidOperationException("A transaction is already active");

            _currentTransaction = new EntityTransaction();
            return _currentTransaction;
        }
    }

    #endregion

    #region Statistics

    public IEntityStatistics GetStatistics()
    {
        ThrowIfDisposed();

        var entityCountByType = new Dictionary<Type, long>();
        foreach (var kvp in _entityTypes)
        {
            var type = kvp.Value;
            entityCountByType[type] = entityCountByType.GetValueOrDefault(type, 0) + 1;
        }

        return new EntityStatistics
        {
            TotalEntityCount = _entityCache.Count,
            EntityCountByType = entityCountByType,
            ActiveReferenceCount = _entityReferences.Values.Sum(refs => refs.Count),
            OrphanedEntityCount = 0, // TODO: Implement orphan detection
            LastIntegrityCheck = null // TODO: Track last integrity check
        };
    }

    #endregion

    #region Helper Methods

    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || (Nullable.GetUnderlyingType(type) != null);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(EntityManager));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_isDisposed) return;

        _currentTransaction?.Dispose();
        _entityCache.Clear();
        _entityReferences.Clear();
        _entityTypes.Clear();

        _isDisposed = true;
    }

    #endregion
}

/// <summary>
/// Represents a reference between entities.
/// </summary>
internal record EntityReference(long TargetId, string RelationshipType);

/// <summary>
/// Exception thrown when entity validation fails.
/// </summary>
public class EntityValidationException : StorageException
{
    public EntityValidationException(string message) : base(message) { }
    public EntityValidationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Default implementation of entity transaction.
/// </summary>
internal class EntityTransaction : IEntityTransaction
{
    private bool _isDisposed;

    public EntityTransaction()
    {
        TransactionId = Guid.NewGuid().ToString();
        StartTime = DateTime.UtcNow;
        IsActive = true;
    }

    public string TransactionId { get; }
    public DateTime StartTime { get; }
    public bool IsActive { get; private set; }

    public void Commit()
    {
        ThrowIfDisposed();
        if (!IsActive)
            throw new InvalidOperationException("Transaction is not active");

        // TODO: Implement actual commit logic
        IsActive = false;
    }

    public void Rollback()
    {
        ThrowIfDisposed();
        if (!IsActive)
            throw new InvalidOperationException("Transaction is not active");

        // TODO: Implement actual rollback logic
        IsActive = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        if (IsActive)
        {
            try
            {
                Rollback();
            }
            catch
            {
                // Ignore rollback errors during dispose
            }
        }

        _isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(EntityTransaction));
    }
}

/// <summary>
/// Default implementation of entity statistics.
/// </summary>
internal class EntityStatistics : IEntityStatistics
{
    public long TotalEntityCount { get; init; }
    public Dictionary<Type, long> EntityCountByType { get; init; } = new();
    public long ActiveReferenceCount { get; init; }
    public long OrphanedEntityCount { get; init; }
    public DateTime? LastIntegrityCheck { get; init; }
}
