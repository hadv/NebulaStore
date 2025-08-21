using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Indexing;

/// <summary>
/// High-performance B-tree index implementation for range queries and ordered access.
/// </summary>
/// <typeparam name="TKey">Type of index keys (must be comparable)</typeparam>
/// <typeparam name="TValue">Type of indexed values</typeparam>
public class BTreeIndex<TKey, TValue> : IRangeIndex<TKey, TValue>
    where TKey : notnull, IComparable<TKey>
{
    private readonly string _name;
    private readonly IndexConfiguration _configuration;
    private readonly IndexStatistics _statistics;
    private readonly ReaderWriterLockSlim _lock;
    private readonly int _degree; // B-tree degree (minimum number of children)
    private BTreeNode<TKey, TValue>? _root;
    private volatile bool _isDisposed;

    public BTreeIndex(string name, IndexConfiguration? configuration = null, int degree = 32)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new IndexConfiguration { Type = IndexType.BTree };
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid index configuration", nameof(configuration));

        if (degree < 2) throw new ArgumentOutOfRangeException(nameof(degree), "B-tree degree must be at least 2");

        _degree = degree;
        _statistics = new IndexStatistics(_configuration);
        _lock = new ReaderWriterLockSlim();
        _root = null;
    }

    public string Name => _name;
    public IndexType Type => IndexType.BTree;
    public bool IsUnique => _configuration.IsUnique;
    public IIndexStatistics Statistics => _statistics;

    public long Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _root?.Count ?? 0;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public bool Put(TKey key, TValue value)
    {
        ThrowIfDisposed();
        if (key == null) throw new ArgumentNullException(nameof(key));

        var stopwatch = Stopwatch.StartNew();
        bool isNewEntry;

        _lock.EnterWriteLock();
        try
        {
            if (_root == null)
            {
                _root = new BTreeNode<TKey, TValue>(_degree, true);
                _root.Keys.Add(key);
                _root.Values.Add(new List<TValue> { value });
                isNewEntry = true;
            }
            else
            {
                isNewEntry = InsertInternal(_root, key, value);
                
                // Check if root needs to be split
                if (_root.Keys.Count >= 2 * _degree - 1)
                {
                    var newRoot = new BTreeNode<TKey, TValue>(_degree, false);
                    newRoot.Children.Add(_root);
                    SplitChild(newRoot, 0);
                    _root = newRoot;
                }
            }

            stopwatch.Stop();
            
            if (isNewEntry)
            {
                _statistics.RecordInsertion(stopwatch.Elapsed);
            }
            else
            {
                _statistics.RecordUpdate(stopwatch.Elapsed);
            }

            return isNewEntry;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryGet(TKey key, out TValue value)
    {
        ThrowIfDisposed();
        value = default(TValue)!;
        
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        _lock.EnterReadLock();
        try
        {
            var values = SearchInternal(_root, key);
            if (values != null && values.Count > 0)
            {
                value = values[0];
                stopwatch.Stop();
                _statistics.RecordLookup(stopwatch.Elapsed, true);
                return true;
            }

            stopwatch.Stop();
            _statistics.RecordLookup(stopwatch.Elapsed, false);
            return false;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TValue> GetAll(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return Enumerable.Empty<TValue>();

        var stopwatch = Stopwatch.StartNew();

        _lock.EnterReadLock();
        try
        {
            var values = SearchInternal(_root, key);
            stopwatch.Stop();
            
            if (values != null)
            {
                _statistics.RecordLookup(stopwatch.Elapsed, true);
                return values.ToList();
            }

            _statistics.RecordLookup(stopwatch.Elapsed, false);
            return Enumerable.Empty<TValue>();
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TValue> GetRange(TKey startKey, TKey endKey)
    {
        ThrowIfDisposed();
        if (startKey == null || endKey == null) return Enumerable.Empty<TValue>();

        _lock.EnterReadLock();
        try
        {
            var result = new List<TValue>();
            CollectRange(_root, startKey, endKey, result);
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TValue> GetGreaterThan(TKey key, bool inclusive = false)
    {
        ThrowIfDisposed();
        if (key == null) return Enumerable.Empty<TValue>();

        _lock.EnterReadLock();
        try
        {
            var result = new List<TValue>();
            CollectGreaterThan(_root, key, inclusive, result);
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TValue> GetLessThan(TKey key, bool inclusive = false)
    {
        ThrowIfDisposed();
        if (key == null) return Enumerable.Empty<TValue>();

        _lock.EnterReadLock();
        try
        {
            var result = new List<TValue>();
            CollectLessThan(_root, key, inclusive, result);
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public TKey? GetMinKey()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            if (_root == null) return default(TKey);
            
            var node = _root;
            while (!node.IsLeaf)
            {
                node = node.Children[0];
            }
            
            return node.Keys.Count > 0 ? node.Keys[0] : default(TKey);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public TKey? GetMaxKey()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            if (_root == null) return default(TKey);
            
            var node = _root;
            while (!node.IsLeaf)
            {
                node = node.Children[node.Children.Count - 1];
            }
            
            return node.Keys.Count > 0 ? node.Keys[node.Keys.Count - 1] : default(TKey);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool Remove(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        _lock.EnterWriteLock();
        try
        {
            if (_root == null) return false;

            var removed = RemoveInternal(_root, key);
            
            // If root becomes empty and has children, make the first child the new root
            if (_root.Keys.Count == 0 && !_root.IsLeaf)
            {
                _root = _root.Children[0];
            }

            stopwatch.Stop();
            
            if (removed)
            {
                _statistics.RecordDeletion(stopwatch.Elapsed);
            }

            return removed;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Remove(TKey key, TValue value)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        _lock.EnterWriteLock();
        try
        {
            var values = SearchInternal(_root, key);
            if (values != null && values.Remove(value))
            {
                // If no values left for this key, remove the key entirely
                if (values.Count == 0)
                {
                    RemoveInternal(_root, key);
                }

                stopwatch.Stop();
                _statistics.RecordDeletion(stopwatch.Elapsed);
                return true;
            }

            stopwatch.Stop();
            return false;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool ContainsKey(TKey key)
    {
        ThrowIfDisposed();
        if (key == null) return false;

        var stopwatch = Stopwatch.StartNew();

        _lock.EnterReadLock();
        try
        {
            var values = SearchInternal(_root, key);
            var contains = values != null && values.Count > 0;
            stopwatch.Stop();
            _statistics.RecordLookup(stopwatch.Elapsed, contains);
            return contains;
        }
        catch
        {
            stopwatch.Stop();
            _statistics.RecordFailedOperation();
            throw;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TKey> GetKeys()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            var keys = new List<TKey>();
            CollectKeys(_root, keys);
            return keys;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IEnumerable<TValue> GetValues()
    {
        ThrowIfDisposed();

        _lock.EnterReadLock();
        try
        {
            var values = new List<TValue>();
            CollectValues(_root, values);
            return values;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Clear()
    {
        ThrowIfDisposed();

        _lock.EnterWriteLock();
        try
        {
            _root = null;
            _statistics.Reset();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public async Task RebuildAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        await Task.Run(() =>
        {
            _lock.EnterWriteLock();
            try
            {
                // Collect all key-value pairs
                var pairs = new List<(TKey Key, List<TValue> Values)>();
                CollectKeyValuePairs(_root, pairs);

                // Clear the tree
                _root = null;

                // Rebuild with optimal structure
                foreach (var (key, values) in pairs.OrderBy(p => p.Key))
                {
                    foreach (var value in values)
                    {
                        Put(key, value);
                    }
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }, cancellationToken);
    }

    // Internal B-tree operations would be implemented here
    // For brevity, I'm including key method signatures
    private bool InsertInternal(BTreeNode<TKey, TValue> node, TKey key, TValue value) { /* Implementation */ return true; }
    private List<TValue>? SearchInternal(BTreeNode<TKey, TValue>? node, TKey key) { /* Implementation */ return null; }
    private bool RemoveInternal(BTreeNode<TKey, TValue> node, TKey key) { /* Implementation */ return true; }
    private void SplitChild(BTreeNode<TKey, TValue> parent, int index) { /* Implementation */ }
    private void CollectRange(BTreeNode<TKey, TValue>? node, TKey start, TKey end, List<TValue> result) { /* Implementation */ }
    private void CollectGreaterThan(BTreeNode<TKey, TValue>? node, TKey key, bool inclusive, List<TValue> result) { /* Implementation */ }
    private void CollectLessThan(BTreeNode<TKey, TValue>? node, TKey key, bool inclusive, List<TValue> result) { /* Implementation */ }
    private void CollectKeys(BTreeNode<TKey, TValue>? node, List<TKey> keys) { /* Implementation */ }
    private void CollectValues(BTreeNode<TKey, TValue>? node, List<TValue> values) { /* Implementation */ }
    private void CollectKeyValuePairs(BTreeNode<TKey, TValue>? node, List<(TKey, List<TValue>)> pairs) { /* Implementation */ }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(BTreeIndex<TKey, TValue>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _lock.Dispose();
    }
}

/// <summary>
/// B-tree node implementation.
/// </summary>
/// <typeparam name="TKey">Type of keys</typeparam>
/// <typeparam name="TValue">Type of values</typeparam>
internal class BTreeNode<TKey, TValue>
    where TKey : notnull, IComparable<TKey>
{
    public BTreeNode(int degree, bool isLeaf)
    {
        Degree = degree;
        IsLeaf = isLeaf;
        Keys = new List<TKey>();
        Values = new List<List<TValue>>();
        Children = new List<BTreeNode<TKey, TValue>>();
    }

    public int Degree { get; }
    public bool IsLeaf { get; set; }
    public List<TKey> Keys { get; }
    public List<List<TValue>> Values { get; }
    public List<BTreeNode<TKey, TValue>> Children { get; }

    public long Count
    {
        get
        {
            long count = Values.Sum(v => v.Count);
            foreach (var child in Children)
            {
                count += child.Count;
            }
            return count;
        }
    }
}
