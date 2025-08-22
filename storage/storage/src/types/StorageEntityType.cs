using System;
using System.Collections.Generic;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Default implementation of IStorageEntityType representing a type of stored objects.
/// </summary>
public class StorageEntityType : IStorageEntityType
{
    #region Private Fields

    private readonly long _typeId;
    private readonly int _channelIndex;
    private readonly IStorageEntityTypeHandler _typeHandler;
    private readonly bool _hasReferences;
    private readonly int _simpleReferenceDataCount;
    private readonly object _lock = new object();

    private long _entityCount;
    private IStorageEntity? _firstEntity;
    private IStorageEntity? _lastEntity;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the StorageEntityType class.
    /// </summary>
    /// <param name="typeId">The type ID.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeHandler">The type handler.</param>
    /// <param name="hasReferences">Whether entities of this type have references.</param>
    /// <param name="simpleReferenceDataCount">The simple reference data count.</param>
    public StorageEntityType(
        long typeId,
        int channelIndex,
        IStorageEntityTypeHandler typeHandler,
        bool hasReferences,
        int simpleReferenceDataCount)
    {
        _typeId = typeId;
        _channelIndex = channelIndex;
        _typeHandler = typeHandler ?? throw new ArgumentNullException(nameof(typeHandler));
        _hasReferences = hasReferences;
        _simpleReferenceDataCount = simpleReferenceDataCount;
    }

    #endregion

    #region IStorageEntityType Implementation

    public long TypeId => _typeId;

    public int ChannelIndex => _channelIndex;

    public IStorageEntityTypeHandler TypeHandler => _typeHandler;

    public long EntityCount => Interlocked.Read(ref _entityCount);

    public bool IsEmpty => EntityCount == 0;

    public bool HasReferences => _hasReferences;

    public int SimpleReferenceDataCount => _simpleReferenceDataCount;

    #endregion

    #region Public Methods

    public void Add(IStorageEntity entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (entity.TypeId != _typeId)
            throw new ArgumentException($"Entity type ID {entity.TypeId} does not match this type ID {_typeId}");

        lock (_lock)
        {
            // Add to the end of the type chain
            if (_lastEntity != null)
            {
                _lastEntity.TypeNext = entity;
            }
            else
            {
                _firstEntity = entity;
            }

            _lastEntity = entity;
            entity.TypeNext = null;

            Interlocked.Increment(ref _entityCount);
        }
    }

    public void Remove(IStorageEntity entity, IStorageEntity? previousInType)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        if (entity.TypeId != _typeId)
            throw new ArgumentException($"Entity type ID {entity.TypeId} does not match this type ID {_typeId}");

        lock (_lock)
        {
            // Remove from type chain
            if (previousInType != null)
            {
                previousInType.TypeNext = entity.TypeNext;
            }
            else
            {
                _firstEntity = entity.TypeNext;
            }

            if (entity == _lastEntity)
            {
                _lastEntity = previousInType;
            }

            entity.TypeNext = null;
            Interlocked.Decrement(ref _entityCount);
        }
    }

    public void IterateEntities(Action<IStorageEntity> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        // Create a snapshot of entities to avoid holding the lock during iteration
        var entities = new List<IStorageEntity>();
        
        lock (_lock)
        {
            var current = _firstEntity;
            while (current != null)
            {
                entities.Add(current);
                current = current.TypeNext;
            }
        }

        // Iterate without holding the lock
        foreach (var entity in entities)
        {
            action(entity);
        }
    }

    public IStorageIdAnalysis ValidateEntities()
    {
        long highestObjectId = 0;
        long entityCount = 0;

        IterateEntities(entity =>
        {
            if (entity.ObjectId > highestObjectId)
            {
                highestObjectId = entity.ObjectId;
            }
            entityCount++;
        });

        return new StorageIdAnalysis(highestObjectId, _typeId, entityCount);
    }

    #endregion

    #region Static Factory Methods

    /// <summary>
    /// Creates a new StorageEntityType instance.
    /// </summary>
    /// <param name="typeId">The type ID.</param>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="typeHandler">The type handler.</param>
    /// <param name="hasReferences">Whether entities of this type have references.</param>
    /// <param name="simpleReferenceDataCount">The simple reference data count.</param>
    /// <returns>A new StorageEntityType instance.</returns>
    public static StorageEntityType New(
        long typeId,
        int channelIndex,
        IStorageEntityTypeHandler typeHandler,
        bool hasReferences,
        int simpleReferenceDataCount)
    {
        return new StorageEntityType(typeId, channelIndex, typeHandler, hasReferences, simpleReferenceDataCount);
    }

    #endregion

    #region Overrides

    public override string ToString()
    {
        return $"StorageEntityType[TypeId={_typeId}, Channel={_channelIndex}, Type={_typeHandler.TypeName}, Entities={EntityCount}]";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageEntityType other && _typeId == other._typeId;
    }

    public override int GetHashCode()
    {
        return _typeId.GetHashCode();
    }

    #endregion
}

/// <summary>
/// Default implementation of IStorageIdAnalysis.
/// </summary>
public class StorageIdAnalysis : IStorageIdAnalysis
{
    public long HighestObjectId { get; }
    public long HighestTypeId { get; }
    public long EntityCount { get; }

    public StorageIdAnalysis(long highestObjectId, long highestTypeId, long entityCount)
    {
        HighestObjectId = highestObjectId;
        HighestTypeId = highestTypeId;
        EntityCount = entityCount;
    }

    public override string ToString()
    {
        return $"StorageIdAnalysis[HighestOID={HighestObjectId}, HighestTID={HighestTypeId}, Entities={EntityCount}]";
    }
}

/// <summary>
/// Default implementation of IStorageEntityTypeHandler.
/// </summary>
public class StorageEntityTypeHandler : IStorageEntityTypeHandler
{
    public long TypeId { get; }
    public Type Type { get; }
    public string TypeName { get; }

    public StorageEntityTypeHandler(long typeId, Type type)
    {
        TypeId = typeId;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        TypeName = type.FullName ?? type.Name;
    }

    public override string ToString()
    {
        return $"StorageEntityTypeHandler[TypeId={TypeId}, Type={TypeName}]";
    }

    public override bool Equals(object? obj)
    {
        return obj is StorageEntityTypeHandler other && TypeId == other.TypeId;
    }

    public override int GetHashCode()
    {
        return TypeId.GetHashCode();
    }
}

/// <summary>
/// Default implementation of ITypeHandler for the storage type dictionary.
/// </summary>
public class TypeHandler : ITypeHandler
{
    public Type HandledType { get; }
    public Type Type { get; }
    public long TypeId { get; }
    public string TypeName { get; }

    public TypeHandler(Type type, long typeId)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        HandledType = type;
        TypeId = typeId;
        TypeName = type.FullName ?? type.Name;
    }

    public byte[] Serialize(object instance)
    {
        // Basic serialization using System.Text.Json for now
        var json = System.Text.Json.JsonSerializer.Serialize(instance);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public object Deserialize(byte[] data)
    {
        // Basic deserialization using System.Text.Json for now
        var json = System.Text.Encoding.UTF8.GetString(data);
        return System.Text.Json.JsonSerializer.Deserialize(json, Type) ?? throw new InvalidOperationException("Failed to deserialize object");
    }

    public long GetSerializedLength(object instance)
    {
        // Estimate serialized length
        var serialized = Serialize(instance);
        return serialized.Length;
    }

    public bool CanHandle(Type type)
    {
        return type == Type;
    }

    public override string ToString()
    {
        return $"TypeHandler[TypeId={TypeId}, Type={TypeName}]";
    }

    public override bool Equals(object? obj)
    {
        return obj is TypeHandler other && TypeId == other.TypeId && Type == other.Type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, TypeId);
    }
}
