using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// Lock-free queue implementation using the Michael & Scott algorithm.
/// Provides high-performance concurrent enqueue and dequeue operations.
/// </summary>
/// <typeparam name="T">Type of elements in the queue</typeparam>
public class LockFreeQueue<T> : ILockFreeQueue<T>
{
    private readonly LockFreeConfiguration _configuration;
    private readonly LockFreeStatistics _statistics;
    private volatile Node _head;
    private volatile Node _tail;

    public LockFreeQueue(LockFreeConfiguration? configuration = null)
    {
        _configuration = configuration ?? new LockFreeConfiguration();
        _statistics = new LockFreeStatistics(_configuration);

        // Initialize with a dummy node
        var dummy = new Node(default(T)!);
        _head = _tail = dummy;
    }

    public int Count
    {
        get
        {
            // Approximate count - not guaranteed to be exact in concurrent scenarios
            int count = 0;
            var current = _head.Next;
            while (current != null && count < 10000) // Prevent infinite loops
            {
                count++;
                current = current.Next;
            }
            return count;
        }
    }

    public bool IsEmpty => _head.Next == null;

    public ILockFreeStatistics Statistics => _statistics;

    public void Enqueue(T item)
    {
        var newNode = new Node(item);
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var tail = _tail;
            var next = tail.Next;

            // Check if tail is still the last node
            if (tail == _tail)
            {
                if (next == null)
                {
                    // Try to link the new node at the end of the list
                    if (Interlocked.CompareExchange(ref tail.Next, newNode, null) == null)
                    {
                        // Successfully linked, now try to swing tail to the new node
                        Interlocked.CompareExchange(ref _tail, newNode, tail);
                        _statistics.RecordSuccessfulOperation();
                        return;
                    }
                }
                else
                {
                    // Tail is lagging, try to advance it
                    Interlocked.CompareExchange(ref _tail, next, tail);
                }
            }

            retryCount++;
            _statistics.RecordRetryAttempt();
            
            if (_configuration.BackoffStrategy != BackoffStrategy.None)
            {
                PerformBackoff(retryCount);
            }
        }

        _statistics.RecordFailedOperation();
        throw new InvalidOperationException($"Failed to enqueue after {_configuration.MaxRetryAttempts} attempts");
    }

    public bool TryDequeue(out T result)
    {
        result = default(T)!;
        var retryCount = 0;

        while (retryCount < _configuration.MaxRetryAttempts)
        {
            _statistics.RecordOperationAttempt();

            var head = _head;
            var tail = _tail;
            var next = head.Next;

            // Check if head is still the first node
            if (head == _head)
            {
                if (head == tail)
                {
                    if (next == null)
                    {
                        // Queue is empty
                        _statistics.RecordSuccessfulOperation();
                        return false;
                    }

                    // Tail is lagging, try to advance it
                    Interlocked.CompareExchange(ref _tail, next, tail);
                }
                else
                {
                    if (next != null)
                    {
                        // Read value before CAS, otherwise another dequeue might free the next node
                        result = next.Data;

                        // Try to swing head to the next node
                        if (Interlocked.CompareExchange(ref _head, next, head) == head)
                        {
                            _statistics.RecordSuccessfulOperation();
                            return true;
                        }
                    }
                }
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

            var head = _head;
            var next = head.Next;

            if (next == null)
            {
                // Queue is empty
                _statistics.RecordSuccessfulOperation();
                return false;
            }

            // Check if head is still the same
            if (head == _head)
            {
                result = next.Data;
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
    /// Node class for the lock-free queue.
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
/// Thread-safe statistics implementation for lock-free data structures.
/// </summary>
public class LockFreeStatistics : ILockFreeStatistics
{
    private readonly LockFreeConfiguration _configuration;
    private long _totalOperations;
    private long _successfulOperations;
    private long _failedOperations;
    private long _retryAttempts;
    private readonly CircularBuffer _contentionWindow;

    public LockFreeStatistics(LockFreeConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _contentionWindow = new CircularBuffer(_configuration.ContentionWindowSize);
    }

    public long TotalOperations => Interlocked.Read(ref _totalOperations);
    public long SuccessfulOperations => Interlocked.Read(ref _successfulOperations);
    public long FailedOperations => Interlocked.Read(ref _failedOperations);
    public long RetryAttempts => Interlocked.Read(ref _retryAttempts);

    public double SuccessRatio
    {
        get
        {
            var total = TotalOperations;
            var successful = SuccessfulOperations;
            return total > 0 ? (double)successful / total : 0.0;
        }
    }

    public double AverageRetryCount
    {
        get
        {
            var total = TotalOperations;
            var retries = RetryAttempts;
            return total > 0 ? (double)retries / total : 0.0;
        }
    }

    public double ContentionLevel => _configuration.EnableContentionMonitoring ? _contentionWindow.GetContentionLevel() : 0.0;

    public void RecordOperationAttempt()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalOperations);
        }
    }

    public void RecordSuccessfulOperation()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _successfulOperations);
            
            if (_configuration.EnableContentionMonitoring)
            {
                _contentionWindow.RecordSuccess();
            }
        }
    }

    public void RecordFailedOperation()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _failedOperations);
            
            if (_configuration.EnableContentionMonitoring)
            {
                _contentionWindow.RecordFailure();
            }
        }
    }

    public void RecordRetryAttempt()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _retryAttempts);
            
            if (_configuration.EnableContentionMonitoring)
            {
                _contentionWindow.RecordRetry();
            }
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalOperations, 0);
        Interlocked.Exchange(ref _successfulOperations, 0);
        Interlocked.Exchange(ref _failedOperations, 0);
        Interlocked.Exchange(ref _retryAttempts, 0);
        _contentionWindow.Reset();
    }

    /// <summary>
    /// Circular buffer for tracking contention over a sliding window.
    /// </summary>
    private class CircularBuffer
    {
        private readonly int[] _buffer;
        private volatile int _index;
        private readonly int _size;

        public CircularBuffer(int size)
        {
            _size = size;
            _buffer = new int[size];
        }

        public void RecordSuccess()
        {
            RecordEvent(0); // 0 = success
        }

        public void RecordFailure()
        {
            RecordEvent(2); // 2 = failure (higher contention)
        }

        public void RecordRetry()
        {
            RecordEvent(1); // 1 = retry (medium contention)
        }

        private void RecordEvent(int value)
        {
            var currentIndex = Interlocked.Increment(ref _index) % _size;
            _buffer[currentIndex] = value;
        }

        public double GetContentionLevel()
        {
            var sum = 0;
            var count = 0;
            
            for (int i = 0; i < _size; i++)
            {
                var value = _buffer[i];
                sum += value;
                count++;
            }

            return count > 0 ? (double)sum / (count * 2) : 0.0; // Normalize to 0-1 range
        }

        public void Reset()
        {
            Array.Clear(_buffer, 0, _size);
            Interlocked.Exchange(ref _index, 0);
        }
    }
}
