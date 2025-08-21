using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.IO;

/// <summary>
/// Interface for compression providers with different algorithms and performance characteristics.
/// </summary>
public interface ICompressionProvider
{
    /// <summary>
    /// Gets the compression algorithm name.
    /// </summary>
    string AlgorithmName { get; }

    /// <summary>
    /// Gets the compression level.
    /// </summary>
    CompressionLevel Level { get; }

    /// <summary>
    /// Gets whether this provider supports streaming compression.
    /// </summary>
    bool SupportsStreaming { get; }

    /// <summary>
    /// Compresses data synchronously.
    /// </summary>
    /// <param name="data">Data to compress</param>
    /// <returns>Compressed data</returns>
    byte[] Compress(byte[] data);

    /// <summary>
    /// Decompresses data synchronously.
    /// </summary>
    /// <param name="compressedData">Compressed data</param>
    /// <returns>Decompressed data</returns>
    byte[] Decompress(byte[] compressedData);

    /// <summary>
    /// Compresses data asynchronously.
    /// </summary>
    /// <param name="data">Data to compress</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compressed data</returns>
    Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Decompresses data asynchronously.
    /// </summary>
    /// <param name="compressedData">Compressed data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Decompressed data</returns>
    Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a compression stream.
    /// </summary>
    /// <param name="output">Output stream</param>
    /// <returns>Compression stream</returns>
    Stream CreateCompressionStream(Stream output);

    /// <summary>
    /// Creates a decompression stream.
    /// </summary>
    /// <param name="input">Input stream</param>
    /// <returns>Decompression stream</returns>
    Stream CreateDecompressionStream(Stream input);

    /// <summary>
    /// Estimates the compressed size for the given data size.
    /// </summary>
    /// <param name="originalSize">Original data size</param>
    /// <returns>Estimated compressed size</returns>
    long EstimateCompressedSize(long originalSize);

    /// <summary>
    /// Gets compression statistics.
    /// </summary>
    ICompressionStatistics Statistics { get; }
}

/// <summary>
/// Interface for compression statistics.
/// </summary>
public interface ICompressionStatistics
{
    /// <summary>
    /// Gets the total number of compression operations.
    /// </summary>
    long TotalCompressions { get; }

    /// <summary>
    /// Gets the total number of decompression operations.
    /// </summary>
    long TotalDecompressions { get; }

    /// <summary>
    /// Gets the total bytes compressed.
    /// </summary>
    long TotalBytesCompressed { get; }

    /// <summary>
    /// Gets the total bytes decompressed.
    /// </summary>
    long TotalBytesDecompressed { get; }

    /// <summary>
    /// Gets the total compressed output size.
    /// </summary>
    long TotalCompressedSize { get; }

    /// <summary>
    /// Gets the average compression ratio.
    /// </summary>
    double AverageCompressionRatio { get; }

    /// <summary>
    /// Gets the average compression time in milliseconds.
    /// </summary>
    double AverageCompressionTimeMs { get; }

    /// <summary>
    /// Gets the average decompression time in milliseconds.
    /// </summary>
    double AverageDecompressionTimeMs { get; }

    /// <summary>
    /// Gets the compression throughput in bytes per second.
    /// </summary>
    double CompressionThroughputBytesPerSecond { get; }

    /// <summary>
    /// Gets the decompression throughput in bytes per second.
    /// </summary>
    double DecompressionThroughputBytesPerSecond { get; }

    /// <summary>
    /// Resets all statistics.
    /// </summary>
    void Reset();
}

/// <summary>
/// GZip compression provider implementation.
/// </summary>
public class GZipCompressionProvider : ICompressionProvider
{
    private readonly CompressionLevel _level;
    private readonly CompressionStatistics _statistics;

    public GZipCompressionProvider(CompressionLevel level = CompressionLevel.Optimal)
    {
        _level = level;
        _statistics = new CompressionStatistics();
    }

    public string AlgorithmName => "GZip";
    public CompressionLevel Level => _level;
    public bool SupportsStreaming => true;
    public ICompressionStatistics Statistics => _statistics;

    public byte[] Compress(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var startTime = DateTime.UtcNow;
        
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, _level))
        {
            gzip.Write(data, 0, data.Length);
        }
        
        var compressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordCompression(data.Length, compressed.Length, duration);
        
        return compressed;
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));

        var startTime = DateTime.UtcNow;
        
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        gzip.CopyTo(output);
        
        var decompressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordDecompression(compressedData.Length, decompressed.Length, duration);
        
        return decompressed;
    }

    public async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var startTime = DateTime.UtcNow;
        
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, _level))
        {
            await gzip.WriteAsync(data, 0, data.Length, cancellationToken);
        }
        
        var compressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordCompression(data.Length, compressed.Length, duration);
        
        return compressed;
    }

    public async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));

        var startTime = DateTime.UtcNow;
        
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        await gzip.CopyToAsync(output, cancellationToken);
        
        var decompressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordDecompression(compressedData.Length, decompressed.Length, duration);
        
        return decompressed;
    }

    public Stream CreateCompressionStream(Stream output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        return new GZipStream(output, _level);
    }

    public Stream CreateDecompressionStream(Stream input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        return new GZipStream(input, CompressionMode.Decompress);
    }

    public long EstimateCompressedSize(long originalSize)
    {
        // GZip typically achieves 2:1 to 4:1 compression ratio for text data
        // Use conservative estimate of 50% compression
        return Math.Max(originalSize / 2, originalSize - (originalSize * 3 / 4));
    }
}

/// <summary>
/// Deflate compression provider implementation.
/// </summary>
public class DeflateCompressionProvider : ICompressionProvider
{
    private readonly CompressionLevel _level;
    private readonly CompressionStatistics _statistics;

    public DeflateCompressionProvider(CompressionLevel level = CompressionLevel.Optimal)
    {
        _level = level;
        _statistics = new CompressionStatistics();
    }

    public string AlgorithmName => "Deflate";
    public CompressionLevel Level => _level;
    public bool SupportsStreaming => true;
    public ICompressionStatistics Statistics => _statistics;

    public byte[] Compress(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var startTime = DateTime.UtcNow;
        
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, _level))
        {
            deflate.Write(data, 0, data.Length);
        }
        
        var compressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordCompression(data.Length, compressed.Length, duration);
        
        return compressed;
    }

    public byte[] Decompress(byte[] compressedData)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));

        var startTime = DateTime.UtcNow;
        
        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        deflate.CopyTo(output);
        
        var decompressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordDecompression(compressedData.Length, decompressed.Length, duration);
        
        return decompressed;
    }

    public async Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var startTime = DateTime.UtcNow;
        
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, _level))
        {
            await deflate.WriteAsync(data, 0, data.Length, cancellationToken);
        }
        
        var compressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordCompression(data.Length, compressed.Length, duration);
        
        return compressed;
    }

    public async Task<byte[]> DecompressAsync(byte[] compressedData, CancellationToken cancellationToken = default)
    {
        if (compressedData == null) throw new ArgumentNullException(nameof(compressedData));

        var startTime = DateTime.UtcNow;
        
        using var input = new MemoryStream(compressedData);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        
        await deflate.CopyToAsync(output, cancellationToken);
        
        var decompressed = output.ToArray();
        var duration = DateTime.UtcNow - startTime;
        
        _statistics.RecordDecompression(compressedData.Length, decompressed.Length, duration);
        
        return decompressed;
    }

    public Stream CreateCompressionStream(Stream output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        return new DeflateStream(output, _level);
    }

    public Stream CreateDecompressionStream(Stream input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        return new DeflateStream(input, CompressionMode.Decompress);
    }

    public long EstimateCompressedSize(long originalSize)
    {
        // Deflate typically achieves similar compression to GZip but with less overhead
        return Math.Max(originalSize / 2, originalSize - (originalSize * 3 / 4));
    }
}

/// <summary>
/// Thread-safe implementation of compression statistics.
/// </summary>
public class CompressionStatistics : ICompressionStatistics
{
    private long _totalCompressions;
    private long _totalDecompressions;
    private long _totalBytesCompressed;
    private long _totalBytesDecompressed;
    private long _totalCompressedSize;
    private long _totalCompressionTimeMs;
    private long _totalDecompressionTimeMs;
    private DateTime _startTime;

    public CompressionStatistics()
    {
        _startTime = DateTime.UtcNow;
    }

    public long TotalCompressions => Interlocked.Read(ref _totalCompressions);
    public long TotalDecompressions => Interlocked.Read(ref _totalDecompressions);
    public long TotalBytesCompressed => Interlocked.Read(ref _totalBytesCompressed);
    public long TotalBytesDecompressed => Interlocked.Read(ref _totalBytesDecompressed);
    public long TotalCompressedSize => Interlocked.Read(ref _totalCompressedSize);

    public double AverageCompressionRatio
    {
        get
        {
            var compressed = TotalBytesCompressed;
            var compressedSize = TotalCompressedSize;
            return compressed > 0 ? (double)compressedSize / compressed : 0.0;
        }
    }

    public double AverageCompressionTimeMs
    {
        get
        {
            var operations = TotalCompressions;
            return operations > 0 ? (double)Interlocked.Read(ref _totalCompressionTimeMs) / operations : 0.0;
        }
    }

    public double AverageDecompressionTimeMs
    {
        get
        {
            var operations = TotalDecompressions;
            return operations > 0 ? (double)Interlocked.Read(ref _totalDecompressionTimeMs) / operations : 0.0;
        }
    }

    public double CompressionThroughputBytesPerSecond
    {
        get
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalBytesCompressed / elapsed.TotalSeconds : 0.0;
        }
    }

    public double DecompressionThroughputBytesPerSecond
    {
        get
        {
            var elapsed = DateTime.UtcNow - _startTime;
            return elapsed.TotalSeconds > 0 ? TotalBytesDecompressed / elapsed.TotalSeconds : 0.0;
        }
    }

    public void RecordCompression(long originalSize, long compressedSize, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalCompressions);
        Interlocked.Add(ref _totalBytesCompressed, originalSize);
        Interlocked.Add(ref _totalCompressedSize, compressedSize);
        Interlocked.Add(ref _totalCompressionTimeMs, (long)duration.TotalMilliseconds);
    }

    public void RecordDecompression(long compressedSize, long decompressedSize, TimeSpan duration)
    {
        Interlocked.Increment(ref _totalDecompressions);
        Interlocked.Add(ref _totalBytesDecompressed, decompressedSize);
        Interlocked.Add(ref _totalDecompressionTimeMs, (long)duration.TotalMilliseconds);
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _totalCompressions, 0);
        Interlocked.Exchange(ref _totalDecompressions, 0);
        Interlocked.Exchange(ref _totalBytesCompressed, 0);
        Interlocked.Exchange(ref _totalBytesDecompressed, 0);
        Interlocked.Exchange(ref _totalCompressedSize, 0);
        Interlocked.Exchange(ref _totalCompressionTimeMs, 0);
        Interlocked.Exchange(ref _totalDecompressionTimeMs, 0);
        _startTime = DateTime.UtcNow;
    }
}
