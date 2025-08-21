using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NebulaStore.Storage;

/// <summary>
/// Enhanced interface for storage type dictionary with advanced features.
/// Extends the basic type dictionary with persistence, type evolution, and metadata management.
/// </summary>
public interface IEnhancedStorageTypeDictionary : IStorageTypeDictionary
{
    /// <summary>
    /// Gets the type definition for the specified type ID.
    /// </summary>
    /// <param name="typeId">The type ID to get the definition for</param>
    /// <returns>The type definition, or null if not found</returns>
    IStorageTypeDefinition? GetTypeDefinition(long typeId);

    /// <summary>
    /// Gets the type definition for the specified type.
    /// </summary>
    /// <param name="type">The type to get the definition for</param>
    /// <returns>The type definition, or null if not found</returns>
    IStorageTypeDefinition? GetTypeDefinition(Type type);

    /// <summary>
    /// Registers a type definition with the dictionary.
    /// </summary>
    /// <param name="typeDefinition">The type definition to register</param>
    /// <returns>True if the type definition was registered, false if it already existed</returns>
    bool RegisterTypeDefinition(IStorageTypeDefinition typeDefinition);

    /// <summary>
    /// Gets all registered type definitions.
    /// </summary>
    /// <returns>A read-only collection of all type definitions</returns>
    IReadOnlyDictionary<long, IStorageTypeDefinition> GetAllTypeDefinitions();

    /// <summary>
    /// Gets the highest type ID currently in use.
    /// </summary>
    /// <returns>The highest type ID</returns>
    long GetHighestTypeId();

    /// <summary>
    /// Validates that a type definition is compatible with an existing one.
    /// </summary>
    /// <param name="typeDefinition">The type definition to validate</param>
    /// <returns>True if compatible, false otherwise</returns>
    bool ValidateTypeDefinition(IStorageTypeDefinition typeDefinition);

    /// <summary>
    /// Saves the type dictionary to persistent storage.
    /// </summary>
    /// <param name="filePath">The file path to save to</param>
    /// <returns>A task representing the asynchronous save operation</returns>
    Task SaveAsync(string filePath);

    /// <summary>
    /// Loads the type dictionary from persistent storage.
    /// </summary>
    /// <param name="filePath">The file path to load from</param>
    /// <returns>A task representing the asynchronous load operation</returns>
    Task LoadAsync(string filePath);

    /// <summary>
    /// Gets type lineage information for type evolution tracking.
    /// </summary>
    /// <param name="type">The type to get lineage for</param>
    /// <returns>The type lineage, or null if not found</returns>
    IStorageTypeLineage? GetTypeLineage(Type type);

    /// <summary>
    /// Gets type lineage information by type name.
    /// </summary>
    /// <param name="typeName">The type name to get lineage for</param>
    /// <returns>The type lineage, or null if not found</returns>
    IStorageTypeLineage? GetTypeLineage(string typeName);

    /// <summary>
    /// Ensures a type lineage exists for the specified type.
    /// </summary>
    /// <param name="type">The type to ensure lineage for</param>
    /// <returns>The type lineage</returns>
    IStorageTypeLineage EnsureTypeLineage(Type type);

    /// <summary>
    /// Gets the entity type handler for the specified type ID.
    /// </summary>
    /// <param name="typeId">The type ID to get the handler for</param>
    /// <returns>The entity type handler, or null if not found</returns>
    IStorageEntityTypeHandler? GetEntityTypeHandler(long typeId);

    /// <summary>
    /// Validates an entity against its type definition.
    /// </summary>
    /// <param name="length">The entity length</param>
    /// <param name="typeId">The entity type ID</param>
    /// <param name="objectId">The entity object ID</param>
    /// <returns>The entity type handler for the validated entity</returns>
    IStorageEntityTypeHandler ValidateEntity(long length, long typeId, long objectId);
}

/// <summary>
/// Interface for storage type definitions.
/// Represents metadata about a type including its structure and persistence information.
/// </summary>
public interface IStorageTypeDefinition
{
    /// <summary>
    /// Gets the unique type ID.
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
    /// Gets the type version for evolution tracking.
    /// </summary>
    long TypeVersion { get; }

    /// <summary>
    /// Gets a value indicating whether this type is a primitive type.
    /// </summary>
    bool IsPrimitiveType { get; }

    /// <summary>
    /// Gets a value indicating whether this type has persisted references to other objects.
    /// </summary>
    bool HasPersistedReferences { get; }

    /// <summary>
    /// Gets a value indicating whether this type has variable length when persisted.
    /// </summary>
    bool HasPersistedVariableLength { get; }

    /// <summary>
    /// Gets the minimum persisted length for instances of this type.
    /// </summary>
    long MinimumPersistedLength { get; }

    /// <summary>
    /// Gets the maximum persisted length for instances of this type.
    /// </summary>
    long MaximumPersistedLength { get; }

    /// <summary>
    /// Gets the type members that are persisted.
    /// </summary>
    IReadOnlyList<IStorageTypeDefinitionMember> PersistedMembers { get; }

    /// <summary>
    /// Gets the creation timestamp of this type definition.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// Gets the last modification timestamp of this type definition.
    /// </summary>
    DateTime ModifiedAt { get; }
}

/// <summary>
/// Interface for storage type definition members.
/// Represents metadata about a specific field or property of a type.
/// </summary>
public interface IStorageTypeDefinitionMember
{
    /// <summary>
    /// Gets the member name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the member type.
    /// </summary>
    Type MemberType { get; }

    /// <summary>
    /// Gets the member type ID if it's a reference type.
    /// </summary>
    long? MemberTypeId { get; }

    /// <summary>
    /// Gets a value indicating whether this member is a reference to another object.
    /// </summary>
    bool IsReference { get; }

    /// <summary>
    /// Gets a value indicating whether this member has variable length.
    /// </summary>
    bool IsVariableLength { get; }

    /// <summary>
    /// Gets the offset of this member in the persisted data.
    /// </summary>
    long Offset { get; }

    /// <summary>
    /// Gets the length of this member in the persisted data.
    /// </summary>
    long Length { get; }
}

/// <summary>
/// Interface for storage type lineage.
/// Tracks the evolution of a type over time.
/// </summary>
public interface IStorageTypeLineage
{
    /// <summary>
    /// Gets the type name.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Gets the current type.
    /// </summary>
    Type CurrentType { get; }

    /// <summary>
    /// Gets all versions of this type.
    /// </summary>
    IReadOnlyList<IStorageTypeDefinition> TypeVersions { get; }

    /// <summary>
    /// Gets the latest type definition.
    /// </summary>
    IStorageTypeDefinition LatestDefinition { get; }

    /// <summary>
    /// Gets a type definition by version.
    /// </summary>
    /// <param name="version">The version to get</param>
    /// <returns>The type definition for the specified version, or null if not found</returns>
    IStorageTypeDefinition? GetDefinitionByVersion(long version);

    /// <summary>
    /// Adds a new version of the type.
    /// </summary>
    /// <param name="typeDefinition">The new type definition</param>
    void AddVersion(IStorageTypeDefinition typeDefinition);
}

/// <summary>
/// Interface for storage entity type handlers.
/// Handles type-specific operations for entities.
/// </summary>
public interface IStorageEntityTypeHandler
{
    /// <summary>
    /// Gets the type definition this handler is for.
    /// </summary>
    IStorageTypeDefinition TypeDefinition { get; }

    /// <summary>
    /// Gets the number of simple references in this type.
    /// </summary>
    long SimpleReferenceCount { get; }

    /// <summary>
    /// Gets the minimum entity length for this type.
    /// </summary>
    long MinimumLength { get; }

    /// <summary>
    /// Gets the maximum entity length for this type.
    /// </summary>
    long MaximumLength { get; }

    /// <summary>
    /// Validates an entity of this type.
    /// </summary>
    /// <param name="length">The entity length</param>
    /// <param name="objectId">The entity object ID</param>
    /// <returns>True if the entity is valid</returns>
    bool IsValidEntity(long length, long objectId);

    /// <summary>
    /// Validates an entity of this type and throws an exception if invalid.
    /// </summary>
    /// <param name="length">The entity length</param>
    /// <param name="objectId">The entity object ID</param>
    void ValidateEntity(long length, long objectId);

    /// <summary>
    /// Iterates over all object references in an entity.
    /// </summary>
    /// <param name="entityData">The entity data</param>
    /// <param name="referenceHandler">The handler to call for each reference</param>
    void IterateReferences(ReadOnlySpan<byte> entityData, Action<long> referenceHandler);
}
