using System;
using System.Collections.Generic;
using System.Linq;

namespace NebulaStore.GigaMap;

/// <summary>
/// Equality condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class EqualityCondition<T, TKey> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, TKey> _index;
    private readonly TKey _key;

    public EqualityCondition(IBitmapIndex<T, TKey> index, TKey key)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _key = key;
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return _index.Search(k => _index.Indexer.KeyEqualityComparer.Equals(k, _key));
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Inequality condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class InequalityCondition<T, TKey> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, TKey> _index;
    private readonly TKey _key;

    public InequalityCondition(IBitmapIndex<T, TKey> index, TKey key)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _key = key;
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return _index.Search(k => !_index.Indexer.KeyEqualityComparer.Equals(k, _key));
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Membership condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class MembershipCondition<T, TKey> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, TKey> _index;
    private readonly HashSet<TKey> _keys;

    public MembershipCondition(IBitmapIndex<T, TKey> index, HashSet<TKey> keys)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return _index.Search(k => _keys.Contains(k));
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// Predicate condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class PredicateCondition<T, TKey> : ICondition<T> where T : class
{
    private readonly IBitmapIndex<T, TKey> _index;
    private readonly Func<TKey, bool> _predicate;

    public PredicateCondition(IBitmapIndex<T, TKey> index, Func<TKey, bool> predicate)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        return _index.Search(_predicate);
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// AND condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
internal class AndCondition<T> : ICondition<T> where T : class
{
    private readonly ICondition<T> _left;
    private readonly ICondition<T> _right;

    public AndCondition(ICondition<T> left, ICondition<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        var leftResult = _left.Evaluate(bitmapIndices);
        var rightResult = _right.Evaluate(bitmapIndices);
        return leftResult.And(rightResult);
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// OR condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
internal class OrCondition<T> : ICondition<T> where T : class
{
    private readonly ICondition<T> _left;
    private readonly ICondition<T> _right;

    public OrCondition(ICondition<T> left, ICondition<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        var leftResult = _left.Evaluate(bitmapIndices);
        var rightResult = _right.Evaluate(bitmapIndices);
        return leftResult.Or(rightResult);
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return new NotCondition<T>(this);
    }
}

/// <summary>
/// NOT condition implementation.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
internal class NotCondition<T> : ICondition<T> where T : class
{
    private readonly ICondition<T> _condition;

    public NotCondition(ICondition<T> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
    }

    public IBitmapResult Evaluate(IBitmapIndices<T> bitmapIndices)
    {
        var result = _condition.Evaluate(bitmapIndices);
        // Note: In a real implementation, we'd need access to the total entity count
        // For now, we'll return an empty result as a placeholder
        return new DefaultBitmapResult(new HashSet<long>());
    }

    public ICondition<T> And(ICondition<T> other)
    {
        return new AndCondition<T>(this, other);
    }

    public ICondition<T> Or(ICondition<T> other)
    {
        return new OrCondition<T>(this, other);
    }

    public ICondition<T> Not()
    {
        return _condition; // Double negation
    }
}