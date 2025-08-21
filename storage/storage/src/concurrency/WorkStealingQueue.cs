using System;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Concurrency;

/// <summary>
/// High-performance work-stealing queue implementation for load balancing across threads.
/// Uses a double-ended queue with lock-free operations for the owner thread and minimal locking for stealing.
/// </summary>
/// <typeparam name="T">Type of work items</typeparam>
public class WorkStealingQueue<T> : IWorkStealingQueue<T>
{
    private readonly string _name;
    private readonly WorkStealingConfiguration _configuration;
    private readonly WorkStealingStatistics _statistics;
    private readonly object _stealLock = new();
    
    private T[] _array;
    private volatile int _head; // For stealing (FIFO)
    private volatile int _tail; // For local operations (LIFO)
    private volatile bool _isDisposed;

    public WorkStealingQueue(string name, WorkStealingConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new WorkStealingConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid work-stealing configuration", nameof(configuration));

        _statistics = new WorkStealingStatistics(_configuration);
        _array = new T[_configuration.InitialCapacity];
        _head = 0;
        _tail = 0;
    }

    public string Name => _name;

    public int Count
    {
        get
        {
            var head = _head;
            var tail = _tail;
            return Math.Max(0, tail - head);
        }
    }

    public bool IsEmpty => Count == 0;

    public IWorkStealingStatistics Statistics => _statistics;

    public double LoadFactor
    {
        get
        {
            var capacity = _array.Length;
            var count = Count;
            return capacity > 0 ? (double)count / capacity : 0.0;
        }
    }

    public bool IsStealingCandidate => LoadFactor >= _configuration.StealingThreshold;

    public void LocalPush(T item)
    {
        ThrowIfDisposed();
        
        var tail = _tail;
        var head = _head;
        var array = _array;

        // Check if we need to resize
        if (tail - head >= array.Length - 1)
        {
            if (_configuration.EnableDynamicResizing)
            {
                ResizeArray();
                array = _array; // Get the new array
            }
            else
            {
                throw new InvalidOperationException("Queue is full and dynamic resizing is disabled");
            }
        }

        // Store the item
        array[tail % array.Length] = item;
        
        // Update tail (this makes the item visible to stealers)
        _tail = tail + 1;
        
        _statistics.RecordLocalPush();
    }

    public bool LocalTryPop(out T result)
    {
        ThrowIfDisposed();
        result = default(T)!;
        
        var tail = _tail;
        var head = _head;
        
        if (tail <= head)
        {
            // Queue is empty
            return false;
        }

        // Decrement tail first (this hides the item from stealers)
        tail = --_tail;
        
        var array = _array;
        result = array[tail % array.Length];
        
        // Check if there was a race with a stealer
        if (tail <= _head)
        {
            // Race detected, restore tail and use the steal lock
            _tail = tail + 1;
            
            lock (_stealLock)
            {
                if (tail <= _head)
                {
                    // Stealer won the race
                    result = default(T)!;
                    return false;
                }
                
                // We won the race, decrement tail again
                _tail = tail;
            }
        }
        
        _statistics.RecordLocalPop();
        return true;
    }

    public bool TrySteal(out T result)
    {
        ThrowIfDisposed();
        result = default(T)!;
        
        lock (_stealLock)
        {
            var head = _head;
            var tail = _tail;
            
            if (head >= tail)
            {
                // Queue is empty
                _statistics.RecordStealAttempt(false);
                return false;
            }
            
            var array = _array;
            result = array[head % array.Length];
            
            // Update head (this removes the item from the queue)
            _head = head + 1;
            
            _statistics.RecordStealAttempt(true);
            return true;
        }
    }

    private void ResizeArray()
    {
        lock (_stealLock)
        {
            var oldArray = _array;
            var oldCapacity = oldArray.Length;
            var newCapacity = Math.Min((int)(oldCapacity * _configuration.ResizeFactor), _configuration.MaxCapacity);
            
            if (newCapacity <= oldCapacity)
            {
                throw new InvalidOperationException("Cannot resize queue beyond maximum capacity");
            }
            
            var newArray = new T[newCapacity];
            var head = _head;
            var tail = _tail;
            var count = tail - head;
            
            // Copy existing items to the new array
            for (int i = 0; i < count; i++)
            {
                newArray[i] = oldArray[(head + i) % oldCapacity];
            }
            
            // Update the array and indices
            _array = newArray;
            _head = 0;
            _tail = count;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WorkStealingQueue<T>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        
        // Clear the array
        Array.Clear(_array, 0, _array.Length);
    }
}

/// <summary>
/// Thread-safe statistics implementation for work-stealing queues.
/// </summary>
public class WorkStealingStatistics : IWorkStealingStatistics
{
    private readonly WorkStealingConfiguration _configuration;
    private long _totalLocalPushes;
    private long _totalLocalPops;
    private long _totalStealAttempts;
    private long _totalSuccessfulSteals;
    private long _totalUtilizationSamples;
    private long _totalUtilization;
    private int _peakQueueSize;

    public WorkStealingStatistics(WorkStealingConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public long TotalLocalPushes => Interlocked.Read(ref _totalLocalPushes);
    public long TotalLocalPops => Interlocked.Read(ref _totalLocalPops);
    public long TotalStealAttempts => Interlocked.Read(ref _totalStealAttempts);
    public long TotalSuccessfulSteals => Interlocked.Read(ref _totalSuccessfulSteals);

    public double StealSuccessRatio
    {
        get
        {
            var attempts = TotalStealAttempts;
            var successful = TotalSuccessfulSteals;
            return attempts > 0 ? (double)successful / attempts : 0.0;
        }
    }

    public double AverageUtilization
    {
        get
        {
            var samples = Interlocked.Read(ref _totalUtilizationSamples);
            var utilization = Interlocked.Read(ref _totalUtilization);
            return samples > 0 ? (double)utilization / samples : 0.0;
        }
    }

    public int PeakQueueSize => Interlocked.Read(ref _peakQueueSize);

    public double ContentionLevel
    {
        get
        {
            var attempts = TotalStealAttempts;
            var pushes = TotalLocalPushes;
            var totalOperations = attempts + pushes;
            return totalOperations > 0 ? (double)attempts / totalOperations : 0.0;
        }
    }

    public void RecordLocalPush()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalLocalPushes);
        }
    }

    public void RecordLocalPop()
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalLocalPops);
        }
    }

    public void RecordStealAttempt(bool successful)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalStealAttempts);
            
            if (successful)
            {
                Interlocked.Increment(ref _totalSuccessfulSteals);
            }
        }
    }

    public void RecordUtilization(double utilization)
    {
        if (_configuration.EnableStatistics)
        {
            Interlocked.Increment(ref _totalUtilizationSamples);
            Interlocked.Add(ref _totalUtilization, (long)(utilization * 1000)); // Store as per-mille
        }
    }

    public void RecordQueueSize(int size)
    {
        if (_configuration.EnableStatistics)
        {
            UpdatePeakQueueSize(size);
        }
    }

    private void UpdatePeakQueueSize(int currentSize)
    {
        var currentPeak = _peakQueueSize;
        while (currentSize > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakQueueSize, currentSize, currentPeak);
            if (originalPeak == currentPeak)
                break;
            currentPeak = originalPeak;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalLocalPushes, 0);
        Interlocked.Exchange(ref _totalLocalPops, 0);
        Interlocked.Exchange(ref _totalStealAttempts, 0);
        Interlocked.Exchange(ref _totalSuccessfulSteals, 0);
        Interlocked.Exchange(ref _totalUtilizationSamples, 0);
        Interlocked.Exchange(ref _totalUtilization, 0);
        Interlocked.Exchange(ref _peakQueueSize, 0);
    }
}

/// <summary>
/// Work-stealing scheduler that coordinates multiple work-stealing queues for optimal load balancing.
/// </summary>
/// <typeparam name="T">Type of work items</typeparam>
public class WorkStealingScheduler<T> : IDisposable
{
    private readonly WorkStealingConfiguration _configuration;
    private readonly ThreadLocal<WorkStealingQueue<T>> _localQueues;
    private readonly WorkStealingQueue<T>[] _allQueues;
    private readonly Random _random;
    private volatile bool _isDisposed;

    public WorkStealingScheduler(int workerCount, WorkStealingConfiguration? configuration = null)
    {
        if (workerCount <= 0) throw new ArgumentOutOfRangeException(nameof(workerCount));

        _configuration = configuration ?? new WorkStealingConfiguration();
        _allQueues = new WorkStealingQueue<T>[workerCount];
        _random = new Random();

        // Initialize queues
        for (int i = 0; i < workerCount; i++)
        {
            _allQueues[i] = new WorkStealingQueue<T>($"WorkerQueue_{i}", _configuration);
        }

        // Set up thread-local queue assignment
        var queueIndex = 0;
        _localQueues = new ThreadLocal<WorkStealingQueue<T>>(() =>
        {
            var index = Interlocked.Increment(ref queueIndex) % workerCount;
            return _allQueues[index];
        });
    }

    /// <summary>
    /// Gets the number of worker queues.
    /// </summary>
    public int WorkerCount => _allQueues.Length;

    /// <summary>
    /// Gets the total number of work items across all queues.
    /// </summary>
    public int TotalWorkItems => _allQueues.Sum(q => q.Count);

    /// <summary>
    /// Enqueues a work item to the current thread's local queue.
    /// </summary>
    /// <param name="item">Work item to enqueue</param>
    public void Enqueue(T item)
    {
        ThrowIfDisposed();
        var localQueue = _localQueues.Value!;
        localQueue.LocalPush(item);
    }

    /// <summary>
    /// Attempts to dequeue a work item, first from the local queue, then by stealing.
    /// </summary>
    /// <param name="result">The dequeued work item if successful</param>
    /// <returns>True if a work item was dequeued, false if no work is available</returns>
    public bool TryDequeue(out T result)
    {
        ThrowIfDisposed();
        result = default(T)!;

        var localQueue = _localQueues.Value!;

        // Try local queue first (LIFO for better cache locality)
        if (localQueue.LocalTryPop(out result))
        {
            return true;
        }

        // Local queue is empty, try stealing from other queues
        return TryStealWork(out result);
    }

    private bool TryStealWork(out T result)
    {
        result = default(T)!;

        // Find queues that are good candidates for stealing
        var candidates = _allQueues.Where(q => q.IsStealingCandidate).ToArray();

        if (candidates.Length == 0)
        {
            // No good candidates, try all queues
            candidates = _allQueues;
        }

        // Randomize the order to avoid bias
        for (int i = 0; i < candidates.Length; i++)
        {
            var randomIndex = _random.Next(candidates.Length);
            if (candidates[randomIndex].TrySteal(out result))
            {
                return true;
            }
        }

        return false;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(WorkStealingScheduler<T>));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _localQueues.Dispose();

        foreach (var queue in _allQueues)
        {
            queue.Dispose();
        }
    }
}
