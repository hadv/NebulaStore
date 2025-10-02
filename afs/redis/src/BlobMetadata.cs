namespace NebulaStore.Afs.Redis;

/// <summary>
/// Metadata for a blob stored in Redis.
/// Contains the key and size information for a blob.
/// </summary>
public sealed class BlobMetadata
{
    /// <summary>
    /// Gets the Redis key for this blob.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the size of the blob in bytes.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Initializes a new instance of the BlobMetadata class.
    /// </summary>
    /// <param name="key">The Redis key</param>
    /// <param name="size">The blob size in bytes</param>
    /// <exception cref="ArgumentException">Thrown if key is null or empty</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if size is negative</exception>
    private BlobMetadata(string key, long size)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Size cannot be negative");

        Key = key;
        Size = size;
    }

    /// <summary>
    /// Creates a new BlobMetadata instance.
    /// </summary>
    /// <param name="key">The Redis key</param>
    /// <param name="size">The blob size in bytes</param>
    /// <returns>A new BlobMetadata instance</returns>
    public static BlobMetadata New(string key, long size)
    {
        return new BlobMetadata(key, size);
    }

    /// <summary>
    /// Returns a string representation of this blob metadata.
    /// </summary>
    public override string ToString()
    {
        return $"BlobMetadata(Key={Key}, Size={Size})";
    }
}

