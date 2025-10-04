using System;
using System.IO;
using NebulaStore.Afs.Blobstore;

namespace NebulaStore.Afs.Nio;

/// <summary>
/// Interface for NIO readable files.
/// </summary>
public interface INioReadableFile : INioFileWrapper
{
}

/// <summary>
/// NIO readable file implementation.
/// </summary>
/// <typeparam name="TUser">The type of user context</typeparam>
public class NioReadableFile<TUser> : NioFileWrapperBase<TUser>, INioReadableFile
{
    /// <summary>
    /// Creates a new readable file wrapper.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <returns>A new readable file instance</returns>
    public static NioReadableFile<TUser> New(
        BlobStorePath blobPath,
        TUser? user,
        string path)
    {
        return New(blobPath, user, path, null);
    }

    /// <summary>
    /// Creates a new readable file wrapper with an existing file stream.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <param name="fileStream">The optional file stream</param>
    /// <returns>A new readable file instance</returns>
    public static NioReadableFile<TUser> New(
        BlobStorePath blobPath,
        TUser? user,
        string path,
        FileStream? fileStream)
    {
        return new NioReadableFile<TUser>(blobPath, user, path, fileStream);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NioReadableFile{TUser}"/> class.
    /// </summary>
    /// <param name="blobPath">The blob store path</param>
    /// <param name="user">The user context</param>
    /// <param name="path">The file system path</param>
    /// <param name="fileStream">The optional file stream</param>
    protected NioReadableFile(
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
        return FileMode.Open;
    }

    /// <inheritdoc/>
    protected override FileAccess GetDefaultFileAccess()
    {
        return FileAccess.Read;
    }

    /// <inheritdoc/>
    protected override FileShare GetDefaultFileShare()
    {
        return FileShare.Read;
    }

    /// <inheritdoc/>
    protected override void ValidateOpenOptions(FileMode mode, FileAccess access, FileShare share)
    {
        // Readable files should not allow write access
        if (access.HasFlag(FileAccess.Write))
        {
            throw new ArgumentException(
                $"Invalid FileAccess for {GetType().Name}: Write access is not allowed for readable files.",
                nameof(access));
        }

        // Readable files should not allow create modes
        if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.Append)
        {
            throw new ArgumentException(
                $"Invalid FileMode for {GetType().Name}: {mode} is not allowed for readable files.",
                nameof(mode));
        }
    }
}

