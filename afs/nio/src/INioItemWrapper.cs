using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO item wrappers that provide access to file system paths.
/// </summary>
public interface INioItemWrapper
{
    /// <summary>
    /// Gets the file system path for this item.
    /// </summary>
    string Path { get; }
    
    /// <summary>
    /// Gets the blob store path representation.
    /// </summary>
    BlobStorePath BlobPath { get; }
}

