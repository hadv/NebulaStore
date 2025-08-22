using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Ultra-thin delegating type that connects the application context to a storage instance via a Persistence layer.
/// Note that this is a rather "internal" type that users usually do not have to use or care about.
/// Since IStorageManager implements this interface, it is normally sufficient to use just that.
/// </summary>
public interface IStorageConnection : IStorer
{
    /// <summary>
    /// Issues a full garbage collection to be executed.
    /// Garbage collection marks all persisted objects/records that are reachable from the root and
    /// deletes all non-marked records.
    /// </summary>
    void IssueFullGarbageCollection();

    /// <summary>
    /// Issues garbage collection to be executed, limited to the time budget specified.
    /// </summary>
    /// <param name="timeBudget">The time budget to be used to perform garbage collection.</param>
    /// <returns>Whether the call has completed garbage collection.</returns>
    bool IssueGarbageCollection(TimeSpan timeBudget);

    /// <summary>
    /// Issues a full storage file check to be executed.
    /// File checking evaluates every storage data file about being either too small, too big
    /// or having too many logical "gaps" in it.
    /// </summary>
    void IssueFullFileCheck();

    /// <summary>
    /// Issues a storage file check to be executed, limited to the time budget specified.
    /// </summary>
    /// <param name="timeBudget">The time budget to be used to perform file checking.</param>
    /// <returns>Whether the call has completed file checking.</returns>
    bool IssueFileCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues a full storage cache check to be executed.
    /// Cache checking evaluates every cache entity data about being worth to be kept in cache.
    /// </summary>
    void IssueFullCacheCheck();

    /// <summary>
    /// Issues a storage cache check to be executed, limited to the time budget specified.
    /// </summary>
    /// <param name="timeBudget">The time budget to be used to perform cache checking.</param>
    /// <returns>Whether the used cache size is 0 or became 0 via the performed check.</returns>
    bool IssueCacheCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues a full backup of the whole storage to be executed.
    /// </summary>
    /// <param name="targetDirectory">The directory to write the backup data into.</param>
    void IssueFullBackup(DirectoryInfo targetDirectory);

    /// <summary>
    /// Issue a cleanup of the transaction log to reduce size regardless of its current size.
    /// </summary>
    void IssueTransactionsLogCleanup();

    /// <summary>
    /// Creates storage statistics about every channel in the storage.
    /// </summary>
    /// <returns>Storage statistics based on the current state.</returns>
    IStorageStatistics CreateStorageStatistics();

    /// <summary>
    /// Exports the data of all channels in the storage.
    /// This is useful to safely create a complete copy of the storage, e.g. a full backup.
    /// </summary>
    /// <param name="targetDirectory">The target directory for the export.</param>
    /// <param name="performGarbageCollection">Whether garbage collection shall be performed before export.</param>
    void ExportChannels(DirectoryInfo targetDirectory, bool performGarbageCollection = true);

    /// <summary>
    /// Imports all files from the specified directory.
    /// The files are assumed to be in the native binary format used internally by the storage.
    /// </summary>
    /// <param name="importDirectory">The directory containing files to import.</param>
    void ImportFiles(DirectoryInfo importDirectory);

    /// <summary>
    /// Returns the persistence manager used by this storage connection.
    /// </summary>
    IPersistenceManager PersistenceManager { get; }
}
