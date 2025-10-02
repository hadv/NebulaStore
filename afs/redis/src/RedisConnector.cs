using System.Text.RegularExpressions;
using StackExchange.Redis;
using NebulaStore.Afs.Blobstore;
using NebulaStore.Afs.Blobstore.Types;

namespace NebulaStore.Afs.Redis;

/// <summary>
/// Redis implementation of IBlobStoreConnector.
/// Stores blobs as key-value pairs in Redis using the StackExchange.Redis client.
/// </summary>
/// <remarks>
/// This connector stores files as numbered blob entries in Redis.
/// Each blob is stored as a separate Redis key with binary data as the value.
/// 
/// First create a Redis connection:
/// <code>
/// var redis = ConnectionMultiplexer.Connect("localhost:6379");
/// var connector = RedisConnector.New(redis);
/// var fileSystem = BlobStoreFileSystem.New(connector);
/// </code>
/// </remarks>
public class RedisConnector : BlobStoreConnectorBase
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly bool _useCache;
    private readonly Dictionary<string, bool> _directoryExistsCache = new();
    private readonly Dictionary<string, bool> _fileExistsCache = new();
    private readonly Dictionary<string, long> _fileSizeCache = new();
    private readonly object _cacheLock = new();
    private readonly TimeSpan _commandTimeout;

    /// <summary>
    /// Initializes a new instance of the RedisConnector class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer</param>
    /// <param name="useCache">Whether to enable caching</param>
    /// <param name="databaseNumber">The Redis database number (default: 0)</param>
    /// <param name="commandTimeout">Command timeout (default: 1 minute)</param>
    private RedisConnector(
        IConnectionMultiplexer redis,
        bool useCache = false,
        int databaseNumber = 0,
        TimeSpan? commandTimeout = null)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _database = _redis.GetDatabase(databaseNumber);
        _useCache = useCache;
        _commandTimeout = commandTimeout ?? TimeSpan.FromMinutes(1);
    }

    /// <summary>
    /// Creates a new Redis connector.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer</param>
    /// <param name="databaseNumber">The Redis database number (default: 0)</param>
    /// <returns>A new Redis connector</returns>
    public static RedisConnector New(IConnectionMultiplexer redis, int databaseNumber = 0)
    {
        return new RedisConnector(redis, useCache: false, databaseNumber: databaseNumber);
    }

    /// <summary>
    /// Creates a new Redis connector from a connection string.
    /// </summary>
    /// <param name="connectionString">The Redis connection string</param>
    /// <param name="databaseNumber">The Redis database number (default: 0)</param>
    /// <returns>A new Redis connector</returns>
    public static RedisConnector New(string connectionString, int databaseNumber = 0)
    {
        var redis = ConnectionMultiplexer.Connect(connectionString);
        return new RedisConnector(redis, useCache: false, databaseNumber: databaseNumber);
    }

    /// <summary>
    /// Creates a new Redis connector with caching enabled.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer</param>
    /// <param name="databaseNumber">The Redis database number (default: 0)</param>
    /// <returns>A new Redis connector with caching</returns>
    public static RedisConnector Caching(IConnectionMultiplexer redis, int databaseNumber = 0)
    {
        return new RedisConnector(redis, useCache: true, databaseNumber: databaseNumber);
    }

    /// <summary>
    /// Creates a new Redis connector with caching from a connection string.
    /// </summary>
    /// <param name="connectionString">The Redis connection string</param>
    /// <param name="databaseNumber">The Redis database number (default: 0)</param>
    /// <returns>A new Redis connector with caching</returns>
    public static RedisConnector Caching(string connectionString, int databaseNumber = 0)
    {
        var redis = ConnectionMultiplexer.Connect(connectionString);
        return new RedisConnector(redis, useCache: true, databaseNumber: databaseNumber);
    }

    /// <summary>
    /// Gets all blobs for a file.
    /// </summary>
    private List<BlobMetadata> GetBlobs(BlobStorePath file)
    {
        var prefix = ToBlobKeyPrefixWithContainer(file);
        var pattern = BlobKeyRegex(prefix);
        var regex = new Regex(pattern);

        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: prefix + "*");

        var blobs = new List<BlobMetadata>();
        foreach (var key in keys)
        {
            var keyStr = key.ToString();
            if (regex.IsMatch(keyStr))
            {
                var length = _database.StringLength(key);
                blobs.Add(BlobMetadata.New(keyStr, length));
            }
        }

        // Sort by blob number
        blobs.Sort((a, b) => ExtractBlobNumber(a.Key).CompareTo(ExtractBlobNumber(b.Key)));
        return blobs;
    }

    /// <summary>
    /// Gets child keys for a directory.
    /// </summary>
    private List<string> GetChildKeys(BlobStorePath directory)
    {
        var prefix = ToChildKeysPrefixWithContainer(directory);
        var pattern = ChildKeysRegexWithContainer(directory);
        var regex = new Regex(pattern);

        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: prefix + "*");

        var childKeys = new List<string>();
        foreach (var key in keys)
        {
            var keyStr = key.ToString();
            if (regex.IsMatch(keyStr))
            {
                childKeys.Add(keyStr);
            }
        }

        return childKeys;
    }

    /// <summary>
    /// Extracts the blob number from a blob key.
    /// </summary>
    private static long ExtractBlobNumber(string key)
    {
        var lastDot = key.LastIndexOf(NumberSuffixSeparatorChar);
        if (lastDot >= 0 && lastDot < key.Length - 1)
        {
            if (long.TryParse(key.Substring(lastDot + 1), out var number))
            {
                return number;
            }
        }
        return 0;
    }

    /// <summary>
    /// Gets the next blob number for a file.
    /// </summary>
    private long GetNextBlobNumber(BlobStorePath file)
    {
        var blobs = GetBlobs(file);
        if (blobs.Count == 0)
        {
            return 0;
        }

        var maxNumber = blobs.Max(b => ExtractBlobNumber(b.Key));
        return maxNumber + 1;
    }

    /// <summary>
    /// Creates a blob key prefix with container.
    /// </summary>
    private static string ToBlobKeyPrefixWithContainer(BlobStorePath file)
    {
        return string.Join(BlobStorePath.Separator, file.PathElements) + NumberSuffixSeparator;
    }

    /// <summary>
    /// Creates a blob key with container.
    /// </summary>
    private static string ToBlobKeyWithContainer(BlobStorePath file, long number)
    {
        return ToBlobKeyPrefixWithContainer(file) + number.ToString();
    }

    /// <summary>
    /// Creates a regex pattern for blob keys.
    /// </summary>
    private static string BlobKeyRegex(string prefix)
    {
        return "^" + Regex.Escape(prefix) + "[0-9]+$";
    }

    /// <summary>
    /// Creates a child keys prefix with container.
    /// </summary>
    private static string ToChildKeysPrefixWithContainer(BlobStorePath directory)
    {
        return string.Join(BlobStorePath.Separator, directory.PathElements) + BlobStorePath.Separator;
    }

    /// <summary>
    /// Creates a regex pattern for child keys.
    /// </summary>
    private static string ChildKeysRegexWithContainer(BlobStorePath directory)
    {
        var prefix = ToChildKeysPrefixWithContainer(directory);
        return "^" + Regex.Escape(prefix) + "[^" + Regex.Escape(BlobStorePath.Separator) + "]+";
    }

    /// <summary>
    /// Invalidates cache entries for a file.
    /// </summary>
    private void InvalidateCache(string key)
    {
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache.Remove(key);
                _fileSizeCache.Remove(key);
            }
        }
    }

    public override long GetFileSize(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileSizeCache.TryGetValue(file.ToString(), out var cachedSize))
                {
                    return cachedSize;
                }
            }
        }

        var blobs = GetBlobs(file);
        var totalSize = blobs.Sum(b => b.Size);

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileSizeCache[file.ToString()] = totalSize;
            }
        }

        return totalSize;
    }

    public override bool DirectoryExists(BlobStorePath directory)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_directoryExistsCache.TryGetValue(directory.ToString(), out var cachedExists))
                {
                    return cachedExists;
                }
            }
        }

        // In Redis, directories are virtual - they exist if there are any keys with that prefix
        var result = true;

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _directoryExistsCache[directory.ToString()] = result;
            }
        }

        return result;
    }

    public override bool FileExists(BlobStorePath file)
    {
        EnsureNotDisposed();

        if (_useCache)
        {
            lock (_cacheLock)
            {
                if (_fileExistsCache.TryGetValue(file.ToString(), out var cachedExists))
                {
                    return cachedExists;
                }
            }
        }

        var blobs = GetBlobs(file);
        var exists = blobs.Count > 0;

        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache[file.ToString()] = exists;
            }
        }

        return exists;
    }

    public override void VisitChildren(BlobStorePath directory, IBlobStorePathVisitor visitor)
    {
        EnsureNotDisposed();

        var childKeys = GetChildKeys(directory);
        var directoryNames = new HashSet<string>();
        var fileNames = new HashSet<string>();

        foreach (var key in childKeys)
        {
            if (IsDirectoryKey(key))
            {
                var dirName = DirectoryNameOfKey(key);
                directoryNames.Add(dirName);
            }
            else
            {
                var fileName = FileNameOfKey(key);
                fileNames.Add(fileName);
            }
        }

        foreach (var dirName in directoryNames)
        {
            visitor.VisitDirectory(directory, dirName);
        }

        foreach (var fileName in fileNames)
        {
            visitor.VisitFile(directory, fileName);
        }
    }

    /// <summary>
    /// Extracts the directory name from a directory key.
    /// </summary>
    private static string DirectoryNameOfKey(string key)
    {
        var lastSeparator = -1;
        for (int i = key.Length - 2; i >= 0; i--)
        {
            if (key[i] == BlobStorePath.SeparatorChar)
            {
                lastSeparator = i;
                break;
            }
        }
        return key.Substring(lastSeparator + 1, key.Length - lastSeparator - 2);
    }

    /// <summary>
    /// Extracts the file name from a blob key.
    /// </summary>
    private static string FileNameOfKey(string key)
    {
        var lastSeparator = key.LastIndexOf(BlobStorePath.SeparatorChar);
        var lastDot = key.LastIndexOf(NumberSuffixSeparatorChar);
        return key.Substring(lastSeparator + 1, lastDot - lastSeparator - 1);
    }

    public override bool IsEmpty(BlobStorePath directory)
    {
        EnsureNotDisposed();
        var childKeys = GetChildKeys(directory);
        return childKeys.Count == 0;
    }

    public override bool CreateDirectory(BlobStorePath directory)
    {
        EnsureNotDisposed();

        // In Redis, directories are virtual - they don't need to be created
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _directoryExistsCache[directory.ToString()] = true;
            }
        }

        return true;
    }

    public override bool CreateFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        // Files are created on first write
        return true;
    }

    public override bool DeleteFile(BlobStorePath file)
    {
        EnsureNotDisposed();

        var blobs = GetBlobs(file);
        if (blobs.Count == 0)
        {
            return false;
        }

        var keys = blobs.Select(b => (RedisKey)b.Key).ToArray();
        var deleted = _database.KeyDelete(keys);

        InvalidateCache(file.ToString());

        return deleted == blobs.Count;
    }

    public override byte[] ReadData(BlobStorePath file, long offset, long length)
    {
        EnsureNotDisposed();

        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        var blobs = GetBlobs(file);
        var totalSize = blobs.Sum(b => b.Size);
        var actualLength = length > 0 ? length : totalSize - offset;

        if (actualLength <= 0)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[actualLength];
        var resultOffset = 0;
        var remaining = actualLength;
        var skipped = 0L;

        foreach (var blob in blobs)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (skipped + blob.Size <= offset)
            {
                skipped += blob.Size;
                continue;
            }

            var blobOffset = skipped < offset ? offset - skipped : 0;
            var amount = Math.Min(blob.Size - blobOffset, remaining);

            var data = _database.StringGetRange(blob.Key, blobOffset, blobOffset + amount - 1);
            if (data.HasValue)
            {
                var bytes = (byte[])data!;
                Array.Copy(bytes, 0, result, resultOffset, bytes.Length);
                resultOffset += bytes.Length;
                remaining -= bytes.Length;
            }

            skipped += blob.Size;
        }

        return result;
    }

    public override long ReadData(BlobStorePath file, byte[] targetBuffer, long offset, long length)
    {
        EnsureNotDisposed();

        if (length == 0)
        {
            return 0;
        }

        var data = ReadData(file, offset, length);
        Array.Copy(data, 0, targetBuffer, 0, data.Length);
        return data.Length;
    }

    public override long WriteData(BlobStorePath file, IEnumerable<byte[]> sourceBuffers)
    {
        EnsureNotDisposed();

        // Calculate total size
        var totalSize = sourceBuffers.Sum(b => b.Length);
        if (totalSize == 0)
        {
            return 0;
        }

        // Combine all buffers into one
        var combinedData = new byte[totalSize];
        var offset = 0;
        foreach (var buffer in sourceBuffers)
        {
            Array.Copy(buffer, 0, combinedData, offset, buffer.Length);
            offset += buffer.Length;
        }

        // Get next blob number
        var nextBlobNumber = GetNextBlobNumber(file);
        var blobKey = ToBlobKeyWithContainer(file, nextBlobNumber);

        // Write to Redis
        var success = _database.StringSet(blobKey, combinedData);
        if (!success)
        {
            throw new IOException($"Failed to write data to Redis key: {blobKey}");
        }

        // Update cache
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache[file.ToString()] = true;
                if (_fileSizeCache.TryGetValue(file.ToString(), out var currentSize))
                {
                    _fileSizeCache[file.ToString()] = currentSize + totalSize;
                }
                else
                {
                    _fileSizeCache[file.ToString()] = totalSize;
                }
            }
        }

        return totalSize;
    }

    public override void MoveFile(BlobStorePath sourceFile, BlobStorePath targetFile)
    {
        EnsureNotDisposed();

        // Copy the file
        CopyFile(sourceFile, targetFile, 0, -1);

        // Delete the source
        DeleteFile(sourceFile);

        // Update cache
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileExistsCache[sourceFile.ToString()] = false;
                _fileExistsCache[targetFile.ToString()] = true;

                if (_fileSizeCache.TryGetValue(sourceFile.ToString(), out var size))
                {
                    _fileSizeCache.Remove(sourceFile.ToString());
                    _fileSizeCache[targetFile.ToString()] = size;
                }
            }
        }
    }

    public override long CopyFile(BlobStorePath sourceFile, BlobStorePath targetFile, long offset, long length)
    {
        EnsureNotDisposed();

        var data = ReadData(sourceFile, offset, length);
        return WriteData(targetFile, new[] { data });
    }

    public override void TruncateFile(BlobStorePath file, long newLength)
    {
        EnsureNotDisposed();

        if (newLength == 0)
        {
            DeleteFile(file);
            return;
        }

        var blobs = GetBlobs(file);
        var currentOffset = 0L;
        BlobMetadata? targetBlob = null;
        var blobIndex = 0;

        // Find the blob that contains the truncation point
        for (int i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            var blobStart = currentOffset;
            var blobEnd = currentOffset + blob.Size - 1;

            if (blobStart <= newLength && blobEnd >= newLength)
            {
                targetBlob = blob;
                blobIndex = i;
                break;
            }

            currentOffset += blob.Size;
        }

        if (targetBlob == null)
        {
            throw new ArgumentException("New length exceeds file length");
        }

        var blobStart2 = currentOffset;

        // Delete all blobs after the target blob
        if (blobIndex < blobs.Count - 1)
        {
            var keysToDelete = blobs.Skip(blobIndex + 1).Select(b => (RedisKey)b.Key).ToArray();
            _database.KeyDelete(keysToDelete);
        }

        // If truncation point is at the start of the blob, delete it too
        if (blobStart2 == newLength)
        {
            _database.KeyDelete(targetBlob.Key);
        }
        // If truncation point is in the middle of the blob, truncate it
        else if (blobStart2 + targetBlob.Size > newLength)
        {
            var newBlobLength = newLength - blobStart2;
            var data = _database.StringGetRange(targetBlob.Key, 0, newBlobLength - 1);
            _database.KeyDelete(targetBlob.Key);
            _database.StringSet(targetBlob.Key, data);
        }

        // Update cache
        if (_useCache)
        {
            lock (_cacheLock)
            {
                _fileSizeCache[file.ToString()] = newLength;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Note: We don't dispose the Redis connection as it may be shared
            // The caller is responsible for disposing the IConnectionMultiplexer
        }
    }
}
