using System;
using System.Collections.Generic;

namespace NebulaStore.Storage;

/// <summary>
/// Default implementation of storage type definition.
/// Represents metadata about a type including its structure and persistence information.
/// </summary>
public class StorageTypeDefinition : IStorageTypeDefinition
{
    public long TypeId { get; }
    public string TypeName { get; }
    public Type Type { get; }
    public long TypeVersion { get; }
    public bool IsPrimitiveType { get; }
    public bool HasPersistedReferences { get; }
    public bool HasPersistedVariableLength { get; }
    public long MinimumPersistedLength { get; }
    public long MaximumPersistedLength { get; }
    public IReadOnlyList<IStorageTypeDefinitionMember> PersistedMembers { get; }
    public DateTime CreatedAt { get; }
    public DateTime ModifiedAt { get; }

    public StorageTypeDefinition(
        long typeId,
        string typeName,
        Type type,
        long typeVersion,
        bool isPrimitiveType,
        bool hasPersistedReferences,
        bool hasPersistedVariableLength,
        long minimumPersistedLength,
        long maximumPersistedLength,
        IReadOnlyList<IStorageTypeDefinitionMember> persistedMembers,
        DateTime createdAt,
        DateTime modifiedAt)
    {
        TypeId = typeId;
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        TypeVersion = typeVersion;
        IsPrimitiveType = isPrimitiveType;
        HasPersistedReferences = hasPersistedReferences;
        HasPersistedVariableLength = hasPersistedVariableLength;
        MinimumPersistedLength = minimumPersistedLength;
        MaximumPersistedLength = maximumPersistedLength;
        PersistedMembers = persistedMembers ?? throw new ArgumentNullException(nameof(persistedMembers));
        CreatedAt = createdAt;
        ModifiedAt = modifiedAt;
    }

    public override string ToString()
    {
        return $"TypeDefinition[{TypeId}]: {TypeName} v{TypeVersion}";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageTypeDefinition other && TypeId == other.TypeId;
    }

    public override int GetHashCode()
    {
        return TypeId.GetHashCode();
    }
}

/// <summary>
/// Default implementation of storage type definition member.
/// Represents metadata about a specific field or property of a type.
/// </summary>
public class StorageTypeDefinitionMember : IStorageTypeDefinitionMember
{
    public string Name { get; }
    public Type MemberType { get; }
    public long? MemberTypeId { get; }
    public bool IsReference { get; }
    public bool IsVariableLength { get; }
    public long Offset { get; }
    public long Length { get; }

    public StorageTypeDefinitionMember(
        string name,
        Type memberType,
        long? memberTypeId,
        bool isReference,
        bool isVariableLength,
        long offset,
        long length)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        MemberType = memberType ?? throw new ArgumentNullException(nameof(memberType));
        MemberTypeId = memberTypeId;
        IsReference = isReference;
        IsVariableLength = isVariableLength;
        Offset = offset;
        Length = length;
    }

    public override string ToString()
    {
        return $"Member[{Name}]: {MemberType.Name} @ {Offset}+{Length}";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageTypeDefinitionMember other && 
               Name == other.Name && 
               MemberType == other.MemberType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, MemberType);
    }
}

/// <summary>
/// Default implementation of storage type lineage.
/// Tracks the evolution of a type over time.
/// </summary>
public class StorageTypeLineage : IStorageTypeLineage
{
    private readonly List<IStorageTypeDefinition> _typeVersions = new();
    private readonly object _lock = new();

    public string TypeName { get; }
    public Type CurrentType { get; }

    public IReadOnlyList<IStorageTypeDefinition> TypeVersions
    {
        get
        {
            lock (_lock)
            {
                return _typeVersions.ToList();
            }
        }
    }

    public IStorageTypeDefinition LatestDefinition
    {
        get
        {
            lock (_lock)
            {
                return _typeVersions.Count > 0 
                    ? _typeVersions.OrderByDescending(tv => tv.TypeVersion).First()
                    : throw new InvalidOperationException("No type versions available");
            }
        }
    }

    public StorageTypeLineage(string typeName, Type currentType)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        CurrentType = currentType ?? throw new ArgumentNullException(nameof(currentType));
    }

    public IStorageTypeDefinition? GetDefinitionByVersion(long version)
    {
        lock (_lock)
        {
            return _typeVersions.FirstOrDefault(tv => tv.TypeVersion == version);
        }
    }

    public void AddVersion(IStorageTypeDefinition typeDefinition)
    {
        if (typeDefinition == null)
            throw new ArgumentNullException(nameof(typeDefinition));

        if (typeDefinition.TypeName != TypeName)
            throw new ArgumentException($"Type definition name '{typeDefinition.TypeName}' does not match lineage name '{TypeName}'");

        lock (_lock)
        {
            // Check if version already exists
            if (_typeVersions.Any(tv => tv.TypeVersion == typeDefinition.TypeVersion))
                return;

            _typeVersions.Add(typeDefinition);
            _typeVersions.Sort((a, b) => a.TypeVersion.CompareTo(b.TypeVersion));
        }
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return $"TypeLineage[{TypeName}]: {_typeVersions.Count} versions";
        }
    }
}

/// <summary>
/// Default implementation of storage entity type handler.
/// Handles type-specific operations for entities.
/// </summary>
public class StorageEntityTypeHandler : IStorageEntityTypeHandler
{
    public IStorageTypeDefinition TypeDefinition { get; }
    public long SimpleReferenceCount { get; }
    public long MinimumLength { get; }
    public long MaximumLength { get; }

    public StorageEntityTypeHandler(IStorageTypeDefinition typeDefinition)
    {
        TypeDefinition = typeDefinition ?? throw new ArgumentNullException(nameof(typeDefinition));
        SimpleReferenceCount = CalculateSimpleReferenceCount(typeDefinition);
        MinimumLength = typeDefinition.MinimumPersistedLength;
        MaximumLength = typeDefinition.MaximumPersistedLength;
    }

    public bool IsValidEntity(long length, long objectId)
    {
        if (length < MinimumLength)
            return false;

        if (MaximumLength != long.MaxValue && length > MaximumLength)
            return false;

        return true;
    }

    public void ValidateEntity(long length, long objectId)
    {
        if (length < MinimumLength)
        {
            throw new StorageException(
                $"Invalid entity length for object ID {objectId} of type {TypeDefinition.TypeName}: " +
                $"{length} < {MinimumLength}");
        }

        if (MaximumLength != long.MaxValue && length > MaximumLength)
        {
            throw new StorageException(
                $"Invalid entity length for object ID {objectId} of type {TypeDefinition.TypeName}: " +
                $"{length} > {MaximumLength}");
        }
    }

    public void IterateReferences(ReadOnlySpan<byte> entityData, Action<long> referenceHandler)
    {
        if (referenceHandler == null)
            throw new ArgumentNullException(nameof(referenceHandler));

        if (!TypeDefinition.HasPersistedReferences)
            return;

        // Iterate through reference members
        foreach (var member in TypeDefinition.PersistedMembers)
        {
            if (!member.IsReference)
                continue;

            if (member.Offset + 8 > entityData.Length) // 8 bytes for object ID
                continue;

            // Read object ID from the entity data
            var objectIdBytes = entityData.Slice((int)member.Offset, 8);
            var objectId = BitConverter.ToInt64(objectIdBytes);

            if (objectId != 0) // 0 means null reference
            {
                referenceHandler(objectId);
            }
        }
    }

    private static long CalculateSimpleReferenceCount(IStorageTypeDefinition typeDefinition)
    {
        return typeDefinition.PersistedMembers.Count(m => m.IsReference && !m.IsVariableLength);
    }

    public override string ToString()
    {
        return $"EntityTypeHandler[{TypeDefinition.TypeName}]";
    }
}
