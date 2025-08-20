using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IGigaConstraints.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
internal class DefaultGigaConstraints<T> : IGigaConstraints<T> where T : class
{
    private readonly DefaultUniqueConstraints<T> _uniqueConstraints;
    private readonly DefaultCustomConstraints<T> _customConstraints;
    private readonly List<IConstraint<T>> _allConstraints = new();

    public DefaultGigaConstraints()
    {
        _uniqueConstraints = new DefaultUniqueConstraints<T>(this);
        _customConstraints = new DefaultCustomConstraints<T>(this);
    }

    public IUniqueConstraints<T> Unique => _uniqueConstraints;

    public ICustomConstraints<T> Custom => _customConstraints;

    public IReadOnlyCollection<IConstraint<T>> All => _allConstraints.ToList();

    public void Check(long entityId, T? replacedEntity, T entity)
    {
        foreach (var constraint in _allConstraints)
        {
            constraint.Check(entityId, replacedEntity, entity);
        }
    }

    public void Add(IConstraint<T> constraint)
    {
        if (constraint == null)
            throw new ArgumentNullException(nameof(constraint));

        _allConstraints.Add(constraint);
    }

    public bool Remove(IConstraint<T> constraint)
    {
        if (constraint == null)
            throw new ArgumentNullException(nameof(constraint));

        return _allConstraints.Remove(constraint);
    }

    public void Clear()
    {
        _allConstraints.Clear();
        _uniqueConstraints.Clear();
        _customConstraints.Clear();
    }
}

/// <summary>
/// Default implementation of IUniqueConstraints.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
internal class DefaultUniqueConstraints<T> : IUniqueConstraints<T> where T : class
{
    private readonly DefaultGigaConstraints<T> _parent;
    private readonly List<IIndexer<T, object>> _indexers = new();

    public DefaultUniqueConstraints(DefaultGigaConstraints<T> parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public IReadOnlyCollection<IIndexer<T, object>> Indexers => _indexers.ToList();

    public void AddConstraints(IEnumerable<IIndexer<T, object>> indexers)
    {
        if (indexers == null)
            throw new ArgumentNullException(nameof(indexers));

        foreach (var indexer in indexers)
        {
            AddConstraint(indexer);
        }
    }

    public void AddConstraint<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        if (!indexer.IsSuitableAsUniqueConstraint)
            throw new ArgumentException($"Indexer '{indexer.Name}' is not suitable as a unique constraint");

        // Convert to object indexer if needed
        IIndexer<T, object> objectIndexer;
        if (indexer is IIndexer<T, object> directObjectIndexer)
        {
            objectIndexer = directObjectIndexer;
        }
        else
        {
            objectIndexer = Indexer.AsObjectIndexer(indexer);
        }

        if (!_indexers.Any(i => i.Name == objectIndexer.Name))
        {
            _indexers.Add(objectIndexer);
            var constraint = new UniqueConstraint<T>(objectIndexer);
            _parent.Add(constraint);
        }
    }

    public bool RemoveConstraint<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        var objectIndexer = indexer as IIndexer<T, object>;
        if (objectIndexer != null && _indexers.Remove(objectIndexer))
        {
            // Remove the corresponding constraint from parent
            var constraintToRemove = _parent.All.OfType<UniqueConstraint<T>>()
                .FirstOrDefault(c => c.Indexer.Name == indexer.Name);

            if (constraintToRemove != null)
            {
                _parent.Remove(constraintToRemove);
            }

            return true;
        }

        return false;
    }

    public bool HasConstraint<TKey>(IIndexer<T, TKey> indexer)
    {
        if (indexer == null)
            throw new ArgumentNullException(nameof(indexer));

        return _indexers.Any(i => i.Name == indexer.Name);
    }

    public void Clear()
    {
        // Remove all unique constraints from parent
        var uniqueConstraints = _parent.All.OfType<UniqueConstraint<T>>().ToList();
        foreach (var constraint in uniqueConstraints)
        {
            _parent.Remove(constraint);
        }

        _indexers.Clear();
    }
}

/// <summary>
/// Default implementation of ICustomConstraints.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
internal class DefaultCustomConstraints<T> : ICustomConstraints<T> where T : class
{
    private readonly DefaultGigaConstraints<T> _parent;
    private readonly List<ICustomConstraint<T>> _customConstraints = new();

    public DefaultCustomConstraints(DefaultGigaConstraints<T> parent)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    public IReadOnlyCollection<ICustomConstraint<T>> All => _customConstraints.ToList();

    public void AddConstraints(IEnumerable<ICustomConstraint<T>> constraints)
    {
        if (constraints == null)
            throw new ArgumentNullException(nameof(constraints));

        foreach (var constraint in constraints)
        {
            AddConstraint(constraint);
        }
    }

    public void AddConstraint(ICustomConstraint<T> constraint)
    {
        if (constraint == null)
            throw new ArgumentNullException(nameof(constraint));

        _customConstraints.Add(constraint);
        _parent.Add(constraint);
    }

    public bool RemoveConstraint(ICustomConstraint<T> constraint)
    {
        if (constraint == null)
            throw new ArgumentNullException(nameof(constraint));

        if (_customConstraints.Remove(constraint))
        {
            _parent.Remove(constraint);
            return true;
        }

        return false;
    }

    public void Clear()
    {
        // Remove all custom constraints from parent
        foreach (var constraint in _customConstraints.ToList())
        {
            _parent.Remove(constraint);
        }

        _customConstraints.Clear();
    }
}

/// <summary>
/// Implementation of a unique constraint.
/// </summary>
/// <typeparam name="T">The type of entities being constrained</typeparam>
internal class UniqueConstraint<T> : IConstraint<T> where T : class
{
    private readonly IIndexer<T, object> _indexer;
    private readonly Dictionary<object, long> _keyToEntityId = new();

    public UniqueConstraint(IIndexer<T, object> indexer)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
    }

    public string Name => $"Unique_{_indexer.Name}";

    public IIndexer<T, object> Indexer => _indexer;

    public void Check(long entityId, T? replacedEntity, T entity)
    {
        if (entity == null)
            throw new ArgumentNullException(nameof(entity));

        var key = _indexer.Index(entity);

        // Remove old mapping if this is an update
        if (replacedEntity != null)
        {
            var oldKey = _indexer.Index(replacedEntity);
            _keyToEntityId.Remove(oldKey);
        }

        // Check if key already exists for a different entity
        if (_keyToEntityId.TryGetValue(key, out var existingEntityId) && existingEntityId != entityId)
        {
            throw new ConstraintViolationException(
                Name,
                $"Unique constraint violation: Key '{key}' already exists for entity {existingEntityId}");
        }

        // Add or update the mapping
        _keyToEntityId[key] = entityId;
    }
}