using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

namespace NebulaStore.Storage.Embedded.Caching;

/// <summary>
/// Disk-based cache implementation with compression and efficient serialization.
/// Provides persistent storage for large datasets with automatic compression.
/// </summary>
public class DiskCache<TKey, TValue> : AbstractCache<TKey, TValue>
    where TKey : notnull
{
    private readonly string _cacheDirectory;
    private readonly bool _enableCompression;
    private readonly CompressionLevel _compressionLevel;
    private readonly ConcurrentDictionary<TKey, DiskCacheEntry> _index;
    private readonly ReaderWriterLockSlim _indexLock;
    private readonly SemaphoreSlim _diskOperationSemaphore;
    private volatile long _currentSizeInBytes;
    private readonly Timer? _compactionTimer;

    public DiskCache(
        string name,
        string cacheDirectory,
        long maxCapacity = 100000,
        long maxSizeInBytes = 1024L * 1024 * 1024, // 1GB default
        ICacheEvictionPolicy<TKey, TValue>? evictionPolicy = null,
        bool enableCompression = true,
        CompressionLevel compressionLevel = CompressionLevel.Optimal,
        TimeSpan? cleanupInterval = null,
        TimeSpan? compactionInterval = null)
        : base(
            name,
            maxCapacity,
            maxSizeInBytes,
            evictionPolicy ?? new LruEvictionPolicy<TKey, TValue>(),
            cleanupInterval ?? TimeSpan.FromMinutes(10))
    {
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        _enableCompression = enableCompression;
        _compressionLevel = compressionLevel;
        _index = new ConcurrentDictionary<TKey, DiskCacheEntry>();
        _indexLock = new ReaderWriterLockSlim();
        _diskOperationSemaphore = new SemaphoreSlim(Environment.ProcessorCount * 2); // Limit concurrent disk operations

        // Ensure cache directory exists
        Directory.CreateDirectory(_cacheDirectory);

        // Load existing index
        LoadIndex();

        // Set up compaction timer
        if (compactionInterval.HasValue && compactionInterval.Value > TimeSpan.Zero)
        {
            _compactionTimer = new Timer(PerformCompaction, null, compactionInterval.Value, compactionInterval.Value);
        }
    }

    public override long Count => _index.Count;

    public override long SizeInBytes => Interlocked.Read(ref _currentSizeInBytes);

    public override double HitRatio => _statistics.HitRatio;

    protected override TValue? GetInternal(TKey key)
    {
        if (!_index.TryGetValue(key, out var entry))
            return default;

        // Check if entry is expired
        if (entry.IsExpired)
        {
            RemoveInternal(key);
            return default;
        }

        try
        {
            var filePath = GetFilePath(entry.FileId);
            if (!File.Exists(filePath))
            {
                // File was deleted externally, remove from index
                RemoveInternal(key);
                return default;
            }

            var data = File.ReadAllBytes(filePath);
            var decompressedData = _enableCompression ? Decompress(data) : data;
            var value = MessagePackSerializer.Deserialize<TValue>(decompressedData);

            // Update access time
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            _evictionPolicy.OnEntryAccessed(CreateCacheEntry(key, value, entry));

            return value;
        }
        catch
        {
            // Error reading file, remove from index
            RemoveInternal(key);
            return default;
        }
    }

    protected override async Task<TValue?> GetInternalAsync(TKey key, CancellationToken cancellationToken)
    {
        if (!_index.TryGetValue(key, out var entry))
            return default;

        // Check if entry is expired
        if (entry.IsExpired)
        {
            RemoveInternal(key);
            return default;
        }

        await _diskOperationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetFilePath(entry.FileId);
            if (!File.Exists(filePath))
            {
                // File was deleted externally, remove from index
                RemoveInternal(key);
                return default;
            }

            var data = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var decompressedData = _enableCompression ? await DecompressAsync(data) : data;
            var value = MessagePackSerializer.Deserialize<TValue>(decompressedData);

            // Update access time
            entry.LastAccessedAt = DateTime.UtcNow;
            entry.AccessCount++;
            _evictionPolicy.OnEntryAccessed(CreateCacheEntry(key, value, entry));

            return value;
        }
        catch
        {
            // Error reading file, remove from index
            RemoveInternal(key);
            return default;
        }
        finally
        {
            _diskOperationSemaphore.Release();
        }
    }

    protected override void PutInternal(ICacheEntry<TKey, TValue> entry)
    {
        var key = entry.Key;
        var value = entry.Value;

        try
        {
            var data = MessagePackSerializer.Serialize(value);
            var compressedData = _enableCompression ? Compress(data) : data;
            var fileId = GenerateFileId(key);
            var filePath = GetFilePath(fileId);

            File.WriteAllBytes(filePath, compressedData);

            var diskEntry = new DiskCacheEntry
            {
                FileId = fileId,
                CreatedAt = entry.CreatedAt,
                LastAccessedAt = entry.LastAccessedAt,
                LastModifiedAt = entry.LastModifiedAt,
                AccessCount = entry.AccessCount,
                SizeInBytes = compressedData.Length,
                TimeToLive = entry.TimeToLive,
                Priority = entry.Priority,
                IsDirty = false
            };

            // Update index
            var oldEntry = _index.AddOrUpdate(key, diskEntry, (k, existing) =>
            {
                // Remove old file if it exists
                var oldFilePath = GetFilePath(existing.FileId);
                if (File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }
                Interlocked.Add(ref _currentSizeInBytes, -existing.SizeInBytes);
                return diskEntry;
            });

            // If this was a new entry, update size tracking
            if (ReferenceEquals(oldEntry, diskEntry))
            {
                Interlocked.Add(ref _currentSizeInBytes, diskEntry.SizeInBytes);
            }
            else
            {
                Interlocked.Add(ref _currentSizeInBytes, diskEntry.SizeInBytes);
            }
        }
        catch
        {
            // Error writing to disk, don't add to index
            throw;
        }
    }

    protected override async Task PutInternalAsync(ICacheEntry<TKey, TValue> entry, CancellationToken cancellationToken)
    {
        var key = entry.Key;
        var value = entry.Value;

        await _diskOperationSemaphore.WaitAsync(cancellationToken);
        try
        {
            var data = MessagePackSerializer.Serialize(value);
            var compressedData = _enableCompression ? await CompressAsync(data) : data;
            var fileId = GenerateFileId(key);
            var filePath = GetFilePath(fileId);

            await File.WriteAllBytesAsync(filePath, compressedData, cancellationToken);

            var diskEntry = new DiskCacheEntry
            {
                FileId = fileId,
                CreatedAt = entry.CreatedAt,
                LastAccessedAt = entry.LastAccessedAt,
                LastModifiedAt = entry.LastModifiedAt,
                AccessCount = entry.AccessCount,
                SizeInBytes = compressedData.Length,
                TimeToLive = entry.TimeToLive,
                Priority = entry.Priority,
                IsDirty = false
            };

            // Update index
            var oldEntry = _index.AddOrUpdate(key, diskEntry, (k, existing) =>
            {
                // Remove old file if it exists
                var oldFilePath = GetFilePath(existing.FileId);
                if (File.Exists(oldFilePath))
                {
                    File.Delete(oldFilePath);
                }
                Interlocked.Add(ref _currentSizeInBytes, -existing.SizeInBytes);
                return diskEntry;
            });

            // If this was a new entry, update size tracking
            if (ReferenceEquals(oldEntry, diskEntry))
            {
                Interlocked.Add(ref _currentSizeInBytes, diskEntry.SizeInBytes);
            }
            else
            {
                Interlocked.Add(ref _currentSizeInBytes, diskEntry.SizeInBytes);
            }
        }
        catch
        {
            // Error writing to disk, don't add to index
            throw;
        }
        finally
        {
            _diskOperationSemaphore.Release();
        }
    }

    protected override ICacheEntry<TKey, TValue>? RemoveInternal(TKey key)
    {
        if (_index.TryRemove(key, out var entry))
        {
            // Delete file
            var filePath = GetFilePath(entry.FileId);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            Interlocked.Add(ref _currentSizeInBytes, -entry.SizeInBytes);
            return CreateCacheEntry(key, default!, entry);
        }
        return null;
    }

    protected override IEnumerable<ICacheEntry<TKey, TValue>> GetAllEntries()
    {
        return _index.Select(kvp => CreateCacheEntry(kvp.Key, default!, kvp.Value)).ToList();
    }

    protected override IEnumerable<ICacheEntry<TKey, TValue>> GetExpiredEntries()
    {
        return _index.Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => CreateCacheEntry(kvp.Key, default!, kvp.Value))
                    .ToList();
    }

    public override bool ContainsKey(TKey key)
    {
        return _index.ContainsKey(key) && !_index[key].IsExpired;
    }

    public override IEnumerable<TKey> GetKeys()
    {
        return _index.Keys.ToList();
    }

    public override void Clear()
    {
        _indexLock.EnterWriteLock();
        try
        {
            // Delete all files
            foreach (var entry in _index.Values)
            {
                var filePath = GetFilePath(entry.FileId);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            _index.Clear();
            Interlocked.Exchange(ref _currentSizeInBytes, 0);
            _statistics.UpdateCurrentStats(0, 0);
        }
        finally
        {
            _indexLock.ExitWriteLock();
        }
    }

    public override ICacheEntryMetadata? GetEntryMetadata(TKey key)
    {
        if (_index.TryGetValue(key, out var entry))
        {
            return new CacheEntryMetadata(
                entry.CreatedAt,
                entry.LastAccessedAt,
                entry.LastModifiedAt,
                entry.AccessCount,
                entry.SizeInBytes,
                entry.Priority,
                entry.IsDirty,
                entry.IsExpired
            );
        }
        return null;
    }

    private string GetFilePath(string fileId)
    {
        return Path.Combine(_cacheDirectory, $"{fileId}.cache");
    }

    private string GenerateFileId(TKey key)
    {
        var keyString = key.ToString() ?? string.Empty;
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        return Convert.ToHexString(hash);
    }

    private byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, _compressionLevel))
        {
            gzip.Write(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private async Task<byte[]> CompressAsync(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, _compressionLevel))
        {
            await gzip.WriteAsync(data, 0, data.Length);
        }
        return output.ToArray();
    }

    private byte[] Decompress(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private async Task<byte[]> DecompressAsync(byte[] compressedData)
    {
        using var input = new MemoryStream(compressedData);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        await gzip.CopyToAsync(output);
        return output.ToArray();
    }

    private ICacheEntry<TKey, TValue> CreateCacheEntry(TKey key, TValue value, DiskCacheEntry diskEntry)
    {
        var entry = new CacheEntry<TKey, TValue>(key, value, diskEntry.TimeToLive, diskEntry.Priority);
        // Note: We can't directly update the internal metadata of CacheEntry
        // In a production implementation, you'd want a more sophisticated approach
        return entry;
    }

    private void LoadIndex()
    {
        // Load existing cache files and rebuild index
        if (!Directory.Exists(_cacheDirectory))
            return;

        var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.cache");
        long totalSize = 0;

        foreach (var filePath in cacheFiles)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                totalSize += fileInfo.Length;
                
                // For now, we'll skip loading the full index from disk
                // In a production implementation, you'd want to persist and load the index
            }
            catch
            {
                // Ignore corrupted files
            }
        }

        Interlocked.Exchange(ref _currentSizeInBytes, totalSize);
    }

    private void PerformCompaction(object? state)
    {
        try
        {
            // Remove expired entries and compact files
            ClearExpired();
            
            // Additional compaction logic could be added here
            // such as defragmenting files or merging small files
        }
        catch
        {
            // Ignore compaction errors
        }
    }

    public override void Dispose()
    {
        if (_isDisposed)
            return;

        base.Dispose();
        _compactionTimer?.Dispose();
        _indexLock.Dispose();
        _diskOperationSemaphore.Dispose();
    }
}

/// <summary>
/// Represents a disk cache entry with metadata.
/// </summary>
internal class DiskCacheEntry
{
    public string FileId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime LastModifiedAt { get; set; }
    public long AccessCount { get; set; }
    public long SizeInBytes { get; set; }
    public TimeSpan? TimeToLive { get; set; }
    public CacheEntryPriority Priority { get; set; }
    public bool IsDirty { get; set; }

    public bool IsExpired
    {
        get
        {
            if (TimeToLive == null)
                return false;
            return DateTime.UtcNow - CreatedAt > TimeToLive.Value;
        }
    }
}
