using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NebulaStore.Storage.Embedded.Types;

/// <summary>
/// Interface for storage file management operations.
/// Handles storage file creation, writing, cleanup, and maintenance operations.
/// </summary>
public interface IStorageFileManager : IStorageChannelResetablePart, IDisposable
{
    /// <summary>
    /// Gets the channel index for this file manager.
    /// </summary>
    int ChannelIndex { get; }

    /// <summary>
    /// Stores chunks of data to storage files.
    /// </summary>
    /// <param name="timestamp">The timestamp for the store operation.</param>
    /// <param name="dataBuffers">The data buffers to store.</param>
    /// <returns>Array of storage positions for the stored chunks.</returns>
    /// <exception cref="StorageFileException">Thrown when chunk writing fails.</exception>
    long[] StoreChunks(long timestamp, byte[][] dataBuffers);

    /// <summary>
    /// Rolls back the current write operation.
    /// </summary>
    void RollbackWrite();

    /// <summary>
    /// Commits the current write operation.
    /// </summary>
    void CommitWrite();

    /// <summary>
    /// Reads the storage inventory from existing files.
    /// </summary>
    /// <returns>The storage inventory.</returns>
    IStorageInventory ReadStorage();

    /// <summary>
    /// Initializes storage with the specified parameters.
    /// </summary>
    /// <param name="taskTimestamp">The task timestamp.</param>
    /// <param name="consistentStoreTimestamp">The consistent store timestamp.</param>
    /// <param name="storageInventory">The storage inventory.</param>
    /// <param name="parentChannel">The parent storage channel.</param>
    /// <returns>The storage ID analysis result.</returns>
    IStorageIdAnalysis InitializeStorage(
        long taskTimestamp,
        long consistentStoreTimestamp,
        IStorageInventory storageInventory,
        IStorageChannel parentChannel);

    /// <summary>
    /// Gets the current storage file.
    /// </summary>
    /// <returns>The current storage file.</returns>
    IStorageLiveDataFile CurrentStorageFile { get; }

    /// <summary>
    /// Iterates over all storage files.
    /// </summary>
    /// <param name="procedure">The procedure to execute for each file.</param>
    void IterateStorageFiles(Action<IStorageLiveDataFile> procedure);

    /// <summary>
    /// Performs incremental file cleanup check within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for cleanup operations.</param>
    /// <returns>True if cleanup completed within the time budget.</returns>
    bool IncrementalFileCleanupCheck(TimeSpan timeBudget);

    /// <summary>
    /// Issues a file cleanup check within the specified time budget.
    /// </summary>
    /// <param name="timeBudget">The time budget for cleanup operations.</param>
    /// <returns>True if cleanup completed within the time budget.</returns>
    bool IssuedFileCleanupCheck(TimeSpan timeBudget);

    /// <summary>
    /// Exports data using the specified file provider.
    /// </summary>
    /// <param name="fileProvider">The file provider for export operations.</param>
    void ExportData(IStorageLiveFileProvider fileProvider);

    /// <summary>
    /// Creates raw file statistics for this channel.
    /// </summary>
    /// <returns>The raw file statistics.</returns>
    IStorageRawFileStatistics CreateRawFileStatistics();

    /// <summary>
    /// Restarts the file cleanup cursor.
    /// </summary>
    void RestartFileCleanupCursor();

    /// <summary>
    /// Loads data for the specified entity.
    /// </summary>
    /// <param name="dataFile">The data file containing the entity.</param>
    /// <param name="entity">The entity to load data for.</param>
    /// <param name="length">The length of data to load.</param>
    /// <param name="cacheChange">The cache size change.</param>
    void LoadData(IStorageLiveDataFile dataFile, IStorageEntity entity, long length, long cacheChange);

    /// <summary>
    /// Prepares for data import operations.
    /// </summary>
    void PrepareImport();

    /// <summary>
    /// Copies data from the specified import source.
    /// </summary>
    /// <param name="importSource">The import source.</param>
    void CopyData(IStorageImportSource importSource);

    /// <summary>
    /// Commits the import operation with the specified timestamp.
    /// </summary>
    /// <param name="taskTimestamp">The task timestamp.</param>
    void CommitImport(long taskTimestamp);

    /// <summary>
    /// Rolls back the import operation.
    /// </summary>
    void RollbackImport();

    /// <summary>
    /// Issues a transaction file check.
    /// </summary>
    /// <param name="checkSize">Whether to check the file size.</param>
    /// <returns>True if the check completed successfully.</returns>
    bool IssuedTransactionFileCheck(bool checkSize);
}

/// <summary>
/// Interface for storage live data files.
/// </summary>
public interface IStorageLiveDataFile : IStorageFile
{
    /// <summary>
    /// Gets the file number.
    /// </summary>
    long Number { get; }

    /// <summary>
    /// Gets the channel index.
    /// </summary>
    int ChannelIndex { get; }

    /// <summary>
    /// Gets the total length of the file.
    /// </summary>
    long TotalLength { get; }

    /// <summary>
    /// Gets the data length (content length) of the file.
    /// </summary>
    long DataLength { get; }

    /// <summary>
    /// Gets a value indicating whether the file has content.
    /// </summary>
    bool HasContent { get; }

    /// <summary>
    /// Gets a value indicating whether the file has users.
    /// </summary>
    bool HasUsers { get; }

    /// <summary>
    /// Gets the file identifier.
    /// </summary>
    string Identifier { get; }

    /// <summary>
    /// Reads bytes from the file into the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="position">The position to start reading from.</param>
    /// <returns>The number of bytes read.</returns>
    int ReadBytes(byte[] buffer, long position);

    /// <summary>
    /// Increases the content length by the specified amount.
    /// </summary>
    /// <param name="length">The length to add.</param>
    void IncreaseContentLength(long length);

    /// <summary>
    /// Truncates the file to the specified length.
    /// </summary>
    /// <param name="length">The new length.</param>
    void Truncate(long length);

    /// <summary>
    /// Copies the file to the specified target.
    /// </summary>
    /// <param name="target">The target stream.</param>
    void CopyTo(Stream target);

    /// <summary>
    /// Determines whether the file needs retirement based on the evaluator.
    /// </summary>
    /// <param name="evaluator">The data file evaluator.</param>
    /// <returns>True if the file needs retirement.</returns>
    bool NeedsRetirement(IStorageDataFileEvaluator evaluator);

    /// <summary>
    /// Registers usage of this file.
    /// </summary>
    /// <param name="user">The file user.</param>
    void RegisterUsage(IStorageFileUser user);

    /// <summary>
    /// Unregisters usage of this file.
    /// </summary>
    /// <param name="user">The file user.</param>
    /// <param name="cause">The cause of unregistration (if any).</param>
    void UnregisterUsage(IStorageFileUser user, Exception? cause);

    /// <summary>
    /// Appends an entity entry to the file.
    /// </summary>
    /// <param name="entity">The entity to append.</param>
    void AppendEntry(IStorageEntity entity);

    /// <summary>
    /// Removes a head-bound chain of entities.
    /// </summary>
    /// <param name="newHead">The new head entity.</param>
    /// <param name="removedLength">The length of removed data.</param>
    void RemoveHeadBoundChain(IStorageEntity newHead, long removedLength);

    /// <summary>
    /// Adds a chain of entities to the tail.
    /// </summary>
    /// <param name="first">The first entity in the chain.</param>
    /// <param name="last">The last entity in the chain.</param>
    void AddChainToTail(IStorageEntity first, IStorageEntity last);
}

/// <summary>
/// Interface for storage files.
/// </summary>
public interface IStorageFile
{
    /// <summary>
    /// Gets the file size.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Gets a value indicating whether the file exists.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// Gets a value indicating whether the file is empty.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Ensures the file exists.
    /// </summary>
    void EnsureExists();

    /// <summary>
    /// Closes the file.
    /// </summary>
    void Close();

    /// <summary>
    /// Deletes the file.
    /// </summary>
    void Delete();
}

/// <summary>
/// Interface for storage file users.
/// </summary>
public interface IStorageFileUser
{
    /// <summary>
    /// Gets the channel index.
    /// </summary>
    int ChannelIndex { get; }
}

/// <summary>
/// Interface for storage data file evaluators.
/// </summary>
public interface IStorageDataFileEvaluator
{
    /// <summary>
    /// Gets the maximum file size.
    /// </summary>
    long FileMaximumSize { get; }

    /// <summary>
    /// Gets the maximum transaction file size.
    /// </summary>
    long TransactionFileMaximumSize { get; }

    /// <summary>
    /// Gets a value indicating whether file cleanup is enabled.
    /// </summary>
    bool IsFileCleanupEnabled { get; }

    /// <summary>
    /// Determines whether the specified file needs dissolving.
    /// </summary>
    /// <param name="file">The file to evaluate.</param>
    /// <returns>True if the file needs dissolving.</returns>
    bool NeedsDissolving(IStorageLiveDataFile file);
}

/// <summary>
/// Interface for storage write controllers.
/// </summary>
public interface IStorageWriteController
{
    /// <summary>
    /// Gets a value indicating whether file cleanup is enabled.
    /// </summary>
    bool IsFileCleanupEnabled { get; }

    /// <summary>
    /// Validates that file cleanup is enabled.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when file cleanup is disabled.</exception>
    void ValidateIsFileCleanupEnabled();
}

/// <summary>
/// Interface for storage file writers.
/// </summary>
public interface IStorageFileWriter
{
    /// <summary>
    /// Writes store data to the specified file.
    /// </summary>
    /// <param name="file">The target file.</param>
    /// <param name="dataBuffers">The data buffers to write.</param>
    /// <returns>The number of bytes written.</returns>
    long WriteStore(IStorageLiveDataFile file, IEnumerable<byte[]> dataBuffers);

    /// <summary>
    /// Writes transfer data from source to target file.
    /// </summary>
    /// <param name="sourceFile">The source file.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="length">The length to transfer.</param>
    /// <param name="targetFile">The target file.</param>
    /// <returns>The number of bytes transferred.</returns>
    long WriteTransfer(IStorageLiveDataFile sourceFile, long sourceOffset, long length, IStorageLiveDataFile targetFile);

    /// <summary>
    /// Writes import data to the specified file.
    /// </summary>
    /// <param name="importSource">The import source.</param>
    /// <param name="position">The position in the source.</param>
    /// <param name="length">The length to import.</param>
    /// <param name="targetFile">The target file.</param>
    void WriteImport(IStorageImportSource importSource, long position, long length, IStorageLiveDataFile targetFile);

    /// <summary>
    /// Truncates the specified file to the given length.
    /// </summary>
    /// <param name="file">The file to truncate.</param>
    /// <param name="length">The new length.</param>
    /// <param name="fileProvider">The file provider.</param>
    void Truncate(IStorageLiveDataFile file, long length, IStorageLiveFileProvider fileProvider);

    /// <summary>
    /// Deletes the specified file.
    /// </summary>
    /// <param name="file">The file to delete.</param>
    /// <param name="writeController">The write controller.</param>
    /// <param name="fileProvider">The file provider.</param>
    void Delete(IStorageLiveDataFile file, IStorageWriteController writeController, IStorageLiveFileProvider fileProvider);

    /// <summary>
    /// Writes a transaction entry for file creation.
    /// </summary>
    /// <param name="transactionsFile">The transactions file.</param>
    /// <param name="entryData">The entry data.</param>
    /// <param name="dataFile">The data file.</param>
    void WriteTransactionEntryCreate(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile);

    /// <summary>
    /// Writes a transaction entry for store operations.
    /// </summary>
    /// <param name="transactionsFile">The transactions file.</param>
    /// <param name="entryData">The entry data.</param>
    /// <param name="dataFile">The data file.</param>
    /// <param name="offset">The data file offset.</param>
    /// <param name="length">The store length.</param>
    void WriteTransactionEntryStore(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile, long offset, long length);

    /// <summary>
    /// Writes a transaction entry for transfer operations.
    /// </summary>
    /// <param name="transactionsFile">The transactions file.</param>
    /// <param name="entryData">The entry data.</param>
    /// <param name="sourceFile">The source file.</param>
    /// <param name="sourceOffset">The source offset.</param>
    /// <param name="length">The transfer length.</param>
    void WriteTransactionEntryTransfer(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile sourceFile, long sourceOffset, long length);

    /// <summary>
    /// Writes a transaction entry for file deletion.
    /// </summary>
    /// <param name="transactionsFile">The transactions file.</param>
    /// <param name="entryData">The entry data.</param>
    /// <param name="dataFile">The data file.</param>
    void WriteTransactionEntryDelete(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile);

    /// <summary>
    /// Writes a transaction entry for file truncation.
    /// </summary>
    /// <param name="transactionsFile">The transactions file.</param>
    /// <param name="entryData">The entry data.</param>
    /// <param name="dataFile">The data file.</param>
    /// <param name="newLength">The new file length.</param>
    void WriteTransactionEntryTruncate(IStorageLiveTransactionsFile transactionsFile, IEnumerable<byte[]> entryData, IStorageLiveDataFile dataFile, long newLength);

    /// <summary>
    /// Writes data to the specified file.
    /// </summary>
    /// <param name="file">The target file.</param>
    /// <param name="data">The data to write.</param>
    /// <returns>The number of bytes written.</returns>
    long Write(IStorageFile file, IEnumerable<byte[]> data);
}

/// <summary>
/// Interface for storage live transactions files.
/// </summary>
public interface IStorageLiveTransactionsFile : IStorageFile
{
    /// <summary>
    /// Gets the channel index.
    /// </summary>
    int ChannelIndex { get; }

    /// <summary>
    /// Processes the file with the specified processor.
    /// </summary>
    /// <typeparam name="T">The processor type.</typeparam>
    /// <param name="processor">The processor to use.</param>
    /// <returns>The processor result.</returns>
    T ProcessBy<T>(T processor) where T : class;

    /// <summary>
    /// Registers usage of this file.
    /// </summary>
    /// <param name="user">The file user.</param>
    void RegisterUsage(IStorageFileUser user);

    /// <summary>
    /// Unregisters usage of this file.
    /// </summary>
    /// <param name="user">The file user.</param>
    /// <param name="cause">The cause of unregistration (if any).</param>
    void UnregisterUsage(IStorageFileUser user, Exception? cause);
}

/// <summary>
/// Interface for storage backup handlers.
/// </summary>
public interface IStorageBackupHandler
{
    /// <summary>
    /// Initializes the backup handler for the specified channel.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    void Initialize(int channelIndex);

    /// <summary>
    /// Synchronizes the backup with the specified inventory.
    /// </summary>
    /// <param name="inventory">The storage inventory.</param>
    void Synchronize(IStorageInventory inventory);
}

/// <summary>
/// Interface for storage timestamp providers.
/// </summary>
public interface IStorageTimestampProvider
{
    /// <summary>
    /// Gets the current nanosecond timestamp.
    /// </summary>
    /// <returns>The current timestamp in nanoseconds.</returns>
    long CurrentNanoTimestamp { get; }
}

/// <summary>
/// Interface for initial data file number providers.
/// </summary>
public interface IStorageInitialDataFileNumberProvider
{
    /// <summary>
    /// Provides the initial data file number for the specified channel.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <returns>The initial data file number.</returns>
    long ProvideInitialDataFileNumber(int channelIndex);
}
