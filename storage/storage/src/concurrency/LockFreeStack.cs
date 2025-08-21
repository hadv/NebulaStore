using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Lock-free stack implementation using the Treiber algorithm.
/// Provides high-performance concurrent push and pop operations.
/// </summary>
/// <typeparam name="T">Type of elements in the stack</typeparam>
public class LockFreeStack<T> : ILockFreeStack<T>
{
    private readonly LockFreeConfiguration _configuration;
    private readonly LockFreeStatistics _statistics;
    private volatile Node? _head;

    public LockFreeStack(LockFreeConfiguration? configuration = null)
    {
        _configuration = configuration ?? new LockFreeConfiguration();
        _statistics = new LockFreeStatistics(_configuration);
        _head = null;
    }

    public int Count
    {
        get
        {
            // Approximate count - not guaranteed to be exact in concurrent scenarios
            int count = 0;
            var current = _head;
            while (current != null && count < 10000) // Prevent infinite loops
            {
                count++;
                current = current.Next;
            }
            return count;
        }
    }

    public bool IsEmpty => _head == null;

    public ILockFreeStatistics Statistics => _statistics;

    public void Push(T item)
    {
        var newNode = new Node(item);
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var currentHead = _head;
            newNode.Next = currentHead;

            // Try to update head to point to the new node
            if (Interlocked.CompareExchange(ref _head, newNode, currentHead) == currentHead)
            {
                _statistics.RecordSuccessfulOperation();
                return;
            }

            retryCount++;
            _statistics.RecordRetryAttempt();
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        _statistics.RecordFailedOperation();
        throw new InvalidOperationException($"Failed to push after {_configuration.MaxRetryAttempts} attempts");
    }

    public bool TryPop(out T result)
    {
        result = default(T)!;
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var currentHead = _head;
            
            if (currentHead == null)
            {
                // Stack is empty
                _statistics.RecordSuccessfulOperation();
                return false;
            }

            var newHead = currentHead.Next;

            // Try to update head to point to the next node
            if (Interlocked.CompareExchange(ref _head, newHead, currentHead) == currentHead)
            {
                result = currentHead.Data;
                _statistics.RecordSuccessfulOperation();
                return true;
            }

            retryCount++;
            _statistics.RecordRetryAttempt();
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        _statistics.RecordFailedOperation();
        return false;
    }

    public bool TryPeek(out T result)
    {
        result = default(T)!;
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var currentHead = _head;
            
            if (currentHead == null)
            {
                // Stack is empty
                _statistics.RecordSuccessfulOperation();
                return false;
            }

            // Check if head is still the same (no ABA problem for peek)
            if (currentHead == _head)
            {
                result = currentHead.Data;
                _statistics.RecordSuccessfulOperation();
                return true;
            }

            retryCount++;
            _statistics.RecordRetryAttempt();
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        _statistics.RecordFailedOperation();
        return false;
    }

    /// <summary>
    /// Clears all elements from the stack.
    /// </summary>
    public void Clear()
    {
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            var currentHead = _head;
            
            if (currentHead == null)
            {
                // Stack is already empty
                return;
            }

            // Try to set head to null
            if (Interlocked.CompareExchange(ref _head, null, currentHead) == currentHead)
            {
                return;
            }

            retryCount++;
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        throw new InvalidOperationException($"Failed to clear stack after {_configuration.MaxRetryAttempts} attempts");
    }

    /// <summary>
    /// Attempts to pop multiple items from the stack.
    /// </summary>
    /// <param name="maxItems">Maximum number of items to pop</param>
    /// <returns>Array of popped items</returns>
    public T[] TryPopMany(int maxItems)
    {
        if (maxItems <= 0) throw new ArgumentOutOfRangeException(nameof(maxItems));

        var results = new T[maxItems];
        var count = 0;

        for (int i = 0; i < maxItems; i++)
        {
            if (TryPop(out var item))
            {
                results[count++] = item;
            }
            else
            {
                break; // Stack is empty
            }
        }

        // Resize array to actual count
        if (count < maxItems)
        {
            Array.Resize(ref results, count);
        }

        return results;
    }

    /// <summary>
    /// Pushes multiple items onto the stack.
    /// </summary>
    /// <param name="items">Items to push</param>
    public void PushMany(T[] items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        // Push in reverse order to maintain order
        for (int i = items.Length - 1; i >= 0; i--)
        {
            Push(items[i]);
        }
    }

    private void PerformBackoff(int retryCount)
    {
        var delay = CalculateBackoffDelay(retryCount);
        if (delay > 0)
        {
            // Use SpinWait for very short delays, Thread.Sleep for longer ones
            if (delay < 1000) // Less than 1ms
            {
                var spinWait = new SpinWait();
                for (int i = 0; i < delay / 10; i++)
                {
                    spinWait.SpinOnce();
                }
            }
            else
            {
                Thread.Sleep(delay / 1000); // Convert microseconds to milliseconds
            }
        }
    }

    private int CalculateBackoffDelay(int retryCount)
    {
        return _configuration.BackoffStrategy switch
        {
            BackoffStrategy.None => 0,
            BackoffStrategy.Linear => Math.Min(
                _configuration.InitialBackoffMicroseconds * retryCount,
                _configuration.MaxBackoffMicroseconds),
            BackoffStrategy.Exponential => Math.Min(
                _configuration.InitialBackoffMicroseconds * (1 << Math.Min(retryCount, 20)),
                _configuration.MaxBackoffMicroseconds),
            BackoffStrategy.Random => Random.Shared.Next(
                _configuration.InitialBackoffMicroseconds,
                Math.Min(_configuration.MaxBackoffMicroseconds, _configuration.InitialBackoffMicroseconds * retryCount)),
            _ => 0
        };
    }

    /// <summary>
    /// Node class for the lock-free stack.
    /// </summary>
    private class Node
    {
        public readonly T Data;
        public volatile Node? Next;

        public Node(T data)
        {
            Data = data;
            Next = null;
        }
    }
}

/// <summary>
/// Lock-free stack with hazard pointers to prevent ABA problems.
/// More complex but safer implementation for scenarios with high contention.
/// </summary>
/// <typeparam name="T">Type of elements in the stack</typeparam>
public class HazardPointerStack<T> : ILockFreeStack<T>
{
    private readonly LockFreeConfiguration _configuration;
    private readonly LockFreeStatistics _statistics;
    private readonly HazardPointerManager _hazardManager;
    private volatile Node? _head;

    public HazardPointerStack(LockFreeConfiguration? configuration = null)
    {
        _configuration = configuration ?? new LockFreeConfiguration();
        _statistics = new LockFreeStatistics(_configuration);
        _hazardManager = new HazardPointerManager();
        _head = null;
    }

    public int Count
    {
        get
        {
            int count = 0;
            var current = _head;
            while (current != null && count < 10000)
            {
                count++;
                current = current.Next;
            }
            return count;
        }
    }

    public bool IsEmpty => _head == null;

    public ILockFreeStatistics Statistics => _statistics;

    public void Push(T item)
    {
        var newNode = new Node(item);
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var currentHead = _head;
            newNode.Next = currentHead;

            if (Interlocked.CompareExchange(ref _head, newNode, currentHead) == currentHead)
            {
                _statistics.RecordSuccessfulOperation();
                return;
            }

            retryCount++;
            _statistics.RecordRetryAttempt();
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        _statistics.RecordFailedOperation();
        throw new InvalidOperationException($"Failed to push after {_configuration.MaxRetryAttempts} attempts");
    }

    public bool TryPop(out T result)
    {
        result = default(T)!;
        var retryCount = 0;
        var hazardPointer = _hazardManager.AcquireHazardPointer();

        try
        {
            while (retryCount < _configuration.MaxRetryAttempts)
            {
                _statistics.RecordOperationAttempt();

                var currentHead = _head;
                
                if (currentHead == null)
                {
                    _statistics.RecordSuccessfulOperation();
                    return false;
                }

                // Set hazard pointer to protect the node
                hazardPointer.Set(currentHead);

                // Re-read head to ensure it hasn't changed
                if (currentHead != _head)
                {
                    retryCount++;
                    _statistics.RecordRetryAttempt();
                    continue;
                }

                var newHead = currentHead.Next;

                if (Interlocked.CompareExchange(ref _head, newHead, currentHead) == currentHead)
                {
                    result = currentHead.Data;
                    _statistics.RecordSuccessfulOperation();
                    
                    // Schedule node for deletion
                    _hazardManager.RetireNode(currentHead);
                    return true;
                }

                retryCount++;
                _statistics.RecordRetryAttempt();
                
                if (_configuration.BackoffStrategy != BackoffStrategy.None)
                {
                    PerformBackoff(retryCount);
                }
            }

            _statistics.RecordFailedOperation();
            return false;
        }
        finally
        {
            _hazardManager.ReleaseHazardPointer(hazardPointer);
        }
    }

    public bool TryPeek(out T result)
    {
        result = default(T)!;
        var hazardPointer = _hazardManager.AcquireHazardPointer();

        try
        {
            var currentHead = _head;
            
            if (currentHead == null)
            {
                return false;
            }

            hazardPointer.Set(currentHead);

            // Re-read to ensure consistency
            if (currentHead == _head)
            {
                result = currentHead.Data;
                return true;
            }

            return false;
        }
        finally
        {
            _hazardManager.ReleaseHazardPointer(hazardPointer);
        }
    }

    private void PerformBackoff(int retryCount)
    {
        var delay = CalculateBackoffDelay(retryCount);
        if (delay > 0)
        {
            if (delay < 1000)
            {
                var spinWait = new SpinWait();
                for (int i = 0; i < delay / 10; i++)
                {
                    spinWait.SpinOnce();
                }
            }
            else
            {
                Thread.Sleep(delay / 1000);
            }
        }
    }

    private int CalculateBackoffDelay(int retryCount)
    {
        return _configuration.BackoffStrategy switch
        {
            BackoffStrategy.None => 0,
            BackoffStrategy.Linear => Math.Min(
                _configuration.InitialBackoffMicroseconds * retryCount,
                _configuration.MaxBackoffMicroseconds),
            BackoffStrategy.Exponential => Math.Min(
                _configuration.InitialBackoffMicroseconds * (1 << Math.Min(retryCount, 20)),
                _configuration.MaxBackoffMicroseconds),
            BackoffStrategy.Random => Random.Shared.Next(
                _configuration.InitialBackoffMicroseconds,
                Math.Min(_configuration.MaxBackoffMicroseconds, _configuration.InitialBackoffMicroseconds * retryCount)),
            _ => 0
        };
    }

    private class Node
    {
        public readonly T Data;
        public volatile Node? Next;

        public Node(T data)
        {
            Data = data;
            Next = null;
        }
    }

    private class HazardPointer
    {
        private volatile Node? _pointer;

        public void Set(Node? node)
        {
            _pointer = node;
        }

        public Node? Get() => _pointer;

        public void Clear()
        {
            _pointer = null;
        }
    }

    private class HazardPointerManager
    {
        private readonly ThreadLocal<HazardPointer> _hazardPointers = new(() => new HazardPointer());

        public HazardPointer AcquireHazardPointer()
        {
            return _hazardPointers.Value!;
        }

        public void ReleaseHazardPointer(HazardPointer pointer)
        {
            pointer.Clear();
        }

        public void RetireNode(Node node)
        {
            // In a full implementation, this would add the node to a retirement list
            // and periodically scan hazard pointers to safely delete nodes
            // For simplicity, we're not implementing the full hazard pointer protocol
        }
    }
}
