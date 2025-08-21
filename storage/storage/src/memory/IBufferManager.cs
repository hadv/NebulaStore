using System;
using System.Buffers;

namespace NebulaStore.Storage.Embedded.Memory;

/// <summary>
/// Interface for high-performance buffer management with pooling and automatic sizing.
/// </summary>
public interface IBufferManager : IDisposable
{
    /// <summary>
    /// Gets the buffer manager name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the total number of buffers managed.
    /// </summary>
    int TotalBuffers { get; }

    /// <summary>
    /// Gets the total memory allocated in bytes.
    /// </summary>
    long TotalMemoryAllocated { get; }

    /// <summary>
    /// Gets buffer management statistics.
    /// </summary>
    IBufferManagerStatistics Statistics { get; }

    /// <summary>
    /// Rents a buffer of at least the specified size.
    /// </summary>
    /// <param name="minimumSize">Minimum buffer size required</param>
    /// <returns>Rented buffer</returns>
    byte[] RentBuffer(int minimumSize);

    /// <summary>
    /// Gets a buffer of the specified size (alias for RentBuffer for compatibility).
    /// </summary>
    /// <param name="size">Buffer size required</param>
    /// <returns>Buffer</returns>
    byte[] GetBuffer(int size);

    /// <summary>
    /// Returns a buffer to the pool.
    /// </summary>
    /// <param name="buffer">Buffer to return</param>
    /// <param name="clearBuffer">Whether to clear the buffer contents</param>
    void ReturnBuffer(byte[] buffer, bool clearBuffer = true);

    /// <summary>
    /// Rents a managed buffer that automatically returns to the pool when disposed.
    /// </summary>
    /// <param name="minimumSize">Minimum buffer size required</param>
    /// <returns>Managed buffer</returns>
    IManagedBuffer RentManagedBuffer(int minimumSize);

    /// <summary>
    /// Rents a buffer from the shared array pool.
    /// </summary>
    /// <param name="minimumSize">Minimum buffer size required</param>
    /// <returns>Rented buffer</returns>
    byte[] RentSharedBuffer(int minimumSize);

    /// <summary>
    /// Returns a buffer to the shared array pool.
    /// </summary>
    /// <param name="buffer">Buffer to return</param>
    /// <param name="clearBuffer">Whether to clear the buffer contents</param>
    void ReturnSharedBuffer(byte[] buffer, bool clearBuffer = true);

    /// <summary>
    /// Allocates a pinned buffer for unmanaged interop.
    /// </summary>
    /// <param name="size">Buffer size</param>
    /// <returns>Pinned buffer</returns>
    IPinnedBuffer AllocatePinnedBuffer(int size);

    /// <summary>
    /// Gets the optimal buffer size for the given minimum size.
    /// </summary>
    /// <param name="minimumSize">Minimum required size</param>
    /// <returns>Optimal buffer size</returns>
    int GetOptimalBufferSize(int minimumSize);

    /// <summary>
    /// Trims unused buffers to reduce memory usage.
    /// </summary>
    /// <returns>Number of buffers trimmed</returns>
    int TrimBuffers();

    /// <summary>
    /// Preloads buffers of common sizes.
    /// </summary>
    /// <param name="sizes">Buffer sizes to preload</param>
    /// <param name="countPerSize">Number of buffers per size</param>
    void PreloadBuffers(int[] sizes, int countPerSize = 5);
}

/// <summary>
/// Interface for managed buffers that automatically return to the pool.
/// </summary>
public interface IManagedBuffer : IDisposable
{
    /// <summary>
    /// Gets the buffer array.
    /// </summary>
    byte[] Buffer { get; }

    /// <summary>
    /// Gets the buffer size.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    bool IsDisposed { get; }

    /// <summary>
    /// Gets a span over the buffer.
    /// </summary>
    /// <returns>Buffer span</returns>
    Span<byte> AsSpan();

    /// <summary>
    /// Gets a span over a portion of the buffer.
    /// </summary>
    /// <param name="start">Start index</param>
    /// <param name="length">Length</param>
    /// <returns>Buffer span</returns>
    Span<byte> AsSpan(int start, int length);

    /// <summary>
    /// Gets a memory over the buffer.
    /// </summary>
    /// <returns>Buffer memory</returns>
    Memory<byte> AsMemory();

    /// <summary>
    /// Gets a memory over a portion of the buffer.
    /// </summary>
    /// <param name="start">Start index</param>
    /// <param name="length">Length</param>
    /// <returns>Buffer memory</returns>
    Memory<byte> AsMemory(int start, int length);
}

/// <summary>
/// Interface for pinned buffers for unmanaged interop.
/// </summary>
public interface IPinnedBuffer : IDisposable
{
    /// <summary>
    /// Gets the buffer array.
    /// </summary>
    byte[] Buffer { get; }

    /// <summary>
    /// Gets the buffer size.
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Gets the pinned memory handle.
    /// </summary>
    MemoryHandle Handle { get; }

    /// <summary>
    /// Gets whether the buffer is pinned.
    /// </summary>
    bool IsPinned { get; }

    /// <summary>
    /// Gets whether the buffer has been disposed.
    /// </summary>
    bool IsDisposed { get; }
}

/// <summary>
/// Interface for buffer manager statistics.
/// </summary>
public interface IBufferManagerStatistics
{
    /// <summary>
    /// Gets the total number of buffer rentals.
    /// </summary>
    long TotalRentals { get; }

    /// <summary>
    /// Gets the total number of buffer returns.
    /// </summary>
    long TotalReturns { get; }

    /// <summary>
    /// Gets the total number of buffer allocations.
    /// </summary>
    long TotalAllocations { get; }

    /// <summary>
    /// Gets the total bytes allocated.
    /// </summary>
    long TotalBytesAllocated { get; }

    /// <summary>
    /// Gets the current number of rented buffers.
    /// </summary>
    int CurrentRentedBuffers { get; }

    /// <summary>
    /// Gets the current number of pooled buffers.
    /// </summary>
    int CurrentPooledBuffers { get; }

    /// <summary>
    /// Gets the pool hit ratio.
    /// </summary>
    double PoolHitRatio { get; }

    /// <summary>
    /// Gets the average buffer utilization.
    /// </summary>
    double AverageUtilization { get; }

    /// <summary>
    /// Gets the peak memory usage in bytes.
    /// </summary>
    long PeakMemoryUsage { get; }

    /// <summary>
    /// Gets the current memory usage in bytes.
    /// </summary>
    long CurrentMemoryUsage { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Configuration for buffer managers.
/// </summary>
public class BufferManagerConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of buffers per size bucket.
    /// </summary>
    public int MaxBuffersPerBucket { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum total memory to allocate.
    /// </summary>
    public long MaxTotalMemory { get; set; } = 100 * 1024 * 1024; // 100MB

    /// <summary>
    /// Gets or sets whether to use the shared array pool.
    /// </summary>
    public bool UseSharedArrayPool { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable buffer trimming.
    /// </summary>
    public bool EnableTrimming { get; set; } = true;

    /// <summary>
    /// Gets or sets the trimming interval.
    /// </summary>
    public TimeSpan TrimmingInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Gets or sets the buffer size buckets.
    /// </summary>
    public int[] BufferSizeBuckets { get; set; } = new[]
    {
        1024,      // 1KB
        4096,      // 4KB
        16384,     // 16KB
        65536,     // 64KB
        262144,    // 256KB
        1048576,   // 1MB
        4194304    // 4MB
    };

    /// <summary>
    /// Gets or sets whether to enable statistics collection.
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to clear buffers on return.
    /// </summary>
    public bool ClearBuffersOnReturn { get; set; } = true;

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>True if valid, false otherwise</returns>
    public bool IsValid()
    {
        return MaxBuffersPerBucket > 0 &&
               MaxTotalMemory > 0 &&
               TrimmingInterval > TimeSpan.Zero &&
               BufferSizeBuckets != null &&
               BufferSizeBuckets.Length > 0 &&
               BufferSizeBuckets.All(size => size > 0);
    }

    /// <summary>
    /// Creates a copy of this configuration.
    /// </summary>
    /// <returns>A new configuration instance with the same values</returns>
    public BufferManagerConfiguration Clone()
    {
        return new BufferManagerConfiguration
        {
            MaxBuffersPerBucket = MaxBuffersPerBucket,
            MaxTotalMemory = MaxTotalMemory,
            UseSharedArrayPool = UseSharedArrayPool,
            EnableTrimming = EnableTrimming,
            TrimmingInterval = TrimmingInterval,
            BufferSizeBuckets = (int[])BufferSizeBuckets.Clone(),
            EnableStatistics = EnableStatistics,
            ClearBuffersOnReturn = ClearBuffersOnReturn
        };
    }

    public override string ToString()
    {
        return $"BufferManagerConfiguration[MaxBuffersPerBucket={MaxBuffersPerBucket}, " +
               $"MaxTotalMemory={MaxTotalMemory:N0}, UseSharedPool={UseSharedArrayPool}, " +
               $"Trimming={EnableTrimming}, Statistics={EnableStatistics}]";
    }
}
