using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage;

/// <summary>
/// Enhanced implementation of storage type dictionary with advanced features.
/// Provides type evolution, persistence, and comprehensive metadata management.
/// </summary>
public class EnhancedStorageTypeDictionary : IEnhancedStorageTypeDictionary
{
    private readonly ConcurrentDictionary<Type, long> _typeToId = new();
    private readonly ConcurrentDictionary<long, Type> _idToType = new();
    private readonly ConcurrentDictionary<long, IStorageTypeDefinition> _typeDefinitions = new();
    private readonly ConcurrentDictionary<string, IStorageTypeLineage> _typeLineages = new();
    private readonly ConcurrentDictionary<long, IStorageEntityTypeHandler> _entityTypeHandlers = new();
    private readonly object _lock = new();
    private long _nextTypeId = 1;

    public int TypeCount => _typeToId.Count;

    #region Basic Type Dictionary Implementation

    public long RegisterType(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _typeToId.GetOrAdd(type, t =>
        {
            var typeId = Interlocked.Increment(ref _nextTypeId);
            _idToType.TryAdd(typeId, t);

            // Create a basic type definition for the newly registered type
            var typeDefinition = CreateTypeDefinition(typeId, t);
            _typeDefinitions.TryAdd(typeId, typeDefinition);

            // Ensure type lineage exists
            EnsureTypeLineage(t);

            // Create entity type handler
            var entityHandler = new StorageEntityTypeHandler(typeDefinition);
            _entityTypeHandlers.TryAdd(typeId, entityHandler);

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

    #endregion

    #region Enhanced Type Dictionary Implementation

    public IStorageTypeDefinition? GetTypeDefinition(long typeId)
    {
        return _typeDefinitions.TryGetValue(typeId, out var definition) ? definition : null;
    }

    public IStorageTypeDefinition? GetTypeDefinition(Type type)
    {
        var typeId = GetTypeId(type);
        return typeId != -1 ? GetTypeDefinition(typeId) : null;
    }

    public bool RegisterTypeDefinition(IStorageTypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
            throw new ArgumentNullException(nameof(typeDefinition));

        lock (_lock)
        {
            // Check if type definition already exists
            if (_typeDefinitions.ContainsKey(typeDefinition.TypeId))
            {
                return false;
            }

            // Register the type if not already registered
            if (!_typeToId.ContainsKey(typeDefinition.Type))
            {
                _typeToId.TryAdd(typeDefinition.Type, typeDefinition.TypeId);
                _idToType.TryAdd(typeDefinition.TypeId, typeDefinition.Type);
            }

            // Add type definition
            _typeDefinitions.TryAdd(typeDefinition.TypeId, typeDefinition);

            // Update type lineage
            var lineage = EnsureTypeLineage(typeDefinition.Type);
            lineage.AddVersion(typeDefinition);

            // Create entity type handler
            var entityHandler = new StorageEntityTypeHandler(typeDefinition);
            _entityTypeHandlers.TryAdd(typeDefinition.TypeId, entityHandler);

            // Update next type ID if necessary
            if (typeDefinition.TypeId >= _nextTypeId)
            {
                _nextTypeId = typeDefinition.TypeId + 1;
            }

            return true;
        }
    }

    public IReadOnlyDictionary<long, IStorageTypeDefinition> GetAllTypeDefinitions()
    {
        return _typeDefinitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public long GetHighestTypeId()
    {
        return _typeDefinitions.Keys.DefaultIfEmpty(0).Max();
    }

    public bool ValidateTypeDefinition(IStorageTypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
            return false;

        var existingDefinition = GetTypeDefinition(typeDefinition.TypeId);
        if (existingDefinition == null)
            return true; // New type definition is always valid

        // Check structural compatibility
        return AreTypeDefinitionsCompatible(existingDefinition, typeDefinition);
    }

    #endregion

    #region Type Lineage Management

    public IStorageTypeLineage? GetTypeLineage(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return _typeLineages.TryGetValue(type.FullName ?? type.Name, out var lineage) ? lineage : null;
    }

    public IStorageTypeLineage? GetTypeLineage(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            throw new ArgumentException("Type name cannot be null or empty", nameof(typeName));

        return _typeLineages.TryGetValue(typeName, out var lineage) ? lineage : null;
    }

    public IStorageTypeLineage EnsureTypeLineage(Type type)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        var typeName = type.FullName ?? type.Name;
        return _typeLineages.GetOrAdd(typeName, _ => new StorageTypeLineage(typeName, type));
    }

    #endregion

    #region Entity Type Handler Management

    public IStorageEntityTypeHandler? GetEntityTypeHandler(long typeId)
    {
        return _entityTypeHandlers.TryGetValue(typeId, out var handler) ? handler : null;
    }

    public IStorageEntityTypeHandler ValidateEntity(long length, long typeId, long objectId)
    {
        var handler = GetEntityTypeHandler(typeId);
        if (handler == null)
        {
            throw new StorageException($"Unknown type ID {typeId} for entity with object ID {objectId} and length {length}");
        }

        handler.ValidateEntity(length, objectId);
        return handler;
    }

    #endregion

    #region Persistence

    public async Task SaveAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var data = new TypeDictionaryData
        {
            NextTypeId = _nextTypeId,
            TypeDefinitions = _typeDefinitions.Values.Select(td => new SerializableTypeDefinition(td)).ToList(),
            TypeLineages = _typeLineages.Values.Select(tl => new SerializableTypeLineage(tl)).ToList()
        };

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task LoadAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            return; // No existing data to load

        var json = await File.ReadAllTextAsync(filePath);
        var data = JsonSerializer.Deserialize<TypeDictionaryData>(json);

        if (data == null)
            return;

        lock (_lock)
        {
            _nextTypeId = data.NextTypeId;

            // Load type definitions
            foreach (var serializableDefinition in data.TypeDefinitions)
            {
                var typeDefinition = serializableDefinition.ToTypeDefinition();
                if (typeDefinition != null)
                {
                    RegisterTypeDefinition(typeDefinition);
                }
            }

            // Load type lineages
            foreach (var serializableLineage in data.TypeLineages)
            {
                var lineage = serializableLineage.ToTypeLineage();
                if (lineage != null)
                {
                    _typeLineages.TryAdd(lineage.TypeName, lineage);
                }
            }
        }
    }

    #endregion

    #region Helper Methods

    private IStorageTypeDefinition CreateTypeDefinition(long typeId, Type type)
    {
        return new StorageTypeDefinition(
            typeId: typeId,
            typeName: type.FullName ?? type.Name,
            type: type,
            typeVersion: 1,
            isPrimitiveType: type.IsPrimitive,
            hasPersistedReferences: !type.IsPrimitive && !type.IsValueType,
            hasPersistedVariableLength: IsVariableLength(type),
            minimumPersistedLength: CalculateMinimumLength(type),
            maximumPersistedLength: CalculateMaximumLength(type),
            persistedMembers: GetPersistedMembers(type),
            createdAt: DateTime.UtcNow,
            modifiedAt: DateTime.UtcNow
        );
    }

    private static bool IsVariableLength(Type type)
    {
        // Simple heuristic: strings and arrays have variable length
        return type == typeof(string) || type.IsArray || 
               (type.IsGenericType && (
                   type.GetGenericTypeDefinition() == typeof(List<>) ||
                   type.GetGenericTypeDefinition() == typeof(IList<>) ||
                   type.GetGenericTypeDefinition() == typeof(ICollection<>)
               ));
    }

    private static long CalculateMinimumLength(Type type)
    {
        if (type.IsPrimitive)
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(type);
        }
        
        // For reference types, minimum is just the object header
        return 8; // Basic object header size
    }

    private static long CalculateMaximumLength(Type type)
    {
        if (type.IsPrimitive)
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(type);
        }
        
        // For variable length types, use a large maximum
        if (IsVariableLength(type))
        {
            return long.MaxValue;
        }
        
        // For fixed-size reference types, calculate based on fields
        return CalculateMinimumLength(type); // Simplified for now
    }

    private static List<IStorageTypeDefinitionMember> GetPersistedMembers(Type type)
    {
        var members = new List<IStorageTypeDefinitionMember>();
        var fields = type.GetFields(System.Reflection.BindingFlags.Instance | 
                                   System.Reflection.BindingFlags.Public | 
                                   System.Reflection.BindingFlags.NonPublic);

        long offset = 0;
        foreach (var field in fields)
        {
            var member = new StorageTypeDefinitionMember(
                name: field.Name,
                memberType: field.FieldType,
                memberTypeId: null, // Will be resolved later
                isReference: !field.FieldType.IsValueType,
                isVariableLength: IsVariableLength(field.FieldType),
                offset: offset,
                length: CalculateMinimumLength(field.FieldType)
            );
            
            members.Add(member);
            offset += member.Length;
        }

        return members;
    }

    private static bool AreTypeDefinitionsCompatible(IStorageTypeDefinition existing, IStorageTypeDefinition candidate)
    {
        // Basic compatibility check - same type name and compatible structure
        if (existing.TypeName != candidate.TypeName)
            return false;

        // Version should be greater or equal
        if (candidate.TypeVersion < existing.TypeVersion)
            return false;

        // For now, assume compatible if basic properties match
        return existing.IsPrimitiveType == candidate.IsPrimitiveType;
    }

    #endregion
}

/// <summary>
/// Data structure for serializing type dictionary to persistent storage.
/// </summary>
internal class TypeDictionaryData
{
    public long NextTypeId { get; set; }
    public List<SerializableTypeDefinition> TypeDefinitions { get; set; } = new();
    public List<SerializableTypeLineage> TypeLineages { get; set; } = new();
}

/// <summary>
/// Serializable representation of a type definition.
/// </summary>
internal class SerializableTypeDefinition
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string TypeAssemblyQualifiedName { get; set; } = string.Empty;
    public long TypeVersion { get; set; }
    public bool IsPrimitiveType { get; set; }
    public bool HasPersistedReferences { get; set; }
    public bool HasPersistedVariableLength { get; set; }
    public long MinimumPersistedLength { get; set; }
    public long MaximumPersistedLength { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public List<SerializableTypeDefinitionMember> PersistedMembers { get; set; } = new();

    public SerializableTypeDefinition() { }

    public SerializableTypeDefinition(IStorageTypeDefinition definition)
    {
        TypeId = definition.TypeId;
        TypeName = definition.TypeName;
        TypeAssemblyQualifiedName = definition.Type.AssemblyQualifiedName ?? definition.Type.FullName ?? definition.Type.Name;
        TypeVersion = definition.TypeVersion;
        IsPrimitiveType = definition.IsPrimitiveType;
        HasPersistedReferences = definition.HasPersistedReferences;
        HasPersistedVariableLength = definition.HasPersistedVariableLength;
        MinimumPersistedLength = definition.MinimumPersistedLength;
        MaximumPersistedLength = definition.MaximumPersistedLength;
        CreatedAt = definition.CreatedAt;
        ModifiedAt = definition.ModifiedAt;
        PersistedMembers = definition.PersistedMembers.Select(m => new SerializableTypeDefinitionMember(m)).ToList();
    }

    public IStorageTypeDefinition? ToTypeDefinition()
    {
        var type = Type.GetType(TypeAssemblyQualifiedName);
        if (type == null)
            return null;

        var members = PersistedMembers.Select(m => m.ToTypeDefinitionMember()).Where(m => m != null).Cast<IStorageTypeDefinitionMember>().ToList();

        return new StorageTypeDefinition(
            TypeId, TypeName, type, TypeVersion, IsPrimitiveType, HasPersistedReferences,
            HasPersistedVariableLength, MinimumPersistedLength, MaximumPersistedLength,
            members, CreatedAt, ModifiedAt);
    }
}

/// <summary>
/// Serializable representation of a type definition member.
/// </summary>
internal class SerializableTypeDefinitionMember
{
    public string Name { get; set; } = string.Empty;
    public string MemberTypeAssemblyQualifiedName { get; set; } = string.Empty;
    public long? MemberTypeId { get; set; }
    public bool IsReference { get; set; }
    public bool IsVariableLength { get; set; }
    public long Offset { get; set; }
    public long Length { get; set; }

    public SerializableTypeDefinitionMember() { }

    public SerializableTypeDefinitionMember(IStorageTypeDefinitionMember member)
    {
        Name = member.Name;
        MemberTypeAssemblyQualifiedName = member.MemberType.AssemblyQualifiedName ?? member.MemberType.FullName ?? member.MemberType.Name;
        MemberTypeId = member.MemberTypeId;
        IsReference = member.IsReference;
        IsVariableLength = member.IsVariableLength;
        Offset = member.Offset;
        Length = member.Length;
    }

    public IStorageTypeDefinitionMember? ToTypeDefinitionMember()
    {
        var memberType = Type.GetType(MemberTypeAssemblyQualifiedName);
        if (memberType == null)
            return null;

        return new StorageTypeDefinitionMember(Name, memberType, MemberTypeId, IsReference, IsVariableLength, Offset, Length);
    }
}

/// <summary>
/// Serializable representation of a type lineage.
/// </summary>
internal class SerializableTypeLineage
{
    public string TypeName { get; set; } = string.Empty;
    public string CurrentTypeAssemblyQualifiedName { get; set; } = string.Empty;
    public List<long> TypeVersionIds { get; set; } = new();

    public SerializableTypeLineage() { }

    public SerializableTypeLineage(IStorageTypeLineage lineage)
    {
        TypeName = lineage.TypeName;
        CurrentTypeAssemblyQualifiedName = lineage.CurrentType.AssemblyQualifiedName ?? lineage.CurrentType.FullName ?? lineage.CurrentType.Name;
        TypeVersionIds = lineage.TypeVersions.Select(tv => tv.TypeId).ToList();
    }

    public IStorageTypeLineage? ToTypeLineage()
    {
        var currentType = Type.GetType(CurrentTypeAssemblyQualifiedName);
        if (currentType == null)
            return null;

        return new StorageTypeLineage(TypeName, currentType);
    }
}
