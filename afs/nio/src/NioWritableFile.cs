using System;
using System.IO;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO writable files.
/// </summary>
public interface INioWritableFile : INioReadableFile
{
}

/// <summary>
/// NIO writable file implementation.
/// </summary>
/// <typeparam name="TUser">The type of user context</typeparam>
public class NioWritableFile<TUser> : NioReadableFile<TUser>, INioWritableFile
{
    /// <summary>
    /// Creates a new writable file wrapper.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <returns>A new writable file instance</returns>
    public new static NioWritableFile<TUser> New(
        BlobStorePath blobPath,
        TUser? user,
        string path)
    {
        return New(blobPath, user, path, null);
    }

    /// <summary>
    /// Creates a new writable file wrapper with an existing file stream.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <param name="fileStream">The optional file stream</param>
    /// <returns>A new writable file instance</returns>
    public new static NioWritableFile<TUser> New(
        BlobStorePath blobPath,
        TUser? user,
        string path,
        FileStream? fileStream)
    {
        return new NioWritableFile<TUser>(blobPath, user, path, fileStream);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NioWritableFile{TUser}"/> class.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <param name="fileStream">The optional file stream</param>
    protected NioWritableFile(
        BlobStorePath blobPath,
        TUser? user,
        string path,
        FileStream? fileStream)
        : base(blobPath, user, path, fileStream)
    {
    }

    /// <inheritdoc/>
    protected override FileMode GetDefaultFileMode()
    {
        return FileMode.OpenOrCreate;
    }

    /// <inheritdoc/>
    protected override FileAccess GetDefaultFileAccess()
    {
        return FileAccess.ReadWrite;
    }

    /// <inheritdoc/>
    protected override FileShare GetDefaultFileShare()
    {
        return FileShare.None;
    }

    /// <inheritdoc/>
    protected override void ValidateOpenOptions(FileMode mode, FileAccess access, FileShare share)
    {
        // Writable files allow all options, so no validation needed
        // Override the base class validation that restricts write access
    }
}

