using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.GigaMap;

/// <summary>
/// Default implementation of IGigaQuery.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
internal class DefaultGigaQuery<T> : IGigaQuery<T> where T : class
{
    private readonly IGigaMap<T> _gigaMap;
    private readonly List<ICondition<T>> _conditions = new();
    private int _limit = -1;
    private int _offset = 0;

    public DefaultGigaQuery(IGigaMap<T> gigaMap)
    {
        _gigaMap = gigaMap ?? throw new ArgumentNullException(nameof(gigaMap));
    }

    internal IGigaMap<T> GigaMap => _gigaMap;

    public IGigaQuery<T> And(ICondition<T> condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        _conditions.Add(condition);
        return this;
    }

    public IConditionBuilder<T, TKey> And<TKey>(IIndexIdentifier<T, TKey> index)
    {
        if (index == null)
            throw new ArgumentNullException(nameof(index));

        return new DefaultConditionBuilder<T, TKey>(this, index);
    }

    public IConditionBuilder<T, string> And(string stringIndexName)
    {
        if (string.IsNullOrEmpty(stringIndexName))
            throw new ArgumentException("Index name cannot be null or empty", nameof(stringIndexName));

        var index = _gigaMap.Index.Bitmap.Get(stringIndexName);
        if (index == null)
            throw new ArgumentException($"Index '{stringIndexName}' not found", nameof(stringIndexName));

        // Allow any index to be queried with string keys - create a flexible wrapper
        return new DefaultConditionBuilder<T, string>(this, new FlexibleStringIndexWrapper<T>(index));
    }

    public IGigaQuery<T> And(string stringIndexName, string key)
    {
        return And(stringIndexName).Is(key);
    }

    public IConditionBuilder<T, TKey> And<TKey>(string indexName, Type keyType)
    {
        if (string.IsNullOrEmpty(indexName))
            throw new ArgumentException("Index name cannot be null or empty", nameof(indexName));
        if (keyType == null)
            throw new ArgumentNullException(nameof(keyType));

        var index = _gigaMap.Index.Bitmap.Get(indexName);
        if (index == null)
            throw new ArgumentException($"Index '{indexName}' not found", nameof(indexName));

        return new DefaultConditionBuilder<T, TKey>(this, index as IIndexIdentifier<T, TKey> ??
            throw new InvalidOperationException($"Index '{indexName}' is not compatible with key type {keyType.Name}"));
    }

    public IGigaQuery<T> And<TKey>(string indexName, Type keyType, TKey key)
    {
        return And<TKey>(indexName, keyType).Is(key);
    }

    public IGigaQuery<T> Or(ICondition<T> condition)
    {
        if (condition == null)
            throw new ArgumentNullException(nameof(condition));

        // For OR operations, we need to combine with existing conditions
        if (_conditions.Count > 0)
        {
            var combinedCondition = _conditions.Aggregate((left, right) => left.And(right));
            _conditions.Clear();
            _conditions.Add(combinedCondition.Or(condition));
        }
        else
        {
            _conditions.Add(condition);
        }

        return this;
    }

    public IConditionBuilder<T, TKey> Or<TKey>(IIndexIdentifier<T, TKey> index)
    {
        if (index == null)
            throw new ArgumentNullException(nameof(index));

        return new DefaultConditionBuilder<T, TKey>(this, index, isOrOperation: true);
    }

    public IConditionBuilder<T, string> Or(string stringIndexName)
    {
        if (string.IsNullOrEmpty(stringIndexName))
            throw new ArgumentException("Index name cannot be null or empty", nameof(stringIndexName));

        var index = _gigaMap.Index.Bitmap.Get(stringIndexName);
        if (index == null)
            throw new ArgumentException($"Index '{stringIndexName}' not found", nameof(stringIndexName));

        return new DefaultConditionBuilder<T, string>(this, index as IIndexIdentifier<T, string> ??
            throw new InvalidOperationException($"Index '{stringIndexName}' is not a string index"), isOrOperation: true);
    }

    public IGigaQuery<T> Or(string stringIndexName, string key)
    {
        return Or(stringIndexName).Is(key);
    }

    public IGigaQuery<T> Limit(int limit)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be non-negative");

        _limit = limit;
        return this;
    }

    public IGigaQuery<T> Skip(int offset)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative");

        _offset = offset;
        return this;
    }

    public IReadOnlyList<T> Execute()
    {
        var entityIds = GetMatchingEntityIds();

        // Pre-calculate result size for better memory allocation
        var totalCount = entityIds.Count();
        var estimatedResultSize = _limit > 0 ? Math.Min(_limit, totalCount - _offset) : totalCount - _offset;
        estimatedResultSize = Math.Max(0, estimatedResultSize);

        var results = new List<T>(estimatedResultSize);

        var skipped = 0;
        var taken = 0;

        foreach (var entityId in entityIds)
        {
            if (skipped < _offset)
            {
                skipped++;
                continue;
            }

            if (_limit > 0 && taken >= _limit)
                break;

            var entity = _gigaMap.Get(entityId);
            if (entity != null)
            {
                results.Add(entity);
                taken++;
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<T>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Execute(), cancellationToken);
    }

    public T? FirstOrDefault()
    {
        var entityIds = GetMatchingEntityIds();

        var skipped = 0;
        foreach (var entityId in entityIds)
        {
            if (skipped < _offset)
            {
                skipped++;
                continue;
            }

            var entity = _gigaMap.Get(entityId);
            if (entity != null)
                return entity;
        }

        return null;
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => FirstOrDefault(), cancellationToken);
    }

    public long Count()
    {
        var entityIds = GetMatchingEntityIds();
        return entityIds.Count();
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Count(), cancellationToken);
    }

    public bool Any()
    {
        var entityIds = GetMatchingEntityIds();
        return entityIds.Any();
    }

    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Any(), cancellationToken);
    }

    public void ForEach(Action<T> action)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        foreach (var entity in Execute())
        {
            action(entity);
        }
    }

    public async Task ForEachAsync(Action<T> action, CancellationToken cancellationToken = default)
    {
        if (action == null)
            throw new ArgumentNullException(nameof(action));

        await Task.Run(() => ForEach(action), cancellationToken);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Execute().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<long> GetMatchingEntityIds()
    {
        if (_conditions.Count == 0)
        {
            // Return all entity IDs
            return Enumerable.Range(0, (int)_gigaMap.Size).Select(i => (long)i);
        }

        // Combine all conditions with AND
        var combinedCondition = _conditions.Aggregate((left, right) => left.And(right));
        var result = combinedCondition.Evaluate(_gigaMap.Index.Bitmap);

        return result.EntityIds;
    }
}

/// <summary>
/// Default implementation of IConditionBuilder.
/// </summary>
/// <typeparam name="T">The type of entities being queried</typeparam>
/// <typeparam name="TKey">The type of keys in the index</typeparam>
internal class DefaultConditionBuilder<T, TKey> : IConditionBuilder<T, TKey> where T : class
{
    private readonly DefaultGigaQuery<T> _query;
    private readonly IIndexIdentifier<T, TKey> _index;
    private readonly bool _isOrOperation;
    private readonly IGigaMap<T> _gigaMap;

    public DefaultConditionBuilder(DefaultGigaQuery<T> query, IIndexIdentifier<T, TKey> index, bool isOrOperation = false)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _isOrOperation = isOrOperation;
        _gigaMap = query.GigaMap;
    }

    public IGigaQuery<T> Is(TKey key)
    {
        var condition = _index.Is(key);
        return _isOrOperation ? _query.Or(condition) : _query.And(condition);
    }

    public IGigaQuery<T> IsNot(TKey key)
    {
        var condition = _index.IsNot(key);
        return _isOrOperation ? _query.Or(condition) : _query.And(condition);
    }

    public IGigaQuery<T> IsIn(IEnumerable<TKey> keys)
    {
        var condition = _index.IsIn(keys);
        return _isOrOperation ? _query.Or(condition) : _query.And(condition);
    }

    public IGigaQuery<T> IsNotIn(IEnumerable<TKey> keys)
    {
        var condition = _index.IsIn(keys).Not();
        return _isOrOperation ? _query.Or(condition) : _query.And(condition);
    }

    public IGigaQuery<T> Where(Func<TKey, bool> predicate)
    {
        if (predicate == null)
            throw new ArgumentNullException(nameof(predicate));

        // Get the bitmap index for this identifier
        var bitmapIndex = _gigaMap.Index.Bitmap.Get(_index.Name) as IBitmapIndex<T, TKey>;
        if (bitmapIndex == null)
            throw new InvalidOperationException($"Bitmap index for '{_index.Name}' not found");

        var condition = new PredicateCondition<T, TKey>(bitmapIndex, predicate);
        return _isOrOperation ? _query.Or(condition) : _query.And(condition);
    }
}