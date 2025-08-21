using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded;

/// <summary>
/// Interface for advanced entity management operations.
/// Provides comprehensive CRUD operations, relationship handling, and validation.
/// </summary>
public interface IEntityManager : IDisposable
{
    #region Entity Lifecycle Operations

    /// <summary>
    /// Creates a new entity and assigns it a unique object ID.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entity">The entity to create</param>
    /// <returns>The assigned object ID</returns>
    long Create<T>(T entity) where T : class;

    /// <summary>
    /// Creates multiple entities in a batch operation.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entities">The entities to create</param>
    /// <returns>Array of assigned object IDs</returns>
    long[] CreateAll<T>(IEnumerable<T> entities) where T : class;

    /// <summary>
    /// Retrieves an entity by its object ID.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="objectId">The object ID</param>
    /// <returns>The entity if found, null otherwise</returns>
    T? Read<T>(long objectId) where T : class;

    /// <summary>
    /// Retrieves multiple entities by their object IDs.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="objectIds">The object IDs</param>
    /// <returns>Dictionary mapping object IDs to entities (null for not found)</returns>
    Dictionary<long, T?> ReadAll<T>(IEnumerable<long> objectIds) where T : class;

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entity">The entity to update</param>
    /// <returns>The object ID of the updated entity</returns>
    long Update<T>(T entity) where T : class;

    /// <summary>
    /// Updates multiple entities in a batch operation.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entities">The entities to update</param>
    /// <returns>Array of object IDs</returns>
    long[] UpdateAll<T>(IEnumerable<T> entities) where T : class;

    /// <summary>
    /// Deletes an entity by marking it as unreachable.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entity">The entity to delete</param>
    /// <returns>True if the entity was deleted, false if not found</returns>
    bool Delete<T>(T entity) where T : class;

    /// <summary>
    /// Deletes an entity by its object ID.
    /// </summary>
    /// <param name="objectId">The object ID</param>
    /// <returns>True if the entity was deleted, false if not found</returns>
    bool Delete(long objectId);

    /// <summary>
    /// Deletes multiple entities in a batch operation.
    /// </summary>
    /// <param name="objectIds">The object IDs to delete</param>
    /// <returns>Number of entities successfully deleted</returns>
    int DeleteAll(IEnumerable<long> objectIds);

    #endregion

    #region Entity Relationships

    /// <summary>
    /// Establishes a reference relationship between two entities.
    /// </summary>
    /// <param name="sourceId">The source entity object ID</param>
    /// <param name="targetId">The target entity object ID</param>
    /// <param name="relationshipType">The type of relationship</param>
    void AddReference(long sourceId, long targetId, string relationshipType);

    /// <summary>
    /// Removes a reference relationship between two entities.
    /// </summary>
    /// <param name="sourceId">The source entity object ID</param>
    /// <param name="targetId">The target entity object ID</param>
    /// <param name="relationshipType">The type of relationship</param>
    void RemoveReference(long sourceId, long targetId, string relationshipType);

    /// <summary>
    /// Gets all entities referenced by the specified entity.
    /// </summary>
    /// <param name="objectId">The source entity object ID</param>
    /// <param name="relationshipType">The type of relationship (optional)</param>
    /// <returns>Collection of referenced entity object IDs</returns>
    IEnumerable<long> GetReferences(long objectId, string? relationshipType = null);

    /// <summary>
    /// Gets all entities that reference the specified entity.
    /// </summary>
    /// <param name="objectId">The target entity object ID</param>
    /// <param name="relationshipType">The type of relationship (optional)</param>
    /// <returns>Collection of referencing entity object IDs</returns>
    IEnumerable<long> GetReferencedBy(long objectId, string? relationshipType = null);

    #endregion

    #region Entity Validation

    /// <summary>
    /// Validates an entity before storage operations.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entity">The entity to validate</param>
    /// <returns>Validation result</returns>
    EntityValidationResult Validate<T>(T entity) where T : class;

    /// <summary>
    /// Validates multiple entities in a batch operation.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="entities">The entities to validate</param>
    /// <returns>Dictionary mapping entities to validation results</returns>
    Dictionary<T, EntityValidationResult> ValidateAll<T>(IEnumerable<T> entities) where T : class;

    /// <summary>
    /// Performs integrity checking on the entity graph.
    /// </summary>
    /// <returns>Integrity check result</returns>
    EntityIntegrityResult CheckIntegrity();

    #endregion

    #region Entity Queries

    /// <summary>
    /// Finds entities by type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>Collection of entities of the specified type</returns>
    IEnumerable<T> FindByType<T>() where T : class;

    /// <summary>
    /// Finds entities by a predicate function.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <param name="predicate">The predicate function</param>
    /// <returns>Collection of matching entities</returns>
    IEnumerable<T> FindWhere<T>(Func<T, bool> predicate) where T : class;

    /// <summary>
    /// Counts entities by type.
    /// </summary>
    /// <typeparam name="T">The entity type</typeparam>
    /// <returns>Number of entities of the specified type</returns>
    long CountByType<T>() where T : class;

    #endregion

    #region Transaction Support

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    /// <returns>Transaction context</returns>
    IEntityTransaction BeginTransaction();

    /// <summary>
    /// Gets the current transaction context, if any.
    /// </summary>
    IEntityTransaction? CurrentTransaction { get; }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets entity management statistics.
    /// </summary>
    /// <returns>Entity statistics</returns>
    IEntityStatistics GetStatistics();

    #endregion
}

/// <summary>
/// Result of entity validation operations.
/// </summary>
public class EntityValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of entity integrity checking.
/// </summary>
public class EntityIntegrityResult
{
    public bool IsIntegrityMaintained { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<long> OrphanedEntities { get; set; } = new();
    public List<string> BrokenReferences { get; set; } = new();
}

/// <summary>
/// Interface for entity transaction operations.
/// </summary>
public interface IEntityTransaction : IDisposable
{
    /// <summary>
    /// Gets the transaction ID.
    /// </summary>
    string TransactionId { get; }

    /// <summary>
    /// Gets the transaction start time.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets whether the transaction is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    void Commit();

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    void Rollback();
}

/// <summary>
/// Interface for entity management statistics.
/// </summary>
public interface IEntityStatistics
{
    /// <summary>
    /// Gets the total number of entities.
    /// </summary>
    long TotalEntityCount { get; }

    /// <summary>
    /// Gets the number of entities by type.
    /// </summary>
    Dictionary<Type, long> EntityCountByType { get; }

    /// <summary>
    /// Gets the number of active references.
    /// </summary>
    long ActiveReferenceCount { get; }

    /// <summary>
    /// Gets the number of orphaned entities.
    /// </summary>
    long OrphanedEntityCount { get; }

    /// <summary>
    /// Gets the last integrity check time.
    /// </summary>
    DateTime? LastIntegrityCheck { get; }
}
