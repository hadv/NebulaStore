using System;
using System.Threading.Tasks;

namespace NebulaStore.Storage;

/// <summary>
/// Interface for storage connections.
/// Manages the connection to the underlying storage system.
/// </summary>
public interface IStorageConnection : IDisposable
{
    /// <summary>
    /// Creates a new storer instance.
    /// </summary>
    /// <returns>A new storer instance</returns>
    IStorer CreateStorer();

    /// <summary>
    /// Gets a value indicating whether this connection is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Issues a full garbage collection.
    /// </summary>
    void IssueFullGarbageCollection();

    /// <summary>
    /// Issues a garbage collection with the specified time budget.
    /// </summary>
    /// <param name="timeBudgetNanos">The time budget in nanoseconds</param>
    /// <returns>True if garbage collection completed within the time budget</returns>
    bool IssueGarbageCollection(long timeBudgetNanos);

    /// <summary>
    /// Creates a backup of the storage data.
    /// </summary>
    /// <param name="backupDirectory">The backup directory path</param>
    /// <returns>A task representing the backup operation</returns>
    Task CreateBackupAsync(string backupDirectory);

    /// <summary>
    /// Gets storage statistics.
    /// </summary>
    /// <returns>Storage statistics</returns>
    IStorageStatistics GetStatistics();
}
