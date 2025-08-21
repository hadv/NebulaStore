using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// High-performance buffer manager with pooling and automatic sizing.
/// </summary>
public class BufferManager : IBufferManager
{
    private readonly string _name;
    private readonly BufferManagerConfiguration _configuration;
    private readonly ConcurrentDictionary<int, ConcurrentQueue<byte[]>> _bufferPools;
    private readonly ArrayPool<byte> _sharedArrayPool;
    private readonly BufferManagerStatistics _statistics;
    private readonly Timer? _trimmingTimer;
    private volatile bool _isDisposed;

    public BufferManager(string name, BufferManagerConfiguration? configuration = null)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _configuration = configuration ?? new BufferManagerConfiguration();
        
        if (!_configuration.IsValid())
            throw new ArgumentException("Invalid buffer manager configuration", nameof(configuration));

        _bufferPools = new ConcurrentDictionary<int, ConcurrentQueue<byte[]>>();
        _sharedArrayPool = _configuration.UseSharedArrayPool ? ArrayPool<byte>.Shared : ArrayPool<byte>.Create();
        _statistics = new BufferManagerStatistics();

        // Initialize buffer size buckets
        foreach (var size in _configuration.BufferSizeBuckets)
        {
            _bufferPools.TryAdd(size, new ConcurrentQueue<byte[]>());
        }

        // Set up trimming timer
        if (_configuration.EnableTrimming)
        {
            _trimmingTimer = new Timer(PerformTrimming, null, 
                _configuration.TrimmingInterval, _configuration.TrimmingInterval);
        }
    }

    public string Name => _name;
    public int TotalBuffers => _bufferPools.Values.Sum(queue => queue.Count) + _statistics.CurrentRentedBuffers;
    public long TotalMemoryAllocated => _statistics.CurrentMemoryUsage;
    public IBufferManagerStatistics Statistics => _statistics;

    public byte[] RentBuffer(int minimumSize)
    {
        ThrowIfDisposed();
        if (minimumSize <= 0) throw new ArgumentOutOfRangeException(nameof(minimumSize));

        var optimalSize = GetOptimalBufferSize(minimumSize);
        
        // Try to get from pool first
        if (_bufferPools.TryGetValue(optimalSize, out var pool) && pool.TryDequeue(out var buffer))
        {
            _statistics.RecordRental(buffer.Length, true);
            return buffer;
        }

        // Pool miss, allocate new buffer
        var newBuffer = new byte[optimalSize];
        _statistics.RecordRental(newBuffer.Length, false);
        _statistics.RecordAllocation(newBuffer.Length);
        
        return newBuffer;
    }

    public void ReturnBuffer(byte[] buffer, bool clearBuffer = true)
    {
        ThrowIfDisposed();
        if (buffer == null) return;

        if (clearBuffer && _configuration.ClearBuffersOnReturn)
        {
            Array.Clear(buffer, 0, buffer.Length);
        }

        var size = buffer.Length;
        
        // Find the appropriate pool
        if (_bufferPools.TryGetValue(size, out var pool))
        {
            // Check if pool has space
            if (pool.Count < _configuration.MaxBuffersPerBucket)
            {
                pool.Enqueue(buffer);
                _statistics.RecordReturn(size, true);
                return;
            }
        }

        // Pool is full or size doesn't match, discard buffer
        _statistics.RecordReturn(size, false);
    }

    public IManagedBuffer RentManagedBuffer(int minimumSize)
    {
        ThrowIfDisposed();
        var buffer = RentBuffer(minimumSize);
        return new ManagedBuffer(this, buffer);
    }

    public byte[] GetBuffer(int size)
    {
        return RentBuffer(size);
    }

    public byte[] RentSharedBuffer(int minimumSize)
    {
        ThrowIfDisposed();
        if (minimumSize <= 0) throw new ArgumentOutOfRangeException(nameof(minimumSize));

        var buffer = _sharedArrayPool.Rent(minimumSize);
        _statistics.RecordRental(buffer.Length, true); // Shared pool is always a "hit"
        return buffer;
    }

    public void ReturnSharedBuffer(byte[] buffer, bool clearBuffer = true)
    {
        ThrowIfDisposed();
        if (buffer == null) return;

        _sharedArrayPool.Return(buffer, clearBuffer && _configuration.ClearBuffersOnReturn);
        _statistics.RecordReturn(buffer.Length, true);
    }

    public IPinnedBuffer AllocatePinnedBuffer(int size)
    {
        ThrowIfDisposed();
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

        var buffer = new byte[size];
        _statistics.RecordAllocation(size);
        return new PinnedBuffer(buffer);
    }

    public int GetOptimalBufferSize(int minimumSize)
    {
        if (minimumSize <= 0) return _configuration.BufferSizeBuckets[0];

        // Find the smallest bucket that can accommodate the minimum size
        foreach (var bucketSize in _configuration.BufferSizeBuckets)
        {
            if (bucketSize >= minimumSize)
                return bucketSize;
        }

        // If no bucket is large enough, round up to the next power of 2
        return RoundUpToPowerOfTwo(minimumSize);
    }

    public int TrimBuffers()
    {
        ThrowIfDisposed();

        var trimmed = 0;
        
        foreach (var kvp in _bufferPools)
        {
            var pool = kvp.Value;
            var targetSize = _configuration.MaxBuffersPerBucket / 2; // Trim to 50% capacity
            
            while (pool.Count > targetSize && pool.TryDequeue(out _))
            {
                trimmed++;
            }
        }

        return trimmed;
    }

    public void PreloadBuffers(int[] sizes, int countPerSize = 5)
    {
        ThrowIfDisposed();
        if (sizes == null) throw new ArgumentNullException(nameof(sizes));

        foreach (var size in sizes)
        {
            var optimalSize = GetOptimalBufferSize(size);
            
            if (_bufferPools.TryGetValue(optimalSize, out var pool))
            {
                for (int i = 0; i < countPerSize && pool.Count < _configuration.MaxBuffersPerBucket; i++)
                {
                    var buffer = new byte[optimalSize];
                    pool.Enqueue(buffer);
                    _statistics.RecordAllocation(optimalSize);
                }
            }
        }
    }

    private void PerformTrimming(object? state)
    {
        try
        {
            if (!_isDisposed)
            {
                TrimBuffers();
            }
        }
        catch
        {
            // Ignore trimming errors
        }
    }

    private static int RoundUpToPowerOfTwo(int value)
    {
        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(BufferManager));
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _trimmingTimer?.Dispose();

        // Clear all pools
        foreach (var pool in _bufferPools.Values)
        {
            while (pool.TryDequeue(out _))
            {
                // Just drain the pools
            }
        }

        _bufferPools.Clear();

        // Dispose shared array pool if we created it
        if (!_configuration.UseSharedArrayPool && _sharedArrayPool is IDisposable disposablePool)
        {
            disposablePool.Dispose();
        }
    }
}

/// <summary>
/// Managed buffer that automatically returns to the pool when disposed.
/// </summary>
internal class ManagedBuffer : IManagedBuffer
{
    private readonly IBufferManager _manager;
    private byte[]? _buffer;
    private bool _isDisposed;

    public ManagedBuffer(IBufferManager manager, byte[] buffer)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
    }

    public byte[] Buffer
    {
        get
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ManagedBuffer));
            return _buffer!;
        }
    }

    public int Size => Buffer.Length;
    public bool IsDisposed => _isDisposed;

    public Span<byte> AsSpan()
    {
        return Buffer.AsSpan();
    }

    public Span<byte> AsSpan(int start, int length)
    {
        return Buffer.AsSpan(start, length);
    }

    public Memory<byte> AsMemory()
    {
        return Buffer.AsMemory();
    }

    public Memory<byte> AsMemory(int start, int length)
    {
        return Buffer.AsMemory(start, length);
    }

    public void Dispose()
    {
        if (_isDisposed || _buffer == null)
            return;

        _isDisposed = true;
        _manager.ReturnBuffer(_buffer);
        _buffer = null;
    }
}

/// <summary>
/// Pinned buffer for unmanaged interop.
/// </summary>
internal class PinnedBuffer : IPinnedBuffer
{
    private readonly byte[] _buffer;
    private readonly GCHandle _handle;
    private bool _isDisposed;

    public PinnedBuffer(byte[] buffer)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
    }

    public byte[] Buffer => _buffer;
    public int Size => _buffer.Length;
    public bool IsPinned => _handle.IsAllocated;
    public bool IsDisposed => _isDisposed;

    public MemoryHandle Handle
    {
        get
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PinnedBuffer));
            
            // Return a default memory handle (safe alternative)
            return default;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        
        if (_handle.IsAllocated)
        {
            _handle.Free();
        }
    }
}

/// <summary>
/// Thread-safe implementation of buffer manager statistics.
/// </summary>
public class BufferManagerStatistics : IBufferManagerStatistics
{
    private long _totalRentals;
    private long _totalReturns;
    private long _totalAllocations;
    private long _totalBytesAllocated;
    private long _poolHits;
    private long _currentRentedBuffers;
    private long _currentPooledBuffers;
    private long _peakMemoryUsage;
    private long _currentMemoryUsage;

    public long TotalRentals => Interlocked.Read(ref _totalRentals);
    public long TotalReturns => Interlocked.Read(ref _totalReturns);
    public long TotalAllocations => Interlocked.Read(ref _totalAllocations);
    public long TotalBytesAllocated => Interlocked.Read(ref _totalBytesAllocated);
    public int CurrentRentedBuffers => (int)Interlocked.Read(ref _currentRentedBuffers);
    public int CurrentPooledBuffers => (int)Interlocked.Read(ref _currentPooledBuffers);
    public long PeakMemoryUsage => Interlocked.Read(ref _peakMemoryUsage);
    public long CurrentMemoryUsage => Interlocked.Read(ref _currentMemoryUsage);

    public double PoolHitRatio
    {
        get
        {
            var rentals = TotalRentals;
            var hits = Interlocked.Read(ref _poolHits);
            return rentals > 0 ? (double)hits / rentals : 0.0;
        }
    }

    public double AverageUtilization
    {
        get
        {
            var rented = CurrentRentedBuffers;
            var pooled = CurrentPooledBuffers;
            var total = rented + pooled;
            return total > 0 ? (double)rented / total : 0.0;
        }
    }

    public void RecordRental(int bufferSize, bool fromPool)
    {
        Interlocked.Increment(ref _totalRentals);
        Interlocked.Increment(ref _currentRentedBuffers);

        if (fromPool)
        {
            Interlocked.Increment(ref _poolHits);
            Interlocked.Decrement(ref _currentPooledBuffers);
        }

        UpdateMemoryUsage(bufferSize);
    }

    public void RecordReturn(int bufferSize, bool toPool)
    {
        Interlocked.Increment(ref _totalReturns);
        Interlocked.Decrement(ref _currentRentedBuffers);

        if (toPool)
        {
            Interlocked.Increment(ref _currentPooledBuffers);
        }
        else
        {
            UpdateMemoryUsage(-bufferSize);
        }
    }

    public void RecordAllocation(int bufferSize)
    {
        Interlocked.Increment(ref _totalAllocations);
        Interlocked.Add(ref _totalBytesAllocated, bufferSize);
        UpdateMemoryUsage(bufferSize);
    }

    private void UpdateMemoryUsage(int delta)
    {
        var newUsage = Interlocked.Add(ref _currentMemoryUsage, delta);
        UpdatePeakMemoryUsage(newUsage);
    }

    private void UpdatePeakMemoryUsage(long currentUsage)
    {
        var currentPeak = _peakMemoryUsage;
        while (currentUsage > currentPeak)
        {
            var originalPeak = Interlocked.CompareExchange(ref _peakMemoryUsage, currentUsage, currentPeak);
            if (originalPeak == currentPeak)
                break;
            currentPeak = originalPeak;
        }
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalRentals, 0);
        Interlocked.Exchange(ref _totalReturns, 0);
        Interlocked.Exchange(ref _totalAllocations, 0);
        Interlocked.Exchange(ref _totalBytesAllocated, 0);
        Interlocked.Exchange(ref _poolHits, 0);
        Interlocked.Exchange(ref _peakMemoryUsage, 0);
        // Note: We don't reset current counters as they represent current state
    }
}
